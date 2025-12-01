using Aspire_Full.Agents;
using Aspire_Full.Shared.Models;

namespace Aspire_Full.Tests.Unit.Agents;

public class SubagentSelfReviewServiceTests
{
    [Fact]
    public void GetDefinitionReturnsCatalogEntry()
    {
        var service = new SubagentSelfReviewService(() => DateTimeOffset.UnixEpoch);

        var definition = service.GetDefinition(SubagentRole.VectorStore);

        definition.Name.Should().Be("Vector Store");
        definition.Directory.Should().Contain("vector-store");
    }

    [Fact]
    public void CreateRetrospectiveFallsBackToDefaults()
    {
        var update = SubagentUpdate.Normalize(SubagentRole.UsersKernel, null, null, null, null);
        var service = new SubagentSelfReviewService(() => DateTimeOffset.UnixEpoch);

        var retrospective = service.CreateRetrospective(update);

        retrospective.Highlights.Should().ContainSingle();
        retrospective.Risks.Should().Contain("No risks captured.");
        retrospective.Timestamp.Should().Be(DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public void CreateDelegationPlanMarksGpuRequestsHigh()
    {
        var update = SubagentUpdate.Normalize(SubagentRole.EmbeddingService, null, null, null, new[] { "GPU driver upgrade" });
        var service = new SubagentSelfReviewService(() => DateTimeOffset.UnixEpoch);

        var plan = service.CreateDelegationPlan(update);

        plan.Items.Should().ContainSingle();
        plan.Items[0].Priority.Should().Be(DelegationPriority.High);
    }
}
