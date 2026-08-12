using Cataben.Application.DTOs;
using MediatR;

namespace Cataben.Application.Commands;

// Admin CRUD commands for learning-path catalog entries. Challenge linkage is expressed as an
// ordered list of {Id, Order}; the handler reconciles Challenge.LearningPathId/OrderInPath directly
// (the same pattern SeedData uses), so it does not depend on the nav-collection change tracker.

public class CreateLearningPathCommand : IRequest<AdminLearningPathDto>
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Level { get; set; } = "Beginner";
    public string? Icon { get; set; }
    public string? CoverImage { get; set; }
    public int XpReward { get; set; }
    public int GemReward { get; set; }
    public int Order { get; set; }
    public bool IsPublished { get; set; }
    public List<PathChallengeInput> Challenges { get; set; } = new();
}

public class UpdateLearningPathCommand : IRequest<AdminLearningPathDto>
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Level { get; set; } = "Beginner";
    public string? Icon { get; set; }
    public string? CoverImage { get; set; }
    public int XpReward { get; set; }
    public int GemReward { get; set; }
    public int Order { get; set; }
    public bool IsPublished { get; set; }
    public List<PathChallengeInput> Challenges { get; set; } = new();
}

public class DeleteLearningPathCommand : IRequest<bool>
{
    public Guid Id { get; set; }
}
