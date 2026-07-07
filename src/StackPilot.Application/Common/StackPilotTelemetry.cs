using System.Diagnostics;

namespace StackPilot.Application.Common;

public static class StackPilotTelemetry
{
    public const string SourceName = "StackPilot";

    public static readonly ActivitySource ActivitySource = new(SourceName);

    public static Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal) =>
        ActivitySource.StartActivity(name, kind);
}
