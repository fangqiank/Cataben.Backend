using Cataben.Application.Repositories;
using Cataben.Domain.Entities;
using Cataben.Domain.Enums;
using Cataben.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cataben.Infrastructure.Repositories
{
    public class XpTransactionRepository(AppDbContext context) : IXpTransactionRepository
    {
        public async Task AddAsync(XpTransaction tx, CancellationToken cancellationToken = default)
        {
            await context.XpTransactions.AddAsync(tx, cancellationToken);
        }

        public async Task<IEnumerable<LeaderboardPeriodEntry>> GetLeaderboardInPeriodAsync(
            DateTime since, int count, CancellationToken cancellationToken = default)
        {
            var challenge = XpSource.Challenge;

            // 按 CreatedAt 窗口 ∑ Amount 排名；PeriodSolved = 窗口内 Challenge 来源流水条数（= 解题数）。
            var rows = await (
                from t in context.XpTransactions
                where t.CreatedAt >= since
                group t by t.UserId into g
                join u in context.Users on g.Key equals u.Id
                orderby g.Sum(t => t.Amount) descending
                select new LeaderboardPeriodEntry
                {
                    UserId = u.Id,
                    Username = u.Username,
                    AvatarUrl = u.AvatarUrl,
                    TotalXp = u.Xp,
                    PeriodXp = g.Sum(t => t.Amount),
                    PeriodSolved = g.Count(t => t.Source == challenge),
                    LastActiveAt = u.LastActiveAt
                }
            ).Take(count).ToListAsync(cancellationToken);

            return rows;
        }
    }
}
