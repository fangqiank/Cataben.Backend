using Cataben.Application.DTOs;
using MediatR;

namespace Cataben.Application.Commands
{
    /// <summary>
    /// Reveals the reference solution for a challenge, consuming one of the user's global
    /// reveal credits (see User.RevealsRemaining). Handled by RevealSolutionHandler.
    /// </summary>
    public class RevealSolutionCommand : IRequest<RevealSolutionResultDto>
    {
        public Guid UserId { get; set; }
        public Guid ChallengeId { get; set; }
    }
}
