using Aspire_Full.WebAssembly.Services;
using Microsoft.Extensions.Options;

namespace Aspire_Full.Tests.Unit.WebAssembly;

public class FrontendEnvironmentRegistryTests
{
    [Fact]
    public void GetByKey_ReturnsMatchingDefinition()
    {
        var options = Options.Create(new FrontendOptions
        {
            DefaultEnvironmentKey = FrontendEnvironmentKeys.DevelopmentDocs,
            Environments =
            [
                new FrontendEnvironmentDefinition
                {
                    Key = FrontendEnvironmentKeys.Uat,
                    DisplayName = "UAT",
                    Description = "UAT",
                    ApiBaseAddress = "https://uat" ,
                    DocumentationUrl = "https://uat/docs",
                    BadgeText = "UAT"
                }
            ]
        });

        var registry = new FrontendEnvironmentRegistry(options);
        var result = registry.GetByKey(FrontendEnvironmentKeys.Uat);

        result.Should().NotBeNull();
        result.Key.Should().Be(FrontendEnvironmentKeys.Uat);
        result.ApiBaseAddress.Should().Be("https://uat");
    }

    [Fact]
    public void GetAll_ReturnsFallbackDefinitions_WhenConfigMissing()
    {
        var options = Options.Create(new FrontendOptions());
        var registry = new FrontendEnvironmentRegistry(options);

        registry.GetAll().Should().HaveCount(3);
    }
}
