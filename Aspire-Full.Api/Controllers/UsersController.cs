using Aspire_Full.Api.Data;
using Aspire_Full.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Aspire_Full.Api.Controllers;

/// <summary>
/// Controller for standard user operations including upsert and soft delete (downsert).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<UsersController> _logger;

    public UsersController(AppDbContext context, ILogger<UsersController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all active users.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserResponseDto>>> GetUsers()
    {
        _logger.LogInformation("Getting all active users");

        var users = await _context.Users
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => UserResponseDto.FromUser(u))
            .ToListAsync();

        return Ok(users);
    }

    /// <summary>
    /// Get a specific user by ID.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<UserResponseDto>> GetUser(int id)
    {
        var user = await _context.Users.FindAsync(id);

        if (user == null)
        {
            return NotFound();
        }

        return Ok(UserResponseDto.FromUser(user));
    }

    /// <summary>
    /// Get user by email.
    /// </summary>
    [HttpGet("by-email/{email}")]
    public async Task<ActionResult<UserResponseDto>> GetUserByEmail(string email)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            return NotFound();
        }

        return Ok(UserResponseDto.FromUser(user));
    }

    /// <summary>
    /// Create or update a user (upsert operation).
    /// If a user with the same email exists (including soft-deleted), reactivates and updates them.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<UserResponseDto>> UpsertUser(CreateUserDto dto)
    {
        // Check for existing user (including soft-deleted)
        var existingUser = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == dto.Email);

        if (existingUser != null)
        {
            // Upsert: reactivate and update existing user
            existingUser.DisplayName = dto.DisplayName;
            existingUser.IsActive = true;
            existingUser.DeletedAt = null;
            existingUser.UpdatedAt = DateTime.UtcNow;

            // Only allow role upgrade if currently a User
            if (existingUser.Role == UserRole.User && dto.Role == UserRole.Admin)
            {
                existingUser.Role = dto.Role;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Upserted user {UserId}: {Email} (reactivated)", existingUser.Id, existingUser.Email);

            return Ok(UserResponseDto.FromUser(existingUser));
        }

        // Create new user
        var user = new User
        {
            Email = dto.Email,
            DisplayName = dto.DisplayName,
            Role = dto.Role,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created user {UserId}: {Email}", user.Id, user.Email);

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, UserResponseDto.FromUser(user));
    }

    /// <summary>
    /// Update user details.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<UserResponseDto>> UpdateUser(int id, UpdateUserDto dto)
    {
        var user = await _context.Users.FindAsync(id);

        if (user == null)
        {
            return NotFound();
        }

        if (dto.DisplayName != null)
            user.DisplayName = dto.DisplayName;

        if (dto.IsActive.HasValue)
        {
            user.IsActive = dto.IsActive.Value;
            if (!dto.IsActive.Value)
            {
                user.DeletedAt = DateTime.UtcNow;
            }
            else
            {
                user.DeletedAt = null;
            }
        }

        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated user {UserId}", user.Id);

        return Ok(UserResponseDto.FromUser(user));
    }

    /// <summary>
    /// Soft delete a user (downsert operation).
    /// User is deactivated but data is retained.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DownsertUser(int id)
    {
        var user = await _context.Users.FindAsync(id);

        if (user == null)
        {
            return NotFound();
        }

        // Soft delete (downsert)
        user.IsActive = false;
        user.DeletedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Downserted (soft deleted) user {UserId}: {Email}", id, user.Email);

        return NoContent();
    }

    /// <summary>
    /// Record user login.
    /// </summary>
    [HttpPost("{id}/login")]
    public async Task<ActionResult<UserResponseDto>> RecordLogin(int id)
    {
        var user = await _context.Users.FindAsync(id);

        if (user == null)
        {
            return NotFound();
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Recorded login for user {UserId}", id);

        return Ok(UserResponseDto.FromUser(user));
    }
}
