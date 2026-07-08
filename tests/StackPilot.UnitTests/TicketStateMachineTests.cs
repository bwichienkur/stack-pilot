using StackPilot.Application.Workflow;
using StackPilot.Domain.Enums;

namespace StackPilot.UnitTests;

public class TicketStateMachineTests
{
    [Theory]
    [InlineData(TicketStatus.Submitted, TicketStatus.AiAnalysisPending, true)]
    [InlineData(TicketStatus.AiAnalysisPending, TicketStatus.RequirementsDrafted, true)]
    [InlineData(TicketStatus.AwaitingApproval, TicketStatus.Approved, true)]
    [InlineData(TicketStatus.QaPassed, TicketStatus.UatInProgress, true)]
    [InlineData(TicketStatus.Submitted, TicketStatus.Approved, false)]
    [InlineData(TicketStatus.Closed, TicketStatus.Submitted, false)]
    public void CanTransition_ReturnsExpected(TicketStatus from, TicketStatus to, bool expected) =>
        Assert.Equal(expected, TicketStateMachine.CanTransition(from, to));

    [Fact]
    public void ValidateTransition_AllowsValidPath()
    {
        var exception = Record.Exception(() =>
            TicketStateMachine.ValidateTransition(TicketStatus.Submitted, TicketStatus.AiAnalysisPending));
        Assert.Null(exception);
    }

    [Fact]
    public void ValidateTransition_ThrowsOnInvalidPath()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            TicketStateMachine.ValidateTransition(TicketStatus.Closed, TicketStatus.Submitted));
        Assert.Contains("Invalid ticket status transition", ex.Message);
    }

    [Fact]
    public void GetAllowedNextStatuses_FromSubmitted_IncludesAiAnalysisPending()
    {
        var next = TicketStateMachine.GetAllowedNextStatuses(TicketStatus.Submitted);
        Assert.Contains(nameof(TicketStatus.AiAnalysisPending), next);
    }

    [Fact]
    public void GetAllowedNextStatuses_FromClosed_IsEmpty() =>
        Assert.Empty(TicketStateMachine.GetAllowedNextStatuses(TicketStatus.Closed));
}
