using Aspire_Full.Api.Models;

namespace Aspire_Full.Tests.Unit.Models;

/// <summary>
/// Unit tests for Item model and DTOs.
/// </summary>
public class ItemModelTests
{
    #region Item Tests

    [Fact]
    public void Item_NewInstance_HasDefaultTimestamps()
    {
        // Arrange & Act
        var before = DateTime.UtcNow;
        var item = new Item { Name = "Test" };
        var after = DateTime.UtcNow;

        // Assert
        item.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        item.UpdatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Item_Properties_CanBeSet()
    {
        // Arrange
        var createdAt = DateTime.UtcNow.AddDays(-1);
        var updatedAt = DateTime.UtcNow;

        // Act
        var item = new Item
        {
            Id = 42,
            Name = "Test Item",
            Description = "Test Description",
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };

        // Assert
        item.Id.Should().Be(42);
        item.Name.Should().Be("Test Item");
        item.Description.Should().Be("Test Description");
        item.CreatedAt.Should().Be(createdAt);
        item.UpdatedAt.Should().Be(updatedAt);
    }

    [Fact]
    public void Item_Description_CanBeNull()
    {
        // Arrange & Act
        var item = new Item
        {
            Name = "Item Without Description",
            Description = null
        };

        // Assert
        item.Description.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("A very long name that contains many characters")]
    public void Item_Name_AcceptsVariousLengths(string name)
    {
        // Arrange & Act
        var item = new Item { Name = name };

        // Assert
        item.Name.Should().Be(name);
    }

    #endregion

    #region CreateItemDto Tests

    [Fact]
    public void CreateItemDto_Properties_CanBeSet()
    {
        // Arrange & Act
        var dto = new CreateItemDto
        {
            Name = "New Item",
            Description = "New Description"
        };

        // Assert
        dto.Name.Should().Be("New Item");
        dto.Description.Should().Be("New Description");
    }

    [Fact]
    public void CreateItemDto_Description_CanBeNull()
    {
        // Arrange & Act
        var dto = new CreateItemDto
        {
            Name = "Item"
        };

        // Assert
        dto.Description.Should().BeNull();
    }

    #endregion

    #region UpdateItemDto Tests

    [Fact]
    public void UpdateItemDto_AllProperties_CanBeNull()
    {
        // Arrange & Act
        var dto = new UpdateItemDto();

        // Assert
        dto.Name.Should().BeNull();
        dto.Description.Should().BeNull();
    }

    [Fact]
    public void UpdateItemDto_Properties_CanBeSet()
    {
        // Arrange & Act
        var dto = new UpdateItemDto
        {
            Name = "Updated Name",
            Description = "Updated Description"
        };

        // Assert
        dto.Name.Should().Be("Updated Name");
        dto.Description.Should().Be("Updated Description");
    }

    [Fact]
    public void UpdateItemDto_CanUpdateOnlyName()
    {
        // Arrange & Act
        var dto = new UpdateItemDto { Name = "Only Name" };

        // Assert
        dto.Name.Should().Be("Only Name");
        dto.Description.Should().BeNull();
    }

    [Fact]
    public void UpdateItemDto_CanUpdateOnlyDescription()
    {
        // Arrange & Act
        var dto = new UpdateItemDto { Description = "Only Description" };

        // Assert
        dto.Name.Should().BeNull();
        dto.Description.Should().Be("Only Description");
    }

    #endregion
}
