namespace Invoke.Core.Modes;

public sealed record LauncherModeDefinition(
    string Id,
    string DisplayName,
    ILauncherMode Mode,
    bool AlwaysEvaluate = false,
    int Order = 0);
