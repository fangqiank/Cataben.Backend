using Cataben.Domain.Entities;

namespace Cataben.Domain.Events
{
    public class ChallengeCompletedEvent(Submission submission)
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid UserId { get; private set; } = submission.UserId;
        public Guid ChallengeId { get; private set; } = submission.ChallengeId;
        public Guid SubmissionId { get; private set; } = submission.Id;
        public int Score { get; private set; } = submission.Score;
        public int TotalScore { get; private set; } = submission.TotalScore;
        public bool IsSuccessful { get; private set; } = submission.IsSuccessful;
        public long ExecutionTimeMs { get; private set; } = submission.ExecutionTimeMs;
        public DateTime CompletedAt { get; private set; } = submission.CompletedAt ?? DateTime.UtcNow;
    }
}
