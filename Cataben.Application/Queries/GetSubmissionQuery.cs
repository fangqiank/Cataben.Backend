using Cataben.Application.DTOs;
using MediatR;

namespace Cataben.Application.Queries
{
    public class GetSubmissionQuery: IRequest<SubmissionDto>
    {
        public Guid SubmissionId { get; set; }
        public Guid UserId { get; set; }
    }
    public class GetUserSubmissionsQuery : IRequest<IEnumerable<SubmissionDto>>
    {
        public Guid UserId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public bool? IsSuccessful { get; set; }
    }

    public class GetChallengeSubmissionsQuery : IRequest<IEnumerable<SubmissionDto>>
    {
        public Guid ChallengeId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
