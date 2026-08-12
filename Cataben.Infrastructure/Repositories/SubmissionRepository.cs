using Cataben.Domain.Entities;
using Cataben.Domain.Enums;
using Cataben.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cataben.Infrastructure.Repositories
{
    public class SubmissionRepository(AppDbContext context) : ISubmissionRepository
    {
        public async Task<Submission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await context.Submissions
                .Include(s => s.TestResults)
                .Include(s => s.User)
                .Include(s => s.Challenge)
                .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        }

        public async Task<IEnumerable<Submission>> GetUserSubmissionsAsync(
            Guid userId,
            int page = 1,
            int pageSize = 50,
            CancellationToken cancellationToken = default)
        {
            return await context.Submissions
                .Include(s => s.TestResults)
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.SubmittedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Submission>> GetUserSuccessfulSubmissions(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return await context.Submissions
                .Where(s => s.UserId == userId && s.IsSuccessful && s.Status == SubmissionStatus.Completed)
                .ToListAsync(cancellationToken);
        }

        public async Task<int> GetUserSubmissionForChallenge(
            Guid userId,
            Guid challengeId,
            CancellationToken cancellationToken = default)
        {
            // Counts SUCCESSFUL submissions only, so that:
            //  - the "already solved" gate (SubmitChallengeHandler) does not block retries after a failure, and
            //  - the IsSolved flag on challenges reflects an actual success.
            return await context.Submissions
                .Where(s => s.UserId == userId
                    && s.ChallengeId == challengeId
                    && s.Status == SubmissionStatus.Completed
                    && s.IsSuccessful)
                .CountAsync(cancellationToken);
        }

        public async Task<int> GetAttemptCountForChallenge(
            Guid userId,
            Guid challengeId,
            CancellationToken cancellationToken = default)
        {
            return await context.Submissions
                .Where(s => s.UserId == userId && s.ChallengeId == challengeId)
                .CountAsync(cancellationToken);
        }

        public async Task<int> GetUserSubmissionCountAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await context.Submissions
                .CountAsync(s => s.UserId == userId, cancellationToken);
        }

        public async Task<int> GetUserSuccessfulCountAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await context.Submissions
                .CountAsync(s => s.UserId == userId && s.IsSuccessful && s.Status == SubmissionStatus.Completed, cancellationToken);
        }

        public async Task<IEnumerable<DateTime>> GetUserSubmissionDatesAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await context.Submissions
                .Where(s => s.UserId == userId)
                .Select(s => s.SubmittedAt.Date)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Guid>> GetSolvedChallengeIdsAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await context.Submissions
                .Where(s => s.UserId == userId && s.IsSuccessful && s.Status == SubmissionStatus.Completed)
                .Select(s => s.ChallengeId)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(Submission submission, CancellationToken cancellationToken = default)
        {
            await context.Submissions.AddAsync(submission, cancellationToken);
        }

        public Task UpdateAsync(Submission submission, CancellationToken cancellationToken = default)
        {
            context.Submissions.Update(submission);
            return Task.CompletedTask;
        }

        public Task UpdateStatusAsync(Submission submission, CancellationToken cancellationToken = default)
        {
            context.Submissions.Update(submission);
            return Task.CompletedTask;
        }

        public async Task<IEnumerable<Submission>> GetSubmissionsByStatusAsync(
            SubmissionStatus status,
            int limit = 100,
            CancellationToken cancellationToken = default)
        {
            return await context.Submissions
                .Where(s => s.Status == status)
                .OrderBy(s => s.SubmittedAt)
                .Take(limit)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Submission>> GetSubmissionsByChallengeAsync(Guid challengeId, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
        { 
            return await context.Submissions
                .Where(s => s.ChallengeId == challengeId)
                .OrderByDescending(s => s.SubmittedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
        }

        public async Task<Dictionary<SubmissionStatus, int>> GetSubmissionStatsAsync(
            Guid? userId = null,
            CancellationToken cancellationToken = default)
        {
            var query = context.Submissions.AsQueryable();
            if (userId.HasValue) query = query.Where(s => s.UserId == userId.Value);

            // Single grouped query instead of one query per status value.
            var counts = await query
                .GroupBy(s => s.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            var stats = Enum.GetValues(typeof(SubmissionStatus))
                .Cast<SubmissionStatus>()
                .ToDictionary(status => status, status => 0);

            foreach (var c in counts)
                stats[c.Status] = c.Count;

            return stats;
        }

        public async Task<IEnumerable<SubmissionDayCount>> GetUserSubmissionCountsByDayAsync(
            Guid userId,
            DateTime since,
            CancellationToken cancellationToken = default)
        {
            // Server-side grouping by calendar day. SubmittedAt.Date is translatable by Npgsql
            // (already used by GetUserSubmissionDatesAsync), so this is a single round-trip and
            // powers the activity heatmap.
            return await context.Submissions
                .Where(s => s.UserId == userId && s.SubmittedAt >= since)
                .GroupBy(s => s.SubmittedAt.Date)
                .Select(g => new SubmissionDayCount(g.Key, g.Count()))
                .ToListAsync(cancellationToken);
        }

        public async Task<int> GetUserSubmissionCountInWindowAsync(
            Guid userId,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default)
        {
            // Counts ALL submissions (success + failure) — the Submissions quest metric rewards effort.
            return await context.Submissions
                .CountAsync(s => s.UserId == userId
                    && s.SubmittedAt >= from
                    && s.SubmittedAt < to, cancellationToken);
        }

        public async Task<IEnumerable<Guid>> GetUserSolvedChallengeIdsInWindowAsync(
            Guid userId,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default)
        {
            // Solved = completed + successful, de-duplicated by challenge.
            return await context.Submissions
                .Where(s => s.UserId == userId
                    && s.IsSuccessful
                    && s.Status == SubmissionStatus.Completed
                    && s.SubmittedAt >= from
                    && s.SubmittedAt < to)
                .Select(s => s.ChallengeId)
                .Distinct()
                .ToListAsync(cancellationToken);
        }
    }
}   
    