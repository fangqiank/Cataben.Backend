using Cataben.Application.Commands;
using Cataben.Application.DTOs;
using Cataben.Application.Exceptions;
using Cataben.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cataben.Application.Handlers;

// Admin CRUD handlers for learning-path catalog entries. Challenge linkage is reconciled by writing
// Challenge.LearningPathId/OrderInPath directly (the same pattern SeedData uses), so it does not
// depend on the nav-collection change tracker.

file static class LearningPathAdminOps
{
    // Links the desired challenges (by id+order) to the path and unlinks any currently-linked
    // challenge no longer in the desired list. Caller is responsible for SaveChangesAsync.
    public static async Task ReconcileChallengesAsync(
        IChallengeRepository challengeRepository,
        Guid pathId,
        List<PathChallengeInput> desired,
        CancellationToken cancellationToken)
    {
        var current = (await challengeRepository.GetByLearningPathAsync(pathId, cancellationToken)).ToList();
        var desiredIds = desired.Select(c => c.Id).ToHashSet();

        foreach (var cur in current.Where(c => !desiredIds.Contains(c.Id)))
        {
            cur.SetLearningPath(null);
            cur.SetOrderInPath(0);
        }

        if (desired.Count == 0) return;

        var challenges = (await challengeRepository.GetByIdsAsync(desiredIds, cancellationToken)).ToDictionary(c => c.Id);
        foreach (var input in desired)
        {
            if (challenges.TryGetValue(input.Id, out var challenge))
            {
                challenge.SetLearningPath(pathId);
                challenge.SetOrderInPath(input.Order);
            }
        }
    }

    public static async Task<AdminLearningPathDto> MapAsync(
        IChallengeRepository challengeRepository,
        LearningPath path,
        CancellationToken cancellationToken)
    {
        var challenges = (await challengeRepository.GetByLearningPathAsync(path.Id, cancellationToken))
            .OrderBy(c => c.OrderInPath)
            .Select(c => new AdminPathChallengeDto { Id = c.Id, Title = c.Title, OrderInPath = c.OrderInPath })
            .ToList();

        return new AdminLearningPathDto
        {
            Id = path.Id,
            Name = path.Name,
            Description = path.Description,
            Icon = path.Icon,
            CoverImage = path.CoverImage,
            Level = path.Level,
            Order = path.Order,
            IsPublished = path.IsPublished,
            XpReward = path.XpReward,
            GemReward = path.GemReward,
            CreatedAt = path.CreatedAt,
            PublishedAt = path.PublishedAt,
            CreatedBy = path.CreatedBy,
            Challenges = challenges
        };
    }

    public static void ApplyFields(LearningPath path, string name, string description, string level,
        string? icon, string? coverImage, int xp, int gems, int order)
    {
        path.Update(name, description, level, icon, coverImage);
        path.UpdateRewards(xp, gems);
        path.SetOrder(order);
    }
}

public class CreateLearningPathHandler(
    ILearningPathRepository learningPathRepository,
    IChallengeRepository challengeRepository,
    IUnitOfWork unitOfWork,
    ILogger<CreateLearningPathHandler> logger) : IRequestHandler<CreateLearningPathCommand, AdminLearningPathDto>
{
    public async Task<AdminLearningPathDto> Handle(CreateLearningPathCommand request, CancellationToken cancellationToken)
    {
        var path = new LearningPath(request.Name, request.Description, request.Level);
        LearningPathAdminOps.ApplyFields(path, request.Name, request.Description, request.Level,
            request.Icon, request.CoverImage, request.XpReward, request.GemReward, request.Order);
        if (request.IsPublished) path.Publish();

        await learningPathRepository.AddAsync(path, cancellationToken);
        await LearningPathAdminOps.ReconcileChallengesAsync(challengeRepository, path.Id, request.Challenges, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Learning path created: {LearningPathId} - {Name}", path.Id, path.Name);
        return await LearningPathAdminOps.MapAsync(challengeRepository, path, cancellationToken);
    }
}

public class UpdateLearningPathHandler(
    ILearningPathRepository learningPathRepository,
    IChallengeRepository challengeRepository,
    IUnitOfWork unitOfWork,
    ILogger<UpdateLearningPathHandler> logger) : IRequestHandler<UpdateLearningPathCommand, AdminLearningPathDto>
{
    public async Task<AdminLearningPathDto> Handle(UpdateLearningPathCommand request, CancellationToken cancellationToken)
    {
        var path = await learningPathRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Learning path not found");

        LearningPathAdminOps.ApplyFields(path, request.Name, request.Description, request.Level,
            request.Icon, request.CoverImage, request.XpReward, request.GemReward, request.Order);
        if (request.IsPublished && !path.IsPublished) path.Publish();
        if (!request.IsPublished && path.IsPublished) path.Unpublish();

        await LearningPathAdminOps.ReconcileChallengesAsync(challengeRepository, path.Id, request.Challenges, cancellationToken);

        await learningPathRepository.UpdateAsync(path, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Learning path updated: {LearningPathId}", path.Id);
        return await LearningPathAdminOps.MapAsync(challengeRepository, path, cancellationToken);
    }
}

public class DeleteLearningPathHandler(
    ILearningPathRepository learningPathRepository,
    IChallengeRepository challengeRepository,
    IUnitOfWork unitOfWork,
    ILogger<DeleteLearningPathHandler> logger) : IRequestHandler<DeleteLearningPathCommand, bool>
{
    public async Task<bool> Handle(DeleteLearningPathCommand request, CancellationToken cancellationToken)
    {
        var path = await learningPathRepository.GetByIdAsync(request.Id, cancellationToken);
        if (path == null) return false;

        // Detach challenges first (nullable FK) so deleting the path doesn't cascade-delete them.
        await LearningPathAdminOps.ReconcileChallengesAsync(challengeRepository, path.Id, new List<PathChallengeInput>(), cancellationToken);

        await learningPathRepository.DeleteAsync(path.Id, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Learning path deleted: {LearningPathId}", request.Id);
        return true;
    }
}
