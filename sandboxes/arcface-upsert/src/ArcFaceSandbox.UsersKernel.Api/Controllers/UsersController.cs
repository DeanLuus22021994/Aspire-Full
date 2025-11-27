using ArcFaceSandbox.UsersKernel.Api.Contracts;
using ArcFaceSandbox.UsersKernel.Infrastructure.Data;
using ArcFaceSandbox.UsersKernel.Infrastructure.Entities;
using ArcFaceSandbox.UsersKernel.Infrastructure.Models;
using ArcFaceSandbox.UsersKernel.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArcFaceSandbox.UsersKernel.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class UsersController : ControllerBase
{
    private readonly SandboxUsersDbContext _dbContext;
    private readonly ISandboxUserService _userService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        SandboxUsersDbContext dbContext,
        ISandboxUserService userService,
        ILogger<UsersController> logger)
    {
        _dbContext = dbContext;
        _userService = userService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SandboxUserResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SandboxUserResponse>>> GetUsers(CancellationToken cancellationToken)
    {
        var users = await _dbContext.Users
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => SandboxUserResponse.FromEntity(u))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(users);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SandboxUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SandboxUserResponse>> GetUser(Guid id, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return user is null ? NotFound() : Ok(SandboxUserResponse.FromEntity(user));
    }

    [HttpGet("by-email/{email}")]
    [ProducesResponseType(typeof(SandboxUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SandboxUserResponse>> GetByEmail(string email, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == email.Trim(), cancellationToken)
            .ConfigureAwait(false);

        return user is null ? NotFound() : Ok(SandboxUserResponse.FromEntity(user));
    }

    [HttpPost]
    [ProducesResponseType(typeof(SandboxUserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SandboxUserResponse>> UpsertUser(
        UpsertSandboxUserRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new UserUpsertCommand(request.Email, request.DisplayName, request.Role, request.FaceImageBase64);
            var user = await _userService.UpsertAsync(command, cancellationToken).ConfigureAwait(false);
            var response = SandboxUserResponse.FromEntity(user);
            return CreatedAtAction(nameof(GetUser), new { id = response.Id }, response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid upsert request for email {Email}", request.Email);
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(SandboxUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SandboxUserResponse>> UpdateUser(
        Guid id,
        UpdateSandboxUserRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new UserUpdateCommand(request.DisplayName, request.Role, request.IsActive, request.FaceImageBase64);
            var user = await _userService.UpdateAsync(id, command, cancellationToken).ConfigureAwait(false);
            if (user is null)
            {
                return NotFound();
            }

            return Ok(SandboxUserResponse.FromEntity(user));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid update payload for user {UserId}", id);
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownsertUser(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _userService.DownsertAsync(id, cancellationToken).ConfigureAwait(false);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPost("{id:guid}/login")]
    [ProducesResponseType(typeof(SandboxUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SandboxUserResponse>> RecordLogin(Guid id, CancellationToken cancellationToken)
    {
        var user = await _userService.RecordLoginAsync(id, cancellationToken).ConfigureAwait(false);
        return user is null ? NotFound() : Ok(SandboxUserResponse.FromEntity(user));
    }
}
