using MediatR;

namespace Cataben.Application.Commands
{
    public class DeleteChallengeCommand : IRequest<bool>
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public bool IsPermanent { get; set; } = false;
    }
}
