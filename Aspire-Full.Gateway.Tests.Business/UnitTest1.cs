using Aspire_Full.Gateway.Models;

namespace Aspire_Full.Gateway.Tests.Business;

public class UserBusinessTests
{
    [Fact]
    public void CreateUserDto_SetsDefaultRole()
    {
        var dto = new CreateUserDto
        {
            Email = "test@example.com",
            DisplayName = "Test"
        };

        Assert.Equal(UserRole.User, dto.Role);
    }
}
