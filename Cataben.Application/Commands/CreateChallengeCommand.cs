using Cataben.Application.DTOs;
using Cataben.Domain.Enums;
using MediatR;

namespace Cataben.Application.Commands
{
    public class CreateChallengeCommand: IRequest<ChallengeDto>
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ChallengeType Type { get; set; }
        public string Difficulty { get; set; } = "Medium";
        public string Category { get; set; } = string.Empty;
        public string InitialCode { get; set; } = string.Empty;
        public string SolutionCode { get; set; } = string.Empty;
        public int XpReward { get; set; } = 10;
        public int GemReward { get; set; } = 5;
        public int TimeLimitSeconds { get; set; } = 10;
        public int MemoryLimitMb { get; set; } = 256;
        public List<TestCaseDto> TestCases { get; set; } = new();
        public List<string>? Hints { get; set; }
        public Guid? LearningPathId { get; set; }
    }
}
