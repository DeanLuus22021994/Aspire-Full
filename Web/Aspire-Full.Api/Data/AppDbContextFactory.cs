using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Aspire_Full.Api.Data;

/// <summary>
/// Design-time factory for EF Core migrations when the app isn't running.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        // Use a development connection string for design-time operations
        optionsBuilder.UseNpgsql("Host=localhost;Database=aspiredb;Username=postgres;Password=postgres");

        return new AppDbContext(optionsBuilder.Options);
    }
}
