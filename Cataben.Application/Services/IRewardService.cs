using Cataben.Application.DTOs;

namespace Cataben.Application.Services
{
    public interface IRewardService
    {
        /// <summary>The full active catalog merged with the user's ownership/equip state.</summary>
        Task<RewardStoreDto> GetRewardStoreAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>Spend gems to unlock a reward (idempotent if already owned; Pro-only rejected;
        /// insufficient gems throws InsufficientGemsException). Returns the refreshed store.</summary>
        Task<RewardStoreDto> RedeemAsync(Guid userId, Guid rewardId, CancellationToken cancellationToken = default);

        /// <summary>Equip a owned title/theme (unequips the others in that category). The default theme is
        /// granted on equip at no cost. Returns the refreshed store.</summary>
        Task<RewardStoreDto> EquipAsync(Guid userId, Guid rewardId, CancellationToken cancellationToken = default);
    }
}
