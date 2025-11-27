namespace Aspire_Full.Api.Models;

public class Item
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Foreign key to the user who created this item.
    /// </summary>
    public int? CreatedByUserId { get; set; }

    /// <summary>
    /// Navigation property to the user who created this item.
    /// </summary>
    public User? CreatedByUser { get; set; }
}

public class CreateItemDto
{
    public required string Name { get; set; }
    public string? Description { get; set; }
}

public class UpdateItemDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
}
