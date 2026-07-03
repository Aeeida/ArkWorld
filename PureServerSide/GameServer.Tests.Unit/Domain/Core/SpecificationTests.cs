using System.Linq.Expressions;
using FluentAssertions;
using GameServer.Domain.Core;

namespace GameServer.Tests.Unit.Domain.Core;

public class SpecificationTests
{
    private sealed class GreaterThanSpec(int threshold) : Specification<int>
    {
        public override Expression<Func<int, bool>> ToExpression() =>
            x => x > threshold;
    }

    private sealed class LessThanSpec(int threshold) : Specification<int>
    {
        public override Expression<Func<int, bool>> ToExpression() =>
            x => x < threshold;
    }

    private sealed class EvenSpec : Specification<int>
    {
        public override Expression<Func<int, bool>> ToExpression() =>
            x => x % 2 == 0;
    }

    [Fact]
    public void IsSatisfiedBy_ShouldReturnTrue_WhenMatches()
    {
        var spec = new GreaterThanSpec(5);

        spec.IsSatisfiedBy(10).Should().BeTrue();
    }

    [Fact]
    public void IsSatisfiedBy_ShouldReturnFalse_WhenNotMatches()
    {
        var spec = new GreaterThanSpec(5);

        spec.IsSatisfiedBy(3).Should().BeFalse();
    }

    [Fact]
    public void And_ShouldCombineBothConditions()
    {
        var greaterThan5 = new GreaterThanSpec(5);
        var lessThan20 = new LessThanSpec(20);
        var combined = greaterThan5.And(lessThan20);

        combined.IsSatisfiedBy(10).Should().BeTrue();
        combined.IsSatisfiedBy(3).Should().BeFalse();
        combined.IsSatisfiedBy(25).Should().BeFalse();
    }

    [Fact]
    public void Or_ShouldSatisfyEither()
    {
        var lessThan5 = new LessThanSpec(5);
        var greaterThan20 = new GreaterThanSpec(20);
        var combined = lessThan5.Or(greaterThan20);

        combined.IsSatisfiedBy(3).Should().BeTrue();
        combined.IsSatisfiedBy(25).Should().BeTrue();
        combined.IsSatisfiedBy(10).Should().BeFalse();
    }

    [Fact]
    public void Not_ShouldInvert()
    {
        var even = new EvenSpec();
        var odd = even.Not();

        odd.IsSatisfiedBy(3).Should().BeTrue();
        odd.IsSatisfiedBy(4).Should().BeFalse();
    }

    [Fact]
    public void CombineAll_AndOrNot_ShouldWorkTogether()
    {
        var greaterThan5 = new GreaterThanSpec(5);
        var even = new EvenSpec();

        // Greater than 5 AND even
        var combined = greaterThan5.And(even);

        combined.IsSatisfiedBy(8).Should().BeTrue();
        combined.IsSatisfiedBy(7).Should().BeFalse();
        combined.IsSatisfiedBy(4).Should().BeFalse();
    }

    [Fact]
    public void ToExpression_ShouldReturnValidExpression()
    {
        var spec = new GreaterThanSpec(5);

        var expr = spec.ToExpression();

        expr.Should().NotBeNull();
        var compiled = expr.Compile();
        compiled(10).Should().BeTrue();
        compiled(3).Should().BeFalse();
    }
}
