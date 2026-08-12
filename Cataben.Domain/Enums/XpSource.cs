namespace Cataben.Domain.Enums
{
    /// <summary>XP 流水的来源：解题 / 成就解锁 / 任务领奖。时段排行榜按 CreatedAt 窗口聚合。</summary>
    public enum XpSource
    {
        Challenge = 0,
        Achievement = 1,
        Quest = 2
    }
}
