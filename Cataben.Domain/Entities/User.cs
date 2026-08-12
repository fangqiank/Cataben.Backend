using Cataben.Domain.Enums;

namespace Cataben.Domain.Entities
{
    public class User
    {
        public Guid Id { get; private set; }
        public string Email { get; private set; } = string.Empty;
        public string Username { get; private set; } = string.Empty;
        public string ExternalId { get; private set; } = string.Empty;
        public UserRole Role { get; private set; }
        public int Xp { get; private set; }
        public int Gems { get; private set; }

        // Per-user global "reveal solution" budget (lifetime, shared across all challenges).
        // Decremented by RevealSolutionHandler; 0 disables the reveal button client-side.
        public int RevealsRemaining { get; private set; } = 3;
        public DateTime CreatedAt { get; private set; }
        public DateTime? LastActiveAt { get; private set; }
        public bool IsActive { get; private set; }
        public string? PreferredTheme { get; private set; }
        public string? AvatarUrl { get; private set; }

        private readonly List<UserAchievement> _userAchievements = new();
        public IReadOnlyCollection<UserAchievement> UserAchievements => _userAchievements.AsReadOnly();

        private readonly List<Submission> _submissions = new();
        public IReadOnlyCollection<Submission> Submissions => _submissions.AsReadOnly();

        private User() { }

        public User(string email, string username, string externalId)
        {
            Id = Guid.NewGuid();
            Email = email;
            Username = username;
            ExternalId = externalId;
            Role = UserRole.User;
            Xp = 0;
            Gems = 0;
            IsActive = true;
            CreatedAt = DateTime.UtcNow;
        }

        public void AddXp(int amount)
        {
            Xp += amount;
        }

        public void AddGems(int amount)
        {
            Gems += amount;
        }

        // Spends gems. The caller (service layer) MUST verify sufficiency first and throw
        // InsufficientGemsException — gems must never go negative. Mirrors the trust-based AddGems/AddXp
        // style (entities don't reference the Application-layer exceptions).
        public void SpendGems(int amount)
        {
            Gems -= amount;
        }

        /// <summary>
        /// Consumes one reveal credit. Returns true and decrements when credit remains;
        /// returns false (no state change) when exhausted — the caller then throws ValidationException.
        /// </summary>
        public bool UseReveal()
        {
            if (RevealsRemaining <= 0) return false;
            RevealsRemaining--;
            return true;
        }

        public void UpdateLastActive()
        {
            LastActiveAt = DateTime.UtcNow;
        }

        public void UpdateRole(UserRole role)
        {
            Role = role;
        }

        public void Deactivate()
        {
            IsActive = false;
        }

        public void Activate()
        {
            IsActive = true;
        }

        public void AddSubmission(Submission submission)
        {
            _submissions.Add(submission);
            UpdateLastActive();
        }

        public void AddUserAchievement(UserAchievement userAchievement)
        {
            _userAchievements.Add(userAchievement);
        }

        public static int ComputeLevel(int totalXp) => (int)Math.Floor(Math.Sqrt(totalXp / 100.0)) + 1;

        public int CalculateLevel() => ComputeLevel(Xp);

        public IReadOnlyCollection<UserAchievement> GetCompletedAchievements()
        {
            return _userAchievements.Where(a => a.IsCompleted).ToList().AsReadOnly();
        }

        public int GetAchievementCount()
        {
            return _userAchievements.Count(a => a.IsCompleted);
        }

        public UserAchievement? GetAchievement(string achievementId)
        {
            return _userAchievements.FirstOrDefault(a => a.AchievementId == achievementId);
        }

        public bool HasAchievement(string achievementId)
        {
            return _userAchievements.Any(a => a.AchievementId == achievementId && a.IsCompleted);
        }
    }
}
