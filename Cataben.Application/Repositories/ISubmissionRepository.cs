using Cataben.Domain.Entities;
using Cataben.Domain.Enums;

namespace Cataben.Application.Repositories
{
    public sealed record ChallengeSubmissionStats(int TotalSubmissions, int SuccessfulSubmissions);

    public interface ISubmissionRepository
    {
        Task AddAsync(Submission submission, CancellationToken cancellationToken = default);
        Task<Submission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task LockByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IEnumerable<Submission>> GetSubmissionsByChallengeAsync(Guid challengeId, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);
        Task<IEnumerable<Submission>> GetSubmissionsByStatusAsync(SubmissionStatus status, int limit = 100, CancellationToken cancellationToken = default);
        Task<Dictionary<SubmissionStatus, int>> GetSubmissionStatsAsync(Guid? userId = null, CancellationToken cancellationToken = default);
        Task<int> GetUserSubmissionForChallenge(Guid userId, Guid challengeId, CancellationToken cancellationToken = default);
        Task<int> GetAttemptCountForChallenge(Guid userId, Guid challengeId, CancellationToken cancellationToken = default);
        Task<int> GetUserSubmissionCountAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<int> GetUserSuccessfulCountAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<IEnumerable<DateTime>> GetUserSubmissionDatesAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Guid>> GetSolvedChallengeIdsAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Submission>> GetUserSubmissionsAsync(Guid userId, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);
        Task<IEnumerable<Submission>> GetUserSuccessfulSubmissions(Guid userId, CancellationToken cancellationToken = default);
        Task<Dictionary<Guid, int>> GetSolvedCountsAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default);
        Task<Dictionary<Guid, ChallengeSubmissionStats>> GetChallengeStatsAsync(IEnumerable<Guid> challengeIds, CancellationToken cancellationToken = default);
        Task UpdateAsync(Submission submission, CancellationToken cancellationToken = default);
        Task UpdateStatusAsync(Submission submission, CancellationToken cancellationToken = default);

        // Per-day submission counts (for the activity heatmap). Server-side GroupBy on SubmittedAt.Date.
        Task<IEnumerable<SubmissionDayCount>> GetUserSubmissionCountsByDayAsync(Guid userId, DateTime since, CancellationToken cancellationToken = default);
        // Windowed counts used by the quest engine (absolute progress recompute).
        Task<int> GetUserSubmissionCountInWindowAsync(Guid userId, DateTime from, DateTime to, CancellationToken cancellationToken = default);
        Task<IEnumerable<Guid>> GetUserSolvedChallengeIdsInWindowAsync(Guid userId, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    }
}
