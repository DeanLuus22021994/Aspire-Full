using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ArcFaceSandbox.EmbeddingService;
using ArcFaceSandbox.UsersKernel.Infrastructure.Data;
using ArcFaceSandbox.VectorStore;
using ArcFaceSandbox.UsersKernel.Tests.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArcFaceSandbox.UsersKernel.Tests;

internal sealed class UsersKernelApiFactory : WebApplicationFactory<Program>, IAsyncDisposable
{
    private SqliteConnection? _connection;
    private readonly string _modelPath = Path.Combine(AppContext.BaseDirectory, "TestAssets", "fake-model.onnx");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["ArcFace:Embedding:ModelPath"] = _modelPath,
                ["ArcFace:VectorStore:Endpoint"] = "http://localhost",
                ["ArcFace:VectorStore:CollectionName"] = "integration-tests",
                ["ArcFace:VectorStore:VectorSize"] = SandboxVectorStoreOptions.DefaultVectorSize.ToString(CultureInfo.InvariantCulture),
                ["ArcFace:VectorStore:AutoCreateCollection"] = "false"
            };

            config.AddInMemoryCollection(overrides);
        });
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<SandboxUsersDbContext>));

            services.AddSingleton<SqliteConnection>(_ =>
            {
                if (_connection is null)
                {
                    _connection = new SqliteConnection("DataSource=:memory:");
                    _connection.Open();
                }

                return _connection;
            });

            services.AddDbContext<SandboxUsersDbContext>((sp, options) =>
            {
                var connection = sp.GetRequiredService<SqliteConnection>();
                options.UseSqlite(connection);
            });

            services.AddSingleton<FakeVectorStore>();
            services.AddSingleton<ISandboxVectorStore>(sp => sp.GetRequiredService<FakeVectorStore>());
            services.AddSingleton<IArcFaceEmbeddingService, FakeArcFaceEmbeddingService>();
        });
    }

    public async Task ResetDatabaseAsync()
    {
        if (Services is null)
        {
            throw new InvalidOperationException("CreateClient must be called before resetting the database.");
        }

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SandboxUsersDbContext>();
        await db.Database.EnsureDeletedAsync().ConfigureAwait(false);
        await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
        var vectors = scope.ServiceProvider.GetRequiredService<FakeVectorStore>();
        vectors.Reset();
    }

    public FakeVectorStore GetVectorStore()
    {
        if (Services is null)
        {
            throw new InvalidOperationException("CreateClient must be called before resolving services.");
        }

        using var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<FakeVectorStore>();
    }

    public override async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
        }

        await base.DisposeAsync().ConfigureAwait(false);
    }
}
