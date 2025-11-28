using Aspire_Full.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Aspire_Full.Tests.Unit.Fixtures;

/// <summary>
/// Factory for creating test database contexts with in-memory provider.
/// </summary>
public static class TestDbContextFactory
{
    /// <summary>
    /// Creates a new in-memory database context for testing.
    /// </summary>
    /// <param name="databaseName">Optional unique database name for isolation.</param>
    /// <returns>A configured AppDbContext with in-memory provider.</returns>
    public static AppDbContext CreateContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    /// <summary>
    /// Creates a context and seeds it with test data.
    /// </summary>
    public static AppDbContext CreateSeededContext(string? databaseName = null)
    {
        var context = CreateContext(databaseName);
        SeedTestData(context);
        return context;
    }

    /// <summary>
    /// Seeds the context with standard test data.
    /// </summary>
    public static void SeedTestData(AppDbContext context)
    {
        context.Items.AddRange(
            new Aspire_Full.Api.Models.Item
            {
                Id = 1,
                Name = "Test Item 1",
                Description = "First test item",
                CreatedAt = DateTime.UtcNow.AddDays(-7),
                UpdatedAt = DateTime.UtcNow.AddDays(-7)
            },
            new Aspire_Full.Api.Models.Item
            {
                Id = 2,
                Name = "Test Item 2",
                Description = "Second test item",
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new Aspire_Full.Api.Models.Item
            {
                Id = 3,
                Name = "Test Item 3",
                Description = null,
                CreatedAt = DateTime.UtcNow.AddHours(-2),
                UpdatedAt = DateTime.UtcNow.AddHours(-2)
            }
        );
        context.SaveChanges();
    }
}
