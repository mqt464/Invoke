using Invoke.Core.Config;
using Invoke.Core.Modes;

namespace Invoke.Core.Services;

public static class Scoring
{
    public static double Match(string candidate, string query, double baseScore) =>
        Match(candidate, query, baseScore, matching: "fuzzy", caseSensitive: false, sortingMethod: "normal");

    public static double Match(string candidate, string query, double baseScore, InvokeSettings settings) =>
        Match(candidate, query, baseScore, settings.Matching, settings.CaseSensitive, settings.SortingMethod);

    public static double Match(string candidate, string query, double baseScore, LauncherQueryContext context) =>
        Match(candidate, query, baseScore, context.Settings);

    public static double Match(
        string candidate,
        string query,
        double baseScore,
        string matching,
        bool caseSensitive,
        string sortingMethod)
    {
        candidate ??= string.Empty;
        query ??= string.Empty;
        if (string.IsNullOrWhiteSpace(query))
            return baseScore * 0.2;

        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var exact = candidate.Equals(query, comparison);
        var startsWith = candidate.StartsWith(query, comparison);
        var contains = candidate.Contains(query, comparison);
        var normalizedMatching = string.IsNullOrWhiteSpace(matching) ? "fuzzy" : matching.Trim().ToLowerInvariant();
        var normalizedSorting = string.IsNullOrWhiteSpace(sortingMethod) ? "normal" : sortingMethod.Trim().ToLowerInvariant();

        return normalizedMatching switch
        {
            "prefix" => startsWith ? baseScore + 80 + RankBonus(candidate, query, caseSensitive, normalizedSorting) : -1,
            "contains" or "substring" => contains ? baseScore + 45 + RankBonus(candidate, query, caseSensitive, normalizedSorting) : -1,
            "normal" => ScoreNormal(candidate, query, baseScore, caseSensitive, normalizedSorting, exact, startsWith, contains),
            _ => ScoreFuzzy(candidate, query, baseScore, caseSensitive, normalizedSorting, exact, startsWith, contains)
        };
    }

    private static double ScoreNormal(
        string candidate,
        string query,
        double baseScore,
        bool caseSensitive,
        string sortingMethod,
        bool exact,
        bool startsWith,
        bool contains)
    {
        if (exact)
            return baseScore + 120 + RankBonus(candidate, query, caseSensitive, sortingMethod);

        if (startsWith)
            return baseScore + 80 + RankBonus(candidate, query, caseSensitive, sortingMethod);

        if (contains)
            return baseScore + 45 + RankBonus(candidate, query, caseSensitive, sortingMethod);

        return -1;
    }

    private static double ScoreFuzzy(
        string candidate,
        string query,
        double baseScore,
        bool caseSensitive,
        string sortingMethod,
        bool exact,
        bool startsWith,
        bool contains)
    {
        if (exact)
            return baseScore + 120 + RankBonus(candidate, query, caseSensitive, sortingMethod);

        if (startsWith)
            return baseScore + 80 + RankBonus(candidate, query, caseSensitive, sortingMethod);

        if (contains)
            return baseScore + 45 + RankBonus(candidate, query, caseSensitive, sortingMethod);

        return TryFuzzyScore(candidate, query, caseSensitive, out var fuzzyScore)
            ? baseScore + 12 + fuzzyScore
            : -1;
    }

    private static double RankBonus(string candidate, string query, bool caseSensitive, string sortingMethod) =>
        sortingMethod switch
        {
            "fzf" => TryFuzzyScore(candidate, query, caseSensitive, out var fuzzyScore) ? fuzzyScore : 0,
            "alphabetical" => -candidate.Length * 0.05,
            _ => startsNearFront(candidate, query, caseSensitive)
        };

    private static double startsNearFront(string candidate, string query, bool caseSensitive)
    {
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var index = candidate.IndexOf(query, comparison);
        if (index < 0)
            return 0;

        return Math.Max(0, 18 - (index * 2)) - Math.Min(candidate.Length, 40) * 0.03;
    }

    private static bool TryFuzzyScore(string candidate, string query, bool caseSensitive, out double score)
    {
        score = 0;
        if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(query))
            return false;

        var candidateSpan = caseSensitive ? candidate.AsSpan() : candidate.ToUpperInvariant().AsSpan();
        var querySpan = caseSensitive ? query.AsSpan() : query.ToUpperInvariant().AsSpan();
        var matchPositions = new int[querySpan.Length];
        var queryIndex = 0;
        for (var candidateIndex = 0; candidateIndex < candidateSpan.Length && queryIndex < querySpan.Length; candidateIndex++)
        {
            if (candidateSpan[candidateIndex] != querySpan[queryIndex])
                continue;

            matchPositions[queryIndex] = candidateIndex;
            queryIndex++;
        }

        if (queryIndex != querySpan.Length)
            return false;

        var contiguousBonus = 0d;
        var boundaryBonus = 0d;
        for (var index = 0; index < matchPositions.Length; index++)
        {
            var position = matchPositions[index];
            if (index > 0 && position == matchPositions[index - 1] + 1)
                contiguousBonus += 8;

            if (position == 0 || IsBoundary(candidate[position - 1]))
                boundaryBonus += 6;
        }

        var startBonus = Math.Max(0, 24 - (matchPositions[0] * 2.5));
        var compactnessPenalty = (matchPositions[^1] - matchPositions[0]) - querySpan.Length;
        var lengthPenalty = Math.Max(0, candidate.Length - query.Length) * 0.18;
        score = startBonus + contiguousBonus + boundaryBonus - compactnessPenalty - lengthPenalty;
        return true;
    }

    private static bool IsBoundary(char value) =>
        value is ' ' or '-' or '_' or '\\' or '/' or '.' or '(' or '[';
}
