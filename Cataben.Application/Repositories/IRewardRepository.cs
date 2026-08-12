using Cataben.Domain.Entities;
using Cataben.Domain.Enums;

namespace Cataben.Application.Repositories
{
    public interface IRewardRepository
    {
        Task<IEnumerable<Reward>> GetActiveCatalogAsync(CancellationToken cancellationToken = default);

        /// <summary>All reward definitions (incl. inactive), ordered by Category then Order. Admin list.</summary>
        Task<IEnumerable<Reward>> GetAllAsync(CancellationToken cancellationToken = default);

        Task<Reward?> GetByIdAsync(Guid rewardId, CancellationToken cancellationToken = default);
        Task<IEnumerable<UserReward>> GetUserRewardsAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<UserReward?> GetByUserAndRewardAsync(Guid userId, Guid rewardId, CancellationToken cancellationToken = default);
        Task<IEnumerable<UserReward>> GetEquippedInCategoryAsync(Guid userId, RewardCategory category, CancellationToken cancellationToken = default);
        Task AddAsync(UserReward userReward, CancellationToken cancellationToken = default);

        /// <summary>Add a new reward definition (catalog).</summary>
        Task AddAsync(Reward reward, CancellationToken cancellationToken = default);

        Task UpdateAsync(UserReward userReward, CancellationToken cancellationToken = default);
    }
}
