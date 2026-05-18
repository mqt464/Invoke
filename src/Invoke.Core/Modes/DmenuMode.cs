using Invoke.Core.Actions;
using Invoke.Core.Dmenu;
using Invoke.Core.Results;

namespace Invoke.Core.Modes;

public sealed class DmenuMode : ILauncherInteractiveMode
{
    private readonly DmenuSession _session;
    private readonly Func<string, Task> _selectionWriter;

    public DmenuMode(DmenuSession session, Func<string, Task> selectionWriter)
    {
        _session = session;
        _selectionWriter = selectionWriter;
    }

    public string Id => "dmenu";
    public string DisplayName => "dmenu";
    public int Priority => 100;

    public ValueTask InitializeAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public Task<LauncherModeSnapshot> SearchAsync(LauncherQueryContext context, CancellationToken cancellationToken)
    {
        var filtered = DmenuSelector.FilterIndexed(_session.Entries, context.Terms, !_session.CaseInsensitive ? context.Settings.CaseSensitive : false)
            .Take(context.MaxResults)
            .Select(entry => (Text: entry.Entry, Index: entry.Index))
            .ToArray();
        var entries = filtered
            .Select(entry => new LauncherModeEntry(
                entry.Text,
                entry.Text,
                string.Empty,
                ResultKind.Command,
                600,
                new InvokeAction($"dmenu:{entry.Index}:{entry.Text}", "Select", _ => _selectionWriter(FormatOutput(entry.Text, entry.Index, context.Terms))),
                null,
                entry.Text,
                entry.Index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                entry.Index.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .ToArray();

        var urgent = filtered
            .Select((entry, filteredIndex) => (_session.UrgentRows.Contains(entry.Index), filteredIndex))
            .Where(static item => item.Item1)
            .Select(static item => item.filteredIndex)
            .ToHashSet();
        var active = filtered
            .Select((entry, filteredIndex) => (_session.ActiveRows.Contains(entry.Index), filteredIndex))
            .Where(static item => item.Item1)
            .Select(static item => item.filteredIndex)
            .ToHashSet();
        var newSelection = ResolveNewSelection(filtered, context.Terms);

        return Task.FromResult(new LauncherModeSnapshot(
            Id,
            _session.Prompt,
            _session.Message,
            entries,
            urgent,
            active,
            NewSelection: newSelection,
            NoCustom: _session.NoCustom,
            MarkupRows: _session.MarkupRows));
    }

    public async Task<LauncherModeInteractionResult> ActivateEntryAsync(LauncherQueryContext context, LauncherModeEntry entry, CancellationToken cancellationToken)
    {
        var index = int.TryParse(entry.Info, out var parsedIndex) ? parsedIndex : _session.Entries.IndexOf(entry.Text);
        await _selectionWriter(FormatOutput(entry.Text, index, context.Terms)).ConfigureAwait(false);
        return new LauncherModeInteractionResult(CloseLauncher: true);
    }

    public async Task<LauncherModeInteractionResult> SubmitCustomAsync(LauncherQueryContext context, string input, CancellationToken cancellationToken)
    {
        if (_session.NoCustom)
            return new LauncherModeInteractionResult();

        await _selectionWriter(FormatOutput(input, null, context.Terms)).ConfigureAwait(false);
        return new LauncherModeInteractionResult(CloseLauncher: true);
    }

    public Task<LauncherModeInteractionResult> DeleteEntryAsync(LauncherQueryContext context, LauncherModeEntry entry, CancellationToken cancellationToken) =>
        Task.FromResult(new LauncherModeInteractionResult());

    public Task<LauncherModeInteractionResult> HandleCustomKeyAsync(LauncherQueryContext context, LauncherModeEntry? entry, int customKeyIndex, CancellationToken cancellationToken) =>
        Task.FromResult(new LauncherModeInteractionResult());

    public void Dispose()
    {
    }

    private int? ResolveNewSelection(IReadOnlyList<(string Text, int Index)> filtered, string query)
    {
        if (_session.SelectedRow is { } selectedRow)
        {
            var selectedIndex = Array.FindIndex(filtered.ToArray(), item => item.Index == selectedRow);
            if (selectedIndex >= 0)
                return selectedIndex;
        }

        if (!string.IsNullOrWhiteSpace(_session.PreselectedText))
        {
            var selectedIndex = Array.FindIndex(filtered.ToArray(), item => item.Text.Equals(_session.PreselectedText, _session.CaseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
            if (selectedIndex >= 0)
                return selectedIndex;
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var selectedIndex = Array.FindIndex(filtered.ToArray(), item => item.Text.Equals(query, _session.CaseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
            if (selectedIndex >= 0)
                return selectedIndex;
        }

        return null;
    }

    private string FormatOutput(string value, int? index, string filter)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var token in _session.Format)
        {
            switch (token)
            {
                case 's':
                    builder.Append(value);
                    break;
                case 'p':
                    builder.Append(StripMarkup(value));
                    break;
                case 'i':
                    if (index.HasValue)
                        builder.Append(index.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case 'd':
                    if (index.HasValue)
                        builder.Append((index.Value + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case 'q':
                    builder.Append('"').Append(value.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');
                    break;
                case 'f':
                    builder.Append(filter);
                    break;
            }
        }

        return builder.Length == 0 ? value : builder.ToString();
    }

    private static string StripMarkup(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length);
        var insideTag = false;
        foreach (var character in value)
        {
            if (character == '<')
            {
                insideTag = true;
                continue;
            }

            if (character == '>')
            {
                insideTag = false;
                continue;
            }

            if (!insideTag)
                builder.Append(character);
        }

        return System.Net.WebUtility.HtmlDecode(builder.ToString());
    }
}
