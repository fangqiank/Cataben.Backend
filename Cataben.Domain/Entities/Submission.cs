using Cataben.Domain.Enums;

namespace Cataben.Domain.Entities
{
    public class Submission
    {
        public Guid Id { get; private set; }
        public Guid UserId { get; private set; }
        public User User { get; private set; } = null!;
        public Guid ChallengeId { get; private set; }
        public Challenge Challenge { get; private set; } = null!;
        public string Code { get; private set; } = string.Empty;
        public SubmissionStatus Status { get; private set; }
        public bool IsSuccessful { get; private set; }
        public int Score { get; private set; }
        public int TotalScore { get; private set; }

        public long ExecutionTimeMs { get; private set; }
        public long MemoryUsedBytes { get; private set; }
        public string? QueryPlan { get; private set; }
        public int QueryCost { get; private set; }

        public string? ErrorMessage { get; private set; }
        public string? ErrorStackTrace { get; private set; }
        public string? CompilerOutput { get; private set; }
        public string? CompilerErrors { get; private set; }

        public DateTime SubmittedAt { get; private set; }
        public DateTime? StartedAt { get; private set; }
        public DateTime? CompletedAt { get; private set; }
        public TimeSpan? TimeToComplete { get; private set; }

        public int AttemptNumber { get; private set; }
        public string? UserAgent { get; private set; }
        public string? IpAddress { get; private set; }
        public Dictionary<string, object> Metadata { get; private set; } = new();

        private readonly List<StatusHistory> _statusHistory = new();
        public IReadOnlyCollection<StatusHistory> StatusHistory => _statusHistory.AsReadOnly();

        private readonly List<TestResult> _testResults = new();
        public IReadOnlyCollection<TestResult> TestResults => _testResults.AsReadOnly();

        private Submission() { }

        public Submission(
            User user, 
            Challenge challenge, 
            string code, 
            int attemptNumber, 
            string? userAgent = null, 
            string? ipAddress = null)
        {
            Id = Guid.NewGuid();
            UserId = user.Id;
            User = user;
            ChallengeId = challenge.Id;
            Challenge = challenge;
            Code = code;
            AttemptNumber = attemptNumber;
            UserAgent = userAgent;
            IpAddress = ipAddress;
            Status = SubmissionStatus.Pending;
            IsSuccessful = false;
            Score = 0;
            TotalScore = 0;
            SubmittedAt = DateTime.UtcNow;
            AddStatusHistory(SubmissionStatus.Pending, "Submission received");
        }
        public void MarkAsCompiling()
        {
            UpdateStatus(SubmissionStatus.Compiling, "Compilation started");
            StartedAt ??= DateTime.UtcNow;
        }

        public void MarkAsExecuting()
        {
            UpdateStatus(SubmissionStatus.Executing, "Execution started");
        }

        public void MarkAsTesting()
        {
            UpdateStatus(SubmissionStatus.Testing, "Testing started");
        }

        public void MarkAsCompleted(
            int score, 
            int totalScore, 
            long executionTimeMs, 
            long memoryUsedBytes, 
            string? queryPlan = null)
        {
            Score = score;
            TotalScore = totalScore;
            ExecutionTimeMs = executionTimeMs;
            MemoryUsedBytes = memoryUsedBytes;
            QueryPlan = queryPlan;
            IsSuccessful = score >= totalScore * 0.8;
            CompletedAt = DateTime.UtcNow;
            TimeToComplete = CompletedAt.Value - SubmittedAt;
            UpdateStatus(SubmissionStatus.Completed, $"Completed with score {score}/{totalScore}");
        }

        public void MarkAsFailed(string error, string? stackTrace = null)
        {
            ErrorMessage = error;
            ErrorStackTrace = stackTrace;
            IsSuccessful = false;
            CompletedAt = DateTime.UtcNow;
            UpdateStatus(SubmissionStatus.Failed, $"Failed: {error}");
        }

        public void MarkAsTimeout()
        {
            Status = SubmissionStatus.Timeout;
            ErrorMessage = "Execution timeout exceeded";
            CompletedAt = DateTime.UtcNow;
            AddStatusHistory(SubmissionStatus.Timeout, "Execution timed out");
        }

        public void MarkAsCancelled(string? reason = null)
        {
            Status = SubmissionStatus.Cancelled;
            CompletedAt = DateTime.UtcNow;
            AddStatusHistory(SubmissionStatus.Cancelled, reason ?? "Cancelled by user");
        }

        public void MarkAsPartialPass(int score, int totalScore)
        {
            Score = score;
            TotalScore = totalScore;
            IsSuccessful = false;
            UpdateStatus(SubmissionStatus.PartialPass, $"Partial pass: {score}/{totalScore}");
        }

        public void MarkAsSystemError(string error)
        {
            ErrorMessage = error;
            Status = SubmissionStatus.SystemError;
            CompletedAt = DateTime.UtcNow;
            AddStatusHistory(SubmissionStatus.SystemError, $"System error: {error}");
        }

        private void UpdateStatus(SubmissionStatus newStatus, string? reason = null)
        {
            if (Status == newStatus) return;
            Status = newStatus;
            AddStatusHistory(newStatus, reason);
        }

        private void AddStatusHistory(SubmissionStatus status, string? reason = null)
        {
            _statusHistory.Add(new StatusHistory { Status = status, Reason = reason, Timestamp = DateTime.UtcNow });
        }

        public void AddTestResult(TestResult testResult)
        {
            _testResults.Add(testResult);
        }

        public void SetCompilerOutput(string output, string? errors = null)
        {
            CompilerOutput = output;
            CompilerErrors = errors;
        }

        public void SetQueryInfo(string? queryPlan, int queryCost)
        {
            QueryPlan = queryPlan;
            QueryCost = queryCost;
        }

        public bool IsFinal() => Status is SubmissionStatus.Completed or SubmissionStatus.PartialPass or SubmissionStatus.Failed or SubmissionStatus.Timeout or SubmissionStatus.Cancelled or SubmissionStatus.Rejected or SubmissionStatus.SystemError;

        public bool IsInProgress() => Status is SubmissionStatus.Compiling or SubmissionStatus.Executing or SubmissionStatus.Testing or SubmissionStatus.Pending;

        public bool IsSuccess() => IsSuccessful && Status == SubmissionStatus.Completed;

        public double GetScorePercentage() => TotalScore > 0 ? (double)Score / TotalScore * 100 : 0;
    }

    public class StatusHistory
    {
        public SubmissionStatus Status { get; set; }
        public string? Reason { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class TestResult
    {
        public Guid Id { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public bool Passed { get; private set; }
        public int Score { get; private set; }
        public string? Expected { get; private set; }
        public string? Actual { get; private set; }
        public string? Message { get; private set; }
        public TimeSpan ExecutionTime { get; private set; }
        public long MemoryUsed { get; private set; }

        public TestResult(string name, bool passed, int score)
        {
            Id = Guid.NewGuid();
            Name = name;
            Passed = passed;
            Score = score;
        }

        public TestResult(string name, bool passed, int score, string? expected, string? actual, string? message)
            : this(name, passed, score)
        {
            Expected = expected;
            Actual = actual;
            Message = message;
        }

        public TestResult(string name, bool passed, int score, string? expected, string? actual, string? message, TimeSpan executionTime)
            : this(name, passed, score, expected, actual, message)
        {
            ExecutionTime = executionTime;
        }
    }
}