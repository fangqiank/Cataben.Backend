using Cataben.Application.DTOs;
using Cataben.Application.Exceptions;
using Cataben.Domain.Entities;
using Cataben.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cataben.Infrastructure.Services
{
    /// <summary>
    /// Quest progress engine. Windows are computed lazily from <see cref="DateTime.UtcNow"/>
    /// (daily = today 00:00 UTC, weekly = most recent Monday 00:00 UTC), so there is NO background
    /// reset job — this keeps <c>Cataben.Worker</c> free of any Infrastructure dependency.
    /// Progress is an ABSOLUTE recompute over the submissions table, so
    /// <see cref="CheckAndProgressQuestsAsync"/> is idempotent under retries. Completion and reward are
    /// decoupled: crossing the threshold only marks complete; an explicit <see cref="ClaimRewardAsync"/>
    /// awards XP/gems once. This service persists its own quest rows (separate from the caller's
    /// SaveChanges) so a quest write failure can never roll back a submission.
    /// </summary>
    public class QuestService(
        IQuestRepository questRepository,
        ISubmissionRepository submissionRepository,
        IChallengeRepository challengeRepository,
        IUserRepository userRepository,
        IXpTransactionRepository xpTransactionRepository,
        IUnitOfWork unitOfWork,
        ILogger<QuestService> logger) : IQuestService
    {
        public async Task<IEnumerable<UserQuestDto>> GetActiveUserQuestsAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var quests = (await questRepository.GetActiveAsync(cancellationToken)).ToList();
            var userRows = (await questRepository.GetByUserAsync(userId, cancellationToken)).ToList();

            var dtos = new List<UserQuestDto>();
            var shown = new HashSet<string>();

            // Current-window row (real or virtual) for every active quest, so the UI always shows all quests.
            foreach (var quest in quests)
            {
                var windowStart = CurrentWindowStart(quest.Cadence);
                // Compare Ticks, not ==: Npgsql returns WindowStart with Kind=Unspecified while
                // DateTime.UtcNow.Date has Kind=Utc — DateTime equality is Kind-sensitive.
                var row = userRows.FirstOrDefault(r => r.QuestId == quest.Id && r.WindowStart.Ticks == windowStart.Ticks);
                dtos.Add(ToDto(row, quest, windowStart));
                shown.Add(Key(row?.Id ?? Guid.Empty, quest.Id, windowStart));
            }

            // Any completed-but-unclaimed row from a PREVIOUS window — the user must still be able to claim it.
            foreach (var row in userRows.Where(r => r.IsCompleted && !r.IsClaimed))
            {
                var key = Key(row.Id, row.QuestId, row.WindowStart);
                if (shown.Add(key))
                    dtos.Add(ToDto(row, row.Quest, row.WindowStart));
            }

            return dtos;
        }

        public async Task<IEnumerable<UserQuest>> CheckAndProgressQuestsAsync(
            Guid userId,
            QuestTrigger trigger,
            CancellationToken cancellationToken = default)
        {
            var changed = new List<UserQuest>();

            try
            {
                var quests = (await questRepository.GetActiveAsync(cancellationToken)).ToList();
                if (quests.Count == 0)
                    return changed;

                var user = await userRepository.GetByIdAsync(userId, cancellationToken);
                if (user is null)
                    return changed;

                foreach (var quest in quests)
                {
                    var windowStart = CurrentWindowStart(quest.Cadence);
                    var windowEnd = windowStart.AddDays(quest.Cadence == QuestCadence.Daily ? 1 : 7);

                    // Absolute recompute from the submissions table — idempotent regardless of trigger/retry.
                    var absolute = await ComputeAbsoluteProgressAsync(quest.Metric, userId, windowStart, windowEnd, cancellationToken);
                    if (absolute <= 0)
                        continue; // no activity in this window yet — don't create a row

                    var row = await questRepository.GetByWindowAsync(userId, quest.Id, windowStart, cancellationToken);
                    if (row is null)
                    {
                        row = new UserQuest(user, quest, windowStart);
                        await questRepository.AddAsync(row, cancellationToken);
                    }

                    var prevProgress = row.Progress;
                    var prevCompleted = row.IsCompleted;
                    row.UpdateProgress(absolute); // monotonic + idempotent (clamps to Threshold, marks complete)
                    if (row.Progress != prevProgress || row.IsCompleted != prevCompleted)
                        changed.Add(row);
                }

                if (changed.Count > 0)
                    await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                // Best-effort: quest progress is self-healing (absolute recompute on the next submission
                // converges to the correct value). The submission itself was persisted by the handler
                // BEFORE this call, so it is never lost. Concurrent first-row races surface as a
                // DbUpdateException on the unique index and land here too.
                logger.LogError(ex, "Error progressing quests for user {UserId}", userId);
            }

            return changed;
        }

        public async Task<UserQuestDto?> ClaimRewardAsync(Guid userId, Guid userQuestId, CancellationToken cancellationToken = default)
        {
            var row = await questRepository.GetByIdAsync(userQuestId, cancellationToken);
            if (row is null || row.UserId != userId)
                return null; // not found, or not owned by this user

            if (!row.IsCompleted)
                throw new ValidationException("Quest is not completed yet.");

            var claimed = row.Claim(); // idempotent: awards XP/gems only when completed && !claimed
            if (claimed && row.Quest.XpReward > 0)
            {
                await xpTransactionRepository.AddAsync(
                    new XpTransaction(userId, row.Quest.XpReward, XpSource.Quest, row.Quest.Id),
                    cancellationToken);
            }
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return ToDto(row, row.Quest, row.WindowStart);
        }

        private async Task<int> ComputeAbsoluteProgressAsync(
            QuestMetric metric,
            Guid userId,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken)
        {
            return metric switch
            {
                QuestMetric.Submissions => await submissionRepository
                    .GetUserSubmissionCountInWindowAsync(userId, from, to, cancellationToken),

                QuestMetric.SolvedChallenges => (await submissionRepository
                    .GetUserSolvedChallengeIdsInWindowAsync(userId, from, to, cancellationToken)).Count(),

                QuestMetric.DistinctDifficulties => await CountDistinctDifficultiesAsync(userId, from, to, cancellationToken),

                _ => 0
            };
        }

        private async Task<int> CountDistinctDifficultiesAsync(Guid userId, DateTime from, DateTime to, CancellationToken cancellationToken)
        {
            var solvedIds = (await submissionRepository
                .GetUserSolvedChallengeIdsInWindowAsync(userId, from, to, cancellationToken)).ToList();

            if (solvedIds.Count == 0)
                return 0;

            var challenges = await challengeRepository.GetByIdsAsync(solvedIds, cancellationToken);
            return challenges.Select(c => c.Difficulty.Name).Distinct().Count();
        }

        /// <summary>Current cadence window start (UTC). Daily = today 00:00; Weekly = most recent Monday 00:00.</summary>
        private static DateTime CurrentWindowStart(QuestCadence cadence)
        {
            var now = DateTime.UtcNow;
            if (cadence == QuestCadence.Weekly)
            {
                // Days elapsed since the most recent Monday (0..6, Monday == 0). Sunday (0) → 6, not +1.
                var sinceMonday = ((int)now.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
                return now.Date.AddDays(-sinceMonday);
            }
            return now.Date;
        }

        private static UserQuestDto ToDto(UserQuest? row, Quest quest, DateTime windowStart)
        {
            // Virtual (un-started) rows get Guid.Empty id + zeroed progress; the frontend renders them
            // with no claim button so the full set of active quests is always visible.
            if (row is null)
            {
                return new UserQuestDto
                {
                    Id = Guid.Empty,
                    QuestId = quest.Id,
                    Name = quest.Name,
                    Description = quest.Description,
                    Cadence = quest.Cadence,
                    Metric = quest.Metric,
                    Progress = 0,
                    Threshold = quest.Threshold,
                    XpReward = quest.XpReward,
                    GemReward = quest.GemReward,
                    Icon = quest.Icon,
                    IsCompleted = false,
                    IsClaimed = false,
                    CompletedAt = null,
                    ClaimedAt = null,
                    WindowStart = windowStart,
                    WindowEnd = windowStart.AddDays(quest.Cadence == QuestCadence.Daily ? 1 : 7)
                };
            }

            return new UserQuestDto
            {
                Id = row.Id,
                QuestId = quest.Id,
                Name = quest.Name,
                Description = quest.Description,
                Cadence = quest.Cadence,
                Metric = quest.Metric,
                Progress = row.Progress,
                Threshold = quest.Threshold,
                XpReward = quest.XpReward,
                GemReward = quest.GemReward,
                Icon = quest.Icon,
                IsCompleted = row.IsCompleted,
                IsClaimed = row.IsClaimed,
                CompletedAt = row.CompletedAt,
                ClaimedAt = row.ClaimedAt,
                WindowStart = row.WindowStart,
                WindowEnd = row.WindowEnd
            };
        }

        private static string Key(Guid rowId, string questId, DateTime windowStart) => $"{questId}|{windowStart.Ticks}|{rowId}";
    }
}
