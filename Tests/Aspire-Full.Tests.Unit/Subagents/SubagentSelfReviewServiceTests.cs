using Aspire_Full.Agents;
using Aspire_Full.Shared.Abstractions;
using Aspire_Full.Shared.Models;
using Aspire_Full.Tests.Unit.Fixtures;

namespace Aspire_Full.Tests.Unit.Agents;

public class SubagentSelfReviewServiceTests
{
    private readonly FakeTimeProvider _timeProvider = new(DateTimeOffset.UnixEpoch);

    [Fact]
    public void GetDefinitionReturnsCatalogEntry()
    {
        var service = new SubagentSelfReviewService(_timeProvider);

        var definition = service.GetDefinition(SubagentRole.VectorStore);

        definition.Name.Should().Be("Vector Store");
        definition.Directory.Should().Contain("vector-store");
    }

    [Fact]
    public void CreateRetrospectiveFallsBackToDefaults()
    {
        var update = SubagentUpdate.Normalize(SubagentRole.UsersKernel, null, null, null, null);
        var service = new SubagentSelfReviewService(_timeProvider);

        var retrospective = service.CreateRetrospective(update);

        retrospective.Highlights.Should().ContainSingle();
        retrospective.Risks.Should().Contain("No risks captured.");
        retrospective.Timestamp.Should().Be(DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public void CreateDelegationPlanMarksGpuRequestsHigh()
    {
        var update = SubagentUpdate.Normalize(SubagentRole.EmbeddingService, null, null, null, new[] { "GPU driver upgrade" });
        var service = new SubagentSelfReviewService(_timeProvider);

        var plan = service.CreateDelegationPlan(update);

        plan.Items.Should().ContainSingle();
        plan.Items[0].Priority.Should().Be(DelegationPriority.High);
    }
}
