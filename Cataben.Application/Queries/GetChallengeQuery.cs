using Cataben.Application.DTOs;
using Cataben.Domain.Enums;
using MediatR;

namespace Cataben.Application.Queries
{
    public class GetChallengeQuery: IRequest<ChallengeDto>
    {
        public Guid ChallengeId { get; set; }
        public Guid UserId { get; set; }
    }

    public class GetChallengesQuery : IRequest<IEnumerable<ChallengeDto>>
    {
        public string? Category { get; set; }
        public ChallengeType? Type { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public Guid UserId { get; set; }
    }
}
