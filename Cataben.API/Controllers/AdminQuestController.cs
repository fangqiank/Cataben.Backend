using Cataben.API.Filters;
using Cataben.Application.DTOs;
using Cataben.Domain.Entities;
using Cataben.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Cataben.API.Controllers;

// Admin catalog surface for quests: list all (incl. inactive), create, toggle active. Quests are
// immutable templates once created, so there is no general update/delete. Requires Admin role.
[Route("api/admin/quest")]
[ApiController]
[CustomAuthorize(UserRole.Admin)]
public class AdminQuestController(
    IQuestRepository questRepository,
    IUnitOfWork unitOfWork) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AdminQuestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List()
    {
        var quests = await questRepository.GetAllAsync();
        return Ok(quests.Select(Map));
    }

    [HttpPost]
    [ProducesResponseType(typeof(AdminQuestDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] QuestCreateDto dto)
    {
        var quest = new Quest(dto.Id, dto.Name, dto.Description, dto.Cadence, dto.Metric,
            dto.Threshold, dto.XpReward, dto.GemReward, dto.Icon, dto.Order);

        await questRepository.AddAsync(quest);
        await unitOfWork.SaveChangesAsync();

        return CreatedAtAction(nameof(List), Map(quest));
    }

    [HttpPost("{id}/toggle-active")]
    [ProducesResponseType(typeof(AdminQuestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleActive(string id)
    {
        var quest = await questRepository.GetByIdAsync(id);
        if (quest is null) return NotFound();

        quest.SetActive(!quest.IsActive);
        await unitOfWork.SaveChangesAsync();

        return Ok(Map(quest));
    }

    private static AdminQuestDto Map(Quest q) => new()
    {
        Id = q.Id,
        Name = q.Name,
        Description = q.Description,
        Cadence = q.Cadence,
        Metric = q.Metric,
        Threshold = q.Threshold,
        XpReward = q.XpReward,
        GemReward = q.GemReward,
        Icon = q.Icon,
        IsActive = q.IsActive,
        Order = q.Order
    };
}
