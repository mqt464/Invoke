using Invoke.Core.Config;
using Invoke.Core.Rasi;

namespace Invoke.Core.Modes;

public sealed record LauncherSearchResult(
    string ActiveModeId,
    string Prompt,
    string? Message,
    IReadOnlyList<LauncherModeEntry> Entries,
    IReadOnlyList<LauncherModeDefinition> AvailableModes,
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
    string? RawPrefix = null);
