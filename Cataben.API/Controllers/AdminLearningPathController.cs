using Cataben.API.Filters;
using Cataben.Application.DTOs;
using Cataben.Domain.Entities;
using Cataben.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cataben.API.Controllers;

[Route("api/admin/learning-path")]
[ApiController]
[CustomAuthorize(UserRole.Admin)]
public class AdminLearningPathController(
    ILearningPathRepository learningPathRepository,
    IChallengeRepository challengeRepository,
    IMediator mediator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AdminLearningPathDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List()
    {
        // onlyPublished:false includes drafts. Challenges are omitted on the list view (avoid N+1);
        // the single-GET loads them for the edit form.
        var paths = (await learningPathRepository.GetAllAsync(onlyPublished: false)).OrderBy(p => p.Order);
        return Ok(paths.Select(p => Map(p, null)));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AdminLearningPathDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id)
    {
        var path = await learningPathRepository.GetByIdAsync(id);
        if (path is null) return NotFound();

        var challenges = (await challengeRepository.GetByLearningPathAsync(id))
            .OrderBy(c => c.OrderInPath)
            .Select(c => new AdminPathChallengeDto { Id = c.Id, Title = c.Title, OrderInPath = c.OrderInPath })
            .ToList();
        return Ok(Map(path, challenges));
    }

    [HttpPost]
    [ProducesResponseType(typeof(AdminLearningPathDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateLearningPathCommand command)
    {
        var dto = await mediator.Send(command);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AdminLearningPathDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLearningPathCommand command)
    {
        command.Id = id;
        var dto = await mediator.Send(command);
        return Ok(dto);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await mediator.Send(new DeleteLearningPathCommand { Id = id });
        return NoContent();
    }

    private static AdminLearningPathDto Map(LearningPath p, List<AdminPathChallengeDto>? challenges) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Description = p.Description,
        Icon = p.Icon,
        CoverImage = p.CoverImage,
        Level = p.Level,
        Order = p.Order,
        IsPublished = p.IsPublished,
        XpReward = p.XpReward,
        GemReward = p.GemReward,
        CreatedAt = p.CreatedAt,
        PublishedAt = p.PublishedAt,
        CreatedBy = p.CreatedBy,
        Challenges = challenges ?? new List<AdminPathChallengeDto>()
    };
}
