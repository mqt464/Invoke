namespace Invoke.Core.Config;

public sealed class InvokeSettings
{
    public string LauncherHotkey { get; set; } = string.Empty;
    public string ThemeName { get; set; } = "default";
    public List<string> Modes { get; set; } = ["drun", "run", "files", "window", "combi"];
    public List<ModeEntry> ModeEntries { get; set; } =
    [
        ModeEntry.BuiltIn("drun"),
        ModeEntry.BuiltIn("run"),
        ModeEntry.BuiltIn("files"),
        ModeEntry.BuiltIn("window"),
        ModeEntry.BuiltIn("combi")
    ];
    public string DefaultMode { get; set; } = "drun";
    public List<string> CombiModes { get; set; } = ["drun", "run", "files", "window"];
    public Dictionary<string, string> DisplayNames { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["drun"] = "drun",
        ["run"] = "run",
        ["files"] = "files",
        ["window"] = "window",
        ["combi"] = "combi"
    };
    public Dictionary<string, string> ModeHotkeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> ResultTitleTemplates { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> ResultSubtitleTemplates { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, double> CombiScoreBoosts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> MaxResultsPerMode { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string Matching { get; set; } = "fuzzy";
    public string SortingMethod { get; set; } = "normal";
    public bool CaseSensitive { get; set; }
    public bool Cycle { get; set; } = true;
    public bool SidebarMode { get; set; } = true;
    public bool ShowIcons { get; set; } = true;
    public string Font { get; set; } = "Segoe UI Variable 15";
    public string Location { get; set; } = "north";
    public double XOffset { get; set; }
    public double YOffset { get; set; }
    public int Lines { get; set; } = 8;
    public int Columns { get; set; } = 1;
    public int MaxResults { get; set; } = 8;
    public int DebounceMilliseconds { get; set; } = 35;
    public int LoadingIndicatorDelayMilliseconds { get; set; } = 1200;
    public bool CloseOnFocusLoss { get; set; } = true;
    public bool CloseAfterAction { get; set; } = true;
    public bool ClearQueryOnHide { get; set; } = true;
    public bool AutoSelectFirstResult { get; set; } = true;
    public bool ShowStartPage { get; set; } = true;
    public string? EverythingCliPath { get; set; }
    public string? EverythingPath { get; set; }
    public List<string> PluginOrder { get; set; } = [];
    public Dictionary<string, string> Keybindings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public HotkeySettings Hotkeys { get; set; } = new();
    public SearchSettings Search { get; set; } = new();
    public LauncherSettings Launcher { get; set; } = new();
    public PlacementSettings Placement { get; set; } = new();
    public HistorySettings History { get; set; } = new();

    public HotkeySettings ToHotkeySettings() =>
        new()
        {
            LauncherHotkey = Hotkeys.LauncherHotkey,
            ModeHotkeys = new Dictionary<string, string>(Hotkeys.ModeHotkeys, StringComparer.OrdinalIgnoreCase)
        };
}

public sealed class ModeEntry
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string RawExpression { get; init; }

    public static ModeEntry BuiltIn(string id) =>
        new()
        {
            Id = id,
            DisplayName = id,
            RawExpression = id
        };

}

public sealed class SearchSettings
{
    public int MaxResults { get; set; } = 8;
    public int DebounceMilliseconds { get; set; } = 35;
    public int LoadingIndicatorDelayMilliseconds { get; set; } = 1200;
    public string DefaultMode { get; set; } = "drun";
    public List<string> ProviderOrder { get; set; } = ["window", "drun", "run", "files"];
    public CombiSettings Combi { get; set; } = new();
}

public sealed class LauncherSettings
{
    public string PlacementMode { get; set; } = "activeScreen";
    public string Anchor { get; set; } = "topCenter";
    public string WidthMode { get; set; } = "theme";
    public double Width { get; set; }
    public double MinWidth { get; set; } = 520;
    public double MaxWidth { get; set; } = 1100;
    public int VisibleResults { get; set; } = 8;
    public double HorizontalOffset { get; set; }
    public double VerticalOffset { get; set; }
    public bool ShowStartPage { get; set; } = true;
    public bool SelectionWrap { get; set; } = true;
    public bool AutoSelectFirstResult { get; set; } = true;
    public bool ClearQueryOnHide { get; set; } = true;
    public bool CloseOnFocusLoss { get; set; } = true;
    public bool CloseAfterAction { get; set; } = true;
}

public sealed class CombiSettings
{
    public List<string> Providers { get; set; } = ["drun", "run", "files", "window"];
    public Dictionary<string, double> ScoreBoosts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> MaxResultsPerProvider { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class HotkeySettings
{
    public string LauncherHotkey { get; set; } = string.Empty;
    public Dictionary<string, string> ModeHotkeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class PlacementSettings
{
    public string PlacementMode { get; set; } = "activeScreen";
    public string Anchor { get; set; } = "topCenter";
    public string WidthMode { get; set; } = "theme";
    public double Width { get; set; }
    public double MinWidth { get; set; } = 520;
    public double MaxWidth { get; set; } = 1100;
    public int VisibleResults { get; set; } = 8;
    public double HorizontalOffset { get; set; }
    public double VerticalOffset { get; set; }
}

public sealed class HistorySettings
{
    public bool EnableRecentBoost { get; set; } = true;
    public int MaxRecentItems { get; set; } = 24;
    public double ScoreBoost { get; set; } = 160;
    public List<RecentLaunchEntry> RecentLaunches { get; set; } = [];
}

public sealed class RecentLaunchEntry
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public DateTime LastUsedUtc { get; set; } = DateTime.UtcNow;
    public int LaunchCount { get; set; } = 1;
}
