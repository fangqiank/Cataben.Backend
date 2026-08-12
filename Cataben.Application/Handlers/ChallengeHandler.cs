using Cataben.Application.Commands;
using Cataben.Application.DTOs;
using Cataben.Application.Exceptions;
using Cataben.Domain.Entities;
using Cataben.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cataben.Application.Handlers
{
    public class CreateChallengeHandler(
            IChallengeRepository challengeRepository,
            IUnitOfWork unitOfWork,
            ILogger<CreateChallengeHandler> logger
        ) : IRequestHandler<CreateChallengeCommand, ChallengeDto>
    {
        public async Task<ChallengeDto> Handle(CreateChallengeCommand request, CancellationToken cancellationToken)
        {
            var difficulty = Difficulty.FromName(request.Difficulty);

            var challenge = new Challenge(
                request.Title,
                request.Description,
                request.Type,
                difficulty,
                request.Category);

            challenge.SetInitialCode(request.InitialCode);
            challenge.SetSolution(request.SolutionCode);
            challenge.UpdateRewards(request.XpReward, request.GemReward);
            challenge.UpdateLimits(request.TimeLimitSeconds, request.MemoryLimitMb);
            challenge.SetHints(request.Hints ?? new List<string>());
            if (request.LearningPathId.HasValue)
                challenge.SetLearningPath(request.LearningPathId);

            foreach (var testCase in request.TestCases)
            {
                challenge.AddTestCase(new TestCase(
                    testCase.Name,
                    testCase.Input,
                    testCase.ExpectedOutput,
                    testCase.IsPublic,
                    testCase.Weight));
            }

            await challengeRepository.AddAsync(challenge, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Challenge created: {ChallengeId} - {Title}", challenge.Id, challenge.Title);

            return MapToDto(challenge);
        }

        private ChallengeDto MapToDto(Challenge challenge)
        {
            return new ChallengeDto
            {
                Id = challenge.Id,
                Title = challenge.Title,
                Description = challenge.Description,
                Type = challenge.Type,
                Difficulty = challenge.Difficulty.Name,
                Category = challenge.Category,
                InitialCode = challenge.InitialCode,
                XpReward = challenge.XpReward,
                GemReward = challenge.GemReward,
                Hints = challenge.Hints.ToList(),
                TimeLimitSeconds = challenge.TimeLimitSeconds,
                MemoryLimitMb = challenge.MemoryLimitMb,
                TestCases = challenge.TestCases.Select(t => new TestCaseDto
                {
                    Name = t.Name,
                    Input = t.Input,
                    ExpectedOutput = t.ExpectedOutput,
                    IsPublic = t.IsPublic,
                    Weight = t.Weight
                }).ToList()
            };
        }
    }

    public class UpdateChallengeHandler(
            IChallengeRepository challengeRepository,
            IUnitOfWork unitOfWork,
            ILogger<UpdateChallengeHandler> logger
        ) : IRequestHandler<UpdateChallengeCommand, ChallengeDto>
    {
        public async Task<ChallengeDto> Handle(UpdateChallengeCommand request, CancellationToken cancellationToken)
        {
            var challenge = await challengeRepository.GetByIdAsync(request.Id, cancellationToken);
            if (challenge == null)
                throw new NotFoundException("Challenge not found");

            challenge.Update(
                request.Title,
                request.Description,
                request.Type,
                Difficulty.FromName(request.Difficulty),
                request.Category);

            challenge.SetInitialCode(request.InitialCode);
            challenge.SetSolution(request.SolutionCode);
            challenge.UpdateRewards(request.XpReward, request.GemReward);
            challenge.UpdateLimits(request.TimeLimitSeconds, request.MemoryLimitMb);
            challenge.SetActive(request.IsActive);
            challenge.SetHints(request.Hints ?? new List<string>());
            challenge.SetLearningPath(request.LearningPathId);

            // Replace the public test-case set wholesale. The admin form is the source of truth and
            // always sends the complete desired list, so clear + re-add is the correct diff.
            challenge.ClearTestCases();
            foreach (var testCase in request.TestCases)
            {
                challenge.AddTestCase(new TestCase(
                    testCase.Name,
                    testCase.Input,
                    testCase.ExpectedOutput,
                    testCase.IsPublic,
                    testCase.Weight));
            }

            await challengeRepository.UpdateAsync(challenge, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return MapToDto(challenge);
        }

        private ChallengeDto MapToDto(Challenge challenge)
        {
            return new ChallengeDto
            {
                Id = challenge.Id,
                Title = challenge.Title,
                Description = challenge.Description,
                Type = challenge.Type,
                Difficulty = challenge.Difficulty.Name,
                Category = challenge.Category,
                InitialCode = challenge.InitialCode,
                XpReward = challenge.XpReward,
                GemReward = challenge.GemReward,
                Hints = challenge.Hints.ToList(),
                TimeLimitSeconds = challenge.TimeLimitSeconds,
                MemoryLimitMb = challenge.MemoryLimitMb,
                TestCases = challenge.TestCases.Select(t => new TestCaseDto
                {
                    Name = t.Name,
                    Input = t.Input,
                    ExpectedOutput = t.ExpectedOutput,
                    IsPublic = t.IsPublic,
                    Weight = t.Weight
                }).ToList()
            };
        }
    }

    public class DeleteChallengeHandler(
                IChallengeRepository challengeRepository,
                IUnitOfWork unitOfWork,
                ILogger<DeleteChallengeHandler> logger
                ) : IRequestHandler<DeleteChallengeCommand, bool>
    {
        public async Task<bool> Handle(DeleteChallengeCommand request, CancellationToken cancellationToken)
        {
            var challenge = await challengeRepository.GetByIdAsync(request.Id, cancellationToken);
            if (challenge == null)
                throw new NotFoundException("Challenge not found");

            await challengeRepository.DeleteAsync(request.Id, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Challenge deleted: {ChallengeId}", request.Id);
            return true;
        }
    }
}
