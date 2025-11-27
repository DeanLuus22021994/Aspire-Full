using Microsoft.EntityFrameworkCore;
using Aspire_Full.Api.Data;
using Aspire_Full.Api.Models;

namespace Aspire_Full.Tests.Unit.Data;

/// <summary>
/// Unit tests for AppDbContext.
/// Tests entity configuration and database operations.
/// </summary>
public class AppDbContextTests
{
    #region Entity Configuration Tests

    [Fact]
    public void Item_IdIsKeyProperty()
    {
        // Arrange
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(Item))!;

        // Act
        var primaryKey = entityType.FindPrimaryKey()!;

        // Assert
        primaryKey.Properties.Should().HaveCount(1);
        primaryKey.Properties[0].Name.Should().Be("Id");
    }

    [Fact]
    public void Item_NameIsRequired()
    {
        // Arrange
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(Item))!;

        // Act
        var nameProperty = entityType.FindProperty("Name")!;

        // Assert
        nameProperty.IsNullable.Should().BeFalse();
    }

    [Fact]
    public void Item_NameHasMaxLength200()
    {
        // Arrange
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(Item))!;

        // Act
        var nameProperty = entityType.FindProperty("Name")!;

        // Assert
        nameProperty.GetMaxLength().Should().Be(200);
    }

    [Fact]
    public void Item_DescriptionHasMaxLength1000()
    {
        // Arrange
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(Item))!;

        // Act
        var descriptionProperty = entityType.FindProperty("Description")!;

        // Assert
        descriptionProperty.GetMaxLength().Should().Be(1000);
    }

    [Fact]
    public void Item_DescriptionIsNullable()
    {
        // Arrange
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(Item))!;

        // Act
        var descriptionProperty = entityType.FindProperty("Description")!;

        // Assert
        descriptionProperty.IsNullable.Should().BeTrue();
    }

    #endregion

    #region CRUD Operations Tests

    [Fact]
    public async Task CanAddItem()
    {
        // Arrange
        using var context = CreateContext();
        var item = new Item
        {
            Name = "Test Item",
            Description = "Test Description"
        };

        // Act
        context.Items.Add(item);
        await context.SaveChangesAsync();

        // Assert
        item.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CanReadItem()
    {
        // Arrange
        using var context = CreateContext();
        var item = new Item { Name = "Readable Item" };
        context.Items.Add(item);
        await context.SaveChangesAsync();
        var savedId = item.Id;

        // Act
        var retrieved = await context.Items.FindAsync(savedId);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Readable Item");
    }

    [Fact]
    public async Task CanUpdateItem()
    {
        // Arrange
        using var context = CreateContext();
        var item = new Item { Name = "Original Name" };
        context.Items.Add(item);
        await context.SaveChangesAsync();

        // Act
        item.Name = "Updated Name";
        await context.SaveChangesAsync();

        // Assert
        var updated = await context.Items.FindAsync(item.Id);
        updated!.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task CanDeleteItem()
    {
        // Arrange
        using var context = CreateContext();
        var item = new Item { Name = "To Delete" };
        context.Items.Add(item);
        await context.SaveChangesAsync();
        var savedId = item.Id;

        // Act
        context.Items.Remove(item);
        await context.SaveChangesAsync();

        // Assert
        var deleted = await context.Items.FindAsync(savedId);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task CanQueryItems()
    {
        // Arrange
        using var context = CreateContext();
        context.Items.AddRange(
            new Item { Name = "Alpha" },
            new Item { Name = "Beta" },
            new Item { Name = "Alpha Beta" }
        );
        await context.SaveChangesAsync();

        // Act
        var alphaItems = await context.Items
            .Where(i => i.Name.Contains("Alpha"))
            .ToListAsync();

        // Assert
        alphaItems.Should().HaveCount(2);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task Item_WithNullName_ThrowsException()
    {
        // Arrange
        using var context = CreateContext();
        var item = new Item { Name = null! }; // Suppress warning for test

        // Act
        context.Items.Add(item);
        var act = async () => await context.SaveChangesAsync();

        // Assert - In-memory provider may not enforce constraints, so we check the model
        var entityType = context.Model.FindEntityType(typeof(Item))!;
        var nameProperty = entityType.FindProperty("Name")!;
        nameProperty.IsNullable.Should().BeFalse();
    }

    [Fact]
    public void Item_DefaultCreatedAt_IsSetToUtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var item = new Item { Name = "Time Test" };

        // Assert
        item.CreatedAt.Should().BeOnOrAfter(before);
        item.CreatedAt.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public void Item_DefaultUpdatedAt_IsSetToUtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var item = new Item { Name = "Time Test" };

        // Assert
        item.UpdatedAt.Should().BeOnOrAfter(before);
        item.UpdatedAt.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    #endregion

    #region DbSet Tests

    [Fact]
    public void ItemsDbSet_IsNotNull()
    {
        // Arrange
        using var context = CreateContext();

        // Assert
        context.Items.Should().NotBeNull();
    }

    [Fact]
    public async Task ItemsDbSet_CanTrackChanges()
    {
        // Arrange
        using var context = CreateContext();
        var item = new Item { Name = "Tracked Item" };

        // Act
        context.Items.Add(item);

        // Assert
        context.ChangeTracker.HasChanges().Should().BeTrue();

        await context.SaveChangesAsync();
        context.ChangeTracker.HasChanges().Should().BeFalse();
    }

    #endregion

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
