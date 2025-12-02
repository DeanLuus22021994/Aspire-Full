using ArcFaceSandbox.UsersKernel.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace ArcFaceSandbox.UsersKernel.Infrastructure.Data;

/// <summary>
/// EF Core DbContext used by the sandbox Users kernel.
/// </summary>
public sealed class SandboxUsersDbContext : DbContext
{
    public SandboxUsersDbContext(DbContextOptions<SandboxUsersDbContext> options)
        : base(options)
    {
    }

    public DbSet<SandboxUser> Users => Set<SandboxUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var entity = modelBuilder.Entity<SandboxUser>();
        entity.HasKey(u => u.Id);
        entity.Property(u => u.Id).ValueGeneratedNever();

        entity.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(255);
        entity.HasIndex(u => u.Email).IsUnique();

        entity.Property(u => u.DisplayName)
            .IsRequired()
            .HasMaxLength(100);

        entity.Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        entity.Property(u => u.CreatedAt).IsRequired();
        entity.Property(u => u.UpdatedAt).IsRequired();

        entity.HasQueryFilter(u => u.IsActive);
    }
}
