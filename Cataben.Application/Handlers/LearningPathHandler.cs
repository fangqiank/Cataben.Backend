using Cataben.Application.Commands;
using Cataben.Application.DTOs;
using Cataben.Application.Exceptions;
using Cataben.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cataben.Application.Handlers
{
    public class EnrollInLearningPathHandler(
        ILearningPathRepository learningPathRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ILogger<EnrollInLearningPathHandler> logger)
        : IRequestHandler<EnrollInLearningPathCommand, bool>
    {
        public async Task<bool> Handle(EnrollInLearningPathCommand request, CancellationToken cancellationToken)
        {
            var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null)
                throw new NotFoundException("User not found");

            var path = await learningPathRepository.GetByIdAsync(request.LearningPathId, cancellationToken);
            if (path == null)
                throw new NotFoundException("Learning path not found");

            // Check if already enrolled
            var existingProgress = await learningPathRepository.GetUserProgressAsync(
                request.UserId, request.LearningPathId, cancellationToken);
            if (existingProgress != null)
                return false;

            var progress = new UserLearningPath(user, path);
            await learningPathRepository.AddUserProgressAsync(progress, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation("User {UserId} enrolled in learning path {LearningPathId}",
                request.UserId, request.LearningPathId);

            return true;
        }
    }

    public class UpdateLearningPathProgressHandler(
        ILearningPathRepository learningPathRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ILogger<UpdateLearningPathProgressHandler> logger)
        : IRequestHandler<UpdateLearningPathProgressCommand, UserLearningPathProgressDto>
    {
        public async Task<UserLearningPathProgressDto> Handle(UpdateLearningPathProgressCommand request, CancellationToken cancellationToken)
        {
            var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null)
                throw new NotFoundException("User not found");

            var path = await learningPathRepository.GetByIdAsync(request.LearningPathId, cancellationToken);
            if (path == null)
                throw new NotFoundException("Learning path not found");

            var progress = await learningPathRepository.GetUserProgressAsync(
                request.UserId, request.LearningPathId, cancellationToken);

            if (progress == null)
            {
                progress = new UserLearningPath(user, path);
                await learningPathRepository.AddUserProgressAsync(progress, cancellationToken);
            }

            progress.UpdateProgress(request.CompletedChallenges);
            await learningPathRepository.UpdateUserProgressAsync(progress, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation("User {UserId} progress updated for learning path {LearningPathId}: {Progress}%",
                request.UserId, request.LearningPathId, progress.Progress);

            return new UserLearningPathProgressDto
            {
                LearningPathId = progress.LearningPathId,
                Name = path.Name,
                Progress = progress.Progress,
                CompletedChallenges = progress.CompletedChallenges,
                TotalChallenges = progress.TotalChallenges,
                IsCompleted = progress.IsCompleted,
                StartedAt = progress.StartedAt,
                CompletedAt = progress.CompletedAt,
                LastActivityAt = progress.LastActivityAt
            };
        }
    }
}
