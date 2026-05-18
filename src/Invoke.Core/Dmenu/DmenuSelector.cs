using Invoke.Core.Services;

namespace Invoke.Core.Dmenu;

public static class DmenuSelector
{
    public static IReadOnlyList<(string Entry, int Index)> FilterIndexed(IReadOnlyList<string> entries, string query, bool caseSensitive)
    {
        if (string.IsNullOrWhiteSpace(query))
            return entries.Select((entry, index) => (entry, index)).ToArray();

        return entries
            .Select((entry, index) => (entry, index, score: Score(entry, query, caseSensitive)))
            .Where(static item => item.score >= 0)
            .OrderByDescending(static item => item.score)
            .ThenBy(static item => item.entry, StringComparer.OrdinalIgnoreCase)
            .Select(static item => (item.entry, item.index))
            .ToArray();
    }

    public static IReadOnlyList<string> Filter(IReadOnlyList<string> entries, string query, bool caseSensitive)
    {
        return FilterIndexed(entries, query, caseSensitive)
            .Select(static item => item.Entry)
            .ToArray();
    }

    public static string? SelectBest(IReadOnlyList<string> entries, string query, bool caseSensitive)
    {
        var filtered = Filter(entries, query, caseSensitive);
        return filtered.FirstOrDefault();
    }

    private static double Score(string entry, string query, bool caseSensitive)
    {
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        if (entry.Equals(query, comparison))
            return 1_000;

        if (entry.StartsWith(query, comparison))
            return 900;

        if (entry.Contains(query, comparison))
            return 800;

        return Scoring.Match(entry, query, 700);
    }
}
