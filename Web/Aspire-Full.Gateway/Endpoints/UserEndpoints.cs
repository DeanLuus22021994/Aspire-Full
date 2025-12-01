using Aspire_Full.Embeddings;
using Aspire_Full.Gateway.Data;
using Aspire_Full.Gateway.Models;
using Aspire_Full.Gateway.Services;
using Aspire_Full.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using User = Aspire_Full.Gateway.Models.User;
using UserDto = Aspire_Full.Shared.Models.User;

namespace Aspire_Full.Gateway.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").WithTags("Users");

        group.MapGet("/", GetUsers);
        group.MapGet("/{id}", GetUser);
        group.MapPost("/", UpsertUser);
        group.MapDelete("/{id}", DownsertUser);
        group.MapPost("/{id}/login", RecordLogin);
        group.MapPost("/search", SearchUsers);
    }

    static async Task<IResult> GetUsers(GatewayDbContext db)
    {
        var users = await db.Users
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new UserDto
            {
                Id = u.Id,
                Email = u.Email,
                DisplayName = u.DisplayName,
                Role = u.Role,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt,
                LastLoginAt = u.LastLoginAt
            })
            .ToListAsync();
        return TypedResults.Ok(users);
    }

    static async Task<IResult> GetUser(int id, GatewayDbContext db)
    {
        var user = await db.Users.FindAsync(id);
        return user is null ? TypedResults.NotFound() : TypedResults.Ok(ToDto(user));
    }

    static async Task<IResult> UpsertUser(
        [FromBody] CreateUser dto,
        GatewayDbContext db,
        IEmbeddingService embeddingService,
        IUserVectorService vectorStore,
        ILogger<Program> logger)
    {
        var existingUser = await db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == dto.Email);

        User user;
        if (existingUser != null)
        {
            existingUser.DisplayName = dto.DisplayName;
            existingUser.IsActive = true;
            existingUser.DeletedAt = null;
            existingUser.UpdatedAt = DateTime.UtcNow;
            if (existingUser.Role == UserRole.User && dto.Role == UserRole.Admin)
            {
                existingUser.Role = dto.Role;
            }
            user = existingUser;
        }
        else
        {
            user = new User
            {
                Email = dto.Email,
                DisplayName = dto.DisplayName,
                Role = dto.Role,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);
        }

        await db.SaveChangesAsync();

        // Generate embedding and upsert to vector store (GPU accelerated)
        try
        {
            var embedding = await embeddingService.GenerateEmbeddingAsync(user.DisplayName);
            await vectorStore.UpsertUserVectorAsync(user.Id, user.DisplayName, embedding);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update vector store for user {UserId}", user.Id);
            // We don't fail the request if vector store fails, but we log it.
            // In a real production system, we might want a background job or outbox pattern.
        }

        return TypedResults.Ok(ToDto(user));
    }

    static async Task<IResult> DownsertUser(int id, GatewayDbContext db, IUserVectorService vectorStore)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null)
            return TypedResults.NotFound();

        user.IsActive = false;
        user.DeletedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Remove from vector store
        await vectorStore.DeleteUserVectorAsync(id);

        return TypedResults.NoContent();
    }

    static async Task<IResult> RecordLogin(int id, GatewayDbContext db)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null)
            return TypedResults.NotFound();

        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return TypedResults.Ok(ToDto(user));
    }

    static async Task<IResult> SearchUsers(
        [FromBody] string query,
        IEmbeddingService embeddingService,
        IUserVectorService vectorStore)
    {
        var embedding = await embeddingService.GenerateEmbeddingAsync(query);
        var results = await vectorStore.SearchAsync(embedding);
        return TypedResults.Ok(results);
    }

    static UserDto ToDto(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        DisplayName = user.DisplayName,
        Role = user.Role,
        IsActive = user.IsActive,
        CreatedAt = user.CreatedAt,
        UpdatedAt = user.UpdatedAt,
        LastLoginAt = user.LastLoginAt
    };
}
