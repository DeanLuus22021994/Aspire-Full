using Aspire_Full.Api.Data;
using Aspire_Full.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Aspire_Full.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ItemsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<ItemsController> _logger;
    private readonly TimeProvider _timeProvider;

    public ItemsController(AppDbContext context, ILogger<ItemsController> logger, TimeProvider timeProvider)
    {
        _context = context;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Item>>> GetItems()
    {
        _logger.LogInformation("Getting all items");
        return await _context.Items.OrderByDescending(i => i.CreatedAt).ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Item>> GetItem(int id)
    {
        var item = await _context.Items.FindAsync(id);

        if (item == null)
        {
            return NotFound();
        }

        return item;
    }

    [HttpPost]
    public async Task<ActionResult<Item>> CreateItem(CreateItemDto dto)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var item = new Item
        {
            Name = dto.Name,
            Description = dto.Description,
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.Items.Add(item);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created item {ItemId}: {ItemName}", item.Id, item.Name);

        return CreatedAtAction(nameof(GetItem), new { id = item.Id }, item);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Item>> UpdateItem(int id, UpdateItemDto dto)
    {
        var item = await _context.Items.FindAsync(id);

        if (item == null)
        {
            return NotFound();
        }

        if (dto.Name != null)
            item.Name = dto.Name;

        if (dto.Description != null)
            item.Description = dto.Description;

        item.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated item {ItemId}", item.Id);

        return item;
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteItem(int id)
    {
        var item = await _context.Items.FindAsync(id);

        if (item == null)
        {
            return NotFound();
        }

        _context.Items.Remove(item);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted item {ItemId}", id);

        return NoContent();
    }
}
