using System.Globalization;
using System.Text;
using Invoke.Core.Plugins.Rich;
using Invoke.Core.Rasi;

namespace Invoke.Core.Config;

public sealed class ConfigService
{
    private const string ConfigFileName = "config.rasi";
    private const string ThemeExtension = ".rasi";
    private const string ThemesDirectoryName = "themes";
    private const string ScriptsDirectoryName = "scripts";
    private const string RichScriptManifestFileName = "script.toml";
    private readonly RasiLoader _loader = new();
    private static readonly IReadOnlyDictionary<string, string> DefaultKeybindings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["kb-accept-entry"] = "Return",
        ["kb-cancel"] = "Escape",
        ["kb-row-up"] = "Up",
        ["kb-row-down"] = "Down"
    };
    private static readonly string[] SupportedKeybindings =
    [
        "kb-accept-entry",
        "kb-accept-alt",
        "kb-accept-custom",
        "kb-cancel",
        "kb-row-up",
        "kb-row-down",
        "kb-row-left",
        "kb-row-right",
        "kb-row-first",
        "kb-row-last",
        "kb-row-page-up",
        "kb-row-page-down",
        "kb-move-front",
        "kb-move-end",
        "kb-remove-char-back",
        "kb-remove-char-forward",
        "kb-remove-to-sol",
        "kb-remove-to-eol",
        "kb-clear-line",
        "kb-delete-entry",
        "kb-mode-next",
        "kb-mode-previous",
        "kb-mode-complete",
        "kb-primary-paste",
        "kb-secondary-paste",
        "kb-toggle-case-sensitivity",
        "kb-custom-1",
        "kb-custom-2",
        "kb-custom-3",
        "kb-custom-4",
        "kb-custom-5"
    ];

    public string ConfigDirectory { get; }
    public string ConfigPath => Path.Combine(ConfigDirectory, ConfigFileName);
    public string ThemesDirectory => Path.Combine(ConfigDirectory, ThemesDirectoryName);
    public string ScriptsDirectory => Path.Combine(ConfigDirectory, ScriptsDirectoryName);
    public string ScriptOrderPath => Path.Combine(ScriptsDirectory, "order.txt");

    public ConfigService(string? configDirectory = null)
    {
        ConfigDirectory = configDirectory ?? ResolveDefaultConfigDirectory();
        EnsureSeedContent();
    }

    public InvokeSettings LoadSettings()
    {
        var document = _loader.LoadFile(ConfigPath);
        var configuration = document.Sections.GetValueOrDefault("configuration", new Dictionary<string, RasiValue>(StringComparer.OrdinalIgnoreCase));
        var settings = new InvokeSettings
        {
            ThemeName = GetString(configuration, "theme", "default"),
            LauncherHotkey = GetOptionalString(configuration, "launcher-hotkey") ?? string.Empty,
            Matching = GetString(configuration, "matching", "fuzzy"),
            SortingMethod = GetString(configuration, "sorting-method", "normal"),
            CaseSensitive = GetBool(configuration, "case-sensitive"),
            Cycle = GetBool(configuration, "cycle", true),
            SidebarMode = GetBool(configuration, "sidebar-mode", true),
            ShowIcons = GetBool(configuration, "show-icons", true),
            Font = GetString(configuration, "font", "Segoe UI Variable 15"),
            Location = GetString(configuration, "location", "north"),
            XOffset = GetNumber(configuration, "xoffset"),
            YOffset = GetNumber(configuration, "yoffset"),
            Lines = Clamp((int)GetNumber(configuration, "lines", 8), 1, 24),
            Columns = Clamp((int)GetNumber(configuration, "columns", 1), 1, 8),
            MaxResults = Clamp((int)GetNumber(configuration, "lines", 8), 1, 50),
            DebounceMilliseconds = Clamp((int)GetNumber(configuration, "search-delay", 35), 0, 2_000),
            LoadingIndicatorDelayMilliseconds = Clamp((int)GetNumber(configuration, "loading-delay", 1200), 0, 10_000),
            CloseOnFocusLoss = GetBool(configuration, "close-on-focus-loss", true),
            CloseAfterAction = GetBool(configuration, "close-after-action", true),
            ClearQueryOnHide = GetBool(configuration, "clear-query-on-hide", true),
            AutoSelectFirstResult = GetBool(configuration, "auto-select", true),
            ShowStartPage = GetBool(configuration, "show-start-page", true),
            EverythingCliPath = GetOptionalString(configuration, "everything-cli-path"),
            EverythingPath = GetOptionalString(configuration, "everything-path")
        };

        settings.ModeEntries = ParseModes(GetString(configuration, "modes", DefaultModesConfigValue));
        settings.Modes = settings.ModeEntries.Select(static mode => mode.Id).ToList();
        settings.DefaultMode = ResolveConfiguredModeId(GetString(configuration, "default-mode", settings.ModeEntries.FirstOrDefault()?.DisplayName ?? "drun"), settings.ModeEntries);
        settings.CombiModes = ParseModeReferences(GetString(configuration, "combi-modes", "drun,run,window"), settings.ModeEntries);
        settings.Hotkeys.LauncherHotkey = settings.LauncherHotkey;
        settings.Placement.Anchor = NormalizeLocationAnchor(settings.Location);
        settings.Placement.PlacementMode = "activeScreen";
        settings.Placement.WidthMode = "theme";
        settings.Placement.HorizontalOffset = settings.XOffset;
        settings.Placement.VerticalOffset = settings.YOffset;
        settings.Placement.VisibleResults = settings.Lines;
        settings.Search.MaxResults = settings.MaxResults;
        settings.Search.DebounceMilliseconds = settings.DebounceMilliseconds;
        settings.Search.LoadingIndicatorDelayMilliseconds = settings.LoadingIndicatorDelayMilliseconds;
        settings.Search.DefaultMode = settings.DefaultMode;
        settings.Search.ProviderOrder = settings.Modes
            .Where(static mode => mode is "drun" or "run" or "files" or "window")
            .ToList();
        settings.Search.Combi.Providers = [.. settings.CombiModes];
        settings.PluginOrder = LoadScriptOrder();
        settings.Launcher.PlacementMode = settings.Placement.PlacementMode;
        settings.Launcher.Anchor = settings.Placement.Anchor;
        settings.Launcher.WidthMode = settings.Placement.WidthMode;
        settings.Launcher.Width = settings.Placement.Width;
        settings.Launcher.MinWidth = settings.Placement.MinWidth;
        settings.Launcher.MaxWidth = settings.Placement.MaxWidth;
        settings.Launcher.VisibleResults = settings.Lines;
        settings.Launcher.HorizontalOffset = settings.XOffset;
        settings.Launcher.VerticalOffset = settings.YOffset;
        settings.Launcher.ShowStartPage = settings.ShowStartPage;
        settings.Launcher.SelectionWrap = settings.Cycle;
        settings.Launcher.AutoSelectFirstResult = settings.AutoSelectFirstResult;
        settings.Launcher.ClearQueryOnHide = settings.ClearQueryOnHide;
        settings.Launcher.CloseOnFocusLoss = settings.CloseOnFocusLoss;
        settings.Launcher.CloseAfterAction = settings.CloseAfterAction;

        foreach (var mode in settings.ModeEntries)
        {
            settings.DisplayNames[mode.Id] = GetString(configuration, $"display-{mode.Id}", mode.DisplayName);
            var hotkey = GetOptionalString(configuration, $"mode-{mode.Id}-hotkey");
            if (!string.IsNullOrWhiteSpace(hotkey))
            {
                settings.ModeHotkeys[mode.Id] = hotkey;
                settings.Hotkeys.ModeHotkeys[mode.Id] = hotkey;
            }
        }

        settings.ResultTitleTemplates = ReadPrefixedStringMap(configuration, "result-title-");
        settings.ResultSubtitleTemplates = ReadPrefixedStringMap(configuration, "result-subtitle-");

        settings.Keybindings = BuildKeybindings(configuration);

        return settings;
    }

    public void SaveSettings(InvokeSettings settings)
    {
        Directory.CreateDirectory(ConfigDirectory);
        var builder = new StringBuilder();
        builder.AppendLine("configuration {");
        AppendProperty(builder, "modes", string.Join(',', settings.ModeEntries.Select(static mode => mode.RawExpression)));
        AppendProperty(builder, "default-mode", settings.DefaultMode);
        AppendProperty(builder, "combi-modes", string.Join(',', settings.CombiModes));
        AppendProperty(builder, "theme", settings.ThemeName);
        if (!string.IsNullOrWhiteSpace(settings.LauncherHotkey))
            AppendProperty(builder, "launcher-hotkey", settings.LauncherHotkey);
        AppendProperty(builder, "matching", settings.Matching);
        AppendProperty(builder, "sorting-method", settings.SortingMethod);
        AppendProperty(builder, "case-sensitive", settings.CaseSensitive);
        AppendProperty(builder, "cycle", settings.Cycle);
        AppendProperty(builder, "sidebar-mode", settings.SidebarMode);
        AppendProperty(builder, "show-icons", settings.ShowIcons);
        AppendProperty(builder, "font", settings.Font);
        AppendProperty(builder, "location", settings.Location);
        AppendProperty(builder, "xoffset", settings.XOffset);
        AppendProperty(builder, "yoffset", settings.YOffset);
        AppendProperty(builder, "lines", settings.Lines);
        AppendProperty(builder, "columns", settings.Columns);
        AppendProperty(builder, "search-delay", settings.DebounceMilliseconds);
        AppendProperty(builder, "loading-delay", settings.LoadingIndicatorDelayMilliseconds);
        AppendProperty(builder, "close-on-focus-loss", settings.CloseOnFocusLoss);
        AppendProperty(builder, "close-after-action", settings.CloseAfterAction);
        AppendProperty(builder, "clear-query-on-hide", settings.ClearQueryOnHide);
        AppendProperty(builder, "auto-select", settings.AutoSelectFirstResult);
        AppendProperty(builder, "show-start-page", settings.ShowStartPage);
        if (!string.IsNullOrWhiteSpace(settings.EverythingCliPath))
            AppendProperty(builder, "everything-cli-path", settings.EverythingCliPath);
        if (!string.IsNullOrWhiteSpace(settings.EverythingPath))
            AppendProperty(builder, "everything-path", settings.EverythingPath);
        foreach (var displayName in settings.DisplayNames.OrderBy(static item => item.Key, StringComparer.OrdinalIgnoreCase))
            AppendProperty(builder, $"display-{displayName.Key}", displayName.Value);
        foreach (var template in settings.ResultTitleTemplates.OrderBy(static item => item.Key, StringComparer.OrdinalIgnoreCase))
            AppendProperty(builder, $"result-title-{template.Key}", template.Value);
        foreach (var template in settings.ResultSubtitleTemplates.OrderBy(static item => item.Key, StringComparer.OrdinalIgnoreCase))
            AppendProperty(builder, $"result-subtitle-{template.Key}", template.Value);
        foreach (var hotkey in settings.ModeHotkeys.OrderBy(static item => item.Key, StringComparer.OrdinalIgnoreCase))
            AppendProperty(builder, $"mode-{hotkey.Key}-hotkey", hotkey.Value);
        foreach (var keybinding in settings.Keybindings.OrderBy(static item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(keybinding.Value))
                continue;

            if (DefaultKeybindings.TryGetValue(keybinding.Key, out var defaultBinding) &&
                string.Equals(keybinding.Value, defaultBinding, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            AppendProperty(builder, keybinding.Key, keybinding.Value);
        }
        builder.AppendLine("}");
        File.WriteAllText(ConfigPath, builder.ToString());
    }

    private static Dictionary<string, string> BuildKeybindings(IReadOnlyDictionary<string, RasiValue> configuration)
    {
        var keybindings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var defaultBinding in DefaultKeybindings)
            keybindings[defaultBinding.Key] = defaultBinding.Value;

        foreach (var bindingName in SupportedKeybindings)
        {
            var configuredBinding = GetOptionalString(configuration, bindingName);
            if (configuredBinding is null)
                continue;

            if (string.IsNullOrWhiteSpace(configuredBinding))
            {
                keybindings.Remove(bindingName);
                continue;
            }

            keybindings[bindingName] = configuredBinding;
        }

        return keybindings;
    }

    public IReadOnlyDictionary<string, ThemeSettings> LoadThemes()
    {
        Directory.CreateDirectory(ThemesDirectory);
        var themes = new Dictionary<string, ThemeSettings>(StringComparer.OrdinalIgnoreCase);
        foreach (var themePath in Directory.EnumerateFiles(ThemesDirectory, "*" + ThemeExtension, SearchOption.TopDirectoryOnly))
        {
            var key = Path.GetFileNameWithoutExtension(themePath);
            themes[key] = LoadThemeFromPath(themePath);
        }

        if (themes.Count == 0)
        {
            EnsureDefaultTheme();
            themes["default"] = LoadThemeFromPath(Path.Combine(ThemesDirectory, "default.rasi"));
        }

        return themes;
    }

    public ThemeSettings LoadTheme(string? themeName = null)
    {
        var key = string.IsNullOrWhiteSpace(themeName) ? "default" : themeName.Trim();
        var path = Path.Combine(ThemesDirectory, key + ThemeExtension);
        if (!File.Exists(path))
            path = Path.Combine(ThemesDirectory, "default.rasi");

        return LoadThemeFromPath(path);
    }

    public ThemeSettings ApplyThemeOverlay(ThemeSettings baseTheme, string themeSnippet)
    {
        var overlayDocument = RasiParser.Parse(themeSnippet);
        return ApplyThemeOverlay(baseTheme, overlayDocument);
    }

    public ThemeSettings ApplyThemeOverlay(ThemeSettings baseTheme, RasiDocument overlayDocument)
    {
        return ApplyThemeDocument(baseTheme.Clone(), overlayDocument);
    }

    public IReadOnlyList<RichScriptDefinition> LoadRichScripts()
    {
        Directory.CreateDirectory(ScriptsDirectory);
        var configuredOrder = LoadScriptOrder();
        var orderLookup = configuredOrder
            .Select((scriptId, index) => (scriptId, index))
            .ToDictionary(static entry => entry.scriptId, static entry => entry.index, StringComparer.OrdinalIgnoreCase);

        return Directory.EnumerateFiles(ScriptsDirectory, RichScriptManifestFileName, SearchOption.AllDirectories)
            .Select(ParseRichScriptManifest)
            .Where(static script => script is not null)
            .Select(static script => script!)
            .OrderBy(script => orderLookup.GetValueOrDefault(script.Manifest.Id, int.MaxValue))
            .ThenBy(static script => script.Manifest.Priority)
            .ThenBy(static script => script.Manifest.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public string GetThemePath(string themeName) => Path.Combine(ThemesDirectory, themeName + ThemeExtension);

    private ThemeSettings LoadThemeFromPath(string path)
    {
        var document = _loader.LoadFile(path);
        var theme = new ThemeSettings
        {
            Name = Path.GetFileNameWithoutExtension(path),
            BaseDirectory = Path.GetDirectoryName(path) ?? ThemesDirectory
        };

        return ApplyThemeDocument(theme, document);
    }

    private static ThemeSettings ApplyThemeDocument(ThemeSettings theme, RasiDocument document)
    {
        var resolvedSections = ResolveThemeSections(theme, document);
        theme.WidgetProperties = resolvedSections;

        theme.Background = GetString(resolvedSections, "window", "background-color", GetString(resolvedSections, "*", "background-color", theme.Background));
        theme.Foreground = GetString(resolvedSections, "window", "text-color", GetString(resolvedSections, "*", "text-color", theme.Foreground));
        theme.MutedForeground = GetString(resolvedSections, "prompt", "text-color", GetString(resolvedSections, "message", "text-color", theme.MutedForeground));
        theme.Accent = GetString(resolvedSections, "element selected", "border-color", GetString(resolvedSections, "*", "accent-color", theme.Accent));
        theme.SelectedBackground = GetString(resolvedSections, "element selected", "background-color", theme.SelectedBackground);
        theme.Border = GetString(resolvedSections, "window", "border-color", theme.Border);
        theme.ShowPrompt = GetBool(resolvedSections, "prompt", "enabled", theme.ShowPrompt);
        theme.PromptPersistent = GetBool(resolvedSections, "prompt", "persistent", theme.PromptPersistent);
        theme.PromptOpacity = Math.Clamp(GetDistance(resolvedSections, "prompt", "opacity", theme.PromptOpacity), 0, 1);
        theme.ResultLayout = GetString(resolvedSections, "*", "result-layout", theme.ResultLayout);
        theme.ShowSubtitles = GetBool(resolvedSections, "*", "show-subtitles", theme.ShowSubtitles);
        theme.ShowSelectionAccent = GetBool(resolvedSections, "*", "show-selection-accent", theme.ShowSelectionAccent);
        theme.ShowIcons = GetBool(resolvedSections, "element-icon", "enabled", GetBool(resolvedSections, "mode-switcher", "show-icons", theme.ShowIcons));
        theme.ShowModeSwitcher = GetBool(resolvedSections, "mode-switcher", "enabled", true);
        theme.WindowWidth = GetDistance(resolvedSections, "window", "width", theme.WindowWidth);
        theme.CornerRadius = GetDistance(resolvedSections, "window", "border-radius", theme.CornerRadius);
        theme.RowHeight = GetDistance(resolvedSections, "element", "height", theme.RowHeight);
        theme.ResultCornerRadius = GetDistance(resolvedSections, "element", "border-radius", theme.ResultCornerRadius);
        theme.ResultGap = GetDistance(resolvedSections, "listview", "spacing", theme.ResultGap);
        theme.IconSize = GetDistance(resolvedSections, "element-icon", "size", theme.IconSize);
        theme.IconColumnWidth = GetDistance(resolvedSections, "element-icon", "column-width", theme.IconColumnWidth);
        theme.IconContainerSize = GetDistance(resolvedSections, "element-icon", "container-size", theme.IconContainerSize);
        theme.FallbackIconSize = GetDistance(resolvedSections, "element-icon", "fallback-size", theme.FallbackIconSize);
        theme.IconContainerSize = Math.Max(theme.IconSize + 6, theme.IconContainerSize);
        theme.SearchHorizontalPadding = GetPaddingHorizontal(resolvedSections, "inputbar", theme.SearchHorizontalPadding);
        theme.SearchVerticalPadding = GetPaddingVertical(resolvedSections, "inputbar", theme.SearchVerticalPadding);
        theme.ResultHorizontalPadding = GetPaddingHorizontal(resolvedSections, "element", theme.ResultHorizontalPadding);
        theme.ResultVerticalPadding = GetPaddingVertical(resolvedSections, "element", theme.ResultVerticalPadding);
        theme.ResultTextLeftMargin = GetDistance(resolvedSections, "element-text", "margin-left", theme.ResultTextLeftMargin);
        theme.ResultTextRightMargin = GetDistance(resolvedSections, "element-text", "margin-right", theme.ResultTextRightMargin);
        theme.SeparatorThickness = GetDistance(resolvedSections, "*", "separator-thickness", theme.SeparatorThickness);
        theme.AnimateResults = GetBool(resolvedSections, "*", "animate-results", theme.AnimateResults);
        theme.ResultsAnimationDurationMilliseconds = GetDistance(resolvedSections, "*", "results-animation-duration", theme.ResultsAnimationDurationMilliseconds);
        theme.ResultsAnimationOffsetY = GetDistance(resolvedSections, "*", "results-animation-offset-y", theme.ResultsAnimationOffsetY);
        theme.ResultsAnimationStaggerMilliseconds = GetDistance(resolvedSections, "*", "results-animation-stagger", theme.ResultsAnimationStaggerMilliseconds);
        theme.AnimateSelection = GetBool(resolvedSections, "*", "animate-selection", theme.AnimateSelection);
        theme.SelectionAnimationDurationMilliseconds = GetDistance(resolvedSections, "*", "selection-animation-duration", theme.SelectionAnimationDurationMilliseconds);
        theme.SelectionAnimationOffsetX = GetDistance(resolvedSections, "*", "selection-animation-offset-x", theme.SelectionAnimationOffsetX);
        theme.PromptText = GetString(resolvedSections, "prompt", "text", theme.PromptText);
        theme.MessageText = GetString(resolvedSections, "message", "text", string.Empty);
        return theme;
    }

    private static Dictionary<string, Dictionary<string, string>> ResolveThemeSections(ThemeSettings theme, RasiDocument document)
    {
        var resolvedSections = theme.WidgetProperties.ToDictionary(
            static entry => entry.Key,
            static entry => new Dictionary<string, string>(entry.Value, StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
        foreach (var sectionName in document.Sections.Keys)
            ResolveSection(sectionName, document, theme, resolvedSections, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        return resolvedSections;
    }

    private static Dictionary<string, string> ResolveSection(
        string sectionName,
        RasiDocument document,
        ThemeSettings theme,
        Dictionary<string, Dictionary<string, string>> cache,
        HashSet<string> stack)
    {
        if (cache.TryGetValue(sectionName, out var existing) && !document.Sections.ContainsKey(sectionName))
            return existing;

        if (!document.Sections.TryGetValue(sectionName, out var source))
            return cache.GetValueOrDefault(sectionName, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        var stackKey = $"section:{sectionName}";
        if (!stack.Add(stackKey))
            return cache.GetValueOrDefault(sectionName, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        try
        {
            var resolved = cache.TryGetValue(sectionName, out var cached)
                ? new Dictionary<string, string>(cached, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var inheritedSection in GetInheritedSectionChain(sectionName))
            {
                if (inheritedSection.Equals(sectionName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var inheritedValues = ResolveSection(inheritedSection, document, theme, cache, stack);
                foreach (var property in inheritedValues)
                    resolved.TryAdd(property.Key, property.Value);
            }

            foreach (var property in source.Keys)
            {
                resolved[property] = ResolveSectionProperty(sectionName, property, document, theme, cache, stack);
            }

            cache[sectionName] = resolved;
            return resolved;
        }
        finally
        {
            stack.Remove(stackKey);
        }
    }

    private static string ResolveSectionProperty(
        string sectionName,
        string propertyName,
        RasiDocument document,
        ThemeSettings theme,
        Dictionary<string, Dictionary<string, string>> cache,
        HashSet<string> stack)
    {
        var stackKey = $"{sectionName}.{propertyName}";
        if (!stack.Add(stackKey))
            return string.Empty;

        try
        {
            if (!document.Sections.TryGetValue(sectionName, out var source) || !source.TryGetValue(propertyName, out var value))
                return GetInheritedThemeProperty(sectionName, propertyName, document, theme, cache, stack);

            return ResolveThemeValue(sectionName, propertyName, value, document, theme, cache, stack);
        }
        finally
        {
            stack.Remove(stackKey);
        }
    }

    private static string ResolveThemeValue(
        string sectionName,
        string propertyName,
        RasiValue value,
        RasiDocument document,
        ThemeSettings theme,
        Dictionary<string, Dictionary<string, string>> cache,
        HashSet<string> stack)
    {
        if (value.Kind == RasiValueKind.Identifier &&
            value.AsString().Equals("inherit", StringComparison.OrdinalIgnoreCase))
        {
            return GetInheritedThemeProperty(sectionName, propertyName, document, theme, cache, stack);
        }

        if (value.Kind == RasiValueKind.Function)
        {
            var functionName = value.FunctionName ?? string.Empty;
            if (functionName.Equals("env", StringComparison.OrdinalIgnoreCase))
            {
                var variableName = value.FunctionArguments?.FirstOrDefault()?.AsString() ?? string.Empty;
                var envValue = string.IsNullOrWhiteSpace(variableName) ? string.Empty : Environment.GetEnvironmentVariable(variableName) ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(envValue))
                    return envValue;

                var fallback = value.FunctionArguments is { Count: > 1 } ? value.FunctionArguments[1] : null;
                return fallback is null ? string.Empty : ResolveThemeValue(sectionName, propertyName, fallback, document, theme, cache, stack);
            }

            if (functionName.Equals("var", StringComparison.OrdinalIgnoreCase))
            {
                var referenceName = value.FunctionArguments?.FirstOrDefault()?.AsString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(referenceName))
                {
                    var resolved = ResolveReferencedVariable(sectionName, referenceName, document, theme, cache, stack);
                    if (!string.IsNullOrWhiteSpace(resolved))
                        return resolved;
                }

                var fallback = value.FunctionArguments is { Count: > 1 } ? value.FunctionArguments[1] : null;
                return fallback is null ? string.Empty : ResolveThemeValue(sectionName, propertyName, fallback, document, theme, cache, stack);
            }
        }

        if (value.Kind == RasiValueKind.List && value.ListValue is not null)
        {
            return string.Join(
                ' ',
                value.ListValue.Select(item => ResolveThemeValue(sectionName, propertyName, item, document, theme, cache, stack))
                    .Where(static item => !string.IsNullOrWhiteSpace(item)));
        }

        return value.AsString();
    }

    private static string ResolveReferencedVariable(
        string sectionName,
        string propertyName,
        RasiDocument document,
        ThemeSettings theme,
        Dictionary<string, Dictionary<string, string>> cache,
        HashSet<string> stack)
    {
        if (document.Sections.TryGetValue(sectionName, out var currentSection) &&
            currentSection.ContainsKey(propertyName))
        {
            return ResolveSectionProperty(sectionName, propertyName, document, theme, cache, stack);
        }

        foreach (var inheritedSection in GetInheritedSectionChain(sectionName))
        {
            if (document.Sections.TryGetValue(inheritedSection, out var inheritedDocumentSection) &&
                inheritedDocumentSection.ContainsKey(propertyName))
            {
                return ResolveSectionProperty(inheritedSection, propertyName, document, theme, cache, stack);
            }

            if (cache.TryGetValue(inheritedSection, out var resolvedSection) &&
                resolvedSection.TryGetValue(propertyName, out var resolvedValue))
            {
                return resolvedValue;
            }

            if (theme.WidgetProperties.TryGetValue(inheritedSection, out var themeSection) &&
                themeSection.TryGetValue(propertyName, out var inheritedThemeValue))
            {
                return inheritedThemeValue;
            }
        }

        return string.Empty;
    }

    private static string GetInheritedThemeProperty(
        string sectionName,
        string propertyName,
        RasiDocument document,
        ThemeSettings theme,
        Dictionary<string, Dictionary<string, string>> cache,
        HashSet<string> stack)
    {
        foreach (var inheritedSection in GetInheritedSectionChain(sectionName))
        {
            if (inheritedSection.Equals(sectionName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (document.Sections.TryGetValue(inheritedSection, out var source) &&
                source.ContainsKey(propertyName))
            {
                return ResolveSectionProperty(inheritedSection, propertyName, document, theme, cache, stack);
            }

            if (cache.TryGetValue(inheritedSection, out var resolvedSection) &&
                resolvedSection.TryGetValue(propertyName, out var resolvedValue))
            {
                return resolvedValue;
            }

            if (theme.WidgetProperties.TryGetValue(inheritedSection, out var existingThemeSection) &&
                existingThemeSection.TryGetValue(propertyName, out var existingThemeValue))
            {
                return existingThemeValue;
            }
        }

        return string.Empty;
    }

    private static IEnumerable<string> GetInheritedSectionChain(string sectionName)
    {
        yield return sectionName;
        var parts = sectionName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var length = parts.Length - 1; length >= 1; length--)
            yield return string.Join(' ', parts.Take(length));

        if (!sectionName.Equals("*", StringComparison.OrdinalIgnoreCase))
            yield return "*";
    }

    private RichScriptDefinition? ParseRichScriptManifest(string manifestPath)
    {
        var data = TomlLite.Parse(File.ReadAllText(manifestPath));
        if (!data.TryGetValue("id", out var idObject) || idObject is not string id || string.IsNullOrWhiteSpace(id))
            return null;

        var manifest = new RichScriptManifest
        {
            Id = id,
            Name = GetTomlString(data, "name", id),
            Kind = GetTomlString(data, "kind", "external"),
            Entry = GetTomlString(data, "entry", string.Empty),
            Arguments = GetTomlOptionalString(data, "arguments"),
            Priority = GetTomlInt(data, "priority", 50),
            KeepAlive = GetTomlBool(data, "keep_alive"),
            RunForQuery = GetTomlBool(data, "run_for_query"),
            Triggers = GetTomlList(data, "triggers"),
            TriggerMode = GetTomlString(data, "trigger_mode", RichScriptManifest.ExactTriggerMode),
            BackgroundEntry = GetTomlOptionalString(data, "background_entry"),
            BackgroundArguments = GetTomlOptionalString(data, "background_arguments"),
            BackgroundRestartOnExit = GetTomlBool(data, "background_restart_on_exit")
        };

        return new RichScriptDefinition(Path.GetDirectoryName(manifestPath) ?? ScriptsDirectory, manifest);
    }

    private void EnsureSeedContent()
    {
        Directory.CreateDirectory(ConfigDirectory);
        Directory.CreateDirectory(ThemesDirectory);
        Directory.CreateDirectory(ScriptsDirectory);

        EnsureDefaultConfig();

        EnsureDefaultTheme();
        EnsureScriptOrderFile();
    }

    private void EnsureDefaultConfig()
    {
        if (!File.Exists(ConfigPath))
        {
            File.WriteAllText(ConfigPath, DefaultConfigText);
            return;
        }

        var existing = File.ReadAllText(ConfigPath);
        if (NormalizeSeedText(existing) == NormalizeSeedText(PriorDefaultConfigText))
            File.WriteAllText(ConfigPath, DefaultConfigText);
    }

    private void EnsureScriptOrderFile()
    {
        if (File.Exists(ScriptOrderPath))
            return;

        File.WriteAllText(
            ScriptOrderPath,
            """
            # One script id per line. Top to bottom = higher priority in Invoke.
            """);
    }

    private void EnsureDefaultTheme()
    {
        var defaultThemePath = Path.Combine(ThemesDirectory, "default.rasi");
        if (!File.Exists(defaultThemePath))
        {
            File.WriteAllText(defaultThemePath, DefaultThemeText);
            return;
        }

        var existing = File.ReadAllText(defaultThemePath);
        if (NormalizeSeedText(existing) == NormalizeSeedText(LegacyDefaultThemeText) ||
            NormalizeSeedText(existing) == NormalizeSeedText(PriorDefaultThemeText) ||
            NormalizeSeedText(existing) == NormalizeSeedText(PreviousDefaultThemeText) ||
            NormalizeSeedText(existing) == NormalizeSeedText(CurrentDefaultThemeText))
            File.WriteAllText(defaultThemePath, DefaultThemeText);
    }

    private static string GetString(IReadOnlyDictionary<string, RasiValue>? section, string name, string fallback = "")
    {
        if (section is null || !section.TryGetValue(name, out var value))
            return fallback;

        return value.AsString(fallback);
    }

    private static string? GetOptionalString(IReadOnlyDictionary<string, RasiValue>? section, string name)
    {
        if (section is null || !section.TryGetValue(name, out var value))
            return null;

        return value.AsString();
    }

    private static bool GetBool(IReadOnlyDictionary<string, RasiValue>? section, string name, bool fallback = false)
    {
        if (section is null || !section.TryGetValue(name, out var value))
            return fallback;

        return value.AsBoolean(fallback);
    }

    private static double GetNumber(IReadOnlyDictionary<string, RasiValue>? section, string name, double fallback = 0)
    {
        if (section is null || !section.TryGetValue(name, out var value))
            return fallback;

        return value.AsNumber(fallback);
    }

    private static double GetDistance(IReadOnlyDictionary<string, RasiValue>? section, string name, double fallback)
    {
        var raw = GetString(section, name, string.Empty);
        if (string.IsNullOrWhiteSpace(raw))
            return fallback;

        var numeric = new string(raw.TakeWhile(static ch => char.IsDigit(ch) || ch is '.' or '-' or '+').ToArray());
        return double.TryParse(numeric, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }

    private static double GetPaddingHorizontal(IReadOnlyDictionary<string, RasiValue>? section, double fallback)
    {
        if (section is null || !section.TryGetValue("padding", out var value))
            return fallback;

        var values = value.Kind == RasiValueKind.List ? value.ListValue?.Select(static item => item.AsNumber()).ToArray() : [value.AsNumber()];
        return values is { Length: > 1 } ? values[1] : values?.FirstOrDefault() ?? fallback;
    }

    private static double GetPaddingVertical(IReadOnlyDictionary<string, RasiValue>? section, double fallback)
    {
        if (section is null || !section.TryGetValue("padding", out var value))
            return fallback;

        var values = value.Kind == RasiValueKind.List ? value.ListValue?.Select(static item => item.AsNumber()).ToArray() : [value.AsNumber()];
        return values?.FirstOrDefault() ?? fallback;
    }

    private static string GetString(
        IReadOnlyDictionary<string, Dictionary<string, string>> sections,
        string sectionName,
        string propertyName,
        string fallback = "")
    {
        if (!sections.TryGetValue(sectionName, out var section) || !section.TryGetValue(propertyName, out var value))
            return fallback;

        return value;
    }

    private static bool GetBool(
        IReadOnlyDictionary<string, Dictionary<string, string>> sections,
        string sectionName,
        string propertyName,
        bool fallback = false)
    {
        var value = GetString(sections, sectionName, propertyName, string.Empty);
        return bool.TryParse(value, out var result) ? result : fallback;
    }

    private static double GetDistance(
        IReadOnlyDictionary<string, Dictionary<string, string>> sections,
        string sectionName,
        string propertyName,
        double fallback)
    {
        var raw = GetString(sections, sectionName, propertyName, string.Empty);
        if (string.IsNullOrWhiteSpace(raw))
            return fallback;

        var numeric = new string(raw.TakeWhile(static ch => char.IsDigit(ch) || ch is '.' or '-' or '+').ToArray());
        return double.TryParse(numeric, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }

    private static double GetPaddingHorizontal(
        IReadOnlyDictionary<string, Dictionary<string, string>> sections,
        string sectionName,
        double fallback)
    {
        var values = ParseBoxValues(GetString(sections, sectionName, "padding", string.Empty));
        return values is { Count: > 1 } ? values[1] : values.FirstOrDefault(fallback);
    }

    private static double GetPaddingVertical(
        IReadOnlyDictionary<string, Dictionary<string, string>> sections,
        string sectionName,
        double fallback)
    {
        var values = ParseBoxValues(GetString(sections, sectionName, "padding", string.Empty));
        return values.FirstOrDefault(fallback);
    }

    private static List<double> ParseBoxValues(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        return raw
            .Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseDistanceToken)
            .Where(static value => value.HasValue)
            .Select(static value => value!.Value)
            .ToList();
    }

    private static double? ParseDistanceToken(string raw)
    {
        var numeric = new string(raw.TakeWhile(static ch => char.IsDigit(ch) || ch is '.' or '-' or '+').ToArray());
        return double.TryParse(numeric, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static string GetTomlString(IReadOnlyDictionary<string, object> data, string key, string fallback) =>
        data.TryGetValue(key, out var value) && value is string text ? text : fallback;

    private static string? GetTomlOptionalString(IReadOnlyDictionary<string, object> data, string key) =>
        data.TryGetValue(key, out var value) && value is string text ? text : null;

    private static int GetTomlInt(IReadOnlyDictionary<string, object> data, string key, int fallback) =>
        data.TryGetValue(key, out var value) switch
        {
            true when value is int intValue => intValue,
            true when value is double doubleValue => (int)doubleValue,
            _ => fallback
        };

    private static bool GetTomlBool(IReadOnlyDictionary<string, object> data, string key) =>
        data.TryGetValue(key, out var value) && value is bool boolValue && boolValue;

    private static List<string> GetTomlList(IReadOnlyDictionary<string, object> data, string key) =>
        data.TryGetValue(key, out var value) && value is string[] list ? [.. list] : [];

    private static void AppendProperty(StringBuilder builder, string name, string value) =>
        builder.Append("  ").Append(name).Append(": \"").Append(value.Replace("\"", "\\\"")).AppendLine("\";");

    private static void AppendProperty(StringBuilder builder, string name, bool value) =>
        builder.Append("  ").Append(name).Append(": ").Append(value ? "true" : "false").AppendLine(";");

    private static void AppendProperty(StringBuilder builder, string name, double value) =>
        builder.Append("  ").Append(name).Append(": ").Append(value.ToString(CultureInfo.InvariantCulture)).AppendLine(";");

    private static void AppendProperty(StringBuilder builder, string name, int value) =>
        builder.Append("  ").Append(name).Append(": ").Append(value.ToString(CultureInfo.InvariantCulture)).AppendLine(";");

    private List<ModeEntry> ParseModes(string value)
    {
        var expressions = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var modes = new List<ModeEntry>();
        foreach (var expression in expressions)
        {
            var parsed = ParseModeExpression(expression);
            if (modes.Any(existing => existing.Id.Equals(parsed.Id, StringComparison.OrdinalIgnoreCase)))
                continue;

            modes.Add(parsed);
        }

        return modes;
    }

    private ModeEntry ParseModeExpression(string expression)
    {
        var id = NormalizeMode(expression);
        return new ModeEntry
        {
            Id = id,
            DisplayName = id,
            RawExpression = expression.Trim()
        };
    }

    private static List<string> ParseModeReferences(string value, IReadOnlyList<ModeEntry> configuredModes) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(mode => ResolveConfiguredModeId(mode, configuredModes))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string ResolveConfiguredModeId(string value, IReadOnlyList<ModeEntry> configuredModes)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return "drun";

        var normalizedBuiltIn = NormalizeMode(trimmed);
        var custom = configuredModes.FirstOrDefault(mode =>
            mode.DisplayName.Equals(trimmed, StringComparison.OrdinalIgnoreCase) ||
            mode.Id.Equals(trimmed, StringComparison.OrdinalIgnoreCase) ||
            mode.RawExpression.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        return custom?.Id ?? normalizedBuiltIn;
    }

    private static string NormalizeMode(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "drun" or "apps" => "drun",
            "run" => "run",
            "files" or "file" => "files",
            "window" or "windows" or "windowcd" => "window",
            "combi" => "combi",
            _ => normalized
        };
    }

    private static string NormalizeLocationAnchor(string location) =>
        location.Trim().ToLowerInvariant() switch
        {
            "center" => "center",
            "south" => "bottomCenter",
            "east" => "rightCenter",
            "west" => "leftCenter",
            _ => "topCenter"
        };

    private static int Clamp(int value, int min, int max) => Math.Min(Math.Max(value, min), max);

    private static Dictionary<string, string> ReadPrefixedStringMap(IReadOnlyDictionary<string, RasiValue> section, string prefix)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in section)
        {
            if (!entry.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var key = entry.Key[prefix.Length..].Trim();
            if (string.IsNullOrWhiteSpace(key))
                continue;

            values[key] = entry.Value.AsString();
        }

        return values;
    }

    private static string ResolveDefaultConfigDirectory()
    {
        if (Environment.GetEnvironmentVariable("INVOKE_PORTABLE") == "1")
            return Path.Combine(AppContext.BaseDirectory, "config");

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Invoke");
    }

    private const string DefaultModesConfigValue =
        "drun,run,files,window,combi";

    private const string PriorDefaultConfigText =
        """
        configuration {
          modes: "drun,run,files,window,combi";
          default-mode: "drun";
          combi-modes: "drun,run,files,window";
          theme: "default";
          launcher-hotkey: "Alt+Space";
          matching: "fuzzy";
          sorting-method: "normal";
          case-sensitive: false;
          cycle: true;
          sidebar-mode: true;
          show-icons: true;
          font: "Segoe UI Variable 15";
          location: "north";
          xoffset: 0;
          yoffset: 0;
          lines: 8;
          columns: 1;
          search-delay: 35;
          loading-delay: 1200;
          close-on-focus-loss: true;
          close-after-action: true;
          clear-query-on-hide: true;
          auto-select: true;
          show-start-page: true;
          display-drun: "drun";
          display-run: "run";
          display-files: "files";
          display-window: "window";
          display-combi: "combi";
          result-title-drun: "{display}";
          result-subtitle-drun: "";
          result-title-files: "{display}";
          result-subtitle-files: "{secondary}";
          kb-accept-entry: "Return";
          kb-cancel: "Escape";
          kb-row-up: "Up";
          kb-row-down: "Down";
          kb-mode-next: "Alt+Right";
          kb-mode-previous: "Alt+Left";
        }
        """;

    private const string DefaultConfigText =
        """
        configuration {
          modes: "drun,run,files,window,combi";
          default-mode: "drun";
          combi-modes: "drun,run,files,window";
          theme: "default";
          matching: "fuzzy";
          sorting-method: "normal";
          case-sensitive: false;
          cycle: true;
          sidebar-mode: true;
          show-icons: true;
          font: "Segoe UI Variable 15";
          location: "north";
          xoffset: 0;
          yoffset: 0;
          lines: 8;
          columns: 1;
          search-delay: 35;
          loading-delay: 1200;
          close-on-focus-loss: true;
          close-after-action: true;
          clear-query-on-hide: true;
          auto-select: true;
          show-start-page: true;
          display-drun: "drun";
          display-run: "run";
          display-files: "files";
          display-window: "window";
          display-combi: "combi";
        }
        """;

    private List<string> LoadScriptOrder()
    {
        if (!File.Exists(ScriptOrderPath))
            return [];

        try
        {
            return File.ReadLines(ScriptOrderPath)
                .Select(static line => line.Split('#', 2)[0].Trim())
                .Where(static line => !string.IsNullOrWhiteSpace(line))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static string NormalizeSeedText(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();

    private const string LegacyDefaultThemeText =
        """
        * {
          background-color: #111318;
          text-color: #F2F5F8;
          accent-color: #7DD3FC;
        }

        window {
          width: 720px;
          background-color: #111318;
          text-color: #F2F5F8;
          border-color: #2A3038;
          border-radius: 8px;
        }

        mainbox {
          spacing: 10px;
        }

        inputbar {
          padding: 14 20;
        }

        prompt {
          enabled: true;
          text: "Search";
          text-color: #9AA6B2;
        }

        listview {
          lines: 8;
          columns: 1;
          spacing: 0;
        }

        element {
          padding: 9 18;
          border-radius: 7px;
          height: 52px;
        }

        element selected {
          background-color: #1B2730;
          border-color: #7DD3FC;
        }

        element-icon {
          size: 30px;
        }

        message {
          enabled: true;
          text-color: #9AA6B2;
        }

        mode-switcher {
          enabled: true;
          show-icons: true;
        }
        """;

    private const string PriorDefaultThemeText =
        """
        * {
          background-color: #0F1722;
          text-color: #F4F7FB;
          accent-color: #6FD0C2;
        }

        window {
          width: 760px;
          background-color: #0F1722;
          text-color: #F4F7FB;
          border-color: #263448;
          border: 1px;
          border-radius: 18px;
          padding: 10;
        }

        mainbox {
          spacing: 10;
          children: [inputbar,listview];
        }

        inputbar {
          padding: 12 16;
          background-color: #141D29;
          border-color: #314154;
          text-color: #F4F7FB;
          border: 1px;
          border-radius: 14px;
          spacing: 10;
          children: [prompt,entry];
        }

        prompt {
          enabled: true;
          text: "Applications:";
          text-color: #A6B4C7;
          font-family: "Segoe UI Variable";
        }

        entry {
          placeholder: "";
          text-color: #F4F7FB;
          font-family: "Segoe UI Variable";
        }

        listview {
          lines: 8;
          columns: 1;
          spacing: 6;
          scrollbar: false;
        }

        element {
          padding: 12 14;
          border: 1px;
          border-color: #263448;
          border-radius: 14px;
          height: 56px;
        }

        element selected {
          background-color: #1A2938;
          border-color: #6FD0C2;
        }

        element-text {
          text-color: #E7ECF5;
          placeholder-color: #8897AA;
          font-family: "Segoe UI Variable";
        }

        element-text selected {
          text-color: #FAFCFF;
          placeholder-color: #B7C4D3;
        }

        element-icon {
          size: 26px;
        }

        message {
          enabled: false;
          padding: 16 4 16 2;
          text-color: #8897AA;
          font-family: "Segoe UI Variable";
        }

        mode-switcher {
          enabled: false;
          padding: 4 0 0 0;
          show-icons: true;
        }
        """;

    private const string PreviousDefaultThemeText =
        """
        * {
          background-color: #0F1722;
          text-color: #F4F7FB;
          accent-color: #6FD0C2;
        }

        window {
          width: 760px;
          background-color: #0F1722;
          text-color: #F4F7FB;
          border-color: #263448;
          border: 1px;
          border-radius: 18px;
          padding: 10;
        }

        mainbox {
          spacing: 10;
          children: [inputbar,listview];
        }

        inputbar {
          padding: 12 16;
          background-color: #141D29;
          border-color: #314154;
          text-color: #F4F7FB;
          border: 1px;
          border-radius: 14px;
          spacing: 10;
          children: [prompt,entry];
        }

        prompt {
          enabled: true;
          text: "Applications:";
          text-color: #A6B4C7;
          font-family: "Segoe UI Variable";
        }

        entry {
          placeholder: "";
          text-color: #F4F7FB;
          font-family: "Segoe UI Variable";
        }

        listview {
          lines: 8;
          columns: 1;
          spacing: 6;
          padding: 0 0 6 0;
          scrollbar: false;
        }

        element {
          padding: 12 14;
          border: 1px;
          border-color: #263448;
          border-radius: 14px;
          height: 56px;
        }

        element selected {
          background-color: #1A2938;
          border-color: #6FD0C2;
        }

        element-text {
          text-color: #E7ECF5;
          placeholder-color: #8897AA;
          font-family: "Segoe UI Variable";
        }

        element-text selected {
          text-color: #FAFCFF;
          placeholder-color: #B7C4D3;
        }

        element-icon {
          size: 26px;
        }

        message {
          enabled: false;
          padding: 16 4 16 2;
          text-color: #8897AA;
          font-family: "Segoe UI Variable";
        }

        mode-switcher {
          enabled: false;
          padding: 4 0 0 0;
          show-icons: true;
        }

        overflow-indicator {
          padding: 8 3 8 3;
          border-radius: 999;
          background-color: #142031;
          border-color: #263448;
          text-color: #F4F7FB;
        }

        overflow-indicator-top {
          enabled: true;
        }

        overflow-indicator-bottom {
          enabled: true;
        }
        """;

    private const string CurrentDefaultThemeText =
        """
        * {
          background-color: #11111B;
          text-color: #E8EAF2;
          accent-color: #6D7394;
        }

        window {
          width: 760px;
          background-color: #11111B;
          text-color: #E8EAF2;
          border-color: #2C3146;
          border: 1px;
          border-radius: 4px;
          padding: 0;
        }

        mainbox {
          spacing: 0;
          children: [inputbar,listview];
        }

        inputbar {
          padding: 8 12;
          background-color: #5E6485;
          border-color: #5E6485;
          text-color: #F5F6FB;
          border: 0;
          border-radius: 0;
          spacing: 8;
          children: [prompt,entry];
        }

        prompt {
          enabled: true;
          text: "Applications:";
          text-color: #F5F6FB;
          font-family: "Cascadia Mono";
        }

        entry {
          placeholder: "";
          text-color: #F5F6FB;
          font-family: "Cascadia Mono";
        }

        listview {
          lines: 8;
          columns: 1;
          spacing: 0;
          padding: 10 0 10 0;
          scrollbar: false;
        }

        element {
          padding: 10 16;
          border: 0;
          border-radius: 0;
          height: 52px;
        }

        element selected {
          background-color: #1A1C29;
          border-color: #6D7394;
        }

        element-text {
          text-color: #C7CBDA;
          placeholder-color: #7E8397;
          font-family: "Cascadia Mono";
        }

        element-text selected {
          text-color: #F5F6FB;
          placeholder-color: #B8BCCB;
        }

        element-icon {
          size: 24px;
        }

        message {
          enabled: false;
          padding: 12 16 12 16;
          text-color: #7E8397;
          font-family: "Cascadia Mono";
        }

        mode-switcher {
          enabled: false;
          padding: 0;
          show-icons: true;
        }

        overflow-indicator {
          padding: 8 16 8 16;
          border-radius: 0;
          background-color: #1A1C29;
          border-color: #6D7394;
          text-color: #F5F6FB;
        }

        overflow-indicator-top {
          enabled: true;
        }

        overflow-indicator-bottom {
          enabled: true;
        }
        """;

    private const string DefaultThemeText =
        """
        * {
          background-color: #10131B;
          text-color: #D8DCE7;
          accent-color: #B8CDF4;
          result-layout: "oneLine";
          show-subtitles: false;
          show-selection-accent: false;
          separator-thickness: 0;
          animate-results: true;
          results-animation-duration: 140;
          results-animation-offset-y: 8;
          results-animation-stagger: 16;
          animate-selection: true;
          selection-animation-duration: 125;
          selection-animation-offset-x: 6;
        }

        window {
          width: 420px;
          background-color: #10131B;
          text-color: #D8DCE7;
          border-color: #4A4F67;
          border: 1px;
          border-radius: 3px;
          padding: 16;
        }

        mainbox {
          spacing: 4;
          children: [inputbar,listview];
        }

        inputbar {
          padding: 10 12;
          background-color: transparent;
          border-color: transparent;
          text-color: #D8DCE7;
          border: 0;
          border-radius: 0;
          spacing: 8;
          children: [prompt,entry];
        }

        prompt {
          enabled: true;
          text: "Applications:";
          text-color: #F3F5FB;
          font-family: "Consolas";
          font-size: 14px;
          font-weight: 600;
        }

        entry {
          placeholder: "Type to filter";
          text-color: #D8DCE7;
          placeholder-color: #787F93;
          font-family: "Consolas";
          font-size: 14px;
          font-weight: 400;
        }

        listview {
          lines: 8;
          columns: 1;
          spacing: 0;
          padding: 0 0 8 0;
          scrollbar: false;
        }

        element {
          background-color: transparent;
          padding: 7 12;
          border: 0;
          border-radius: 0;
          height: 42px;
        }

        element selected {
          background-color: #B7CCF4;
          border-color: #B7CCF4;
        }

        element-text {
          text-color: #D8DCE7;
          placeholder-color: #787F93;
          font-family: "Consolas";
          font-size: 14px;
          font-weight: 400;
          margin-left: 0;
          margin-right: 10;
        }

        element-text selected {
          text-color: #10131B;
          placeholder-color: #10131B;
        }

        element-icon {
          size: 22px;
          column-width: 38px;
          container-size: 32px;
          fallback-size: 22px;
        }

        message {
          enabled: false;
          padding: 12 16 12 16;
          text-color: #787F93;
          font-family: "Consolas";
        }

        mode-switcher {
          enabled: false;
          padding: 0;
          show-icons: true;
        }

        overflow-indicator {
          padding: 8 16 8 16;
          border-radius: 0;
          background-color: #171B27;
          border-color: #4A4F67;
          text-color: #F3F5FB;
        }

        overflow-indicator-top {
          enabled: true;
        }

        overflow-indicator-bottom {
          enabled: true;
        }
        """;
}
