using Cataben.Application.DTOs;
using Cataben.Application.Exceptions;
using Cataben.Application.Services;
using Cataben.Domain.Entities;
using Cataben.Domain.Enums;

namespace Cataben.Infrastructure.Services
{
    /// <summary>
    /// Reward shop: reads the catalog merged with a user's ownership/equip state, and executes real gem
    /// redemptions + equip changes. Redemptions spend gems (User.SpendGems) and persist a UserReward row
    /// in the same transaction (UnitOfWork). Pro-only rewards are never redeemable. Titles/themes can be
    /// equipped (one active per category); the default theme is virtually owned and equipped until the
    /// user equips something else.
    /// NOTE: cosmetic EFFECTS (title shown under the username, theme actually restyling the app, streak
    /// freeze auto-consumed on a missed day) are NOT wired here — this service owns the gem economy +
    /// ownership/equip persistence only.
    /// </summary>
    public class RewardService(
        IRewardRepository rewardRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork) : IRewardService
    {
        public async Task<RewardStoreDto> GetRewardStoreAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var catalog = (await rewardRepository.GetActiveCatalogAsync(cancellationToken)).ToList();
            var rows = (await rewardRepository.GetUserRewardsAsync(userId, cancellationToken)).ToList();
            return BuildStore(catalog, rows);
        }

        public async Task<RewardStoreDto> RedeemAsync(Guid userId, Guid rewardId, CancellationToken cancellationToken = default)
        {
            var reward = await rewardRepository.GetByIdAsync(rewardId, cancellationToken)
                ?? throw new NotFoundException($"Reward {rewardId} not found.");

            if (reward.IsProOnly)
                throw new ValidationException("该奖励为 Pro 专属，请升级会员后再来。");

            // Idempotent: already owned — return current state without charging again.
            var existing = await rewardRepository.GetByUserAndRewardAsync(userId, reward.Id, cancellationToken);
            if (existing is not null)
                return await GetRewardStoreAsync(userId, cancellationToken);

            var user = await userRepository.GetByIdBasicAsync(userId, cancellationToken)
                ?? throw new NotFoundException($"User {userId} not found.");

            // Paid items spend gems (sufficiency checked here; entity keeps a trust-based decrement).
            // Free items (e.g. default theme) skip the charge.
            if (reward.Cost > 0)
            {
                if (user.Gems < reward.Cost)
                    throw new InsufficientGemsException();
                user.SpendGems(reward.Cost);
            }

            var row = new UserReward(user, reward);

            // Auto-equip the first title/theme a user buys (nice UX); Pro-only is unreachable here.
            if (reward.Category is RewardCategory.Title or RewardCategory.Theme)
            {
                var equipped = await rewardRepository.GetEquippedInCategoryAsync(userId, reward.Category, cancellationToken);
                if (!equipped.Any())
                    row.Equip();
            }

            await rewardRepository.AddAsync(row, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken); // User gem decrement + new row in one tx

            return await GetRewardStoreAsync(userId, cancellationToken);
        }

        public async Task<RewardStoreDto> EquipAsync(Guid userId, Guid rewardId, CancellationToken cancellationToken = default)
        {
            var reward = await rewardRepository.GetByIdAsync(rewardId, cancellationToken)
                ?? throw new NotFoundException($"Reward {rewardId} not found.");

            if (reward.Category == RewardCategory.StreakFreeze)
                throw new ValidationException("该奖励无法装备。");
            if (reward.IsProOnly)
                throw new ValidationException("该奖励为 Pro 专属，请升级会员后再来。");

            var row = await rewardRepository.GetByUserAndRewardAsync(userId, reward.Id, cancellationToken);
            if (row is null)
            {
                if (reward.IsDefault)
                {
                    // The default theme has no purchase row; grant ownership on equip at no cost.
                    var owner = await userRepository.GetByIdBasicAsync(userId, cancellationToken)
                        ?? throw new NotFoundException($"User {userId} not found.");
                    row = new UserReward(owner, reward);
                    await rewardRepository.AddAsync(row, cancellationToken);
                }
                else
                {
                    throw new ValidationException("请先兑换该奖励。");
                }
            }

            // Single-equipped invariant: unequip everything else in this category, then equip the target.
            var equipped = await rewardRepository.GetEquippedInCategoryAsync(userId, reward.Category, cancellationToken);
            foreach (var other in equipped)
            {
                if (other.Id != row.Id)
                    other.Unequip();
            }
            row.Equip();

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return await GetRewardStoreAsync(userId, cancellationToken);
        }

        private static RewardStoreDto BuildStore(IReadOnlyCollection<Reward> catalog, IReadOnlyList<UserReward> rows)
        {
            var rowByReward = rows.ToDictionary(r => r.RewardId);
            var equippedThemeRewardId = rows.FirstOrDefault(r => r.IsEquipped && r.Reward.Category == RewardCategory.Theme)?.RewardId;
            var defaultTheme = catalog.FirstOrDefault(r => r.Category == RewardCategory.Theme && r.IsDefault);

            var dtos = catalog.Select(r =>
            {
                var owned = rowByReward.ContainsKey(r.Id);
                var equipped = rowByReward.TryGetValue(r.Id, out var row) && row.IsEquipped;

                // The default theme is virtually owned by everyone and shown equipped until the user
                // equips a different theme.
                if (r.IsDefault)
                {
                    owned = true;
                    if (equippedThemeRewardId is null && (defaultTheme is null || r.Id == defaultTheme.Id))
                        equipped = true;
                }

                return new RewardDto
                {
                    Id = r.Id,
                    Key = r.Key,
                    Name = r.Name,
                    Description = r.Description,
                    Category = r.Category,
                    Cost = r.Cost,
                    IsProOnly = r.IsProOnly,
                    Icon = r.Icon,
                    IsOwned = owned,
                    IsEquipped = equipped
                };
            }).ToList();

            return new RewardStoreDto { Rewards = dtos };
        }
    }
}
