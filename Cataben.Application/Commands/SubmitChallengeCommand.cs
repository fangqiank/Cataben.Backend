using Cataben.Application.DTOs;
using MediatR;

namespace Cataben.Application.Commands
{
    public class SubmitChallengeCommand : IRequest<SubmissionResultDto>
    {
        public Guid UserId { get; set; }
        public Guid ChallengeId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string? UserAgent { get; set; }
        public string? IpAddress { get; set; }
    }
}
