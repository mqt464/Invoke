using System.Text.Json.Serialization;

namespace Invoke.Core.Config;

public sealed class ThemeSettings
{
    [JsonPropertyName("$schema")]
    public string Schema { get; set; } = "./theme.rasi";

    [JsonIgnore]
    public string BaseDirectory { get; set; } = string.Empty;

    public string Name { get; set; } = "default";
    public string FontFamily { get; set; } = "Segoe UI Variable";
    public double FontSize { get; set; } = 15;
    public double SearchFontSize { get; set; } = 21;
    public double ResultTitleFontSize { get; set; } = 14.5;
    public double ResultSubtitleFontSize { get; set; } = 12.5;
    public string ResultTitleFontWeight { get; set; } = "SemiBold";
    public string ResultSubtitleFontWeight { get; set; } = "Normal";
    public double WindowWidth { get; set; } = 760;
    public double WindowOpacity { get; set; } = 1;
    public double CornerRadius { get; set; } = 18;
    public double ResultCornerRadius { get; set; } = 14;
    public string ResultLayout { get; set; } = "twoLine";
    public bool ShowPrompt { get; set; } = true;
    public bool PromptPersistent { get; set; }
    public double PromptOpacity { get; set; } = 1;
    public string PromptText { get; set; } = "Search";
    public bool ShowStatusPanel { get; set; }
    public bool ShowIcons { get; set; } = true;
    public bool ShowSubtitles { get; set; } = true;
    public bool ShowSelectionAccent { get; set; } = true;
    public bool ShowItemBorders { get; set; } = true;
    public bool ShowModeSwitcher { get; set; } = true;
    public string MessageText { get; set; } = string.Empty;
    public double RowHeight { get; set; } = 56;
    public double ResultGap { get; set; } = 6;
    public double Spacing { get; set; } = 12;
    public double SearchHorizontalPadding { get; set; } = 18;
    public double SearchVerticalPadding { get; set; } = 14;
    public double ResultHorizontalPadding { get; set; } = 16;
    public double ResultVerticalPadding { get; set; } = 10;
    public double ResultTextLeftMargin { get; set; } = 2;
    public double ResultTextRightMargin { get; set; } = 16;
    public double ResultSubtitleTopMargin { get; set; } = 3;
    public double IconColumnWidth { get; set; } = 48;
    public double IconContainerSize { get; set; } = 38;
    public double IconSize { get; set; } = 26;
    public double FallbackIconSize { get; set; } = 26;
    public double FallbackIconCornerRadius { get; set; } = 10;
    public double FallbackIconOpacity { get; set; } = 0.86;
    public double OuterShadowMargin { get; set; }
    public double OuterShadowOpacity { get; set; }
    public double OuterShadowBlurRadius { get; set; }
    public double OuterShadowDepth { get; set; }
    public double OuterShadowEffectOpacity { get; set; }
    public double SurfaceBorderThickness { get; set; } = 1;
    public double SeparatorThickness { get; set; } = 1;
    public double SurfaceShadowBlurRadius { get; set; }
    public double SurfaceShadowDepth { get; set; }
    public double SurfaceShadowOpacity { get; set; }
    public double WindowTopOffsetMin { get; set; } = 48;
    public double WindowTopOffsetRatio { get; set; } = 0.18;
    public bool AnimateLauncherOpen { get; set; }
    public bool AnimateLauncherClose { get; set; }
    public double LauncherOpenDurationMilliseconds { get; set; } = 150;
    public double LauncherCloseDurationMilliseconds { get; set; } = 110;
    public double LauncherOpenOffsetY { get; set; } = 12;
    public double LauncherCloseOffsetY { get; set; } = 6;
    public double LauncherOpenScale { get; set; } = 0.985;
    public double LauncherCloseScale { get; set; } = 0.992;
    public string LauncherOpenEasing { get; set; } = "CubicOut";
    public string LauncherCloseEasing { get; set; } = "QuadraticOut";
    public bool AnimateResults { get; set; }
    public double ResultsAnimationDurationMilliseconds { get; set; } = 120;
    public double ResultsAnimationOffsetY { get; set; } = 10;
    public double ResultsAnimationStaggerMilliseconds { get; set; } = 18;
    public string ResultsAnimationEasing { get; set; } = "CubicOut";
    public bool AnimateSelection { get; set; }
    public double SelectionAnimationDurationMilliseconds { get; set; } = 110;
    public double SelectionAnimationOffsetX { get; set; } = 7;
    public string SelectionAnimationEasing { get; set; } = "CubicOut";
    public double CaretWidth { get; set; } = 1.5;
    public double CaretCornerRadius { get; set; } = 0.75;
    public double CaretMinHeight { get; set; } = 18;
    public int CaretAnimationDurationMilliseconds { get; set; } = 85;
    public int CaretBlinkMilliseconds { get; set; } = 1350;
    public int CaretTypingBlinkPauseMilliseconds { get; set; } = 650;
    public string CaretAnimationEasing { get; set; } = "CubicOut";
    public double StatusFontSize { get; set; } = 12.5;
    public double StatusTitleFontSize { get; set; } = 13.5;
    public double StatusPanelPadding { get; set; } = 20;
    public double StatusPanelSpacing { get; set; } = 8;
    public double StatusHintOpacity { get; set; } = 0.92;
    public double LoadingIndicatorHeight { get; set; } = 2;
    public double ForegroundOpacity { get; set; } = 1;
    public double MutedForegroundOpacity { get; set; } = 1;
    public double BorderOpacity { get; set; } = 1;
    public double SearchBackgroundOpacity { get; set; } = 0.98;
    public double SearchBorderOpacity { get; set; } = 0.14;
    public double SeparatorOpacity { get; set; } = 0.1;
    public double IconOpacity { get; set; } = 1;
    public double IconBackgroundOpacity { get; set; } = 0;
    public double IconBorderOpacity { get; set; } = 0;
    public string BackgroundImagePath { get; set; } = string.Empty;
    public double BackgroundImageOpacity { get; set; }
    public string BackgroundImageStretch { get; set; } = "UniformToFill";
    public string Background { get; set; } = "#0F1722";
    public string Foreground { get; set; } = "#F4F7FB";
    public string MutedForeground { get; set; } = "#93A1B5";
    public string Accent { get; set; } = "#6FD0C2";
    public string SelectedBackground { get; set; } = "#1A2938";
    public string Border { get; set; } = "#263448";
    public Dictionary<string, string> Resources { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, Dictionary<string, string>> WidgetProperties { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public ThemeSettings Clone()
    {
        return new ThemeSettings
        {
            Schema = Schema,
            BaseDirectory = BaseDirectory,
            Name = Name,
            FontFamily = FontFamily,
            FontSize = FontSize,
            SearchFontSize = SearchFontSize,
            ResultTitleFontSize = ResultTitleFontSize,
            ResultSubtitleFontSize = ResultSubtitleFontSize,
            ResultTitleFontWeight = ResultTitleFontWeight,
            ResultSubtitleFontWeight = ResultSubtitleFontWeight,
            WindowWidth = WindowWidth,
            WindowOpacity = WindowOpacity,
            CornerRadius = CornerRadius,
            ResultCornerRadius = ResultCornerRadius,
            ResultLayout = ResultLayout,
            ShowPrompt = ShowPrompt,
            PromptPersistent = PromptPersistent,
            PromptOpacity = PromptOpacity,
            PromptText = PromptText,
            ShowStatusPanel = ShowStatusPanel,
            ShowIcons = ShowIcons,
            ShowSubtitles = ShowSubtitles,
            ShowSelectionAccent = ShowSelectionAccent,
            ShowItemBorders = ShowItemBorders,
            ShowModeSwitcher = ShowModeSwitcher,
            MessageText = MessageText,
            RowHeight = RowHeight,
            ResultGap = ResultGap,
            Spacing = Spacing,
            SearchHorizontalPadding = SearchHorizontalPadding,
            SearchVerticalPadding = SearchVerticalPadding,
            ResultHorizontalPadding = ResultHorizontalPadding,
            ResultVerticalPadding = ResultVerticalPadding,
            ResultTextLeftMargin = ResultTextLeftMargin,
            ResultTextRightMargin = ResultTextRightMargin,
            ResultSubtitleTopMargin = ResultSubtitleTopMargin,
            IconColumnWidth = IconColumnWidth,
            IconContainerSize = IconContainerSize,
            IconSize = IconSize,
            FallbackIconSize = FallbackIconSize,
            FallbackIconCornerRadius = FallbackIconCornerRadius,
            FallbackIconOpacity = FallbackIconOpacity,
            OuterShadowMargin = OuterShadowMargin,
            OuterShadowOpacity = OuterShadowOpacity,
            OuterShadowBlurRadius = OuterShadowBlurRadius,
            OuterShadowDepth = OuterShadowDepth,
            OuterShadowEffectOpacity = OuterShadowEffectOpacity,
            SurfaceBorderThickness = SurfaceBorderThickness,
            SeparatorThickness = SeparatorThickness,
            SurfaceShadowBlurRadius = SurfaceShadowBlurRadius,
            SurfaceShadowDepth = SurfaceShadowDepth,
            SurfaceShadowOpacity = SurfaceShadowOpacity,
            WindowTopOffsetMin = WindowTopOffsetMin,
            WindowTopOffsetRatio = WindowTopOffsetRatio,
            AnimateLauncherOpen = AnimateLauncherOpen,
            AnimateLauncherClose = AnimateLauncherClose,
            LauncherOpenDurationMilliseconds = LauncherOpenDurationMilliseconds,
            LauncherCloseDurationMilliseconds = LauncherCloseDurationMilliseconds,
            LauncherOpenOffsetY = LauncherOpenOffsetY,
            LauncherCloseOffsetY = LauncherCloseOffsetY,
            LauncherOpenScale = LauncherOpenScale,
            LauncherCloseScale = LauncherCloseScale,
            LauncherOpenEasing = LauncherOpenEasing,
            LauncherCloseEasing = LauncherCloseEasing,
            AnimateResults = AnimateResults,
            ResultsAnimationDurationMilliseconds = ResultsAnimationDurationMilliseconds,
            ResultsAnimationOffsetY = ResultsAnimationOffsetY,
            ResultsAnimationStaggerMilliseconds = ResultsAnimationStaggerMilliseconds,
            ResultsAnimationEasing = ResultsAnimationEasing,
            AnimateSelection = AnimateSelection,
            SelectionAnimationDurationMilliseconds = SelectionAnimationDurationMilliseconds,
            SelectionAnimationOffsetX = SelectionAnimationOffsetX,
            SelectionAnimationEasing = SelectionAnimationEasing,
            CaretWidth = CaretWidth,
            CaretCornerRadius = CaretCornerRadius,
            CaretMinHeight = CaretMinHeight,
            CaretAnimationDurationMilliseconds = CaretAnimationDurationMilliseconds,
            CaretBlinkMilliseconds = CaretBlinkMilliseconds,
            CaretTypingBlinkPauseMilliseconds = CaretTypingBlinkPauseMilliseconds,
            CaretAnimationEasing = CaretAnimationEasing,
            StatusFontSize = StatusFontSize,
            StatusTitleFontSize = StatusTitleFontSize,
            StatusPanelPadding = StatusPanelPadding,
            StatusPanelSpacing = StatusPanelSpacing,
            StatusHintOpacity = StatusHintOpacity,
            LoadingIndicatorHeight = LoadingIndicatorHeight,
            ForegroundOpacity = ForegroundOpacity,
            MutedForegroundOpacity = MutedForegroundOpacity,
            BorderOpacity = BorderOpacity,
            SearchBackgroundOpacity = SearchBackgroundOpacity,
            SearchBorderOpacity = SearchBorderOpacity,
            SeparatorOpacity = SeparatorOpacity,
            IconOpacity = IconOpacity,
            IconBackgroundOpacity = IconBackgroundOpacity,
            IconBorderOpacity = IconBorderOpacity,
            BackgroundImagePath = BackgroundImagePath,
            BackgroundImageOpacity = BackgroundImageOpacity,
            BackgroundImageStretch = BackgroundImageStretch,
            Background = Background,
            Foreground = Foreground,
            MutedForeground = MutedForeground,
            Accent = Accent,
            SelectedBackground = SelectedBackground,
            Border = Border,
            Resources = new Dictionary<string, string>(Resources, StringComparer.OrdinalIgnoreCase),
            WidgetProperties = WidgetProperties.ToDictionary(
                static entry => entry.Key,
                static entry => new Dictionary<string, string>(entry.Value, StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase)
        };
    }
}
