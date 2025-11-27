using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Aspire_Full.Api.Data;
using Aspire_Full.Api.Models;

namespace Aspire_Full.Api.Controllers;

/// <summary>
/// Admin controller for privileged operations.
/// Provides access to all users including soft-deleted, role management, and hard delete.
/// </summary>
[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<AdminController> _logger;

    public AdminController(AppDbContext context, ILogger<AdminController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all users including soft-deleted (bypasses query filter).
    /// </summary>
    [HttpGet("users")]
    public async Task<ActionResult<IEnumerable<AdminUserResponseDto>>> GetAllUsers(
        [FromQuery] bool includeDeleted = true,
        [FromQuery] UserRole? role = null)
    {
        _logger.LogInformation("Admin: Getting all users (includeDeleted: {IncludeDeleted})", includeDeleted);

        var query = _context.Users
            .IgnoreQueryFilters()
            .Include(u => u.Items)
            .AsQueryable();

        if (!includeDeleted)
        {
            query = query.Where(u => u.IsActive);
        }

        if (role.HasValue)
        {
            query = query.Where(u => u.Role == role.Value);
        }

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => AdminUserResponseDto.FromUser(u))
            .ToListAsync();

        return Ok(users);
    }

    /// <summary>
    /// Get a specific user by ID (bypasses query filter).
    /// </summary>
    [HttpGet("users/{id}")]
    public async Task<ActionResult<AdminUserResponseDto>> GetUser(int id)
    {
        var user = await _context.Users
            .IgnoreQueryFilters()
            .Include(u => u.Items)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return NotFound();
        }

        return Ok(AdminUserResponseDto.FromUser(user));
    }

    /// <summary>
    /// Promote a user to admin role.
    /// </summary>
    [HttpPost("users/{id}/promote")]
    public async Task<ActionResult<AdminUserResponseDto>> PromoteToAdmin(int id)
    {
        var user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return NotFound();
        }

        if (user.Role == UserRole.Admin)
        {
            return BadRequest(new { message = "User is already an admin" });
        }

        user.Role = UserRole.Admin;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Admin: Promoted user {UserId} to Admin", id);

        return Ok(AdminUserResponseDto.FromUser(user));
    }

    /// <summary>
    /// Demote an admin to regular user role.
    /// </summary>
    [HttpPost("users/{id}/demote")]
    public async Task<ActionResult<AdminUserResponseDto>> DemoteToUser(int id)
    {
        var user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return NotFound();
        }

        if (user.Role == UserRole.User)
        {
            return BadRequest(new { message = "User is already a regular user" });
        }

        user.Role = UserRole.User;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Admin: Demoted user {UserId} to User", id);

        return Ok(AdminUserResponseDto.FromUser(user));
    }

    /// <summary>
    /// Reactivate a soft-deleted user.
    /// </summary>
    [HttpPost("users/{id}/reactivate")]
    public async Task<ActionResult<AdminUserResponseDto>> ReactivateUser(int id)
    {
        var user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return NotFound();
        }

        if (user.IsActive)
        {
            return BadRequest(new { message = "User is already active" });
        }

        user.IsActive = true;
        user.DeletedAt = null;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Admin: Reactivated user {UserId}", id);

        return Ok(AdminUserResponseDto.FromUser(user));
    }

    /// <summary>
    /// Hard delete a user permanently (irreversible).
    /// Also removes all items created by this user.
    /// </summary>
    [HttpDelete("users/{id}/permanent")]
    public async Task<IActionResult> HardDeleteUser(int id)
    {
        var user = await _context.Users
            .IgnoreQueryFilters()
            .Include(u => u.Items)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return NotFound();
        }

        // Remove user (cascade will handle items based on relationship config)
        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        _logger.LogWarning("Admin: Permanently deleted user {UserId}: {Email}", id, user.Email);

        return NoContent();
    }

    /// <summary>
    /// Bulk soft-delete users by IDs.
    /// </summary>
    [HttpPost("users/bulk-deactivate")]
    public async Task<ActionResult<int>> BulkDeactivateUsers([FromBody] int[] userIds)
    {
        var users = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToListAsync();

        var count = 0;
        foreach (var user in users)
        {
            user.IsActive = false;
            user.DeletedAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;
            count++;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Admin: Bulk deactivated {Count} users", count);

        return Ok(new { deactivatedCount = count });
    }

    /// <summary>
    /// Get user statistics.
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<object>> GetStats()
    {
        var stats = await _context.Users
            .IgnoreQueryFilters()
            .GroupBy(u => 1)
            .Select(g => new
            {
                TotalUsers = g.Count(),
                ActiveUsers = g.Count(u => u.IsActive),
                InactiveUsers = g.Count(u => !u.IsActive),
                Admins = g.Count(u => u.Role == UserRole.Admin),
                RegularUsers = g.Count(u => u.Role == UserRole.User)
            })
            .FirstOrDefaultAsync();

        var itemStats = await _context.Items
            .GroupBy(i => 1)
            .Select(g => new
            {
                TotalItems = g.Count(),
                ItemsWithOwner = g.Count(i => i.CreatedByUserId != null),
                OrphanedItems = g.Count(i => i.CreatedByUserId == null)
            })
            .FirstOrDefaultAsync();

        return Ok(new
        {
            users = stats ?? new { TotalUsers = 0, ActiveUsers = 0, InactiveUsers = 0, Admins = 0, RegularUsers = 0 },
            items = itemStats ?? new { TotalItems = 0, ItemsWithOwner = 0, OrphanedItems = 0 }
        });
    }
}
