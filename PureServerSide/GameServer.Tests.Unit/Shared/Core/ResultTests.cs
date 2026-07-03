using FluentAssertions;
using Game.Shared.Core;

namespace GameServer.Tests.Unit.Shared.Core;

public class ResultGenericTests
{
    [Fact]
    public void Success_ShouldContainValue()
    {
        var result = Result<int>.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failure_ShouldContainError()
    {
        var result = Result<int>.Failure("Not found");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Not found");
    }

    [Fact]
    public void Match_Success_ShouldInvokeSuccessBranch()
    {
        var result = Result<int>.Success(42);

        var output = result.Match(v => v.ToString(), e => e);

        output.Should().Be("42");
    }

    [Fact]
    public void Match_Failure_ShouldInvokeFailureBranch()
    {
        var result = Result<int>.Failure("oops");

        var output = result.Match(v => v.ToString(), e => e);

        output.Should().Be("oops");
    }

    [Fact]
    public void ImplicitConversion_ShouldCreateSuccess()
    {
        Result<int> result = 42;

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Success_WithStringValue_ShouldWork()
    {
        var result = Result<string>.Success("hello");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
    }

    [Fact]
    public void Success_WithComplexType_ShouldWork()
    {
        var guid = Guid.NewGuid();
        var result = Result<Guid>.Success(guid);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(guid);
    }
}

public class ResultNonGenericTests
{
    [Fact]
    public void Success_ShouldBeSuccessful()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failure_ShouldContainError()
    {
        var result = Result.Failure("Something went wrong");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Something went wrong");
    }
}
