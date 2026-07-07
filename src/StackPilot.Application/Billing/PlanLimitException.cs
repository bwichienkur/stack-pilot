namespace StackPilot.Application.Billing;

public class PlanLimitException : Exception
{
    public string LimitCode { get; }

    public PlanLimitException(string limitCode, string message) : base(message)
    {
        LimitCode = limitCode;
    }
}
