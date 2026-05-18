using Invoke.Core.Config;
using Invoke.Core.Results;

namespace Invoke.Core.Services;

public static class RecentLaunchScoring
{
    public static string BuildKey(ResultKind kind, string title, string subtitle, string actionTitle) =>
        $"{kind}|{title}|{subtitle}|{actionTitle}";

    public static double GetBoost(HistorySettings history, ResultKind kind, string title, string subtitle, string actionTitle)
    {
        if (!history.EnableRecentBoost || history.ScoreBoost <= 0 || history.RecentLaunches.Count == 0)
            return 0;

        var key = BuildKey(kind, title, subtitle, actionTitle);
        var recent = history.RecentLaunches.FirstOrDefault(entry => entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (recent is null)
            return 0;

        var frequencyBoost = Math.Min(180, recent.LaunchCount * 18d);
        var recencyBoost = Math.Max(0, 30 - (DateTime.UtcNow - recent.LastUsedUtc).TotalDays);
        return history.ScoreBoost + frequencyBoost + recencyBoost;
    }
}
