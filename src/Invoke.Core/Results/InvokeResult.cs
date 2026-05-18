using Invoke.Core.Actions;

namespace Invoke.Core.Results;

public sealed record InvokeResult(
    string Title,
    string Subtitle,
    ResultKind Kind,
    double Score,
    InvokeAction Action,
    string? Icon = null)
{
    public static InvokeResult Error(string source, string message) =>
        new(
            $"{source} error",
            message,
            ResultKind.Error,
            -1000,
            new InvokeAction("noop", "Dismiss", _ => Task.CompletedTask));
}

public enum ResultKind
{
    App,
    File,
    Folder,
    Web,
    Command,
    Config,
    Install,
    Uninstall,
    Error
}
