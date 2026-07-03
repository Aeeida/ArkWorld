using FluentAssertions;
using GameServer.Grains.Interfaces;
using GameServer.Tests.Integration.Infrastructure;

namespace GameServer.Tests.Integration.Grains;

public class SovereigntyGrainTests : GrainTestBase
{
    [Fact]
    public async Task Claim_Unclaimed_ShouldSucceed()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISovereigntyGrain>("system-alpha");
        var allianceId = Guid.NewGuid();

        var result = await grain.ClaimAsync(allianceId);

        result.Should().BeTrue();
        var state = await grain.GetStateAsync();
        state.OwnerAllianceId.Should().Be(allianceId);
        state.Status.Should().Be("Claimed");
        state.ClaimedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Claim_AlreadyClaimed_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISovereigntyGrain>("system-beta");
        await grain.ClaimAsync(Guid.NewGuid());

        var result = await grain.ClaimAsync(Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Claim_WhileContested_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISovereigntyGrain>("system-gamma");
        await grain.ContestAsync(Guid.NewGuid());

        var result = await grain.ClaimAsync(Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Contest_ShouldCreateContest()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISovereigntyGrain>("system-delta");
        var attackerId = Guid.NewGuid();

        var contestId = await grain.ContestAsync(attackerId);

        contestId.Should().NotBe(Guid.Empty);
        var state = await grain.GetStateAsync();
        state.Status.Should().Be("Contested");
        state.ActiveContests.Should().HaveCount(1);
        state.ActiveContests[0].AttackingAllianceId.Should().Be(attackerId);
        state.ActiveContests[0].Status.Should().Be("InProgress");
    }

    [Fact]
    public async Task Contest_MultipleContests_ShouldTrackAll()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISovereigntyGrain>("system-epsilon");

        await grain.ContestAsync(Guid.NewGuid());
        await grain.ContestAsync(Guid.NewGuid());

        var state = await grain.GetStateAsync();
        state.ActiveContests.Should().HaveCount(2);
    }

    [Fact]
    public async Task ResolveContest_AttackerWins_ShouldTransferSovereignty()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISovereigntyGrain>("system-zeta");
        var defenderId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        await grain.ClaimAsync(defenderId);

        var contestId = await grain.ContestAsync(attackerId);
        var result = await grain.ResolveContestAsync(contestId, attackerId);

        result.Should().BeTrue();
        var state = await grain.GetStateAsync();
        state.OwnerAllianceId.Should().Be(attackerId);
        state.ActiveContests.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveContest_DefenderWins_ShouldKeepSovereignty()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISovereigntyGrain>("system-eta");
        var defenderId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        await grain.ClaimAsync(defenderId);

        var contestId = await grain.ContestAsync(attackerId);
        var result = await grain.ResolveContestAsync(contestId, defenderId);

        result.Should().BeTrue();
        var state = await grain.GetStateAsync();
        state.OwnerAllianceId.Should().Be(defenderId);
        state.ActiveContests.Should().BeEmpty();
        state.Status.Should().Be("Claimed");
    }

    [Fact]
    public async Task ResolveContest_NonExistent_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISovereigntyGrain>("system-theta");

        var result = await grain.ResolveContestAsync(Guid.NewGuid(), Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task PlaceStructure_ShouldReturnStructureId()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISovereigntyGrain>("system-iota");
        var allianceId = Guid.NewGuid();

        var structureId = await grain.PlaceStructureAsync(allianceId, "Citadel");

        structureId.Should().NotBe(Guid.Empty);
        var state = await grain.GetStateAsync();
        state.Structures.Should().HaveCount(1);
        state.Structures[0].StructureType.Should().Be("Citadel");
        state.Structures[0].OwnerAllianceId.Should().Be(allianceId);
        state.Structures[0].HealthPercent.Should().Be(100.0);
        state.Structures[0].Status.Should().Be("Online");
    }

    [Fact]
    public async Task PlaceStructure_MultipleStructures_ShouldTrackAll()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISovereigntyGrain>("system-kappa");
        var allianceId = Guid.NewGuid();

        await grain.PlaceStructureAsync(allianceId, "Citadel");
        await grain.PlaceStructureAsync(allianceId, "Refinery");
        await grain.PlaceStructureAsync(allianceId, "JumpGate");

        var state = await grain.GetStateAsync();
        state.Structures.Should().HaveCount(3);
    }

    [Fact]
    public async Task DestroyStructure_Existing_ShouldSucceed()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISovereigntyGrain>("system-lambda");
        var structureId = await grain.PlaceStructureAsync(Guid.NewGuid(), "Citadel");

        var result = await grain.DestroyStructureAsync(structureId);

        result.Should().BeTrue();
        var state = await grain.GetStateAsync();
        state.Structures.Should().BeEmpty();
    }

    [Fact]
    public async Task DestroyStructure_NonExistent_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISovereigntyGrain>("system-mu");

        var result = await grain.DestroyStructureAsync(Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SetTaxRate_ByOwner_ShouldSucceed()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISovereigntyGrain>("system-nu");
        var allianceId = Guid.NewGuid();
        await grain.ClaimAsync(allianceId);

        var result = await grain.SetTaxRateAsync(allianceId, 0.1m);

        result.Should().BeTrue();
        var state = await grain.GetStateAsync();
        state.TaxRate.Should().Be(0.1m);
    }

    [Fact]
    public async Task SetTaxRate_ByNonOwner_ShouldReturnFalse()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISovereigntyGrain>("system-xi");
        await grain.ClaimAsync(Guid.NewGuid());

        var result = await grain.SetTaxRateAsync(Guid.NewGuid(), 0.1m);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SetTaxRate_AboveMax_ShouldClampTo50Percent()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISovereigntyGrain>("system-omicron");
        var allianceId = Guid.NewGuid();
        await grain.ClaimAsync(allianceId);

        await grain.SetTaxRateAsync(allianceId, 0.9m);

        var state = await grain.GetStateAsync();
        state.TaxRate.Should().Be(0.5m);
    }

    [Fact]
    public async Task SetTaxRate_BelowMin_ShouldClampToZero()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISovereigntyGrain>("system-pi");
        var allianceId = Guid.NewGuid();
        await grain.ClaimAsync(allianceId);

        await grain.SetTaxRateAsync(allianceId, -0.5m);

        var state = await grain.GetStateAsync();
        state.TaxRate.Should().Be(0m);
    }

    [Fact]
    public async Task SolarSystemId_ShouldMatchGrainKey()
    {
        var grain = Cluster.GrainFactory.GetGrain<ISovereigntyGrain>("my-system-key");

        var state = await grain.GetStateAsync();

        state.SolarSystemId.Should().Be("my-system-key");
    }
}
