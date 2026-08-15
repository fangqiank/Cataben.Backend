using Cataben.Domain.Enums;
using Cataben.Shared.Constants;
using Cataben.Shared.Messaging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;

namespace Cataben.Worker.Services
{
    /// <summary>
    /// Consumes <c>code.execute</c> over NATS (JetStream durable — survives Worker crash +
    /// NATS restart), compiles/runs the submitted C# in-process, runs tests, and publishes the
    /// result back on <c>code.result.{executionId}</c> (Core NATS). Horizontal scaling is via
    /// replicas sharing the durable consumer name; JetStream load-balances pulls between them.
    /// </summary>
    public class ExecutionWorker : BackgroundService
    {
        private readonly IMessageBus _messageBus;
        private readonly SandboxExecutor _sandboxExecutor;
        private readonly TestRunner _testRunner;
        private readonly ResourceMonitor _resourceMonitor;
        private readonly IMemoryCache _cache;
        private readonly ILogger<ExecutionWorker> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly SemaphoreSlim _semaphore;
        private readonly int _maxConcurrentTasks;
        private readonly string _resultSigningKey;
        private int _activeTasks;

        public ExecutionWorker(
            IMessageBus messageBus,
            SandboxExecutor sandboxExecutor,
            TestRunner testRunner,
            ResourceMonitor resourceMonitor,
            IMemoryCache cache,
            ILogger<ExecutionWorker> logger,
            IServiceProvider serviceProvider,
            IConfiguration configuration)
        {
            _messageBus = messageBus;
            _sandboxExecutor = sandboxExecutor;
            _testRunner = testRunner;
            _resourceMonitor = resourceMonitor;
            _cache = cache;
            _logger = logger;
            _serviceProvider = serviceProvider;

            _maxConcurrentTasks = configuration.GetValue<int>("Worker:MaxConcurrentTasks", 10);
            _resultSigningKey = configuration["Nats:ResultSigningKey"] ?? string.Empty;
            _semaphore = new SemaphoreSlim(_maxConcurrentTasks);
            _activeTasks = 0;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ExecutionWorker started with {Max} concurrent tasks", _maxConcurrentTasks);

            // code.execute is the critical task-dispatch subject → JetStream durable consumer.
            // The shared durable name means every Worker replica pulls from the same consumer,
            // so JetStream load-balances executions between them (horizontal scaling).
            var execTask = _messageBus.SubscribeDurableAsync<ExecutionMessage>(
                ApplicationConstants.QueueNames.CodeExecution,
                ApplicationConstants.Nats.ExecutionsDurableConsumer,
                ProcessExecutionMessage,
                stoppingToken);

            // worker.health is a request/reply broadcast → Core NATS, no queue group (every
            // worker receives the ping and replies).
            var healthTask = _messageBus.SubscribeAsync<HealthCheckMessage>(
                ApplicationConstants.QueueNames.WorkerHealth,
                queueGroup: null,
                ProcessHealthCheck,
                stoppingToken);

            var monitorTask = MonitorResourcesAsync(stoppingToken);

            // All three loops respect stoppingToken; WhenAll completes on shutdown.
            await Task.WhenAll(execTask, healthTask, monitorTask);
        }

        private async Task ProcessExecutionMessage(ExecutionMessage message, CancellationToken cancellationToken)
        {
            // Defensive: NatsMessageBus now ACK-terminates null payloads, but guard here too so a
            // null message can never reach the try/catch below. (Original poison-loop bug: a null
            // message NRE'd at the first deref in the try, the catch deref'd `message` again, that
            // NRE escaped → NAK → ~1M+ redeliveries because the consumer had max_deliver=-1.)
            if (message is null)
            {
                _logger.LogWarning("ProcessExecutionMessage received a null message; skipping");
                return;
            }

            using var activity = Diagnostics.StartActivity("ProcessExecution");
            activity?.SetTag("execution.id", message.ExecutionId);
            activity?.SetTag("user.id", message.UserId);
            activity?.SetTag("challenge.id", message.ChallengeId);

            // Bound concurrency within this worker. (The durable consume loop awaits each
            // handler, so in practice this bounds re-entrancy; throughput scales via replicas.)
            await _semaphore.WaitAsync(cancellationToken);
            Interlocked.Increment(ref _activeTasks);

            try
            {
                _logger.LogInformation("Processing execution {ExecutionId} (Active: {Active}/{Max})",
                    message.ExecutionId, _activeTasks, _maxConcurrentTasks);

                // Idempotency on redelivery: if we already computed this execution (same
                // executionId, e.g. JetStream redelivered after an AckTimeout), return cached.
                var cacheKey = $"execution:{message.ExecutionId}";
                if (_cache.TryGetValue<ExecutionResult>(cacheKey, out var cachedResult))
                {
                    _logger.LogInformation("Returning cached result for {ExecutionId}", message.ExecutionId);
                    await PublishResult(message, cachedResult!);
                    return;
                }

                // 1. Notify status → Executing
                await UpdateSubmissionStatus(message.SubmissionId, SubmissionStatus.Executing, cancellationToken);

                // 2. Compile once (shared across every run).
                var stopwatch = Stopwatch.StartNew();
                var memoryBefore = GC.GetTotalMemory(forceFullCollection: true);
                var executionResult = new ExecutionResult();

                var compilation = await _sandboxExecutor.CompileAsync(message.Code ?? string.Empty, cancellationToken);
                if (!compilation.Success)
                {
                    // Compile errors are the submitter's fault → Failed (not SystemError). Short-circuit:
                    // no point running/evaluating code that didn't build.
                    executionResult.IsSuccessful = false;
                    executionResult.Error = string.Join("\n", compilation.Errors ?? new[] { "Compilation failed" });
                    executionResult.Status = SubmissionStatus.Failed;
                    executionResult.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
                    executionResult.MemoryAllocatedBytes = GC.GetTotalMemory(forceFullCollection: true) - memoryBefore;

                    await UpdateSubmissionStatus(message.SubmissionId, SubmissionStatus.Failed, cancellationToken);
                    _cache.Set(cacheKey, executionResult, TimeSpan.FromMinutes(5));
                    await PublishResult(message, executionResult);

                    activity?.SetTag("execution.success", false);
                    _logger.LogWarning("Execution {ExecutionId} failed at compile: {Errors}",
                        message.ExecutionId, executionResult.Error);
                    return;
                }

                // 3. Flatten public + hidden cases into evaluations (public/hidden judged identically).
                var cases = BuildEvaluations(message);

                // 4. Shared run (no stdin). Needed by: non-Algorithm challenges (Input is ignored, so
                //    every case shares one run), any case lacking an Input, OR a run-only submission
                //    with no cases at all. It is deliberately SKIPPED when every case has its own
                //    Input: a stdin-fed program run with no stdin would throw at its first ReadLine
                //    and falsely report SystemError despite the per-input runs all passing.
                var needSharedRun = cases.Count == 0
                    || message.ChallengeType != ChallengeType.Algorithm
                    || cases.Any(c => string.IsNullOrWhiteSpace(c.Input));

                SingleRunResult? sharedRun = needSharedRun
                    ? await _sandboxExecutor.RunAsync(
                        compilation.AssemblyBytes!, stdin: null, message.TimeoutSeconds, cancellationToken)
                    : null;
                var runCount = needSharedRun ? 1 : 0;

                // 5. Per-distinct-Input runs — only Algorithm challenges honor the per-case Input field.
                var perInputRuns = new Dictionary<string, SingleRunResult>(StringComparer.Ordinal);
                if (message.ChallengeType == ChallengeType.Algorithm)
                {
                    foreach (var input in cases
                                 .Where(c => !string.IsNullOrWhiteSpace(c.Input))
                                 .Select(c => c.Input!)
                                 .Distinct(StringComparer.Ordinal))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        perInputRuns[input] = await _sandboxExecutor.RunAsync(
                            compilation.AssemblyBytes!, input, message.TimeoutSeconds, cancellationToken);
                        runCount++;
                    }
                }

                // 6. Bind each evaluation to the run that produced its output. When needSharedRun is
                //    false every case has its own Input, so the null-forgiving `: sharedRun!` branch is
                //    never reached; when it is reached, needSharedRun is true and sharedRun is non-null.
                foreach (var c in cases)
                {
                    c.Run = message.ChallengeType == ChallengeType.Algorithm && !string.IsNullOrWhiteSpace(c.Input)
                        ? perInputRuns[c.Input!]
                        : sharedRun!;
                }

                stopwatch.Stop();
                executionResult.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
                executionResult.MemoryAllocatedBytes = GC.GetTotalMemory(forceFullCollection: true) - memoryBefore;
                var primaryRun = sharedRun ?? perInputRuns.Values.FirstOrDefault();
                executionResult.Output = primaryRun?.Output ?? string.Empty;

                _logger.LogInformation("Execution {ExecutionId}: ran {Runs} time(s) for {Cases} case(s)",
                    message.ExecutionId, runCount, cases.Count);

                // 7. Evaluate against test cases (or, for run-only submissions, success = clean run).
                if (message.HasTests && cases.Count > 0)
                {
                    executionResult.TestResults = await _testRunner.EvaluateAsync(cases, cancellationToken);

                    // SCORING FIX: TestResult.Score is already (passed ? weight : 0), i.e. the ACHIEVED
                    // score. totalScore MUST come from the input case WEIGHTS — the old code summed
                    // TestResult.Score for BOTH, making totalScore==score and IsSuccessful always true
                    // (a program that merely compiled+ran was judged "pass", regardless of test pass rate).
                    // The totalScore>0 guard prevents 0>=0 from passing when there are no/zero weights.
                    var totalScore = cases.Sum(c => c.Weight);
                    var score = executionResult.TestResults.Sum(t => t.Score);
                    executionResult.Score = score;
                    executionResult.TotalScore = totalScore;
                    executionResult.IsSuccessful = totalScore > 0 && score >= totalScore * 0.8m;
                }
                else
                {
                    executionResult.IsSuccessful = primaryRun?.Success ?? false;
                }

                // 8. Final status: a run-level Timeout/SystemError overrides the score-derived verdict
                //    (a timed-out or crashing run is not a mere "wrong answer"). Only the runs that
                //    actually executed count — a skipped shared run cannot fail the submission.
                var anyTimeout = sharedRun?.Status == SubmissionStatus.Timeout
                    || perInputRuns.Values.Any(r => r.Status == SubmissionStatus.Timeout);
                var anyRuntimeError = sharedRun?.Status == SubmissionStatus.SystemError
                    || perInputRuns.Values.Any(r => r.Status == SubmissionStatus.SystemError);

                if (anyTimeout)
                {
                    executionResult.Status = SubmissionStatus.Timeout;
                    executionResult.IsSuccessful = false;
                    executionResult.Error = $"Execution exceeded the {message.TimeoutSeconds}s time limit";
                }
                else if (anyRuntimeError)
                {
                    executionResult.Status = SubmissionStatus.SystemError;
                    executionResult.IsSuccessful = false;
                    executionResult.Error = sharedRun?.FailureDetail
                        ?? perInputRuns.Values.FirstOrDefault(r => r.Status == SubmissionStatus.SystemError)?.FailureDetail
                        ?? "Execution threw an exception";
                }
                else
                {
                    executionResult.Status = executionResult.IsSuccessful
                        ? SubmissionStatus.Completed
                        : SubmissionStatus.Failed;
                }

                _logger.LogInformation(
                    "Execution {ExecutionId} judged: score={Score}/{TotalScore} success={Success} status={Status} (runs={Runs})",
                    message.ExecutionId, executionResult.Score, executionResult.TotalScore,
                    executionResult.IsSuccessful, executionResult.Status, runCount);

                // 5. Notify final status
                await UpdateSubmissionStatus(message.SubmissionId, executionResult.Status, cancellationToken);

                // 6. Cache (same executionId → same deterministic result)
                _cache.Set(cacheKey, executionResult, TimeSpan.FromMinutes(5));

                // 7. Publish result back to the API (JetStream durable)
                await PublishResult(message, executionResult);

                activity?.SetStatus(ActivityStatusCode.Ok);
                activity?.SetTag("execution.success", executionResult.IsSuccessful);
                activity?.SetTag("execution.time", executionResult.ExecutionTimeMs);
                activity?.SetTag("execution.score", executionResult.Score);

                _logger.LogInformation("Execution {ExecutionId} completed successfully", message.ExecutionId);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Handled user-code/run failures are returned as ExecutionResult instead of throwing.
                // Exceptions here are infrastructure/transient failures, so after best-effort
                // reporting we re-throw to let JetStream NAK and redeliver.
                _logger.LogError(ex, "Error processing execution {ExecutionId}", message.ExecutionId);
                activity?.SetStatus(ActivityStatusCode.Error);
                activity?.RecordException(ex);

                try
                {
                    var errorResult = new ExecutionResult
                    {
                        IsSuccessful = false,
                        Error = ex.Message,
                        Status = SubmissionStatus.SystemError,
                        ExecutionTimeMs = 0,
                        MemoryAllocatedBytes = 0
                    };

                    await UpdateSubmissionStatus(message.SubmissionId, SubmissionStatus.SystemError, cancellationToken);
                    await PublishResult(message, errorResult);
                }
                catch (Exception reportEx)
                {
                    _logger.LogError(reportEx, "Failed to report SystemError for execution {ExecutionId}",
                        message.ExecutionId);
                    // Nothing reached the API — NAK/redeliver is the at-least-once safety net.
                    throw new InvalidOperationException(
                        $"Execution failed and the result could not be published: {ex.Message}",
                        reportEx);
                }

                // Terminal result published — return normally so the message is ACKed.
                // Re-throwing here would NAK and make JetStream redeliver the SAME submission,
                // re-running (compiling + executing) user code up to MaxRedeliveries times for
                // a failure that is already recorded and will recur deterministically.
            }
            finally
            {
                Interlocked.Decrement(ref _activeTasks);
                _semaphore.Release();
            }
        }

        private async Task ProcessHealthCheck(HealthCheckMessage message, CancellationToken cancellationToken)
        {
            try
            {
                var response = new HealthCheckResponse
                {
                    Status = "Healthy",
                    ActiveTasks = _activeTasks,
                    MaxTasks = _maxConcurrentTasks,
                    MemoryUsage = GC.GetTotalMemory(false),
                    Uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime
                };

                await _messageBus.PublishAsync(
                    $"{ApplicationConstants.QueueNames.WorkerHealthResponsePrefix}{message.CorrelationId}",
                    response, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing health check");
            }
        }

        private async Task MonitorResourcesAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var metrics = _resourceMonitor.GetCurrentMetrics();

                    _logger.LogDebug(
                        "Resource Metrics - CPU: {Cpu}%, Memory: {Memory}MB, Threads: {Threads}",
                        metrics.CpuUsagePercent,
                        metrics.MemoryUsageMb,
                        metrics.ThreadCount);

                    if (metrics.MemoryUsageMb > 400)
                        _logger.LogWarning("High memory usage: {Memory}MB", metrics.MemoryUsageMb);

                    if (metrics.CpuUsagePercent > 80)
                        _logger.LogWarning("High CPU usage: {Cpu}%", metrics.CpuUsagePercent);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error monitoring resources");
                }

                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }
        }

        /// <summary>
        /// Flatten the message's public + hidden test cases into a uniform evaluation list. Each case's
        /// <see cref="TestCaseEvaluation.Run"/> is left unset here (null-forgiving) and bound to its
        /// producing run in step 6 of <see cref="ProcessExecutionMessage"/> — before any evaluation.
        /// </summary>
        private static List<TestCaseEvaluation> BuildEvaluations(ExecutionMessage message)
        {
            var cases = new List<TestCaseEvaluation>();

            foreach (var t in message.TestCases)
            {
                cases.Add(new TestCaseEvaluation
                {
                    Name = t.Name,
                    Input = string.IsNullOrEmpty(t.Input) ? null : t.Input,
                    ExpectedOutput = t.ExpectedOutput ?? string.Empty,
                    // Normalize empty/whitespace (existing rows backfilled to "" by the migration)
                    // to "exact" so the evaluator's mode switch always sees a real value.
                    ValidationType = string.IsNullOrWhiteSpace(t.ValidationType) ? "exact" : t.ValidationType,
                    Weight = t.Weight,
                    Run = null! // bound after runs complete (see step 6)
                });
            }

            foreach (var h in message.HiddenTests ?? new List<HiddenTestDto>())
            {
                cases.Add(new TestCaseEvaluation
                {
                    Name = h.Name,
                    Input = string.IsNullOrEmpty(h.Input) ? null : h.Input,
                    ExpectedOutput = h.ExpectedOutput ?? string.Empty,
                    ValidationType = string.IsNullOrWhiteSpace(h.ValidationType) ? "exact" : h.ValidationType,
                    Weight = h.Weight,
                    Run = null! // bound after runs complete (see step 6)
                });
            }

            return cases;
        }

        private async Task UpdateSubmissionStatus(Guid submissionId, SubmissionStatus status, CancellationToken cancellationToken)
        {
            try
            {
                var statusMessage = new SubmissionStatusMessage
                {
                    SubmissionId = submissionId,
                    Status = status,
                    UpdatedAt = DateTime.UtcNow
                };

                await _messageBus.PublishAsync(
                    ApplicationConstants.QueueNames.SubmissionStatusUpdate, statusMessage, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating submission status for {SubmissionId}", submissionId);
            }
        }

        private async Task PublishResult(ExecutionMessage original, ExecutionResult result)
        {
            var resultMessage = new ExecutionResultMessage
            {
                ExecutionId = original.ExecutionId,

                // Correlation back to the submission so the API result consumer can persist
                // and run gamification without an extra lookup.
                SubmissionId = original.SubmissionId,
                UserId = original.UserId,
                ChallengeId = original.ChallengeId,

                IsSuccessful = result.IsSuccessful,
                Output = result.Output,
                Error = result.Error,
                ExecutionTimeMs = result.ExecutionTimeMs,
                MemoryAllocatedBytes = result.MemoryAllocatedBytes,
                QueryPlan = result.QueryPlan,
                QueryCost = result.QueryCost,
                Score = result.Score,
                TotalScore = result.TotalScore,
                Status = result.Status,
                TestResults = result.TestResults?.Select(t => new TestResultMessage
                {
                    Name = t.Name,
                    Passed = t.Passed,
                    Score = t.Score,
                    Message = t.Message,
                    ExecutionTimeMs = t.ExecutionTimeMs,
                    MemoryUsedBytes = t.MemoryUsedBytes
                }).ToList() ?? new()
            };

            resultMessage.Signature = ExecutionResultSigner.Sign(resultMessage, _resultSigningKey);
            await _messageBus.PublishDurableAsync(
                $"{ApplicationConstants.QueueNames.CodeResult}.{original.ExecutionId}",
                resultMessage, CancellationToken.None);
        }
    }
}
