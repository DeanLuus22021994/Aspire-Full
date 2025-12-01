using Aspire_Full.Shared.Models;

namespace Aspire_Full.Gateway.Tests.Business;

public class UserBusinessTests
{
    [Fact]
    public void CreateUser_SetsDefaultRole()
    {
        var dto = new CreateUser
        {
            Email = "test@example.com",
            DisplayName = "Test"
        };

        Assert.Equal(UserRole.User, dto.Role);
    }
}
