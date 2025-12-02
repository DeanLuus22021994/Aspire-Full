using Aspire_Full.Api.Controllers;
using Aspire_Full.Api.Models;
using Aspire_Full.Tests.Unit.Fixtures;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Aspire_Full.Tests.Unit.Controllers;

/// <summary>
/// Unit tests for ItemsController.
/// Tests all CRUD operations with in-memory database.
/// </summary>
public class ItemsControllerTests : IDisposable
{
    private readonly Api.Data.AppDbContext _context;
    private readonly ItemsController _controller;
    private readonly Mock<ILogger<ItemsController>> _loggerMock;
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    public ItemsControllerTests()
    {
        _context = TestDbContextFactory.CreateSeededContext();
        _loggerMock = new Mock<ILogger<ItemsController>>();
        _controller = new ItemsController(_context, _loggerMock.Object, _timeProvider);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    #region GetItems Tests

    [Fact]
    public async Task GetItems_ReturnsAllItems_OrderedByCreatedAtDescending()
    {
        // Act
        var result = await _controller.GetItems();

        // Assert
        var actionResult = result.Result;
        actionResult.Should().BeNull(); // Successful response returns value directly
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(3);

        // Verify ordering (most recent first)
        var items = result.Value!.ToList();
        items[0].Name.Should().Be("Test Item 3");
        items[1].Name.Should().Be("Test Item 2");
        items[2].Name.Should().Be("Test Item 1");
    }

    [Fact]
    public async Task GetItems_EmptyDatabase_ReturnsEmptyList()
    {
        // Arrange
        using var emptyContext = TestDbContextFactory.CreateContext();
        var controller = new ItemsController(emptyContext, _loggerMock.Object, _timeProvider);

        // Act
        var result = await controller.GetItems();

        // Assert
        result.Value.Should().NotBeNull();
        result.Value.Should().BeEmpty();
    }

    #endregion

    #region GetItem Tests

    [Fact]
    public async Task GetItem_ExistingId_ReturnsItem()
    {
        // Act
        var result = await _controller.GetItem(1);

        // Assert
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(1);
        result.Value.Name.Should().Be("Test Item 1");
        result.Value.Description.Should().Be("First test item");
    }

    [Fact]
    public async Task GetItem_NonExistingId_ReturnsNotFound()
    {
        // Act
        var result = await _controller.GetItem(999);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task GetItem_AllSeededIds_ReturnsCorrectItem(int id)
    {
        // Act
        var result = await _controller.GetItem(id);

        // Assert
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(id);
    }

    #endregion

    #region CreateItem Tests

    [Fact]
    public async Task CreateItem_ValidDto_ReturnsCreatedItem()
    {
        // Arrange
        var dto = new CreateItemDto
        {
            Name = "New Item",
            Description = "New item description"
        };

        // Act
        var result = await _controller.CreateItem(dto);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var item = createdResult.Value.Should().BeOfType<Item>().Subject;
        item.Name.Should().Be("New Item");
        item.Description.Should().Be("New item description");
        item.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateItem_WithoutDescription_ReturnsCreatedItem()
    {
        // Arrange
        var dto = new CreateItemDto
        {
            Name = "Item Without Description"
        };

        // Act
        var result = await _controller.CreateItem(dto);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var item = createdResult.Value.Should().BeOfType<Item>().Subject;
        item.Name.Should().Be("Item Without Description");
        item.Description.Should().BeNull();
    }

    [Fact]
    public async Task CreateItem_SetsCreatedAtAndUpdatedAt()
    {
        // Arrange
        var dto = new CreateItemDto { Name = "Timed Item" };
        var beforeCreate = DateTime.UtcNow;

        // Act
        var result = await _controller.CreateItem(dto);
        var afterCreate = DateTime.UtcNow;

        // Assert
        var createdResult = result.Result as CreatedAtActionResult;
        var item = createdResult!.Value as Item;
        item!.CreatedAt.Should().BeOnOrAfter(beforeCreate).And.BeOnOrBefore(afterCreate);
        item.UpdatedAt.Should().BeOnOrAfter(beforeCreate).And.BeOnOrBefore(afterCreate);
    }

    [Fact]
    public async Task CreateItem_PersistsToDatabase()
    {
        // Arrange
        var dto = new CreateItemDto
        {
            Name = "Persisted Item",
            Description = "Should be in DB"
        };

        // Act
        await _controller.CreateItem(dto);

        // Assert
        var savedItem = await _context.Items.FirstOrDefaultAsync(i => i.Name == "Persisted Item");
        savedItem.Should().NotBeNull();
        savedItem!.Description.Should().Be("Should be in DB");
    }

    #endregion

    #region UpdateItem Tests

    [Fact]
    public async Task UpdateItem_ExistingId_UpdatesName()
    {
        // Arrange
        var dto = new UpdateItemDto { Name = "Updated Name" };

        // Act
        var result = await _controller.UpdateItem(1, dto);

        // Assert
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("Updated Name");
        result.Value.Description.Should().Be("First test item"); // Unchanged
    }

    [Fact]
    public async Task UpdateItem_ExistingId_UpdatesDescription()
    {
        // Arrange
        var dto = new UpdateItemDto { Description = "Updated Description" };

        // Act
        var result = await _controller.UpdateItem(1, dto);

        // Assert
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("Test Item 1"); // Unchanged
        result.Value.Description.Should().Be("Updated Description");
    }

    [Fact]
    public async Task UpdateItem_ExistingId_UpdatesBothFields()
    {
        // Arrange
        var dto = new UpdateItemDto
        {
            Name = "Fully Updated",
            Description = "Both fields updated"
        };

        // Act
        var result = await _controller.UpdateItem(1, dto);

        // Assert
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("Fully Updated");
        result.Value.Description.Should().Be("Both fields updated");
    }

    [Fact]
    public async Task UpdateItem_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        var dto = new UpdateItemDto { Name = "Should Fail" };

        // Act
        var result = await _controller.UpdateItem(999, dto);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task UpdateItem_SetsUpdatedAt()
    {
        // Arrange
        var originalItem = await _context.Items.FindAsync(1);
        var originalUpdatedAt = originalItem!.UpdatedAt;
        var dto = new UpdateItemDto { Name = "Time Update Test" };

        // Wait to ensure time difference
        await Task.Delay(10);

        // Act
        var result = await _controller.UpdateItem(1, dto);

        // Assert
        result.Value!.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    #endregion

    #region DeleteItem Tests

    [Fact]
    public async Task DeleteItem_ExistingId_ReturnsNoContent()
    {
        // Act
        var result = await _controller.DeleteItem(1);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteItem_ExistingId_RemovesFromDatabase()
    {
        // Act
        await _controller.DeleteItem(1);

        // Assert
        var item = await _context.Items.FindAsync(1);
        item.Should().BeNull();
    }

    [Fact]
    public async Task DeleteItem_NonExistingId_ReturnsNotFound()
    {
        // Act
        var result = await _controller.DeleteItem(999);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteItem_DoesNotAffectOtherItems()
    {
        // Act
        await _controller.DeleteItem(1);

        // Assert
        var remainingItems = await _context.Items.ToListAsync();
        remainingItems.Should().HaveCount(2);
        remainingItems.Should().NotContain(i => i.Id == 1);
        remainingItems.Should().Contain(i => i.Id == 2);
        remainingItems.Should().Contain(i => i.Id == 3);
    }

    #endregion
}
