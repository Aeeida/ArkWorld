using Xunit;
using Ark.World.Core;
using Ark.World.Data;

namespace Ark.Tests;

public class ModificationLogTests
{
    [Fact]
    public void NewLog_IsEmpty()
    {
        var log = new ModificationLog();
        Assert.Equal(0, log.Count);
        Assert.Empty(log.Entries);
    }

    [Fact]
    public void Append_IncreasesCount()
    {
        var log = new ModificationLog();
        log.Append(new ModificationEntry(0, TerrainModType.Dig, 0, 0, 0, 5, 1));
        Assert.Equal(1, log.Count);
    }

    [Fact]
    public void Append_PreservesOrder()
    {
        var log = new ModificationLog();
        log.Append(new ModificationEntry(1, TerrainModType.Dig, 0, 0, 0, 5, 1));
        log.Append(new ModificationEntry(2, TerrainModType.Fill, 10, 0, 10, 3, 2));
        log.Append(new ModificationEntry(3, TerrainModType.Explosion, 20, 0, 20, 10, 5));

        Assert.Equal(3, log.Count);
        Assert.Equal(1.0, log.Entries[0].Timestamp);
        Assert.Equal(2.0, log.Entries[1].Timestamp);
        Assert.Equal(3.0, log.Entries[2].Timestamp);
    }

    [Fact]
    public void GetModificationsInRange_ReturnsNearby()
    {
        var log = new ModificationLog();
        log.Append(new ModificationEntry(0, TerrainModType.Dig, 10, 0, 10, 5, 1));
        log.Append(new ModificationEntry(0, TerrainModType.Fill, 100, 0, 100, 5, 1));
        log.Append(new ModificationEntry(0, TerrainModType.Dig, 15, 0, 10, 5, 1));

        var nearby = log.GetModificationsInRange(10, 10, 20).ToList();
        Assert.Equal(2, nearby.Count); // first and third are near (10,10)
    }

    [Fact]
    public void GetModificationsInRange_ExcludesDistant()
    {
        var log = new ModificationLog();
        log.Append(new ModificationEntry(0, TerrainModType.Dig, 1000, 0, 1000, 5, 1));

        var nearby = log.GetModificationsInRange(0, 0, 10).ToList();
        Assert.Empty(nearby);
    }

    [Fact]
    public void GetModificationsInRange_IncludesExactBoundary()
    {
        var log = new ModificationLog();
        // Place at distance exactly = range (10 units away from origin)
        log.Append(new ModificationEntry(0, TerrainModType.Dig, 10, 0, 0, 5, 1));

        var nearby = log.GetModificationsInRange(0, 0, 10).ToList();
        Assert.Single(nearby);
    }

    [Fact]
    public void GetModificationsSince_ReturnsAfterTimestamp()
    {
        var log = new ModificationLog();
        log.Append(new ModificationEntry(1, TerrainModType.Dig, 0, 0, 0, 5, 1));
        log.Append(new ModificationEntry(5, TerrainModType.Fill, 0, 0, 0, 5, 1));
        log.Append(new ModificationEntry(10, TerrainModType.Dig, 0, 0, 0, 5, 1));

        var after = log.GetModificationsSince(5).ToList();
        Assert.Single(after);
        Assert.Equal(10.0, after[0].Timestamp);
    }

    [Fact]
    public void GetModificationsSince_ExcludesExactTimestamp()
    {
        var log = new ModificationLog();
        log.Append(new ModificationEntry(5, TerrainModType.Dig, 0, 0, 0, 5, 1));

        var after = log.GetModificationsSince(5).ToList();
        Assert.Empty(after);
    }

    [Fact]
    public void Clear_RemovesAll()
    {
        var log = new ModificationLog();
        log.Append(new ModificationEntry(1, TerrainModType.Dig, 0, 0, 0, 5, 1));
        log.Append(new ModificationEntry(2, TerrainModType.Fill, 0, 0, 0, 5, 1));
        log.Clear();
        Assert.Equal(0, log.Count);
        Assert.Empty(log.Entries);
    }

    [Fact]
    public void ModificationEntry_Metadata_DefaultsToNull()
    {
        var entry = new ModificationEntry(0, TerrainModType.Dig, 0, 0, 0, 5, 1);
        Assert.Null(entry.Metadata);
    }

    [Fact]
    public void ModificationEntry_Metadata_CanBeSet()
    {
        var entry = new ModificationEntry(0, TerrainModType.Dig, 0, 0, 0, 5, 1, "player_action");
        Assert.Equal("player_action", entry.Metadata);
    }
}
