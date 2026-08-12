using Cataben.Application.DTOs;
using Cataben.Domain.Enums;
using MediatR;

namespace Cataben.Application.Commands
{
    public class ExecuteCodeCommand: IRequest<ExecutionResultDto>
    {
        public Guid UserId { get; set; }
        public string Code { get; set; } = string.Empty;
        public Guid? ChallengeId { get; set; }
        public ChallengeType Type { get; set; } = ChallengeType.Algorithm;
        public Dictionary<string, object> Parameters { get; set; } = new();
        public bool IsSubmission { get; set; }
        public string? UserAgent { get; set; }
        public string? IpAddress { get; set; }
    }
}
