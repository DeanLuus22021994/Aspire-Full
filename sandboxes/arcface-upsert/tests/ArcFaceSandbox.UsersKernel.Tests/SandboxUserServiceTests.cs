using ArcFaceSandbox.UsersKernel.Infrastructure.Data;
using ArcFaceSandbox.UsersKernel.Infrastructure.Entities;
using ArcFaceSandbox.UsersKernel.Infrastructure.Models;
using ArcFaceSandbox.UsersKernel.Infrastructure.Services;
using ArcFaceSandbox.UsersKernel.Tests.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArcFaceSandbox.UsersKernel.Tests;

public sealed class SandboxUserServiceTests : IAsyncLifetime
{
    private const string Base64Face = "AQIDBA=="; // arbitrary bytes

    private readonly FakeArcFaceEmbeddingService _embedding = new();
    private readonly FakeVectorStore _vectorStore = new();
    private readonly SqliteConnection _connection;
    private readonly SandboxUsersDbContext _context;
    private readonly SandboxUserService _service;

    public SandboxUserServiceTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _service = new SandboxUserService(_context, _embedding, _vectorStore, NullLogger<SandboxUserService>.Instance);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _context.Dispose();
        _connection.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task UpsertAsync_CreatesUserAndVector()
    {
        var command = new UserUpsertCommand("tester@example.com", "Tester", SandboxUserRole.Admin, Base64Face);

        var user = await _service.UpsertAsync(command);

        Assert.True(user.IsActive);
        Assert.Equal(command.Email, user.Email);
        Assert.NotNull(await _context.Users.FindAsync(user.Id));
        Assert.True(_vectorStore.Documents.ContainsKey(user.Id.ToString("N")));
    }

    [Fact]
    public async Task UpsertAsync_ReactivatesSoftDeletedUser()
    {
        var existing = new SandboxUser
        {
            Id = Guid.NewGuid(),
            Email = "soft@example.com",
            DisplayName = "Soft",
            Role = SandboxUserRole.User,
            IsActive = false,
            DeletedAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            UpdatedAt = DateTime.UtcNow.AddDays(-2)
        };
        _context.Users.Add(existing);
        await _context.SaveChangesAsync();

        var command = new UserUpsertCommand(existing.Email, "Reactivated", SandboxUserRole.Admin, Base64Face);
        var user = await _service.UpsertAsync(command);

        Assert.True(user.IsActive);
        Assert.Null(user.DeletedAt);
        Assert.Equal(SandboxUserRole.Admin, user.Role);
    }

    [Fact]
    public async Task UpdateAsync_WithoutEmbedding_RefreshesMetadataOnly()
    {
        var upserted = await _service.UpsertAsync(new UserUpsertCommand("meta@example.com", "Meta", SandboxUserRole.User, Base64Face));
        var docId = upserted.Id.ToString("N");
        var previousEmbedding = _vectorStore.Documents[docId].Embedding.ToArray();

        var updated = await _service.UpdateAsync(upserted.Id, new UserUpdateCommand("Meta Updated", null, true, null));

        Assert.NotNull(updated);
        Assert.Equal("Meta Updated", updated!.DisplayName);
        Assert.Equal("Meta Updated", _vectorStore.Documents[docId].Content);
        Assert.True(previousEmbedding.SequenceEqual(_vectorStore.Documents[docId].Embedding.ToArray()));
    }

    [Fact]
    public async Task DownsertAsync_SoftDeletesAndMarksVector()
    {
        var upserted = await _service.UpsertAsync(new UserUpsertCommand("down@example.com", "Down", SandboxUserRole.User, Base64Face));
        var docId = upserted.Id.ToString("N");

        var result = await _service.DownsertAsync(upserted.Id);

        Assert.True(result);
        var user = await _context.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == upserted.Id);
        Assert.False(user.IsActive);
        Assert.NotNull(user.DeletedAt);
        Assert.True(_vectorStore.Documents[docId].IsDeleted);
    }
}
