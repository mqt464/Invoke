using Invoke.Core.Config;
using System.Globalization;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace Invoke.App;

public sealed class AppTheme
{
    public AppTheme(ThemeSettings settings)
    {
        Settings = settings;
        Background = BrushResource("background", settings.Background, "#111318");
        Foreground = BrushResource("foreground", settings.Foreground, "#F2F5F8", settings.ForegroundOpacity);
        MutedForeground = BrushResource("mutedForeground", settings.MutedForeground, "#9AA6B2", settings.MutedForegroundOpacity);
        Accent = BrushResource("accent", settings.Accent, "#7DD3FC");
        SelectedBackground = BrushResource("selectedBackground", settings.SelectedBackground, "#1B2730");
        Border = BrushResource("border", settings.Border, "#2A3038", settings.BorderOpacity);
        SearchBackground = BrushResource("searchBackground", settings.Background, settings.SearchBackgroundOpacity);
        SearchBorder = BrushResource("searchBorder", settings.Foreground, settings.SearchBorderOpacity);
        Separator = BrushResource("separator", settings.Foreground, settings.SeparatorOpacity);
        HoverBackground = BrushResource("hoverBackground", settings.Foreground, 0.055);
        HoverBorder = BrushResource("hoverBorder", settings.Foreground, 0.08);
        SelectedBorder = BrushResource("selectedBorder", settings.Accent, 0.46);
        BadgeBackground = BrushResource("badgeBackground", settings.Foreground, 0.07);
        BadgeBorder = BrushResource("badgeBorder", settings.Foreground, 0.08);
        IconBackground = BrushResource("iconBackground", settings.Accent, settings.IconBackgroundOpacity);
        IconBorder = BrushResource("iconBorder", settings.Accent, settings.IconBorderOpacity);
        CustomResources = settings.Resources.ToDictionary(
            resource => resource.Key,
            resource => ToResourceValue(resource.Value),
            StringComparer.OrdinalIgnoreCase);
    }

    public ThemeSettings Settings { get; }
    public MediaBrush Background { get; }
    public MediaBrush Foreground { get; }
    public MediaBrush MutedForeground { get; }
    public MediaBrush Accent { get; }
    public MediaBrush SelectedBackground { get; }
    public MediaBrush Border { get; }
    public MediaBrush SearchBackground { get; }
    public MediaBrush SearchBorder { get; }
    public MediaBrush Separator { get; }
    public MediaBrush HoverBackground { get; }
    public MediaBrush HoverBorder { get; }
    public MediaBrush SelectedBorder { get; }
    public MediaBrush BadgeBackground { get; }
    public MediaBrush BadgeBorder { get; }
    public MediaBrush IconBackground { get; }
    public MediaBrush IconBorder { get; }
    public IReadOnlyDictionary<string, object> CustomResources { get; }

    private MediaBrush BrushResource(string key, string preferredHex, string fallbackHex) =>
        Brush(GetResource(key) ?? preferredHex, fallbackHex);

    private MediaBrush BrushResource(string key, string preferredHex, string fallbackHex, double opacity) =>
        GetResource(key) is { } hex
            ? Brush(hex, fallbackHex)
            : BrushWithAlpha(preferredHex, opacity);

    private MediaBrush BrushResource(string key, string preferredHex, double fallbackOpacity) =>
        GetResource(key) is { } hex
            ? Brush(hex, preferredHex)
            : BrushWithAlpha(preferredHex, fallbackOpacity);

    private string? GetResource(string key) =>
        Settings.Resources.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

    private static MediaBrush Brush(string hex, string fallbackHex)
    {
        MediaColor color;
        try
        {
            color = (MediaColor)MediaColorConverter.ConvertFromString(hex);
        }
        catch
        {
            color = (MediaColor)MediaColorConverter.ConvertFromString(fallbackHex);
        }

        var brush = new MediaSolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static MediaBrush BrushWithAlpha(string hex, double opacity)
    {
        MediaColor color;
        try
        {
            color = (MediaColor)MediaColorConverter.ConvertFromString(hex);
        }
        catch
        {
            color = (MediaColor)MediaColorConverter.ConvertFromString("#000000");
        }

        color.A = (byte)Math.Round(Math.Clamp(opacity, 0, 1) * 255);
        var brush = new MediaSolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static object ToResourceValue(string value)
    {
        if (TryBrush(value, out var brush))
            return brush;

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? number
            : value;
    }

    private static bool TryBrush(string value, out MediaBrush brush)
    {
        try
        {
            var color = (MediaColor)MediaColorConverter.ConvertFromString(value);
            brush = new MediaSolidColorBrush(color);
            brush.Freeze();
            return true;
        }
        catch
        {
            brush = null!;
            return false;
        }
    }
}
