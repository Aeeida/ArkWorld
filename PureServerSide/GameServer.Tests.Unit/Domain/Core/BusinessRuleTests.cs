using FluentAssertions;
using GameServer.Domain.Core;

namespace GameServer.Tests.Unit.Domain.Core;

public class BusinessRuleTests
{
    private sealed class AlwaysBrokenRule : IBusinessRule
    {
        public string RuleName => "AlwaysBroken";
        public string Message => "This rule is always broken.";
        public bool IsBroken() => true;
    }

    private sealed class NeverBrokenRule : IBusinessRule
    {
        public string RuleName => "NeverBroken";
        public string Message => "This rule is never broken.";
        public bool IsBroken() => false;
    }

    [Fact]
    public void CheckRule_BrokenRule_ShouldThrowBusinessRuleException()
    {
        var rule = new AlwaysBrokenRule();

        var act = () => rule.CheckRule();

        act.Should().Throw<BusinessRuleException>()
            .Which.BrokenRule.RuleName.Should().Be("AlwaysBroken");
    }

    [Fact]
    public void CheckRule_ValidRule_ShouldNotThrow()
    {
        var rule = new NeverBrokenRule();

        var act = () => rule.CheckRule();

        act.Should().NotThrow();
    }

    [Fact]
    public void BusinessRuleException_ShouldContainMessage()
    {
        var rule = new AlwaysBrokenRule();

        var act = () => rule.CheckRule();

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("This rule is always broken.");
    }

    [Fact]
    public void BusinessRuleException_ShouldContainBrokenRule()
    {
        var rule = new AlwaysBrokenRule();

        try
        {
            rule.CheckRule();
        }
        catch (BusinessRuleException ex)
        {
            ex.BrokenRule.Should().BeSameAs(rule);
            return;
        }

        Assert.Fail("Expected BusinessRuleException was not thrown.");
    }
}
