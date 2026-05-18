using System.Globalization;

namespace Invoke.Core.Plugins.Rich;

internal static class TomlLite
{
    public static Dictionary<string, object> Parse(string text)
    {
        var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var commentIndex = line.IndexOf('#');
            if (commentIndex >= 0)
                line = line[..commentIndex].Trim();
            if (line.Length == 0)
                continue;

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
                continue;

            var key = line[..separatorIndex].Trim();
            var rawValue = line[(separatorIndex + 1)..].Trim();
            values[key] = ParseValue(rawValue);
        }

        return values;
    }

    private static object ParseValue(string rawValue)
    {
        if (rawValue.StartsWith('[') && rawValue.EndsWith(']'))
        {
            var content = rawValue[1..^1];
            return content.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(Unquote)
                .ToArray();
        }

        if (rawValue.StartsWith('"') && rawValue.EndsWith('"'))
            return Unquote(rawValue);

        if (bool.TryParse(rawValue, out var boolValue))
            return boolValue;

        if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
            return intValue;

        if (double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
            return doubleValue;

        return Unquote(rawValue);
    }

    private static string Unquote(string value) => value.Trim().Trim('"');
}
