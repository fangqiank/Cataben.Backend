using Cataben.Domain.Enums;

namespace Cataben.Application.DTOs
{
    public class UserQuestDto
    {
        public Guid Id { get; set; }              // Guid.Empty for virtual (un-started) rows
        public string QuestId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public QuestCadence Cadence { get; set; }
        public QuestMetric Metric { get; set; }
        public int Progress { get; set; }
        public int Threshold { get; set; }
        public int XpReward { get; set; }
        public int GemReward { get; set; }
        public string Icon { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
        public bool IsClaimed { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? ClaimedAt { get; set; }
        public DateTime WindowStart { get; set; }
        public DateTime WindowEnd { get; set; }
    }
}
