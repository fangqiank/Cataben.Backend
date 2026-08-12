using Cataben.API.Filters;
using Cataben.Application.DTOs;
using Cataben.Domain.Entities;
using Cataben.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Cataben.API.Controllers;

// Admin catalog surface for rewards: list all (incl. inactive), create, toggle active. Rewards are
// immutable templates once created, so there is no general update/delete. Requires Admin role.
[Route("api/admin/reward")]
[ApiController]
[CustomAuthorize(UserRole.Admin)]
public class AdminRewardController(
    IRewardRepository rewardRepository,
    IUnitOfWork unitOfWork) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AdminRewardDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List()
    {
        var rewards = await rewardRepository.GetAllAsync();
        return Ok(rewards.Select(Map));
    }

    [HttpPost]
    [ProducesResponseType(typeof(AdminRewardDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] RewardCreateDto dto)
    {
        var reward = new Reward(dto.Key, dto.Name, dto.Description, dto.Category, dto.Cost,
            dto.IsProOnly, dto.Icon, dto.Order, dto.IsDefault);

        await rewardRepository.AddAsync(reward);
        await unitOfWork.SaveChangesAsync();

        return CreatedAtAction(nameof(List), Map(reward));
    }

    [HttpPost("{id:guid}/toggle-active")]
    [ProducesResponseType(typeof(AdminRewardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleActive(Guid id)
    {
        var reward = await rewardRepository.GetByIdAsync(id);
        if (reward is null) return NotFound();

        reward.SetActive(!reward.IsActive);
        await unitOfWork.SaveChangesAsync();

        return Ok(Map(reward));
    }

    private static AdminRewardDto Map(Reward r) => new()
    {
        Id = r.Id,
        Key = r.Key,
        Name = r.Name,
        Description = r.Description,
        Category = r.Category,
        Cost = r.Cost,
        IsProOnly = r.IsProOnly,
        IsDefault = r.IsDefault,
        Icon = r.Icon,
        Order = r.Order,
        IsActive = r.IsActive
    };
}
