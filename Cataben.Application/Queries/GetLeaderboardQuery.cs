using Cataben.Application.DTOs;
using MediatR;

namespace Cataben.Application.Queries
{
    public class GetLeaderboardQuery: IRequest<IEnumerable<LeaderboardDto>>
    {
        public int Limit { get; set; } = 50;
        public string? Category { get; set; }
    } 
}
