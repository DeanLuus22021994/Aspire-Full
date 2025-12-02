using ArcFaceSandbox.UsersKernel.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ArcFaceSandbox.UsersKernel.Tests;

internal static class TestDbContextFactory
{
    public static (SandboxUsersDbContext Context, SqliteConnection Connection) Create()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<SandboxUsersDbContext>()
            .UseSqlite(connection)
            .EnableSensitiveDataLogging()
            .Options;

        var context = new SandboxUsersDbContext(options);
        context.Database.EnsureCreated();

        return (context, connection);
    }
}
