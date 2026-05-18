namespace Invoke.App.Hotkeys;

public sealed class LauncherRequestedEventArgs(string? initialMode) : EventArgs
{
    public string? InitialMode { get; } = initialMode;
}
