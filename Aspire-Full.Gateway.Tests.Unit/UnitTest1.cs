using Aspire_Full.Gateway.Models;

namespace Aspire_Full.Gateway.Tests.Unit;

public class UserTests
{
    [Fact]
    public void UserResponseDto_MapsCorrectly()
    {
        var user = new User
        {
            Id = 1,
            Email = "test@example.com",
            DisplayName = "Test User",
            Role = UserRole.Admin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var dto = UserResponseDto.FromUser(user);

        Assert.Equal(user.Id, dto.Id);
        Assert.Equal(user.Email, dto.Email);
        Assert.Equal(user.DisplayName, dto.DisplayName);
        Assert.Equal(user.Role, dto.Role);
        Assert.Equal(user.IsActive, dto.IsActive);
    }
}
