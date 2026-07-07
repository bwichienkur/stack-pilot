using StackPilot.Domain.Enums;

namespace StackPilot.Application.Workflow;

public static class TicketStateMachine
{
  private static readonly Dictionary<TicketStatus, HashSet<TicketStatus>> AllowedTransitions = new()
  {
    [TicketStatus.Submitted] = [TicketStatus.AiAnalysisPending],
    [TicketStatus.AiAnalysisPending] = [TicketStatus.RequirementsDrafted, TicketStatus.AwaitingApproval],
    [TicketStatus.RequirementsDrafted] = [TicketStatus.AwaitingApproval, TicketStatus.AiAnalysisPending],
    [TicketStatus.AwaitingApproval] = [TicketStatus.Approved, TicketStatus.RequirementsDrafted],
    [TicketStatus.Approved] = [TicketStatus.ImplementationInProgress],
    [TicketStatus.ImplementationInProgress] = [TicketStatus.PullRequestCreated, TicketStatus.BuildRunning],
    [TicketStatus.PullRequestCreated] = [TicketStatus.BuildRunning, TicketStatus.ImplementationInProgress],
    [TicketStatus.BuildRunning] = [TicketStatus.DeployedToTest, TicketStatus.ImplementationInProgress],
    [TicketStatus.DeployedToTest] = [TicketStatus.QaInProgress, TicketStatus.QaFailed],
    [TicketStatus.QaInProgress] = [TicketStatus.QaPassed, TicketStatus.QaFailed],
    [TicketStatus.QaFailed] = [TicketStatus.QaInProgress, TicketStatus.DeployedToTest],
    [TicketStatus.QaPassed] = [TicketStatus.UatInProgress, TicketStatus.UatRejected],
    [TicketStatus.UatInProgress] = [TicketStatus.UatAccepted, TicketStatus.UatRejected],
    [TicketStatus.UatRejected] = [TicketStatus.UatInProgress, TicketStatus.QaPassed],
    [TicketStatus.UatAccepted] = [TicketStatus.ScheduledForProduction],
    [TicketStatus.ScheduledForProduction] = [TicketStatus.DeployedToProduction],
    [TicketStatus.DeployedToProduction] = [TicketStatus.Closed],
    [TicketStatus.Closed] = [],
  };

  public static bool CanTransition(TicketStatus from, TicketStatus to) =>
    AllowedTransitions.TryGetValue(from, out var targets) && targets.Contains(to);

  public static void ValidateTransition(TicketStatus from, TicketStatus to)
  {
    if (!CanTransition(from, to))
      throw new InvalidOperationException($"Invalid ticket status transition from {from} to {to}");
  }

  public static IReadOnlyList<string> GetAllowedNextStatuses(TicketStatus current) =>
    AllowedTransitions.TryGetValue(current, out var targets)
      ? targets.Select(s => s.ToString()).ToList()
      : [];
}
