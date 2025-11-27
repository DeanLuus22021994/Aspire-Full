using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArcFaceSandbox.EmbeddingService;
using ArcFaceSandbox.UsersKernel.Api.Contracts;
using ArcFaceSandbox.UsersKernel.Infrastructure.Entities;

namespace ArcFaceSandbox.UsersKernel.Tests;

public sealed class DiagnosticsControllerIntegrationTests
{
    private const string Base64Face = "AQIDBA==";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task EmbeddingDiagnostics_ReturnsModelMetadata()
    {
        await using var factory = new UsersKernelApiFactory();
        using var client = factory.CreateClient();
        await factory.ResetDatabaseAsync();

        var response = await client.GetAsync("/api/diagnostics/embedding");
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<EmbeddingDiagnosticsResponse>(SerializerOptions);

        Assert.NotNull(payload);
        Assert.Equal("fake", payload!.ModelInfo.ModelName);
        Assert.True(payload.ModelFileExists);
        Assert.Equal(0, payload.ActiveUsers);
    }

    [Fact]
    public async Task VectorStoreDiagnostics_SurfacesUserDocuments()
    {
        await using var factory = new UsersKernelApiFactory();
        using var client = factory.CreateClient();
        await factory.ResetDatabaseAsync();

        var created = await CreateUserAsync(client, "vector-monitor@example.com");

        var response = await client.GetAsync("/api/diagnostics/vector-store");
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<VectorStoreStatusResponse>(SerializerOptions);

        Assert.NotNull(payload);
        Assert.True(payload!.IsReachable);
        Assert.Contains(payload.Documents, doc => doc.UserId == created.Id);
    }

    private static async Task<SandboxUserResponse> CreateUserAsync(HttpClient client, string email)
    {
        var request = new UpsertSandboxUserRequest
        {
            Email = email,
            DisplayName = "Vector Monitor",
            Role = SandboxUserRole.Admin,
            FaceImageBase64 = Base64Face
        };

        var response = await client.PostAsJsonAsync("/api/users", request, SerializerOptions);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<SandboxUserResponse>(SerializerOptions);
        return payload!;
    }

    private sealed record EmbeddingDiagnosticsResponse(
        ArcFaceModelInfo ModelInfo,
        string ModelPath,
        bool ModelFileExists,
        int ActiveUsers,
        int TotalUsers,
        DateTime? LastUserChangeUtc);

    private sealed record VectorStoreStatusResponse(
        string Endpoint,
        string CollectionName,
        int VectorSize,
        bool AutoCreateCollection,
        bool IsReachable,
        IReadOnlyList<VectorDocumentStatus> Documents,
        IReadOnlyList<VectorStoreIssue> Issues);

    private sealed record VectorDocumentStatus(
        Guid UserId,
        string UserEmail,
        string DisplayName,
        string VectorDocumentId,
        bool VectorExists,
        bool IsDeleted,
        DateTime? VectorUpdatedAt,
        DateTime? VectorDeletedAt);

    private sealed record VectorStoreIssue(string Code, string Message);
}
