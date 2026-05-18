using System.Text.RegularExpressions;

namespace Invoke.Core.Services;

public static class ModeEntryFormatter
{
    private static readonly Regex PlaceholderPattern = new(@"\{(?<name>[a-zA-Z0-9_-]+)\}", RegexOptions.Compiled);

    public static string Format(
        string? template,
        string fallback,
        string text,
        string displayText,
        string? secondaryText,
        string? meta,
        string? info,
        string? completionText,
        string? icon,
        string kind,
        string? modeId)
    {
        if (template is null)
            return fallback;

        if (template.Length == 0)
            return string.Empty;

        return PlaceholderPattern.Replace(template, match =>
        {
            var name = match.Groups["name"].Value;
            return name.ToLowerInvariant() switch
            {
                "text" => text,
                "display" or "title" => displayText,
                "secondary" or "subtitle" => secondaryText ?? string.Empty,
                "meta" => meta ?? string.Empty,
                "info" => info ?? string.Empty,
                "completion" => completionText ?? string.Empty,
                "icon" => icon ?? string.Empty,
                "kind" => kind,
                "mode" => modeId ?? string.Empty,
                _ => match.Value
            };
        });
    }
}
