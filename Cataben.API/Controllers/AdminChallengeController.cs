using Cataben.API.Filters;
using Cataben.Application.DTOs;
using Cataben.Domain.Entities;
using Cataben.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cataben.API.Controllers;

// Admin CRUD surface for challenges. Unlike the public ChallengeController, this exposes
// SolutionCode + private test cases and lets admins create/update/delete. All actions require
// Admin role; the role claim is injected by RoleClaimsTransformation.
[Route("api/admin/challenge")]
[ApiController]
[CustomAuthorize(UserRole.Admin)]
public class AdminChallengeController(
    IChallengeRepository challengeRepository,
    IMediator mediator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AdminChallengeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] int pageSize = 500)
    {
        var challenges = await challengeRepository.GetAllAsync(null, null, 1, pageSize);
        return Ok(challenges.Select(MapAdmin));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AdminChallengeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id)
    {
        var challenge = await challengeRepository.GetByIdAsync(id);
        return challenge is null ? NotFound() : Ok(MapAdmin(challenge));
    }

    [HttpPost]
    [ProducesResponseType(typeof(AdminChallengeDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateChallengeCommand command)
    {
        var created = await mediator.Send(command);
        var challenge = await challengeRepository.GetByIdAsync(created.Id);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, MapAdmin(challenge!));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AdminChallengeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateChallengeCommand command)
    {
        command.Id = id;
        var updated = await mediator.Send(command);
        var challenge = await challengeRepository.GetByIdAsync(updated.Id);
        return Ok(MapAdmin(challenge!));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await mediator.Send(new DeleteChallengeCommand { Id = id });
        return NoContent();
    }

    private static AdminChallengeDto MapAdmin(Challenge c) => new()
    {
        Id = c.Id,
        Title = c.Title,
        Description = c.Description,
        Type = c.Type,
        Difficulty = c.Difficulty.Name,
        Category = c.Category,
        InitialCode = c.InitialCode,
        SolutionCode = c.SolutionCode,
        Hints = c.Hints.ToList(),
        XpReward = c.XpReward,
        GemReward = c.GemReward,
        TimeLimitSeconds = c.TimeLimitSeconds,
        MemoryLimitMb = c.MemoryLimitMb,
        IsActive = c.IsActive,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt,
        LearningPathId = c.LearningPathId,
        OrderInPath = c.OrderInPath,
        TestCases = c.TestCases.Select(t => new TestCaseDto
        {
            Name = t.Name,
            Input = t.Input,
            ExpectedOutput = t.ExpectedOutput,
            IsPublic = t.IsPublic,
            Weight = t.Weight
        }).ToList()
    };
}
