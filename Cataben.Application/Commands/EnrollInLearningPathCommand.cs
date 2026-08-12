using Cataben.Application.DTOs;
using MediatR;

namespace Cataben.Application.Commands
{
    public class EnrollInLearningPathCommand : IRequest<bool>
    {
        public Guid UserId { get; set; }
        public Guid LearningPathId { get; set; }
    }

    public class UpdateLearningPathProgressCommand : IRequest<UserLearningPathProgressDto>
    {
        public Guid UserId { get; set; }
        public Guid LearningPathId { get; set; }
        public int CompletedChallenges { get; set; }
    }
}
