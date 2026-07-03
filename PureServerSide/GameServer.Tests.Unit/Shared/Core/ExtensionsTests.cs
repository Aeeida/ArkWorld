using FluentAssertions;
using Game.Shared.Core.Extensions;

namespace GameServer.Tests.Unit.Shared.Core;

public class StringExtensionsTests
{
    [Fact]
    public void IsNullOrWhiteSpace_Null_ShouldReturnTrue()
    {
        string? value = null;
        value.IsNullOrWhiteSpace().Should().BeTrue();
    }

    [Fact]
    public void IsNullOrWhiteSpace_Empty_ShouldReturnTrue()
    {
        "".IsNullOrWhiteSpace().Should().BeTrue();
    }

    [Fact]
    public void IsNullOrWhiteSpace_Whitespace_ShouldReturnTrue()
    {
        "   ".IsNullOrWhiteSpace().Should().BeTrue();
    }

    [Fact]
    public void IsNullOrWhiteSpace_ValidString_ShouldReturnFalse()
    {
        "hello".IsNullOrWhiteSpace().Should().BeFalse();
    }

    [Fact]
    public void ToSnakeCase_PascalCase_ShouldConvert()
    {
        "PlayerName".ToSnakeCase().Should().Be("player_name");
    }

    [Fact]
    public void ToSnakeCase_CamelCase_ShouldConvert()
    {
        "playerName".ToSnakeCase().Should().Be("player_name");
    }

    [Fact]
    public void ToSnakeCase_SingleWord_ShouldLowerCase()
    {
        "Player".ToSnakeCase().Should().Be("player");
    }

    [Fact]
    public void ToSnakeCase_AlreadyLower_ShouldNotChange()
    {
        "player".ToSnakeCase().Should().Be("player");
    }

    [Fact]
    public void ToSnakeCase_MultipleUpperCase_ShouldConvert()
    {
        "GameServerHost".ToSnakeCase().Should().Be("game_server_host");
    }
}

public class SpanExtensionsTests
{
    [Fact]
    public void TryParseGuid_ValidGuid_ShouldSucceed()
    {
        var guidStr = Guid.NewGuid().ToString().AsSpan();

        var success = guidStr.TryParseGuid(out var result);

        success.Should().BeTrue();
        result.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void TryParseGuid_InvalidString_ShouldFail()
    {
        var invalid = "not-a-guid".AsSpan();

        var success = invalid.TryParseGuid(out _);

        success.Should().BeFalse();
    }

    [Fact]
    public void WriteUtf8_ShouldWriteBytes()
    {
        Span<byte> buffer = stackalloc byte[20];
        var written = buffer.WriteUtf8("hello");

        written.Should().Be(5);
    }
}

public class CollectionExtensionsTests
{
    [Fact]
    public void AsReadOnlyList_ShouldReturnReadOnlyList()
    {
        var source = new[] { 1, 2, 3 };

        var result = source.AsReadOnlyList();

        result.Should().BeAssignableTo<IReadOnlyList<int>>();
        result.Should().HaveCount(3);
    }

    [Fact]
    public void AsReadOnlyList_AlreadyReadOnlyList_ShouldReturnSame()
    {
        IReadOnlyList<int> source = [1, 2, 3];

        var result = ((IEnumerable<int>)source).AsReadOnlyList();

        result.Should().BeSameAs(source);
    }
}
