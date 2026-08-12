using Cataben.Domain.Entities;

namespace Cataben.Application.Repositories
{
    public interface IXpTransactionRepository
    {
        Task AddAsync(XpTransaction tx, CancellationToken cancellationToken = default);

        /// <summary>窗口内按 XP 流水 ∑ Amount 排名的用户（覆盖解题/成就/任务全部 3 个 XP 来源）。</summary>
        Task<IEnumerable<LeaderboardPeriodEntry>> GetLeaderboardInPeriodAsync(DateTime since, int count, CancellationToken cancellationToken = default);
    }
}
