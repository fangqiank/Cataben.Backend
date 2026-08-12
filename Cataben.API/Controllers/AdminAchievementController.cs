using Cataben.API.Filters;
using Cataben.Application.DTOs;
using Cataben.Domain.Entities;
using Cataben.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Cataben.API.Controllers;

// Admin catalog surface for achievements: list all, create, toggle hidden (visibility). Achievements
// are immutable templates once created; Xp/Gem derive from Rarity in the constructor. Requires Admin.
[Route("api/admin/achievement")]
[ApiController]
[CustomAuthorize(UserRole.Admin)]
public class AdminAchievementController(
    IAchievementRepository achievementRepository,
    IUnitOfWork unitOfWork) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AdminAchievementDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List()
    {
        var achievements = await achievementRepository.GetAllAsync();
        return Ok(achievements.Select(Map));
    }

    [HttpPost]
    [ProducesResponseType(typeof(AdminAchievementDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] AchievementCreateDto dto)
    {
        var achievement = new Achievement(dto.Id, dto.Name, dto.Description, dto.Category, dto.Rarity,
            dto.RequiredProgress, dto.Type, dto.Icon, dto.BadgeColor);

        // IsHidden / Order aren't ctor params; set them after construction.
        if (dto.IsHidden) achievement.SetHidden(true);

        await achievementRepository.AddAsync(achievement);
        await unitOfWork.SaveChangesAsync();

        return CreatedAtAction(nameof(List), Map(achievement));
    }

    [HttpPost("{id}/toggle-hidden")]
    [ProducesResponseType(typeof(AdminAchievementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleHidden(string id)
    {
        var achievement = await achievementRepository.GetByIdAsync(id);
        if (achievement is null) return NotFound();

        achievement.SetHidden(!achievement.IsHidden);
        await unitOfWork.SaveChangesAsync();

        return Ok(Map(achievement));
    }

    private static AdminAchievementDto Map(Achievement a) => new()
    {
        Id = a.Id,
        Name = a.Name,
        Description = a.Description,
        Category = a.Category,
        Rarity = a.Rarity,
        RequiredProgress = a.RequiredProgress,
        XpReward = a.XpReward,
        GemReward = a.GemReward,
        Icon = a.Icon,
        BadgeColor = a.BadgeColor,
        Type = a.Type,
        IsHidden = a.IsHidden,
        Order = a.Order
    };
}
