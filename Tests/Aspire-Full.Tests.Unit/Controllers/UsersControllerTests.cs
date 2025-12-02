using Aspire_Full.Api.Controllers;
using Aspire_Full.Api.Data;
using Aspire_Full.Api.Models;
using Aspire_Full.Shared.Models;
using Aspire_Full.Tests.Unit.Fixtures;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using User = Aspire_Full.Api.Models.User;
using UserDto = Aspire_Full.Shared.Models.User;

namespace Aspire_Full.Tests.Unit.Controllers;

/// <summary>
/// Tests for the user management kernel (upsert/downsert soft delete flow).
/// Leverages the shared test root (Aspire-Full.Tests.Unit) with the in-memory AppDbContext.
/// </summary>
public sealed class UsersControllerTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly UsersController _controller;
    private readonly Mock<ILogger<UsersController>> _loggerMock = new();
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    public UsersControllerTests()
    {
        _context = TestDbContextFactory.CreateContext();
        _controller = new UsersController(_context, _loggerMock.Object, _timeProvider);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task UpsertUser_NewEmail_CreatesActiveUser()
    {
        // Arrange
        var dto = new CreateUser
        {
            Email = "new.user@example.com",
            DisplayName = "New User",
            Role = UserRole.Admin
        };

        // Act
        var result = await _controller.UpsertUser(dto);

        // Assert
        var created = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var userResponse = created.Value.Should().BeOfType<UserDto>().Subject;
        userResponse.Email.Should().Be(dto.Email);
        userResponse.DisplayName.Should().Be(dto.DisplayName);
        userResponse.Role.Should().Be(UserRole.Admin);

        var savedUser = await _context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == dto.Email);
        savedUser.Should().NotBeNull();
        savedUser!.IsActive.Should().BeTrue();
        savedUser.DeletedAt.Should().BeNull();
    }

    [Fact]
    public async Task UpsertUser_ReactivatesSoftDeletedUser()
    {
        // Arrange
        var existing = new User
        {
            Email = "reactivate@example.com",
            DisplayName = "Old Name",
            Role = UserRole.User,
            IsActive = false,
            DeletedAt = DateTime.UtcNow.AddDays(-3),
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow.AddDays(-4)
        };
        _context.Users.Add(existing);
        await _context.SaveChangesAsync();

        var dto = new CreateUser
        {
            Email = existing.Email,
            DisplayName = "New Name",
            Role = UserRole.Admin
        };

        // Act
        var result = await _controller.UpsertUser(dto);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<UserDto>().Subject;
        response.DisplayName.Should().Be("New Name");
        response.IsActive.Should().BeTrue();

        var userFromDb = await _context.Users.IgnoreQueryFilters().FirstAsync(u => u.Email == dto.Email);
        userFromDb.IsActive.Should().BeTrue();
        userFromDb.DisplayName.Should().Be("New Name");
        userFromDb.DeletedAt.Should().BeNull();
        userFromDb.Role.Should().Be(UserRole.Admin); // Role upgraded per upsert rules
    }

    [Fact]
    public async Task DownsertUser_SoftDeletesAndTracksTimestamps()
    {
        // Arrange
        var user = new User
        {
            Email = "downsert@example.com",
            DisplayName = "Downsert User",
            Role = UserRole.User,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.DownsertUser(user.Id);

        // Assert
        result.Should().BeOfType<NoContentResult>();

        var userFromDb = await _context.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == user.Id);
        userFromDb.IsActive.Should().BeFalse();
        userFromDb.DeletedAt.Should().NotBeNull();
        userFromDb.UpdatedAt.Should().BeOnOrAfter(user.UpdatedAt);
    }
}
