using Invoke.Core.Config;

namespace Invoke.Core.Modes;

public sealed class LauncherModeRegistry : IDisposable
{
    private readonly Dictionary<string, LauncherModeDefinition> _modes;
    private readonly InvokeSettings _settings;
    private readonly ThemeSettings _theme;
    private bool _initialized;

    public LauncherModeRegistry(IEnumerable<LauncherModeDefinition> modes, InvokeSettings settings, ThemeSettings theme)
    {
        _modes = modes.ToDictionary(mode => mode.Id, StringComparer.OrdinalIgnoreCase);
        _settings = settings;
        _theme = theme;
    }

    public IReadOnlyList<LauncherModeDefinition> OrderedModes =>
        ExpandConfiguredModes().ToArray();

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
            return;

        foreach (var mode in _modes.Values)
            await mode.Mode.InitializeAsync(cancellationToken).ConfigureAwait(false);

        _initialized = true;
    }

    public async Task<LauncherSearchResult> SearchAsync(string rawQuery, string? activeMode, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        var normalizedMode = ResolveMode(activeMode);
        var terms = ExtractTerms(rawQuery);
        if (normalizedMode.Equals("combi", StringComparison.OrdinalIgnoreCase))
            return await SearchCombiAsync(rawQuery, terms, cancellationToken).ConfigureAwait(false);

        var definition = _modes[normalizedMode];
        var snapshot = await SearchModeAsync(definition.Mode, rawQuery, terms, normalizedMode, cancellationToken).ConfigureAwait(false);

        var entries = new List<LauncherModeEntry>(snapshot.Entries);
        var displayPrefix = snapshot.DisplayPrefix;
        var rawPrefix = snapshot.RawPrefix;
        foreach (var supplemental in _modes.Values.Where(mode => mode.AlwaysEvaluate && !mode.Id.Equals(normalizedMode, StringComparison.OrdinalIgnoreCase)))
        {
            var supplementalSnapshot = await SearchModeAsync(supplemental.Mode, rawQuery, terms, normalizedMode, cancellationToken).ConfigureAwait(false);
            entries.AddRange(supplementalSnapshot.Entries);
            displayPrefix ??= supplementalSnapshot.DisplayPrefix;
            rawPrefix ??= supplementalSnapshot.RawPrefix;
        }

        return new LauncherSearchResult(
            normalizedMode,
            snapshot.Prompt ?? definition.DisplayName,
            snapshot.Message,
            entries.OrderByDescending(static entry => entry.Score).Take(_settings.MaxResults).ToArray(),
            OrderedModes,
            snapshot.UrgentIndices,
            snapshot.ActiveIndices,
            snapshot.SwitchMode,
            snapshot.ThemeOverlay,
            snapshot.KeepSelection,
            snapshot.KeepFilter,
            snapshot.NewSelection,
            snapshot.UseHotKeys,
            snapshot.NoCustom,
            snapshot.MarkupRows,
            displayPrefix,
            rawPrefix);
    }

    private async Task<LauncherSearchResult> SearchCombiAsync(string rawQuery, string terms, CancellationToken cancellationToken)
    {
        var combiEntries = new List<LauncherModeEntry>();
        string? message = null;
        string? displayPrefix = null;
        string? rawPrefix = null;
        var includedModes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var modeId in _settings.CombiModes)
        {
            if (!_modes.TryGetValue(modeId, out var definition))
                continue;

            includedModes.Add(modeId);
            var snapshot = await SearchModeAsync(definition.Mode, rawQuery, terms, modeId, cancellationToken).ConfigureAwait(false);

            message ??= snapshot.Message;
            displayPrefix ??= snapshot.DisplayPrefix;
            rawPrefix ??= snapshot.RawPrefix;
            combiEntries.AddRange(snapshot.Entries
                .Take(_settings.MaxResultsPerMode.GetValueOrDefault(modeId, _settings.MaxResults))
                .Select(entry => _settings.CombiScoreBoosts.TryGetValue(modeId, out var boost)
                    ? entry with { Score = entry.Score + boost }
                    : entry));
        }

        foreach (var supplemental in _modes.Values.Where(mode => mode.AlwaysEvaluate && !includedModes.Contains(mode.Id)))
        {
            var snapshot = await SearchModeAsync(supplemental.Mode, rawQuery, terms, "combi", cancellationToken).ConfigureAwait(false);

            message ??= snapshot.Message;
            displayPrefix ??= snapshot.DisplayPrefix;
            rawPrefix ??= snapshot.RawPrefix;
            combiEntries.AddRange(snapshot.Entries);
        }

        return new LauncherSearchResult(
            "combi",
            _settings.DisplayNames.GetValueOrDefault("combi", "combi"),
            message,
            combiEntries
                .OrderByDescending(static entry => entry.Score)
                .ThenBy(static entry => entry.DisplayText, StringComparer.CurrentCultureIgnoreCase)
                .Take(_settings.MaxResults)
                .ToArray(),
            OrderedModes,
            null,
            null,
            null,
            null,
            false,
            false,
            null,
            false,
            false,
            false,
            displayPrefix,
            rawPrefix);
    }

    public string ResolveMode(string? activeMode)
    {
        if (!string.IsNullOrWhiteSpace(activeMode))
        {
            if (_modes.ContainsKey(activeMode))
                return activeMode;
        }

        return _settings.DefaultMode;
    }

    public ILauncherMode? GetMode(string? modeId)
    {
        if (string.IsNullOrWhiteSpace(modeId))
            return null;

        return _modes.TryGetValue(modeId, out var definition) ? definition.Mode : null;
    }

    public string? GetNextMode(string currentMode)
        => GetRelativeMode(currentMode, 1);

    public string? GetPreviousMode(string currentMode)
        => GetRelativeMode(currentMode, -1);

    private async Task<LauncherModeSnapshot> SearchModeAsync(
        ILauncherMode mode,
        string rawQuery,
        string terms,
        string activeMode,
        CancellationToken cancellationToken) =>
        await mode.SearchAsync(
            new LauncherQueryContext(rawQuery, terms, _settings, _theme, activeMode, _settings.MaxResults),
            cancellationToken).ConfigureAwait(false);

    private string? GetRelativeMode(string currentMode, int offset)
    {
        var modes = OrderedModes;
        if (modes.Count == 0)
            return null;

        var index = FindModeIndex(modes, currentMode);
        if (index < 0)
            return modes.FirstOrDefault()?.Id;

        var nextIndex = (index + offset + modes.Count) % modes.Count;
        return modes[nextIndex].Id;
    }

    private static string ExtractTerms(string rawQuery)
    {
        var trimmed = rawQuery.Trim();
        if (!trimmed.StartsWith('/'))
            return trimmed;

        var split = trimmed.IndexOf(' ');
        return split < 0 ? string.Empty : trimmed[(split + 1)..].Trim();
    }

    public void Dispose()
    {
        foreach (var mode in _modes.Values)
            mode.Mode.Dispose();
    }

    private static int FindModeIndex(IReadOnlyList<LauncherModeDefinition> modes, string currentMode)
    {
        for (var index = 0; index < modes.Count; index++)
        {
            if (modes[index].Id.Equals(currentMode, StringComparison.OrdinalIgnoreCase))
                return index;
        }

        return -1;
    }

    private IEnumerable<LauncherModeDefinition> ExpandConfiguredModes()
    {
        var expanded = new List<LauncherModeDefinition>();
        for (var index = 0; index < _settings.Modes.Count; index++)
        {
            var id = _settings.Modes[index];
            if (_modes.TryGetValue(id, out var definition))
                expanded.Add(definition with { Order = index });
        }

        return expanded
            .OrderBy(static definition => definition.Order)
            .ThenBy(static definition => definition.DisplayName, StringComparer.OrdinalIgnoreCase);
    }
}
