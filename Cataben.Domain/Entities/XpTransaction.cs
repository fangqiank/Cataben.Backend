using Cataben.Domain.Enums;

namespace Cataben.Domain.Entities
{
    /// <summary>
    /// Append-only XP 流水账：每一笔 XP 发放（解题 / 成就解锁 / 任务领奖）各记一行。
    /// 时段排行榜按 <see cref="CreatedAt"/> 窗口 ∑ <see cref="Amount"/> 聚合，覆盖全部 3 个 XP 来源，
    /// 从而比"按 Submissions ∑ Challenge.XpReward"更精确（后者漏算成就/任务 XP）。
    /// 历史数据由迁移 AddXpLedger 从 Submissions/UserAchievements/UserQuests 回填。
    /// </summary>
    public class XpTransaction
    {
        public Guid Id { get; private set; }
        public Guid UserId { get; private set; }
        public int Amount { get; private set; }
        public XpSource Source { get; private set; }
        /// <summary>来源实体 id（Challenge=Guid、Achievement/Quest=string），统一以字符串存。</summary>
        public string? SourceId { get; private set; }
        public string? Reason { get; private set; }
        public DateTime CreatedAt { get; private set; }

        private XpTransaction() { }

        public XpTransaction(Guid userId, int amount, XpSource source, string? sourceId = null, string? reason = null)
        {
            Id = Guid.NewGuid();
            UserId = userId;
            Amount = amount;
            Source = source;
            SourceId = sourceId;
            Reason = reason;
            CreatedAt = DateTime.UtcNow;
        }
    }
}
