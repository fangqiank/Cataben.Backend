using Cataben.API.Services;
using Cataben.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cataben.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [EnableRateLimiting("Default")]
    public class RewardController(
        IRewardService rewardService,
        ICurrentUserService currentUser
    ) : ControllerBase
    {
        [HttpGet]
        [ProducesResponseType(typeof(RewardStoreDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetStore()
        {
            var userId = await currentUser.GetUserIdAsync();
            if (userId is null) return NotFound();

            var store = await rewardService.GetRewardStoreAsync(userId.Value);
            return Ok(store);
        }

        [HttpPost("{rewardId:guid}/redeem")]
        [ProducesResponseType(typeof(RewardStoreDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Redeem(Guid rewardId)
        {
            var userId = await currentUser.GetUserIdAsync();
            if (userId is null) return NotFound();

            // InsufficientGemsException -> 400 "宝石不足"; Pro-only / already-owned-noop handled in service.
            var store = await rewardService.RedeemAsync(userId.Value, rewardId);
            return Ok(store);
        }

        [HttpPost("{rewardId:guid}/equip")]
        [ProducesResponseType(typeof(RewardStoreDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Equip(Guid rewardId)
        {
            var userId = await currentUser.GetUserIdAsync();
            if (userId is null) return NotFound();

            var store = await rewardService.EquipAsync(userId.Value, rewardId);
            return Ok(store);
        }
    }
}
