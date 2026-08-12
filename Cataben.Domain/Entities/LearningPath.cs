namespace Cataben.Domain.Entities
{
    public class LearningPath
    {
        public Guid Id { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public string Description { get; private set; } = string.Empty;
        public string? Icon { get; private set; }
        public string? CoverImage { get; private set; }
        public string Level { get; private set; } = "Beginner";
        public int Order { get; private set; }
        public bool IsPublished { get; private set; }
        public int XpReward { get; private set; }
        public int GemReward { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? PublishedAt { get; private set; }
        public Guid? CreatedBy { get; private set; }

        private readonly List<Challenge> _challenges = new();
        public IReadOnlyCollection<Challenge> Challenges => _challenges.AsReadOnly();

        private LearningPath() { }

        public LearningPath(string name, string description, string level)
        {
            Id = Guid.NewGuid();
            Name = name;
            Description = description;
            Level = level;
            CreatedAt = DateTime.UtcNow;
        }

        public void AddChallenge(Challenge challenge, int order)
        {
            challenge.SetOrderInPath(order);
            _challenges.Add(challenge);
        }

        public void RemoveChallenge(Guid challengeId)
        {
            var match = _challenges.FirstOrDefault(c => c.Id == challengeId);
            if (match is not null)
            {
                match.SetLearningPath(null);
                match.SetOrderInPath(0);
                _challenges.Remove(match);
            }
        }

        public void SetIcon(string? icon)
        {
            Icon = icon;
        }

        public void UpdateRewards(int xpReward, int gemReward)
        {
            XpReward = xpReward;
            GemReward = gemReward;
        }

        public void SetOrder(int order)
        {
            Order = order;
        }

        /// <summary>General admin update for catalog fields (name/description/level/icon/cover).</summary>
        public void Update(string name, string description, string level, string? icon, string? coverImage)
        {
            Name = name;
            Description = description;
            Level = level;
            Icon = icon;
            CoverImage = coverImage;
        }

        public void Publish()
        {
            IsPublished = true;
            PublishedAt = DateTime.UtcNow;
        }

        public void Unpublish()
        {
            IsPublished = false;
        }
    }
}
