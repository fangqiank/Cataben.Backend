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
    public class QuestController(
        IQuestService questService,
        ICurrentUserService currentUser
        ) : ControllerBase
    {
        [HttpGet("active")]
        [ProducesResponseType(typeof(IEnumerable<UserQuestDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetActiveQuests()
        {
            var userId = await currentUser.GetUserIdAsync();
            if (userId is null) return NotFound();

            var quests = await questService.GetActiveUserQuestsAsync(userId.Value);
            return Ok(quests);
        }

        [HttpPost("{userQuestId:guid}/claim")]
        [ProducesResponseType(typeof(UserQuestDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ClaimReward(Guid userQuestId)
        {
            var userId = await currentUser.GetUserIdAsync();
            if (userId is null) return NotFound();

            // null => not found or not owned by this user; not-yet-completed throws ValidationException (->400).
            var result = await questService.ClaimRewardAsync(userId.Value, userQuestId);
            if (result is null)
                return NotFound();

            return Ok(result);
        }
    }
}
