using Cataben.Application.Commands;
using Cataben.Application.DTOs;
using Cataben.Application.Exceptions;
using Cataben.Domain.Entities;
using Cataben.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Cataben.Application.Handlers
{
    public class ExecuteCodeHandler(
        ICodeExecutor codeExecutor,
        IChallengeRepository challengeRepository,
        ICacheService cache,
        IDistributedTracing tracing,
        ILogger<ExecuteCodeHandler> logger
        ) : IRequestHandler<ExecuteCodeCommand, ExecutionResultDto>
    {
        public async Task<ExecutionResultDto> Handle(ExecuteCodeCommand request, CancellationToken cancellationToken)
        {
            using var activity = tracing.StartActivity("ExecuteCode");
            activity?.SetTag("user.id", request.UserId);
            activity?.SetTag("challenge.id", request.ChallengeId);
            activity?.SetTag("code.length", request.Code.Length);

            try
            {
                Challenge? challenge = null;
                if (request.ChallengeId.HasValue)
                {
                    challenge = await challengeRepository.GetByIdAsync(request.ChallengeId.Value, cancellationToken);
                    if (challenge == null)
                        throw new NotFoundException("Challenge not found");
                }

                var cacheKey = GenerateCacheKey(request.Code, challenge);
                if (!request.IsSubmission)
                {
                    var cached = await cache.GetAsync<ExecutionResultDto>(cacheKey);
                    if (cached != null)
                    {
                        activity?.SetTag("cache.hit", true);
                        return cached;
                    }
                }

                var options = new ExecutionOptions
                {
                    Timeout = TimeSpan.FromSeconds(challenge?.TimeLimitSeconds ?? 10),
                    MaxMemoryBytes = (challenge?.MemoryLimitMb ?? 256) * 1024 * 1024,
                    EnableDatabase = challenge?.Type == ChallengeType.Database,
                    CaptureQueryPlan = true
                };

                var result = await codeExecutor.ExecuteAsync(
                    request.Code,
                    request.Parameters,
                    options,
                    cancellationToken);

                activity?.SetStatus(ActivityStatusCode.Ok);
                activity?.SetTag("execution.success", result.IsSuccessful);
                activity?.SetTag("execution.time", result.ExecutionTimeMs);

                if (!request.IsSubmission)
                    await cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5));

                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error executing code");
                activity?.SetStatus(ActivityStatusCode.Error);
                activity?.SetTag("exception.message", ex.Message);
                throw;
            }
        }

        private string GenerateCacheKey(string code, Challenge? challenge)
        {
            var codeHash = Convert.ToBase64String(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(code)));

            return $"execution:{challenge?.Id}:{codeHash}";
        }
    }
}
