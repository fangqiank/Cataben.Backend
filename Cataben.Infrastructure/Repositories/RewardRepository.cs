using Cataben.Application.Repositories;
using Cataben.Domain.Entities;
using Cataben.Domain.Enums;
using Cataben.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cataben.Infrastructure.Repositories
{
    public class RewardRepository(AppDbContext context) : IRewardRepository
    {
        public async Task<IEnumerable<Reward>> GetActiveCatalogAsync(CancellationToken cancellationToken = default)
        {
            return await context.Rewards
                .Where(r => r.IsActive)
                .OrderBy(r => r.Category)
                .ThenBy(r => r.Order)
                .ToListAsync(cancellationToken);
        }

        public async Task<Reward?> GetByIdAsync(Guid rewardId, CancellationToken cancellationToken = default)
        {
            return await context.Rewards.FirstOrDefaultAsync(r => r.Id == rewardId, cancellationToken);
        }

        public async Task<IEnumerable<Reward>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await context.Rewards
                .OrderBy(r => r.Category)
                .ThenBy(r => r.Order)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<UserReward>> GetUserRewardsAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await context.UserRewards
                .Include(ur => ur.Reward)
                .Where(ur => ur.UserId == userId)
                .ToListAsync(cancellationToken);
        }

        public async Task<UserReward?> GetByUserAndRewardAsync(Guid userId, Guid rewardId, CancellationToken cancellationToken = default)
        {
            return await context.UserRewards
                .Include(ur => ur.Reward)
                .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RewardId == rewardId, cancellationToken);
        }

        public async Task<IEnumerable<UserReward>> GetEquippedInCategoryAsync(Guid userId, RewardCategory category, CancellationToken cancellationToken = default)
        {
            return await context.UserRewards
                .Include(ur => ur.Reward)
                .Where(ur => ur.UserId == userId && ur.IsEquipped && ur.Reward.Category == category)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(UserReward userReward, CancellationToken cancellationToken = default)
        {
            await context.UserRewards.AddAsync(userReward, cancellationToken);
        }

        public async Task AddAsync(Reward reward, CancellationToken cancellationToken = default)
        {
            await context.Rewards.AddAsync(reward, cancellationToken);
        }

        public Task UpdateAsync(UserReward userReward, CancellationToken cancellationToken = default)
        {
            context.UserRewards.Update(userReward);
            return Task.CompletedTask;
        }
    }
}
