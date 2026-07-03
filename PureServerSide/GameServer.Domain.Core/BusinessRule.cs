namespace GameServer.Domain.Core;

public interface IBusinessRule
{
    string RuleName { get; }
    string Message { get; }
    bool IsBroken();
}

public class BusinessRuleException(IBusinessRule rule) : Exception(rule.Message)
{
    public IBusinessRule BrokenRule { get; } = rule;
}

public static class BusinessRuleExtensions
{
    public static void CheckRule(this IBusinessRule rule)
    {
        if (rule.IsBroken())
            throw new BusinessRuleException(rule);
    }
}
