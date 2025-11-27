using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArcFaceSandbox.UsersKernel.Api.Contracts;
using ArcFaceSandbox.UsersKernel.Infrastructure.Entities;

namespace ArcFaceSandbox.UsersKernel.Tests;

public sealed class UsersControllerIntegrationTests
{
    private const string Base64Face = "AQIDBA==";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task UpsertUser_CreatesUserAndVectorDocument()
    {
        await using var factory = new UsersKernelApiFactory();
        using var client = factory.CreateClient();
        await factory.ResetDatabaseAsync();

        var request = CreateUpsertRequest("integration@example.com", "Integration", SandboxUserRole.Admin);
        var response = await client.PostAsJsonAsync("/api/users", request, SerializerOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<SandboxUserResponse>(SerializerOptions);
        Assert.NotNull(payload);
        Assert.Equal(request.Email, payload!.Email);
        Assert.Equal(request.DisplayName, payload.DisplayName);

        var vectors = factory.GetVectorStore();
        Assert.True(vectors.Documents.ContainsKey(payload.Id.ToString("N")));
    }

    [Fact]
    public async Task UpdateUser_ChangesMetadataAndVectorContent()
    {
        await using var factory = new UsersKernelApiFactory();
        using var client = factory.CreateClient();
        await factory.ResetDatabaseAsync();

        var created = await CreateUserAsync(client, "update@example.com");
        var update = new UpdateSandboxUserRequest
        {
            DisplayName = "Updated",
            Role = SandboxUserRole.Admin,
            IsActive = true
        };

        var response = await client.PutAsJsonAsync($"/api/users/{created.Id}", update, SerializerOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<SandboxUserResponse>(SerializerOptions);
        Assert.NotNull(payload);
        Assert.Equal(update.DisplayName, payload!.DisplayName);
        Assert.Equal(update.Role, payload.Role);

        var vectors = factory.GetVectorStore();
        var doc = vectors.Documents[payload.Id.ToString("N")];
        Assert.Equal(update.DisplayName, doc.Content);
    }

    [Fact]
    public async Task DownsertUser_SoftDeletesUserAndVector()
    {
        await using var factory = new UsersKernelApiFactory();
        using var client = factory.CreateClient();
        await factory.ResetDatabaseAsync();

        var created = await CreateUserAsync(client, "delete@example.com");

        var deleteResponse = await client.DeleteAsync($"/api/users/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/users/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);

        var vectors = factory.GetVectorStore();
        var doc = vectors.Documents[created.Id.ToString("N")];
        Assert.True(doc.IsDeleted);
    }

    private static UpsertSandboxUserRequest CreateUpsertRequest(string email, string name, SandboxUserRole role) => new()
    {
        Email = email,
        DisplayName = name,
        Role = role,
        FaceImageBase64 = Base64Face
    };

    private static async Task<SandboxUserResponse> CreateUserAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/users", CreateUpsertRequest(email, "Sandbox", SandboxUserRole.User), SerializerOptions);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<SandboxUserResponse>(SerializerOptions);
        return payload!;
    }
}
