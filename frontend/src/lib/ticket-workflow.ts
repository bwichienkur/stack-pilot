const WORKFLOW_TRANSITIONS: Record<string, string[]> = {
  Submitted: ["AiAnalysisPending"],
  AiAnalysisPending: ["RequirementsDrafted"],
  RequirementsDrafted: ["AwaitingApproval", "Approved"],
  AwaitingApproval: ["Approved", "RequirementsDrafted"],
  Approved: ["ImplementationInProgress"],
  ImplementationInProgress: ["PullRequestCreated"],
  PullRequestCreated: ["BuildRunning"],
  BuildRunning: ["DeployedToTest", "QaInProgress"],
  DeployedToTest: ["QaInProgress"],
  QaInProgress: ["QaPassed", "QaFailed"],
  QaFailed: ["ImplementationInProgress"],
  QaPassed: ["UatInProgress"],
  UatInProgress: ["UatAccepted", "UatRejected"],
  UatRejected: ["ImplementationInProgress"],
  UatAccepted: ["ScheduledForProduction"],
  ScheduledForProduction: ["DeployedToProduction"],
  DeployedToProduction: ["Closed"],
};

export function getFallbackWorkflowStates(status: string) {
  return {
    currentStatus: status,
    allowedTransitions: WORKFLOW_TRANSITIONS[status] ?? [],
  };
}
