namespace FleetAutomate.Cli.Output;

internal sealed record ActionExecutionRecord(
    string Path,
    string Name,
    string State,
    long DurationMilliseconds);

internal sealed record TestFlowRunResult(
    string ProjectPath,
    string FlowName,
    string State,
    string BreakReason,
    string? FailedAction,
    IReadOnlyList<ActionExecutionRecord> Actions);
