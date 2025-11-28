using System;
using Aspire.Hosting;
using Aspire_Full.DevContainer;

namespace Aspire_Full.Tests.Unit.DevContainer;

public class DevContainerBuilderExtensionsTests
{
    [Fact]
    public void BuildRuntimeArgumentsIncludesNetworkAndInit()
    {
        var args = DevContainerDefaults.BuildRuntimeArguments("aspire-network");

        args.Should().ContainInOrder("--network", "aspire-network");
        args.Should().Contain("--init");
    }

    [Fact]
    public void EnvironmentVariablesExposePythonVersion()
    {
        DevContainerDefaults.EnvironmentVariables.Should().ContainKey("PYTHON_VERSION");
        DevContainerDefaults.EnvironmentVariables["PYTHON_VERSION"].Should().Be(DevContainerDefaults.PythonVersion);
    }

    [Fact]
    public void AddDevContainerRequiresNetworkName()
    {
        var builder = DistributedApplication.CreateBuilder(Array.Empty<string>());

        var act = () => builder.AddDevContainer(string.Empty);

        act.Should().Throw<ArgumentException>();
    }
}
