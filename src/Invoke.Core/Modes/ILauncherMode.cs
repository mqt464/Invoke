using Invoke.Core.Actions;
using Invoke.Core.Config;
using Invoke.Core.Results;
using Invoke.Core.Rasi;

namespace Invoke.Core.Modes;

public interface ILauncherMode : IDisposable
{
    string Id { get; }
    string DisplayName { get; }
    int Priority { get; }
    ValueTask InitializeAsync(CancellationToken cancellationToken);
    Task<LauncherModeSnapshot> SearchAsync(LauncherQueryContext context, CancellationToken cancellationToken);
}

public interface ILauncherInteractiveMode : ILauncherMode
{
    Task<LauncherModeInteractionResult> ActivateEntryAsync(LauncherQueryContext context, LauncherModeEntry entry, CancellationToken cancellationToken);
    Task<LauncherModeInteractionResult> SubmitCustomAsync(LauncherQueryContext context, string input, CancellationToken cancellationToken);
    Task<LauncherModeInteractionResult> DeleteEntryAsync(LauncherQueryContext context, LauncherModeEntry entry, CancellationToken cancellationToken);
    Task<LauncherModeInteractionResult> HandleCustomKeyAsync(LauncherQueryContext context, LauncherModeEntry? entry, int customKeyIndex, CancellationToken cancellationToken);
}

public sealed record LauncherModeSnapshot(
    string ModeId,
    string? Prompt,
    string? Message,
    IReadOnlyList<LauncherModeEntry> Entries,
    IReadOnlySet<int>? UrgentIndices = null,
    IReadOnlySet<int>? ActiveIndices = null,
    string? SwitchMode = null,
    RasiDocument? ThemeOverlay = null,
    bool KeepSelection = false,
    bool KeepFilter = false,
    int? NewSelection = null,
    bool UseHotKeys = false,
    bool NoCustom = false,
    bool MarkupRows = false,
    string? DisplayPrefix = null,
    string? RawPrefix = null)
{
    public int EntryCount => Entries.Count;
}

public sealed record LauncherModeInteractionResult(
    bool KeepFilter = false,
    string? SwitchMode = null,
    bool CloseLauncher = false,
    LauncherModeSnapshot? Snapshot = null);

public sealed record LauncherModeEntry(
    string Text,
    string DisplayText,
    string SecondaryText,
    ResultKind Kind,
    double Score,
    InvokeAction Action,
    string? Icon = null,
    string? CompletionText = null,
    string? Meta = null,
    string? Info = null,
    bool NonSelectable = false,
    bool Permanent = false,
    bool Urgent = false,
    bool Active = false,
    string? Id = null)
{
    public string IdentityKey => string.IsNullOrWhiteSpace(Id)
        ? $"{Kind}|{DisplayText}|{SecondaryText}|{Action.Title}|{Icon}|{Info}"
        : $"id:{Id}";
}

public sealed record LauncherQueryContext(
    string RawQuery,
    string Terms,
    InvokeSettings Settings,
    ThemeSettings Theme,
    string? ActiveMode,
    int MaxResults);
