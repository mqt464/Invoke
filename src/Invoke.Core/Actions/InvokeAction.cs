namespace Invoke.Core.Actions;

public enum InvokeActionKind
{
    Execute,
    OpenUrl,
    OpenPath,
    CopyText,
    SetQuery
}

public sealed record InvokeAction(
    string Id,
    string Title,
    Func<CancellationToken, Task> ExecuteAsync,
    bool RequiresConfirmation = false,
    string? ConfirmationText = null,
    InvokeActionKind Kind = InvokeActionKind.Execute,
    string? Target = null);
