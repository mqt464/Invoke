using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Invoke.App.Hotkeys;
using Invoke.Core.Actions;
using Invoke.Core.Config;
using Invoke.Core.Modes;
using Invoke.Core.Results;
using Invoke.Core.Services;
using MediaBrush = System.Windows.Media.Brush;

namespace Invoke.App;

public partial class MainWindow : Window
{
    private const int WhMouseLl = 14;
    private const int WmSysCommand = 0x0112;
    private const int WmSysChar = 0x0106;
    private const int WmLButtonDown = 0x0201;
    private const int WmRButtonDown = 0x0204;
    private const int WmMButtonDown = 0x0207;
    private const int WmXButtonDown = 0x020B;
    private const int ScKeymenu = 0xF100;
    private const uint MonitorDefaultToNearest = 2;
    private static readonly HttpClient RemoteIconClient = new();
    private static readonly Regex PlaceholderPattern = new(@"\{(?<name>[a-zA-Z0-9_-]+)\}", RegexOptions.Compiled);

    private LauncherModeRegistry _modeRegistry;
    private readonly ConfigService _configService;
    private InvokeSettings _settings;
    private readonly ObservableCollection<ResultViewModel> _results = [];
    private readonly LowLevelMouseProc _mouseProc;
    private readonly HashSet<string> _appliedThemeResourceKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _pendingAnimatedResultKeys = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _searchCts;
    private AppTheme _theme;
    private ThemeSettings _baseThemeSettings;
    private DispatcherTimer? _focusRetryTimer;
    private ScrollViewer? _resultsScrollViewer;
    private bool _isUpdatingQueryText;
    private bool _isSearching;
    private bool _showLoadingIndicator;
    private bool _isClosing;
    private bool _suppressSelectionAnimation;
    private string _entryPlaceholderText = string.Empty;
    private MediaBrush _entryPlaceholderBrush = System.Windows.Media.Brushes.Transparent;
    private string _rawQuery = string.Empty;
    private string? _activeMode;
    private string? _currentModeMessage;
    private bool _currentNoCustom;
    private bool _currentUseHotKeys;
    private bool _currentMarkupRows;
    private string? _currentDisplayPrefix;
    private string? _currentRawPrefix;
    private bool _iconChromeEnabled;
    private MediaBrush _iconChromeBackground = System.Windows.Media.Brushes.Transparent;
    private MediaBrush _iconChromeBorder = System.Windows.Media.Brushes.Transparent;
    private IReadOnlyList<string> _lastAnimatedResultKeys = [];
    private int _lastAnimatedSelectionIndex = -1;
    private int _focusRequestId;
    private int _searchRequestId;
    private IntPtr _mouseHook;
    private long _suppressDeactivateUntilTicks;

    public event Action? LauncherHidden;

    public MainWindow(
        LauncherModeRegistry modeRegistry,
        ConfigService configService,
        InvokeSettings settings,
        ThemeSettings themeSettings)
    {
        InitializeComponent();
        _modeRegistry = modeRegistry;
        _configService = configService;
        _settings = settings;
        _baseThemeSettings = themeSettings;
        _theme = new AppTheme(themeSettings);
        _mouseProc = MouseHookCallback;
        IsVisibleChanged += OnIsVisibleChanged;
        Activated += OnActivated;
        Loaded += OnWindowLoaded;
        SizeChanged += OnWindowSizeChanged;
        MainboxGrid.SizeChanged += OnMainboxGridSizeChanged;
        ResultsContentHost.SizeChanged += OnResultsContentHostSizeChanged;

        ApplyTheme(_theme);
        RefreshModeSwitcher();
        ResultsList.ItemsSource = _results;
        UpdateResultsSurfaceState();
        Hide();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        if (PresentationSource.FromVisual(this) is HwndSource source)
            source.AddHook(WndProc);
    }

    public void ActivateLauncher(bool fromHotkey = false, string? initialMode = null)
    {
        if (IsVisible)
        {
            if (initialMode is not null)
                ApplyLauncherMode(initialMode);

            if (fromHotkey)
                ArmHotkeyDeactivateGuard();

            ForceForeground();
            ScheduleSearchBoxFocus();
            return;
        }

        if (fromHotkey)
            ArmHotkeyDeactivateGuard();

        PositionLauncher();
        Opacity = _theme.Settings.WindowOpacity;
        PrepareOpenAnimationState();
        Show();
        UpdateLayout();
        PositionLauncher();
        _isClosing = false;
        ApplyLauncherMode(initialMode);
        ForceForeground();
        StartOutsideClickWatcher();
        ScheduleSearchBoxFocus();
        BeginOpenAnimation();
    }

    public void FocusSearchBar()
    {
        ScheduleSearchBoxFocus();
    }

    public void PrefillQueryAndSearch(string query, string? mode = null)
    {
        if (!string.IsNullOrWhiteSpace(mode))
            _activeMode = mode;

        _rawQuery = query ?? string.Empty;
        ApplyQueryDisplayText(_rawQuery);
        UpdatePromptVisibility(_rawQuery);
        SearchCurrentQuery();
    }

    public void ApplyThemeSettings(ThemeSettings themeSettings)
    {
        _baseThemeSettings = themeSettings;
        _theme = new AppTheme(themeSettings);
        ApplyTheme(_theme);
        RefreshModeSwitcher();
        RefreshResultTheme();
    }

    public void ApplyRuntime(LauncherModeRegistry modeRegistry, InvokeSettings settings, ThemeSettings themeSettings)
    {
        _modeRegistry = modeRegistry;
        _settings = settings;
        ApplyThemeSettings(themeSettings);
    }

    public void RefreshFromConfig()
    {
        if (string.IsNullOrWhiteSpace(_rawQuery))
            return;

        ApplyQueryDisplayText(_rawQuery);
        SearchCurrentQuery();
    }

    private void ForceForeground()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
        {
            ShowWindow(handle, SwShownormal);
            BringWindowToTop(handle);
            ForceForegroundWindow(handle);
            SetActiveWindow(handle);
            SetFocus(handle);
        }

        Activate();
    }

    private bool ForceFocusSearchBox()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
            SetFocus(handle);

        QueryBox.Focusable = true;
        FocusManager.SetFocusedElement(this, QueryBox);
        var controlFocused = QueryBox.Focus();
        var keyboardFocused = Keyboard.Focus(QueryBox) == QueryBox;
        QueryBox.CaretIndex = QueryBox.Text.Length;
        QueryBox.Select(QueryBox.Text.Length, 0);
        return IsActive && controlFocused && keyboardFocused && QueryBox.IsKeyboardFocusWithin;
    }

    private void ScheduleSearchBoxFocus()
    {
        var requestId = ++_focusRequestId;
        var attemptsRemaining = 4;

        _focusRetryTimer?.Stop();
        var focusRetryTimer = new DispatcherTimer(DispatcherPriority.Input, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _focusRetryTimer = focusRetryTimer;

        void TryFocus()
        {
            if (!IsVisible || requestId != _focusRequestId)
            {
                focusRetryTimer.Stop();
                return;
            }

            ForceFocusSearchBox();
            if (--attemptsRemaining <= 0)
                focusRetryTimer.Stop();
        }

        TryFocus();
        Dispatcher.BeginInvoke(TryFocus, DispatcherPriority.Input);
        Dispatcher.BeginInvoke(TryFocus, DispatcherPriority.ApplicationIdle);
        focusRetryTimer.Tick += (_, _) => TryFocus();
        focusRetryTimer.Start();
    }

    private void OnActivated(object? sender, EventArgs e)
    {
        if (IsVisible)
            ScheduleSearchBoxFocus();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
            StartOutsideClickWatcher();
        else
        {
            _focusRetryTimer?.Stop();
            StopOutsideClickWatcher();
            ResetLauncherState(_settings.Launcher.ClearQueryOnHide);
            LauncherHidden?.Invoke();
        }
    }

    private void ResetLauncherState(bool clearQuery)
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = null;
        _isSearching = false;
        _showLoadingIndicator = false;
        _isClosing = false;
        _searchRequestId++;

        if (!clearQuery)
        {
            ApplyModePrompt();
            UpdateResultsSurfaceState();
            return;
        }

        _rawQuery = string.Empty;
        _activeMode = null;
        _lastAnimatedResultKeys = [];
        _pendingAnimatedResultKeys.Clear();
        _lastAnimatedSelectionIndex = -1;
        ApplyQueryDisplayText(string.Empty);
        UpdatePromptVisibility(string.Empty);
        _results.Clear();
        ResultsList.SelectedIndex = -1;
        UpdateResultsSurfaceState();
    }

    private void ApplyTheme(AppTheme theme)
    {
        SizeToContent = SizeToContent.Height;
        Opacity = theme.Settings.WindowOpacity;
        FontFamily = new System.Windows.Media.FontFamily(theme.Settings.FontFamily);
        FontSize = theme.Settings.FontSize;
        Foreground = theme.Foreground;

        Root.Background = System.Windows.Media.Brushes.Transparent;
        Root.BorderBrush = System.Windows.Media.Brushes.Transparent;
        var surfaceRadius = new CornerRadius(theme.Settings.CornerRadius);
        var innerRadius = Math.Max(0, theme.Settings.CornerRadius - 1);

        QueryBox.Foreground = theme.Foreground;
        QueryBox.CaretBrush = System.Windows.Media.Brushes.Transparent;
        QueryDisplayText.Visibility = Visibility.Collapsed;
        ResultsList.Foreground = theme.Foreground;
        ApplyModePrompt();

        Resources["LauncherSurfaceCornerRadius"] = surfaceRadius;
        Resources["LauncherSearchCornerRadius"] = new CornerRadius(innerRadius);
        Resources["LauncherResultsCornerRadius"] = new CornerRadius(0, 0, innerRadius, innerRadius);
        Resources["LauncherSurfaceShadowRadius"] = theme.Settings.CornerRadius;
        Resources["LauncherSurfaceBorderThickness"] = new Thickness(theme.Settings.SurfaceBorderThickness);
        Resources["LauncherSurfacePadding"] = new Thickness(0);
        Resources["LauncherSeparatorThickness"] = new Thickness(0, theme.Settings.SeparatorThickness, 0, 0);
        Resources["LauncherRowHeight"] = theme.Settings.RowHeight;
        Resources["LauncherSearchFontFamily"] = new System.Windows.Media.FontFamily(theme.Settings.FontFamily);
        Resources["LauncherSearchFontSize"] = theme.Settings.SearchFontSize;
        Resources["LauncherSearchFontWeight"] = FontWeights.Medium;
        Resources["LauncherPromptFontFamily"] = new System.Windows.Media.FontFamily(theme.Settings.FontFamily);
        Resources["LauncherPromptFontWeight"] = FontWeights.Medium;
        Resources["LauncherResultTitleFontFamily"] = new System.Windows.Media.FontFamily(theme.Settings.FontFamily);
        Resources["LauncherResultSubtitleFontFamily"] = new System.Windows.Media.FontFamily(theme.Settings.FontFamily);
        Resources["LauncherResultTitleFontSize"] = theme.Settings.ResultTitleFontSize;
        Resources["LauncherResultSubtitleFontSize"] = theme.Settings.ResultSubtitleFontSize;
        Resources["LauncherResultTitleFontWeight"] = ParseFontWeight(theme.Settings.ResultTitleFontWeight, FontWeights.SemiBold);
        Resources["LauncherResultSubtitleFontWeight"] = ParseFontWeight(theme.Settings.ResultSubtitleFontWeight, FontWeights.Normal);
        Resources["LauncherMessageFontFamily"] = new System.Windows.Media.FontFamily(theme.Settings.FontFamily);
        Resources["LauncherMessageFontSize"] = theme.Settings.StatusFontSize;
        Resources["LauncherMessageFontWeight"] = FontWeights.Normal;
        Resources["LauncherInputbarPadding"] = new Thickness(
            theme.Settings.SearchHorizontalPadding,
            theme.Settings.SearchVerticalPadding,
            theme.Settings.SearchHorizontalPadding,
            theme.Settings.SearchVerticalPadding);
        Resources["LauncherSearchPadding"] = new Thickness(0);
        Resources["LauncherResultItemPadding"] = new Thickness(
            theme.Settings.ResultHorizontalPadding,
            theme.Settings.ResultVerticalPadding,
            theme.Settings.ResultHorizontalPadding,
            theme.Settings.ResultVerticalPadding);
        Resources["LauncherSearchTextMargin"] = new Thickness(0);
        Resources["LauncherPromptMargin"] = new Thickness(0);
        Resources["LauncherEntryHostMargin"] = new Thickness(0);
        Resources["LauncherResultTextMargin"] = new Thickness(
            theme.Settings.ResultTextLeftMargin,
            0,
            theme.Settings.ResultTextRightMargin,
            0);
        Resources["LauncherResultSubtitleMargin"] = new Thickness(0, theme.Settings.ResultSubtitleTopMargin, 0, 0);
        Resources["LauncherIconColumnWidth"] = new GridLength(theme.Settings.ShowIcons ? theme.Settings.IconColumnWidth : 0);
        Resources["LauncherIconSlotVisibility"] = theme.Settings.ShowIcons ? Visibility.Visible : Visibility.Collapsed;
        Resources["LauncherIconContainerSize"] = theme.Settings.ShowIcons ? theme.Settings.IconContainerSize : 0d;
        Resources["LauncherIconSize"] = theme.Settings.ShowIcons ? theme.Settings.IconSize : 0d;
        Resources["LauncherIconOpacity"] = theme.Settings.IconOpacity;
        Resources["LauncherFallbackIconSize"] = theme.Settings.FallbackIconSize;
        Resources["LauncherFallbackIconCornerRadius"] = new CornerRadius(theme.Settings.FallbackIconCornerRadius);
        Resources["LauncherIconCornerRadius"] = new CornerRadius(0);
        Resources["LauncherIconBorderThickness"] = new Thickness(0);
        Resources["LauncherFallbackIconOpacity"] = theme.Settings.FallbackIconOpacity;
        Resources["LauncherOuterShadowMargin"] = new Thickness(theme.Settings.OuterShadowMargin);
        Resources["LauncherOuterShadowOpacity"] = theme.Settings.OuterShadowOpacity;
        Resources["LauncherOuterShadowBlurRadius"] = theme.Settings.OuterShadowBlurRadius;
        Resources["LauncherOuterShadowDepth"] = theme.Settings.OuterShadowDepth;
        Resources["LauncherOuterShadowEffectOpacity"] = theme.Settings.OuterShadowEffectOpacity;
        Resources["LauncherSurfaceShadowBlurRadius"] = theme.Settings.SurfaceShadowBlurRadius;
        Resources["LauncherSurfaceShadowDepth"] = theme.Settings.SurfaceShadowDepth;
        Resources["LauncherSurfaceShadowOpacity"] = theme.Settings.SurfaceShadowOpacity;
        Resources["LauncherForegroundBrush"] = theme.Foreground;
        Resources["LauncherMutedBrush"] = theme.MutedForeground;
        Resources["LauncherPromptBrush"] = theme.MutedForeground;
        Resources["LauncherResultForegroundBrush"] = theme.Foreground;
        Resources["LauncherResultSecondaryBrush"] = theme.MutedForeground;
        Resources["LauncherSelectedBrush"] = theme.SelectedBackground;
        Resources["LauncherSelectedBorderBrush"] = theme.SelectedBorder;
        Resources["LauncherHoverBrush"] = theme.HoverBackground;
        Resources["LauncherHoverBorderBrush"] = theme.HoverBorder;
        Resources["LauncherSearchBackgroundBrush"] = theme.SearchBackground;
        Resources["LauncherSearchBorderBrush"] = theme.SearchBorder;
        Resources["LauncherSeparatorBrush"] = theme.Separator;
        Resources["LauncherAccentBrush"] = theme.Accent;
        Resources["LauncherCaretWidth"] = theme.Settings.CaretWidth;
        Resources["LauncherCaretCornerRadius"] = theme.Settings.CaretCornerRadius;
        Resources["LauncherCaretMinHeight"] = theme.Settings.CaretMinHeight;
        Resources["LauncherCaretAnimationDurationMilliseconds"] = theme.Settings.CaretAnimationDurationMilliseconds;
        Resources["LauncherCaretBlinkMilliseconds"] = theme.Settings.CaretBlinkMilliseconds;
        Resources["LauncherCaretTypingBlinkPauseMilliseconds"] = theme.Settings.CaretTypingBlinkPauseMilliseconds;
        Resources["LauncherCaretAnimationEasing"] = theme.Settings.CaretAnimationEasing;
        Resources["LauncherStatusFontFamily"] = new System.Windows.Media.FontFamily(theme.Settings.FontFamily);
        Resources["LauncherStatusFontSize"] = theme.Settings.StatusFontSize;
        Resources["LauncherStatusFontWeight"] = FontWeights.Normal;
        Resources["LauncherStatusTitleFontSize"] = theme.Settings.StatusTitleFontSize;
        Resources["LauncherStatusTitleFontWeight"] = FontWeights.SemiBold;
        Resources["LauncherStatusHintOpacity"] = theme.Settings.StatusHintOpacity;
        Resources["LauncherStatusBodyMargin"] = new Thickness(0, theme.Settings.StatusPanelSpacing, 0, 0);
        Resources["LauncherStatusHintMargin"] = new Thickness(0, theme.Settings.StatusPanelSpacing, 0, 0);
        Resources["LauncherLoadingIndicatorHeight"] = theme.Settings.LoadingIndicatorHeight;
        Resources["LauncherStatusPanelPadding"] = new Thickness(theme.Settings.StatusPanelPadding);
        Resources["LauncherSelectionAccentWidth"] = 2d;
        Resources["LauncherSelectionAccentMargin"] = new Thickness(
            Math.Max(8, theme.Settings.ResultHorizontalPadding - 6),
            Math.Max(8, theme.Settings.ResultVerticalPadding + 2),
            0,
            Math.Max(8, theme.Settings.ResultVerticalPadding + 2));
        Resources["LauncherSubtitleVisibility"] = theme.Settings.ShowSubtitles && theme.Settings.ResultLayout.Equals("twoLine", StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
        Resources["LauncherSelectionAccentVisibility"] = theme.Settings.ShowSelectionAccent ? Visibility.Visible : Visibility.Collapsed;
        Resources["LauncherItemBorderThickness"] = new Thickness(theme.Settings.ShowItemBorders ? 1 : 0);
        Resources["LauncherScrollBarWidth"] = 8d;
        Resources["LauncherScrollBarMargin"] = new Thickness(8, 10, 10, 10);
        Resources["LauncherScrollBarCornerRadius"] = new CornerRadius(999);
        Resources["LauncherScrollBarThumbMinHeight"] = 30d;
        Resources["LauncherScrollBarTrackBrush"] = CreateFrozenBrush(System.Windows.Media.Color.FromArgb(30, 255, 255, 255));
        Resources["LauncherScrollBarTrackBorderBrush"] = CreateFrozenBrush(System.Windows.Media.Color.FromArgb(35, 255, 255, 255));
        Resources["LauncherScrollBarThumbBrush"] = theme.Accent;
        Resources["LauncherScrollBarThumbBorderBrush"] = CreateFrozenBrush(System.Windows.Media.Color.FromArgb(40, 255, 255, 255));
        Resources["LauncherScrollBarThumbHoverBrush"] = CreateFrozenBrush(AdjustColorOpacity(GetBrushColor(theme.Accent, System.Windows.Media.Colors.White), 0.92));
        Resources["LauncherScrollBarThumbPressedBrush"] = CreateFrozenBrush(AdjustColorOpacity(GetBrushColor(theme.Accent, System.Windows.Media.Colors.White), 1));
        Resources["LauncherOverflowBadgePadding"] = new Thickness(8, 3, 8, 3);
        Resources["LauncherOverflowBadgeCornerRadius"] = new CornerRadius(999);
        Resources["LauncherOverflowTopMargin"] = new Thickness(0, 10, 12, 0);
        Resources["LauncherOverflowBottomMargin"] = new Thickness(0, 0, 12, 10);
        Resources["LauncherOverflowBadgeBackgroundBrush"] = CreateFrozenBrush(System.Windows.Media.Colors.Transparent);
        Resources["LauncherOverflowBadgeBorderBrush"] = CreateFrozenBrush(System.Windows.Media.Colors.Transparent);
        Resources["LauncherOverflowBadgeTextBrush"] = theme.Foreground;
        Resources["LauncherOverflowBadgeFontFamily"] = new System.Windows.Media.FontFamily(theme.Settings.FontFamily);
        Resources["LauncherOverflowBadgeFontSize"] = Math.Max(10, theme.Settings.StatusFontSize - 0.5);
        Resources["LauncherOverflowBadgeFontWeight"] = FontWeights.SemiBold;

        Resources[System.Windows.SystemColors.HighlightBrushKey] = theme.SelectedBackground;
        Resources[System.Windows.SystemColors.HighlightTextBrushKey] = theme.Foreground;
        Resources[System.Windows.SystemColors.ControlBrushKey] = theme.SelectedBackground;
        ApplyCustomThemeResources(theme);
        ApplyWidgetTheme(theme);
        ApplyWidgetLayout();
        ApplyBackgroundImage(theme.Settings);
        ApplyLauncherMetrics();
        UpdateSurfaceClip();
        UpdateQueryDisplayOverlay();
        UpdatePromptVisibility(_rawQuery);
        UpdateResultsSurfaceState();
        QueueOverflowIndicatorRefresh();

        if (IsVisible)
            PositionLauncher();
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        UpdateSurfaceClip();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (!ShouldSuppressSystemMenuMessage(IsVisible, msg, wParam))
            return IntPtr.Zero;

        handled = true;
        return IntPtr.Zero;
    }

    internal static bool ShouldSuppressSystemMenuMessage(bool isVisible, int msg, IntPtr wParam)
    {
        if (!isVisible)
            return false;

        if (msg == WmSysChar)
            return true;

        return msg == WmSysCommand && (((int)wParam) & 0xFF00) == ScKeymenu;
    }

    private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateSurfaceClip();
    }

    private void OnMainboxGridSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateSurfaceClip();
    }

    private void OnResultsContentHostSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateResultsClip();
    }

    private void UpdateSurfaceClip()
    {
        if (MainboxGrid.ActualWidth <= 0 || MainboxGrid.ActualHeight <= 0)
            return;

        var radius = Math.Max(0, _theme.Settings.CornerRadius - _theme.Settings.SurfaceBorderThickness);
        MainboxGrid.Clip = new RectangleGeometry(new Rect(0, 0, MainboxGrid.ActualWidth, MainboxGrid.ActualHeight), radius, radius);
        UpdateResultsClip();
    }

    private void UpdateResultsClip()
    {
        if (ResultsContentHost.ActualWidth <= 0 || ResultsContentHost.ActualHeight <= 0)
            return;

        var geometry = new StreamGeometry();
        var rect = new Rect(0, 0, ResultsContentHost.ActualWidth, ResultsContentHost.ActualHeight);
        var bottomLeftRadius = Math.Clamp(ResultsChrome.CornerRadius.BottomLeft, 0, Math.Min(rect.Width / 2d, rect.Height));
        var bottomRightRadius = Math.Clamp(ResultsChrome.CornerRadius.BottomRight, 0, Math.Min(rect.Width / 2d, rect.Height));

        using (var context = geometry.Open())
        {
            context.BeginFigure(rect.TopLeft, true, true);
            context.LineTo(rect.TopRight, true, false);
            context.LineTo(new System.Windows.Point(rect.Right, rect.Bottom - bottomRightRadius), true, false);

            if (bottomRightRadius > 0)
            {
                context.ArcTo(
                    new System.Windows.Point(rect.Right - bottomRightRadius, rect.Bottom),
                    new System.Windows.Size(bottomRightRadius, bottomRightRadius),
                    0,
                    false,
                    SweepDirection.Clockwise,
                    true,
                    false);
            }
            else
            {
                context.LineTo(rect.BottomRight, true, false);
            }

            context.LineTo(new System.Windows.Point(rect.Left + bottomLeftRadius, rect.Bottom), true, false);

            if (bottomLeftRadius > 0)
            {
                context.ArcTo(
                    new System.Windows.Point(rect.Left, rect.Bottom - bottomLeftRadius),
                    new System.Windows.Size(bottomLeftRadius, bottomLeftRadius),
                    0,
                    false,
                    SweepDirection.Clockwise,
                    true,
                    false);
            }
            else
            {
                context.LineTo(rect.BottomLeft, true, false);
            }
        }

        geometry.Freeze();
        ResultsContentHost.Clip = geometry;
    }

    private void ApplyModeOverlay(LauncherSearchResult searchResult)
        => ApplyModeOverlay(searchResult.ThemeOverlay);

    private void ApplyModeOverlay(Invoke.Core.Rasi.RasiDocument? themeOverlay)
    {
        if (themeOverlay is not null)
        {
            var mergedTheme = _configService.ApplyThemeOverlay(_baseThemeSettings, themeOverlay);
            _theme = new AppTheme(mergedTheme);
            ApplyTheme(_theme);
        }
        else if (!ReferenceEquals(_theme.Settings, _baseThemeSettings))
        {
            _theme = new AppTheme(_baseThemeSettings);
            ApplyTheme(_theme);
        }
    }

    private void RefreshModeSwitcher()
    {
        ModeSwitcherPanel.Children.Clear();
        var modes = _modeRegistry.OrderedModes;
        var buttonSpacing = GetWidgetDistance("mode-switcher", "spacing") ?? 8d;
        var buttonPaddingHorizontal = GetWidgetPaddingHorizontal("button", 10d);
        var buttonPaddingVertical = GetWidgetPaddingVertical("button", 4d);
        var buttonFont = ResolveWidgetFont("button", _theme.Settings.FontFamily, _theme.Settings.FontSize, FontWeights.Normal);
        ModeSwitcherChrome.Visibility = _theme.Settings.ShowModeSwitcher && _settings.SidebarMode && modes.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        foreach (var mode in modes)
        {
            var button = new System.Windows.Controls.Button
            {
                Content = mode.DisplayName,
                Margin = new Thickness(0, 0, buttonSpacing, 0),
                Padding = new Thickness(buttonPaddingHorizontal, buttonPaddingVertical, buttonPaddingHorizontal, buttonPaddingVertical),
                Background = mode.Id.Equals(_activeMode, StringComparison.OrdinalIgnoreCase) ? _theme.SelectedBackground : System.Windows.Media.Brushes.Transparent,
                Foreground = mode.Id.Equals(_activeMode, StringComparison.OrdinalIgnoreCase) ? _theme.Foreground : _theme.MutedForeground,
                BorderBrush = mode.Id.Equals(_activeMode, StringComparison.OrdinalIgnoreCase) ? _theme.Accent : System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(mode.Id.Equals(_activeMode, StringComparison.OrdinalIgnoreCase) ? 1 : 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontFamily = new System.Windows.Media.FontFamily(buttonFont.Family),
                FontSize = buttonFont.Size,
                FontWeight = buttonFont.Weight,
                Tag = mode.Id
            };
            button.Click += (_, _) => ApplyLauncherMode((string?)button.Tag);
            ModeSwitcherPanel.Children.Add(button);
        }
    }

    private void ApplyLauncherMetrics()
    {
        var workingArea = ResolveWorkingArea();
        Width = ResolveLauncherWidth(workingArea);
        ResultsList.MaxHeight = ResolveResultsMaxHeight();
    }

    private void UpdatePromptVisibility(string query)
    {
        PromptText.Visibility = _theme.Settings.ShowPrompt && (_theme.Settings.PromptPersistent || string.IsNullOrWhiteSpace(query))
            ? Visibility.Visible
            : Visibility.Collapsed;
        UpdateInputbarPromptLayout();
    }

    private void ApplyBackgroundImage(ThemeSettings settings)
    {
        SurfaceBackgroundImage.Source = null;
        SurfaceBackgroundImage.Visibility = Visibility.Collapsed;
        SurfaceBackgroundImage.Opacity = 0;
        SurfaceBackgroundImage.Stretch = ParseStretch(settings.BackgroundImageStretch);

        if (settings.BackgroundImageOpacity <= 0)
            return;

        var imageSource = LoadBackgroundImage(settings.BackgroundImagePath);
        if (imageSource is null)
            return;

        SurfaceBackgroundImage.Source = imageSource;
        SurfaceBackgroundImage.Opacity = settings.BackgroundImageOpacity;
        SurfaceBackgroundImage.Visibility = Visibility.Visible;
    }

    private ImageSource? LoadBackgroundImage(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return null;

        var resolvedPath = ResolveThemeAssetPath(imagePath);
        if (!Uri.TryCreate(resolvedPath, UriKind.Absolute, out var uri))
            return null;

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = uri;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private string ResolveThemeAssetPath(string path)
    {
        var trimmed = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absoluteUri) && !absoluteUri.IsFile)
            return trimmed;

        if (Path.IsPathRooted(trimmed))
            return new Uri(trimmed).AbsoluteUri;

        if (!string.IsNullOrWhiteSpace(_theme.Settings.BaseDirectory))
        {
            var themeBaseRelative = Path.Combine(_theme.Settings.BaseDirectory, trimmed);
            if (File.Exists(themeBaseRelative))
                return new Uri(themeBaseRelative).AbsoluteUri;
        }

        var themeRelative = Path.Combine(_configService.ThemesDirectory, trimmed);
        if (File.Exists(themeRelative))
            return new Uri(themeRelative).AbsoluteUri;

        var configRelative = Path.Combine(_configService.ConfigDirectory, trimmed);
        return new Uri(configRelative).AbsoluteUri;
    }

    private void ApplyWidgetTheme(AppTheme theme)
    {
        var inputbarBackground = GetWidgetBrush("inputbar", "background-color");
        var inputbarBorder = GetWidgetBrush("inputbar", "border-color");
        var promptBrush = GetWidgetBrush("prompt", "text-color");
        var entryBrush = GetWidgetBrush("entry", "text-color") ?? GetWidgetBrush("inputbar", "text-color");
        var entryPlaceholderBrush = GetWidgetBrush("entry", "placeholder-color") ?? entryBrush ?? theme.MutedForeground;
        var resultBrush = GetWidgetBrush("element-text", "text-color");
        var secondaryBrush = GetWidgetBrush("element-text", "placeholder-color") ?? GetWidgetBrush("message", "text-color");
        var messageBrush = GetWidgetBrush("message", "text-color");
        var selectedBackground = GetWidgetBrush("element selected", "background-color");
        var selectedBorder = GetWidgetBrush("element selected", "border-color");
        var selectedText = GetWidgetBrush("element-text selected", "text-color");
        var selectedSecondary = GetWidgetBrush("element-text selected", "placeholder-color");
        var iconBackground = GetWidgetBrushExact("element-icon", "background-color");
        var iconBorder = GetWidgetBrushExact("element-icon", "border-color");
        var entryPlaceholder = GetWidgetString("entry", "placeholder");
        var windowBorderThickness = GetWidgetDistance("window", "border") ?? theme.Settings.SurfaceBorderThickness;
        var windowPadding = ParseWidgetBox("window", "padding", new Thickness(0));
        var elementBorderThickness = GetWidgetDistance("element", "border");
        var iconBorderThickness = GetWidgetDistanceExact("element-icon", "border") ?? 0d;
        var iconCornerRadius = GetWidgetDistanceExact("element-icon", "border-radius") ?? 0d;

        _iconChromeBackground = iconBackground ?? theme.IconBackground;
        _iconChromeBorder = iconBorder ?? theme.IconBorder;
        _iconChromeEnabled = iconBorderThickness > 0 || !IsTransparentBrush(_iconChromeBackground) || !IsTransparentBrush(_iconChromeBorder);
        var iconContainerSize = _iconChromeEnabled
            ? theme.Settings.IconContainerSize
            : Math.Max(theme.Settings.IconSize, theme.Settings.FallbackIconSize);

        SearchChrome.Background = inputbarBackground ?? theme.SearchBackground;
        SearchChrome.BorderBrush = inputbarBorder ?? theme.SearchBorder;
        SearchChrome.BorderThickness = new Thickness(GetWidgetDistance("inputbar", "border") ?? 0d);
        SearchChrome.CornerRadius = new CornerRadius(GetWidgetDistance("inputbar", "border-radius") ?? Math.Max(0, theme.Settings.CornerRadius - 1));
        SurfaceChrome.BorderThickness = new Thickness(windowBorderThickness);
        SurfaceChrome.Padding = windowPadding;

        QueryBox.Foreground = entryBrush ?? theme.Foreground;
        QueryDisplayText.Foreground = entryBrush ?? theme.Foreground;
        PromptText.Foreground = promptBrush ?? theme.MutedForeground;
        PromptText.Opacity = Math.Clamp(GetWidgetDistance("prompt", "opacity") ?? theme.Settings.PromptOpacity, 0, 1);
        _entryPlaceholderText = entryPlaceholder ?? string.Empty;
        _entryPlaceholderBrush = entryPlaceholderBrush;
        ModeMessageText.Foreground = messageBrush ?? theme.MutedForeground;
        ModeMessageText.Margin = ParseWidgetBox("message", "padding", new Thickness(20, 0, 20, 8));
        ModeMessageText.Visibility = GetWidgetBool("message", "enabled", true) && !string.IsNullOrWhiteSpace(_currentModeMessage)
            ? Visibility.Visible
            : Visibility.Collapsed;

        ApplyWidgetTypography(theme);

        Resources["LauncherPromptBrush"] = promptBrush ?? theme.MutedForeground;
        Resources["LauncherResultForegroundBrush"] = resultBrush ?? theme.Foreground;
        Resources["LauncherResultSecondaryBrush"] = secondaryBrush ?? theme.MutedForeground;
        Resources["LauncherSelectedBrush"] = selectedBackground ?? theme.SelectedBackground;
        Resources["LauncherSelectedBorderBrush"] = selectedBorder ?? theme.SelectedBorder;
        Resources["LauncherSelectedTextBrush"] = selectedText ?? resultBrush ?? theme.Foreground;
        Resources["LauncherSelectedSecondaryBrush"] = selectedSecondary ?? selectedText ?? secondaryBrush ?? theme.MutedForeground;
        Resources["LauncherSurfaceBorderThickness"] = new Thickness(windowBorderThickness);
        Resources["LauncherSurfacePadding"] = windowPadding;
        Resources["LauncherItemBorderThickness"] = new Thickness(_theme.Settings.ShowItemBorders ? (elementBorderThickness ?? 1) : 0);
        Resources["LauncherIconContainerSize"] = theme.Settings.ShowIcons ? iconContainerSize : 0d;
        Resources["LauncherIconBorderThickness"] = new Thickness(iconBorderThickness);
        Resources["LauncherIconCornerRadius"] = new CornerRadius(iconCornerRadius);
        Resources["LauncherScrollBarWidth"] = GetWidgetDistance("scrollbar", "width") ?? 8d;
        Resources["LauncherScrollBarMargin"] = ParseWidgetBox("scrollbar", "margin", new Thickness(8, 10, 10, 10));
        Resources["LauncherScrollBarCornerRadius"] = new CornerRadius(GetWidgetDistance("scrollbar", "border-radius") ?? 999d);
        Resources["LauncherScrollBarThumbMinHeight"] = GetWidgetDistance("scrollbar", "thumb-min-height") ?? 30d;
        Resources["LauncherScrollBarTrackBrush"] = GetWidgetBrush("scrollbar", "background-color") ?? (MediaBrush)Resources["LauncherScrollBarTrackBrush"];
        Resources["LauncherScrollBarTrackBorderBrush"] = GetWidgetBrush("scrollbar", "border-color") ?? (MediaBrush)Resources["LauncherScrollBarTrackBorderBrush"];
        Resources["LauncherScrollBarThumbBrush"] = GetWidgetBrush("scrollbar", "thumb-color") ?? GetWidgetBrush("scrollbar-thumb", "background-color") ?? (MediaBrush)Resources["LauncherScrollBarThumbBrush"];
        Resources["LauncherScrollBarThumbBorderBrush"] = GetWidgetBrush("scrollbar-thumb", "border-color") ?? (MediaBrush)Resources["LauncherScrollBarThumbBorderBrush"];
        Resources["LauncherScrollBarThumbHoverBrush"] = GetWidgetBrush("scrollbar-thumb", "hover-color") ?? (MediaBrush)Resources["LauncherScrollBarThumbHoverBrush"];
        Resources["LauncherScrollBarThumbPressedBrush"] = GetWidgetBrush("scrollbar-thumb", "active-color") ?? (MediaBrush)Resources["LauncherScrollBarThumbPressedBrush"];
        Resources["LauncherOverflowBadgePadding"] = ParseWidgetBox("overflow-indicator", "padding", new Thickness(8, 3, 8, 3));
        Resources["LauncherOverflowBadgeCornerRadius"] = new CornerRadius(GetWidgetDistance("overflow-indicator", "border-radius") ?? 999d);
        Resources["LauncherOverflowTopMargin"] = ParseWidgetBox("overflow-indicator-top", "margin", new Thickness(0, 10, 12, 0));
        Resources["LauncherOverflowBottomMargin"] = ParseWidgetBox("overflow-indicator-bottom", "margin", new Thickness(0, 0, 12, 10));
        Resources["LauncherOverflowBadgeBackgroundBrush"] = GetWidgetBrush("overflow-indicator", "background-color") ?? CreateFrozenBrush(System.Windows.Media.Colors.Transparent);
        Resources["LauncherOverflowBadgeBorderBrush"] = GetWidgetBrush("overflow-indicator", "border-color") ?? CreateFrozenBrush(System.Windows.Media.Colors.Transparent);
        Resources["LauncherOverflowBadgeTextBrush"] = GetWidgetBrush("overflow-indicator", "text-color") ?? theme.Foreground;
        var overflowFont = ResolveWidgetFont("overflow-indicator", theme.Settings.FontFamily, Math.Max(10, theme.Settings.StatusFontSize - 0.5), FontWeights.SemiBold);
        Resources["LauncherOverflowBadgeFontFamily"] = new System.Windows.Media.FontFamily(overflowFont.Family);
        Resources["LauncherOverflowBadgeFontSize"] = overflowFont.Size;
        Resources["LauncherOverflowBadgeFontWeight"] = overflowFont.Weight;

        ResultsList.SetValue(
            ScrollViewer.VerticalScrollBarVisibilityProperty,
            ResolveScrollBarVisibility(GetWidgetBool("listview", "scrollbar", false)));

        ModeSwitcherPanel.Orientation = GetWidgetString("mode-switcher", "orientation")?.Equals("vertical", StringComparison.OrdinalIgnoreCase) is true
            ? System.Windows.Controls.Orientation.Vertical
            : System.Windows.Controls.Orientation.Horizontal;
        ModeSwitcherPanel.Margin = ParseWidgetBox("mode-switcher", "padding", new Thickness(10, 8, 10, 8));
        ApplyListViewPanelLayout();
        QueueOverflowIndicatorRefresh();
    }

    private void ApplyWidgetLayout()
    {
        ApplyMainboxLayout();
        ApplyInputbarLayout();
    }

    private void ApplyMainboxLayout()
    {
        var configuredChildren = ParseWidgetChildren("mainbox");
        var orderedWidgets = configuredChildren.Count == 0
            ? new List<string> { "mode-switcher", "inputbar", "message", "listview" }
            : new List<string>(configuredChildren);

        foreach (var widget in new[] { "mode-switcher", "inputbar", "message", "listview" })
        {
            if (!orderedWidgets.Contains(widget, StringComparer.OrdinalIgnoreCase))
                orderedWidgets.Add(widget);
        }

        var horizontal = string.Equals(GetWidgetString("mainbox", "orientation"), "horizontal", StringComparison.OrdinalIgnoreCase);
        var spacing = GetWidgetDistance("mainbox", "spacing") ?? _theme.Settings.Spacing;
        MainboxGrid.RowDefinitions.Clear();
        MainboxGrid.ColumnDefinitions.Clear();

        if (horizontal)
        {
            var listViewColumn = orderedWidgets.FindIndex(static widget => widget.Equals("listview", StringComparison.OrdinalIgnoreCase));
            for (var column = 0; column < orderedWidgets.Count; column++)
                MainboxGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = column == listViewColumn ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });

            SurfaceBackgroundImage.SetValue(Grid.RowSpanProperty, 1);
            SurfaceBackgroundImage.SetValue(Grid.ColumnSpanProperty, orderedWidgets.Count);
        }
        else
        {
            var listViewRow = orderedWidgets.FindIndex(static widget => widget.Equals("listview", StringComparison.OrdinalIgnoreCase));
            for (var row = 0; row < orderedWidgets.Count; row++)
                MainboxGrid.RowDefinitions.Add(new RowDefinition { Height = row == listViewRow ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });

            SurfaceBackgroundImage.SetValue(Grid.RowSpanProperty, orderedWidgets.Count);
            SurfaceBackgroundImage.SetValue(Grid.ColumnSpanProperty, 1);
        }

        ApplyWidgetSlot("mode-switcher", ModeSwitcherChrome, orderedWidgets, horizontal, spacing);
        ApplyWidgetSlot("inputbar", SearchChrome, orderedWidgets, horizontal, spacing);
        ApplyWidgetSlot("message", ModeMessageText, orderedWidgets, horizontal, spacing);
        ApplyWidgetSlot("listview", ResultsChrome, orderedWidgets, horizontal, spacing);
    }

    private void ApplyInputbarLayout()
    {
        var children = ParseWidgetChildren("inputbar");
        var orderedChildren = children.Count == 0
            ? new List<string> { "prompt", "entry" }
            : new List<string>(children);
        foreach (var child in new[] { "prompt", "entry" })
        {
            if (!orderedChildren.Contains(child, StringComparer.OrdinalIgnoreCase))
                orderedChildren.Add(child);
        }

        var horizontal = !string.Equals(GetWidgetString("inputbar", "orientation"), "vertical", StringComparison.OrdinalIgnoreCase);
        var spacing = GetWidgetDistance("inputbar", "spacing") ?? Math.Max(0, _theme.Settings.Spacing - 2);

        InputbarGrid.RowDefinitions.Clear();
        InputbarGrid.ColumnDefinitions.Clear();

        if (horizontal)
        {
            for (var column = 0; column < orderedChildren.Count; column++)
                InputbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = orderedChildren[column].Equals("entry", StringComparison.OrdinalIgnoreCase) ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });

            ApplyInputbarChildSlot(PromptText, "prompt", orderedChildren, true, spacing);
            ApplyInputbarChildSlot(EntryHost, "entry", orderedChildren, true, spacing);
        }
        else
        {
            for (var row = 0; row < orderedChildren.Count; row++)
                InputbarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            ApplyInputbarChildSlot(PromptText, "prompt", orderedChildren, false, spacing);
            ApplyInputbarChildSlot(EntryHost, "entry", orderedChildren, false, spacing);
        }

        UpdateInputbarPromptLayout();
    }

    private void ApplyListViewPanelLayout()
    {
        var columns = ResolveVisibleResultColumns();
        var lines = ResolveVisibleResultRows();
        var horizontal = string.Equals(GetWidgetString("listview", "orientation"), "horizontal", StringComparison.OrdinalIgnoreCase);

        FrameworkElementFactory panelFactory;
        if (horizontal)
        {
            panelFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.Primitives.UniformGrid));
            panelFactory.SetValue(System.Windows.Controls.Primitives.UniformGrid.RowsProperty, lines);
        }
        else
        {
            panelFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.Primitives.UniformGrid));
            panelFactory.SetValue(System.Windows.Controls.Primitives.UniformGrid.ColumnsProperty, columns);
        }

        ResultsList.ItemsPanel = new ItemsPanelTemplate(panelFactory);
    }

    private int ResolveVisibleResultColumns() =>
        Math.Max(1, (int)(GetWidgetDistance("listview", "columns") ?? _settings.Columns));

    private int ResolveVisibleResultRows() =>
        Math.Max(1, (int)(GetWidgetDistance("listview", "lines") ?? _settings.Launcher.VisibleResults));

    private static void ApplyWidgetSlot(string widgetName, FrameworkElement element, IReadOnlyList<string> orderedWidgets, bool horizontal, double spacing)
    {
        var index = orderedWidgets
            .Select((name, position) => (name, position))
            .FirstOrDefault(item => item.name.Equals(widgetName, StringComparison.OrdinalIgnoreCase))
            .position;

        if (horizontal)
        {
            Grid.SetColumn(element, index);
            Grid.SetRow(element, 0);
            element.Margin = index == 0 ? new Thickness(0) : new Thickness(spacing, 0, 0, 0);
        }
        else
        {
            Grid.SetRow(element, index);
            Grid.SetColumn(element, 0);
            element.Margin = index == 0 ? new Thickness(0) : new Thickness(0, spacing, 0, 0);
        }
    }

    private static void ApplyInputbarChildSlot(FrameworkElement element, string childName, IReadOnlyList<string> orderedChildren, bool horizontal, double spacing)
    {
        var index = orderedChildren
            .Select((name, position) => (name, position))
            .FirstOrDefault(item => item.name.Equals(childName, StringComparison.OrdinalIgnoreCase))
            .position;

        if (horizontal)
        {
            Grid.SetColumn(element, index);
            Grid.SetRow(element, 0);
            element.Margin = new Thickness(0);
        }
        else
        {
            Grid.SetRow(element, index);
            Grid.SetColumn(element, 0);
            element.Margin = new Thickness(0);
        }
    }

    private void UpdateInputbarPromptLayout()
    {
        var horizontal = !string.Equals(GetWidgetString("inputbar", "orientation"), "vertical", StringComparison.OrdinalIgnoreCase);
        var promptVisible = PromptText.Visibility == Visibility.Visible;
        var spacing = promptVisible
            ? GetWidgetDistance("inputbar", "spacing") ?? Math.Max(0, _theme.Settings.Spacing - 2)
            : 0d;

        EntryHost.Margin = horizontal
            ? new Thickness(spacing, 0, 0, 0)
            : new Thickness(0, spacing, 0, 0);
    }

    private List<string> ParseWidgetChildren(string widget)
    {
        var raw = GetWidgetString(widget, "children");
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        return raw
            .Replace("[", string.Empty, StringComparison.Ordinal)
            .Replace("]", string.Empty, StringComparison.Ordinal)
            .Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static child => child.Trim())
            .Where(static child => !string.IsNullOrWhiteSpace(child))
            .ToList();
    }

    private static ScrollBarVisibility ResolveScrollBarVisibility(bool enabled) =>
        enabled ? ScrollBarVisibility.Auto : ScrollBarVisibility.Hidden;

    private static MediaBrush CreateFrozenBrush(System.Windows.Media.Color color)
    {
        var brush = new SolidColorBrush(color);
        if (brush.CanFreeze)
            brush.Freeze();

        return brush;
    }

    private static System.Windows.Media.Color GetBrushColor(MediaBrush brush, System.Windows.Media.Color fallback) =>
        brush switch
        {
            SolidColorBrush solidColorBrush => solidColorBrush.Color,
            _ => fallback
        };

    private static bool IsTransparentBrush(MediaBrush brush) =>
        brush switch
        {
            SolidColorBrush solidColorBrush => solidColorBrush.Color.A == 0,
            _ => false
        };

    private static System.Windows.Media.Color AdjustColorOpacity(System.Windows.Media.Color color, double opacity) =>
        System.Windows.Media.Color.FromArgb(
            (byte)Math.Clamp((int)Math.Round(255 * opacity), 0, 255),
            color.R,
            color.G,
            color.B);

    private string? GetWidgetString(string widget, string propertyName)
    {
        if (_theme.Settings.WidgetProperties.TryGetValue(widget, out var properties) &&
            properties.TryGetValue(propertyName, out var value) &&
            !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (_theme.Settings.WidgetProperties.TryGetValue("*", out var defaults) &&
            defaults.TryGetValue(propertyName, out var defaultValue) &&
            !string.IsNullOrWhiteSpace(defaultValue))
        {
            return defaultValue;
        }

        return null;
    }

    private string? GetWidgetStringExact(string widget, string propertyName)
    {
        if (_theme.Settings.WidgetProperties.TryGetValue(widget, out var properties) &&
            properties.TryGetValue(propertyName, out var value) &&
            !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return null;
    }

    private bool GetWidgetBool(string widget, string propertyName, bool fallback)
    {
        var value = GetWidgetString(widget, propertyName);
        return bool.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private double? GetWidgetDistance(string widget, string propertyName)
    {
        var raw = GetWidgetString(widget, propertyName);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var numeric = new string(raw.TakeWhile(static ch => char.IsDigit(ch) || ch is '.' or '-' or '+').ToArray());
        return double.TryParse(numeric, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private double? GetWidgetDistanceExact(string widget, string propertyName)
    {
        var raw = GetWidgetStringExact(widget, propertyName);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var numeric = new string(raw.TakeWhile(static ch => char.IsDigit(ch) || ch is '.' or '-' or '+').ToArray());
        return double.TryParse(numeric, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private double GetWidgetPaddingHorizontal(string widget, double fallback)
    {
        var values = GetWidgetBoxValues(widget, "padding");
        return values is { Count: > 1 } ? values[1] : values?.FirstOrDefault() ?? fallback;
    }

    private double GetWidgetPaddingVertical(string widget, double fallback)
    {
        var values = GetWidgetBoxValues(widget, "padding");
        return values?.FirstOrDefault() ?? fallback;
    }

    private Thickness ParseWidgetBox(string widget, string propertyName, Thickness fallback)
    {
        var values = GetWidgetBoxValues(widget, propertyName);
        return values switch
        {
            null or { Count: 0 } => fallback,
            { Count: 1 } => new Thickness(values[0]),
            { Count: 2 } => new Thickness(values[1], values[0], values[1], values[0]),
            { Count: 3 } => new Thickness(values[1], values[0], values[1], values[2]),
            _ => new Thickness(values[3], values[0], values[1], values[2])
        };
    }

    private List<double>? GetWidgetBoxValues(string widget, string propertyName)
    {
        var raw = GetWidgetString(widget, propertyName);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var parts = raw
            .Replace("[", string.Empty, StringComparison.Ordinal)
            .Replace("]", string.Empty, StringComparison.Ordinal)
            .Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var values = new List<double>(parts.Length);
        foreach (var part in parts)
        {
            var numeric = new string(part.TakeWhile(static ch => char.IsDigit(ch) || ch is '.' or '-' or '+').ToArray());
            if (double.TryParse(numeric, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                values.Add(parsed);
        }

        return values;
    }

    private MediaBrush? GetWidgetBrush(string widget, string propertyName)
    {
        var raw = GetWidgetString(widget, propertyName);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            var brush = (MediaBrush)new BrushConverter().ConvertFromString(raw)!;
            if (brush.CanFreeze)
                brush.Freeze();

            return brush;
        }
        catch
        {
            return null;
        }
    }

    private MediaBrush? GetWidgetBrushExact(string widget, string propertyName)
    {
        var raw = GetWidgetStringExact(widget, propertyName);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            var brush = (MediaBrush)new BrushConverter().ConvertFromString(raw)!;
            if (brush.CanFreeze)
                brush.Freeze();

            return brush;
        }
        catch
        {
            return null;
        }
    }

    private static Stretch ParseStretch(string stretch) =>
        stretch switch
        {
            "None" => Stretch.None,
            "Fill" => Stretch.Fill,
            "Uniform" => Stretch.Uniform,
            _ => Stretch.UniformToFill
        };

    private static FontWeight ParseFontWeight(string value, FontWeight fallback)
    {
        try
        {
            return new FontWeightConverter().ConvertFromString(value) is FontWeight fontWeight
                ? fontWeight
                : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private void ApplyWidgetTypography(AppTheme theme)
    {
        var searchFont = ResolveWidgetFont("entry", theme.Settings.FontFamily, theme.Settings.SearchFontSize, FontWeights.Medium);
        var promptFont = ResolveWidgetFont("prompt", theme.Settings.FontFamily, theme.Settings.SearchFontSize, FontWeights.Medium);
        var messageFont = ResolveWidgetFont("message", theme.Settings.FontFamily, theme.Settings.StatusFontSize, FontWeights.Normal);
        var titleFont = ResolveWidgetFont("element-text", theme.Settings.FontFamily, theme.Settings.ResultTitleFontSize, ParseFontWeight(theme.Settings.ResultTitleFontWeight, FontWeights.SemiBold));
        var subtitleFont = ResolveWidgetFont("element-text", theme.Settings.FontFamily, theme.Settings.ResultSubtitleFontSize, ParseFontWeight(theme.Settings.ResultSubtitleFontWeight, FontWeights.Normal));
        var buttonFont = ResolveWidgetFont("button", theme.Settings.FontFamily, theme.Settings.FontSize, FontWeights.Normal);
        var statusFont = ResolveWidgetFont("message", theme.Settings.FontFamily, theme.Settings.StatusFontSize, FontWeights.Normal);

        Resources["LauncherSearchFontFamily"] = new System.Windows.Media.FontFamily(searchFont.Family);
        Resources["LauncherSearchFontSize"] = searchFont.Size;
        Resources["LauncherSearchFontWeight"] = searchFont.Weight;
        Resources["LauncherPromptFontFamily"] = new System.Windows.Media.FontFamily(promptFont.Family);
        Resources["LauncherPromptFontWeight"] = promptFont.Weight;
        Resources["LauncherMessageFontFamily"] = new System.Windows.Media.FontFamily(messageFont.Family);
        Resources["LauncherMessageFontSize"] = messageFont.Size;
        Resources["LauncherMessageFontWeight"] = messageFont.Weight;
        Resources["LauncherResultTitleFontFamily"] = new System.Windows.Media.FontFamily(titleFont.Family);
        Resources["LauncherResultTitleFontSize"] = titleFont.Size;
        Resources["LauncherResultTitleFontWeight"] = titleFont.Weight;
        Resources["LauncherResultSubtitleFontFamily"] = new System.Windows.Media.FontFamily(subtitleFont.Family);
        Resources["LauncherResultSubtitleFontSize"] = subtitleFont.Size;
        Resources["LauncherResultSubtitleFontWeight"] = subtitleFont.Weight;
        Resources["LauncherStatusFontFamily"] = new System.Windows.Media.FontFamily(statusFont.Family);
        Resources["LauncherStatusFontSize"] = statusFont.Size;
        Resources["LauncherStatusFontWeight"] = statusFont.Weight;
        Resources["LauncherStatusTitleFontSize"] = Math.Max(statusFont.Size, theme.Settings.StatusTitleFontSize);
        Resources["LauncherStatusTitleFontWeight"] = ParseFontWeight(GetWidgetString("message", "title-font-weight") ?? "SemiBold", FontWeights.SemiBold);

        foreach (var button in ModeSwitcherPanel.Children.OfType<System.Windows.Controls.Button>())
        {
            button.FontFamily = new System.Windows.Media.FontFamily(buttonFont.Family);
            button.FontSize = buttonFont.Size;
            button.FontWeight = buttonFont.Weight;
        }
    }

    private (string Family, double Size, FontWeight Weight) ResolveWidgetFont(string widget, string fallbackFamily, double fallbackSize, FontWeight fallbackWeight)
    {
        var family = GetWidgetString(widget, "font-family");
        var size = GetWidgetDistance(widget, "font-size");
        var weight = ParseFontWeight(GetWidgetString(widget, "font-weight") ?? string.Empty, fallbackWeight);
        var shorthand = ParseWidgetFontShorthand(GetWidgetString(widget, "font"));

        return (
            family ?? shorthand.Family ?? fallbackFamily,
            size ?? shorthand.Size ?? fallbackSize,
            weight);
    }

    private static (string? Family, double? Size) ParseWidgetFontShorthand(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return (null, null);

        var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return (null, null);

        var last = parts[^1].Trim();
        var numeric = new string(last.TakeWhile(static ch => char.IsDigit(ch) || ch is '.' or '-' or '+').ToArray());
        if (!double.TryParse(numeric, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var size))
            return (raw.Trim(), null);

        var family = string.Join(' ', parts[..^1]).Trim();
        return (string.IsNullOrWhiteSpace(family) ? null : family, size);
    }

    private void ApplyCustomThemeResources(AppTheme theme)
    {
        foreach (var key in _appliedThemeResourceKeys)
        {
            if (!theme.CustomResources.ContainsKey(key))
                Resources.Remove(key);
        }

        _appliedThemeResourceKeys.Clear();
        foreach (var resource in theme.CustomResources)
        {
            Resources[resource.Key] = resource.Value;
            _appliedThemeResourceKeys.Add(resource.Key);
        }
    }

    private void PositionLauncher()
    {
        var workingArea = ResolveWorkingArea();
        Width = ResolveLauncherWidth(workingArea);

        var edgeMargin = Math.Max(_theme.Settings.WindowTopOffsetMin, workingArea.Height * _theme.Settings.WindowTopOffsetRatio);
        var anchor = _settings.Launcher.Anchor;

        Left = anchor switch
        {
            "topLeft" or "leftCenter" or "bottomLeft" => workingArea.Left + edgeMargin,
            "topRight" or "rightCenter" or "bottomRight" => workingArea.Right - Width - edgeMargin,
            _ => workingArea.Left + ((workingArea.Width - Width) / 2)
        };

        Top = anchor switch
        {
            "leftCenter" or "center" or "rightCenter" => workingArea.Top + ((workingArea.Height - ActualHeight) / 2),
            "bottomLeft" or "bottomCenter" or "bottomRight" => workingArea.Bottom - ActualHeight - edgeMargin,
            _ => workingArea.Top + edgeMargin
        };

        Left += _settings.Launcher.HorizontalOffset;
        Top += _settings.Launcher.VerticalOffset;
    }

    private double ResolveLauncherWidth(Rect workingArea)
    {
        var width = _theme.Settings.WindowWidth;
        var configuredWidth = _settings.Launcher.Width;

        if (configuredWidth > 0)
        {
            if (_settings.Launcher.WidthMode.Equals("pixels", StringComparison.OrdinalIgnoreCase))
                width = configuredWidth;
            else if (_settings.Launcher.WidthMode.Equals("percent", StringComparison.OrdinalIgnoreCase))
                width = workingArea.Width * (configuredWidth / 100d);
        }

        if (_settings.Launcher.MinWidth > 0)
            width = Math.Max(width, _settings.Launcher.MinWidth);

        if (_settings.Launcher.MaxWidth > 0)
            width = Math.Min(width, _settings.Launcher.MaxWidth);

        return Math.Clamp(width, 240, Math.Max(240, workingArea.Width));
    }

    private double ResolveResultsMaxHeight()
    {
        var visibleRows = ResolveVisibleResultRows();
        if (visibleRows <= 0)
            return double.PositiveInfinity;

        var rowHeight = Math.Max(24, _theme.Settings.RowHeight);
        var rowGap = Math.Max(0, _theme.Settings.ResultGap);
        return (rowHeight * visibleRows) + (rowGap * Math.Max(0, visibleRows - 1));
    }

    private Rect ResolveWorkingArea()
    {
        var mode = _settings.Launcher.PlacementMode;
        if (mode.Equals("primaryScreen", StringComparison.OrdinalIgnoreCase))
        {
            return new Rect(
                SystemParameters.WorkArea.Left,
                SystemParameters.WorkArea.Top,
                SystemParameters.WorkArea.Width,
                SystemParameters.WorkArea.Height);
        }

        var monitorPoint = GetMonitorAnchorPoint(mode);
        var monitor = MonitorFromPoint(monitorPoint, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return new Rect(
                SystemParameters.WorkArea.Left,
                SystemParameters.WorkArea.Top,
                SystemParameters.WorkArea.Width,
                SystemParameters.WorkArea.Height);
        }

        var monitorInfo = new MonitorInfo();
        monitorInfo.Size = Marshal.SizeOf<MonitorInfo>();
        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return new Rect(
                SystemParameters.WorkArea.Left,
                SystemParameters.WorkArea.Top,
                SystemParameters.WorkArea.Width,
                SystemParameters.WorkArea.Height);
        }

        var work = monitorInfo.WorkArea;
        return new Rect(work.Left, work.Top, work.Right - work.Left, work.Bottom - work.Top);
    }

    private PointStruct GetMonitorAnchorPoint(string mode)
    {
        if (mode.Equals("activeScreen", StringComparison.OrdinalIgnoreCase) && IsVisible)
        {
            var centerPoint = PointToScreen(new System.Windows.Point(ActualWidth / 2, ActualHeight / 2));
            return new PointStruct { X = (int)centerPoint.X, Y = (int)centerPoint.Y };
        }

        GetCursorPos(out var cursorPoint);
        return cursorPoint;
    }

    private void OnQueryTextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateQueryDisplayOverlay();

        if (_isUpdatingQueryText)
            return;

        SearchCurrentQuery();
    }

    private async void SearchCurrentQuery()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var searchRequestId = ++_searchRequestId;
        var token = _searchCts.Token;
        var query = NormalizeQueryText(QueryBox.Text);
        _rawQuery = query;
        UpdatePromptVisibility(query);
        _isSearching = !string.IsNullOrWhiteSpace(query);
        _showLoadingIndicator = false;
        UpdateResultsSurfaceState();

        if (string.IsNullOrWhiteSpace(query))
        {
            if (!_settings.Launcher.ShowStartPage)
            {
                _results.Clear();
                _isSearching = false;
                _showLoadingIndicator = false;
                UpdateResultsSurfaceState();
                return;
            }
        }

        ApplyQueryDisplayText(query);
        _ = ShowLoadingIndicatorAfterDelayAsync(searchRequestId, token);

        try
        {
            await Task.Delay(_settings.Search.DebounceMilliseconds, token);
            var searchResult = await _modeRegistry.SearchAsync(query, _activeMode, token);
            if (token.IsCancellationRequested)
                return;

            var requestedMode = string.IsNullOrWhiteSpace(searchResult.SwitchMode)
                ? null
                : _modeRegistry.ResolveMode(searchResult.SwitchMode);
            if (!string.IsNullOrWhiteSpace(requestedMode) &&
                !requestedMode.Equals(searchResult.ActiveModeId, StringComparison.OrdinalIgnoreCase))
            {
                _activeMode = requestedMode;
                _ = Dispatcher.BeginInvoke(new Action(SearchCurrentQuery), DispatcherPriority.Input);
                return;
            }

            _activeMode = searchResult.ActiveModeId;
            ApplySnapshotState(searchResult.Prompt, searchResult.Message, searchResult.Entries, searchResult.ThemeOverlay, searchResult.UrgentIndices, searchResult.ActiveIndices, searchResult.KeepSelection, searchResult.NewSelection, searchResult.UseHotKeys, searchResult.NoCustom, searchResult.MarkupRows, searchResult.DisplayPrefix, searchResult.RawPrefix);
            _isSearching = false;
            _showLoadingIndicator = false;
            UpdateResultsSurfaceState(animateResults: true);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (searchRequestId == _searchRequestId)
            {
                _isSearching = false;
                _showLoadingIndicator = false;
                UpdateResultsSurfaceState();
            }
        }
    }

    private async Task ShowLoadingIndicatorAfterDelayAsync(int searchRequestId, CancellationToken token)
    {
        try
        {
            await Task.Delay(_settings.Search.LoadingIndicatorDelayMilliseconds, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (token.IsCancellationRequested || searchRequestId != _searchRequestId || !_isSearching)
            return;

        _showLoadingIndicator = true;
        UpdateResultsSurfaceState();
    }

    private void RefreshResultTheme()
    {
        if (_results.Count == 0)
        {
            UpdateResultsSurfaceState();
            return;
        }

        var viewModels = _results.Select(result => ResultViewModel.FromResult(result.Source, _theme, _iconChromeBackground, _iconChromeBorder)).ToArray();
        for (var index = 0; index < viewModels.Length; index++)
            viewModels[index].IsLast = index == viewModels.Length - 1;

        var selectedIdentityKey = (ResultsList.SelectedItem as ResultViewModel)?.IdentityKey;
        var selectedIndex = ResultsList.SelectedIndex;
        UpdateResults(viewModels);
        SetSelectedIndexWithoutAnimation(ResolveSelectedIndex(viewModels, selectedIdentityKey, selectedIndex, _settings.Launcher.AutoSelectFirstResult));
        UpdateResultsSurfaceState();
    }

    private string NormalizeQueryText(string text)
    {
        if (!string.IsNullOrEmpty(_currentDisplayPrefix) &&
            !string.IsNullOrEmpty(_currentRawPrefix))
        {
            if (text.StartsWith(_currentDisplayPrefix, StringComparison.Ordinal))
                return _currentRawPrefix + text[_currentDisplayPrefix.Length..];

            var trimmedDisplayPrefix = _currentDisplayPrefix[..^1];
            var trimmedRawPrefix = _currentRawPrefix[..^1];
            if (text.Equals(trimmedDisplayPrefix, StringComparison.Ordinal))
                return trimmedRawPrefix;
        }

        return text;
    }

    private void ApplyQueryDisplayText(string rawQuery)
    {
        var displayText = FormatQueryDisplayText(rawQuery);
        if (QueryBox.Text == displayText)
            return;

        _isUpdatingQueryText = true;
        try
        {
            QueryBox.Text = displayText;
            QueryBox.CaretIndex = displayText.Length;
            QueryBox.Select(displayText.Length, 0);
            UpdateQueryDisplayOverlay();
        }
        finally
        {
            _isUpdatingQueryText = false;
        }
    }

    private void ApplyLauncherMode(string? initialMode)
    {
        _activeMode = string.IsNullOrWhiteSpace(initialMode) ? _modeRegistry.ResolveMode(null) : initialMode;
        var prompt = _settings.DisplayNames.TryGetValue(_activeMode ?? string.Empty, out var configuredPrompt)
            ? configuredPrompt
            ?? _modeRegistry.OrderedModes
                .FirstOrDefault(mode => mode.Id.Equals(_activeMode, StringComparison.OrdinalIgnoreCase))
                ?.DisplayName
            ?? _activeMode
            ?? _settings.DefaultMode
            : _modeRegistry.OrderedModes
                .FirstOrDefault(mode => mode.Id.Equals(_activeMode, StringComparison.OrdinalIgnoreCase))
                ?.DisplayName
            ?? _activeMode
            ?? _settings.DefaultMode;
        ApplyModePrompt(prompt);
        RefreshModeSwitcher();
        ApplyQueryDisplayText(QueryBox.Text);
        SearchCurrentQuery();
    }

    private void ApplyLauncherMode(string? initialMode, bool clearQuery)
    {
        if (clearQuery)
        {
            _rawQuery = string.Empty;
            ApplyQueryDisplayText(string.Empty);
        }

        ApplyLauncherMode(initialMode);
    }

    private void ApplyModePrompt(string? promptOverride = null)
    {
        PromptText.Text = ResolvePromptText(promptOverride);
    }

    private string ResolvePromptText(string? promptOverride = null)
    {
        if (!string.IsNullOrWhiteSpace(promptOverride))
            return promptOverride;

        var activeModeKey = string.IsNullOrWhiteSpace(_activeMode) ? _settings.DefaultMode : _activeMode;
        var configuredDisplayName = _settings.DisplayNames.GetValueOrDefault(activeModeKey ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(configuredDisplayName))
            return configuredDisplayName;

        return _theme.Settings.PromptText;
    }

    private string FormatQueryDisplayText(string rawQuery)
    {
        if (!string.IsNullOrEmpty(_currentRawPrefix) &&
            !string.IsNullOrEmpty(_currentDisplayPrefix) &&
            rawQuery.StartsWith(_currentRawPrefix, StringComparison.Ordinal))
        {
            return _currentDisplayPrefix + rawQuery[_currentRawPrefix.Length..];
        }

        return rawQuery;
    }

    private void UpdateQueryDisplayOverlay()
    {
        QueryDisplayText.Inlines.Clear();

        var text = QueryBox.Text;
        if (string.IsNullOrEmpty(text))
        {
            if (string.IsNullOrWhiteSpace(_entryPlaceholderText))
            {
                QueryDisplayText.Visibility = Visibility.Collapsed;
                return;
            }

            QueryDisplayText.Visibility = Visibility.Visible;
            QueryDisplayText.Inlines.Add(new Run(_entryPlaceholderText) { Foreground = _entryPlaceholderBrush });
            return;
        }

        QueryDisplayText.Visibility = Visibility.Visible;

        var mutedPrefixLength = GetMutedBangPrefixLength(text);
        if (mutedPrefixLength <= 0)
        {
            QueryDisplayText.Visibility = Visibility.Collapsed;
            return;
        }

        QueryDisplayText.Inlines.Add(new Run(text[..mutedPrefixLength]) { Foreground = _theme.MutedForeground });
        QueryDisplayText.Inlines.Add(new Run(text[mutedPrefixLength..]) { Foreground = _theme.Foreground });
    }

    private int GetMutedBangPrefixLength(string text)
        => 0;

    private void UpdateResults(IReadOnlyList<ResultViewModel> viewModels)
    {
        var sharedCount = Math.Min(_results.Count, viewModels.Count);
        for (var index = 0; index < sharedCount; index++)
            _results[index] = viewModels[index];

        while (_results.Count > viewModels.Count)
            _results.RemoveAt(_results.Count - 1);

        for (var index = sharedCount; index < viewModels.Count; index++)
            _results.Add(viewModels[index]);

        QueueDeferredIcons(viewModels);
    }

    private void QueueDeferredIcons(IReadOnlyList<ResultViewModel> viewModels)
    {
        foreach (var viewModel in viewModels)
        {
            if (string.IsNullOrWhiteSpace(viewModel.DeferredIconSource) || viewModel.IconImage is not null)
                continue;

            _ = ResolveDeferredIconAsync(viewModel);
        }
    }

    private async Task ResolveDeferredIconAsync(ResultViewModel viewModel)
    {
        var iconSource = viewModel.DeferredIconSource;
        if (string.IsNullOrWhiteSpace(iconSource))
            return;

        var resolvedIcon = await ResultViewModel.FetchDeferredIconAsync(iconSource, RemoteIconClient).ConfigureAwait(false);
        if (resolvedIcon is null)
            return;

        await Dispatcher.InvokeAsync(() =>
        {
            if (!_results.Contains(viewModel))
                return;

            viewModel.ApplyResolvedIcon(resolvedIcon);
        });
    }

    private void SetSelectedIndexWithoutAnimation(int selectedIndex)
    {
        _suppressSelectionAnimation = true;
        try
        {
            ResultsList.SelectedIndex = selectedIndex;
        }
        finally
        {
            _suppressSelectionAnimation = false;
        }
    }

    private static int ResolveSelectedIndex(
        IReadOnlyList<ResultViewModel> viewModels,
        string? selectedIdentityKey,
        int previousSelectedIndex,
        bool autoSelectFirstResult)
    {
        if (!string.IsNullOrWhiteSpace(selectedIdentityKey))
        {
            for (var index = 0; index < viewModels.Count; index++)
            {
                if (viewModels[index].IdentityKey.Equals(selectedIdentityKey, StringComparison.OrdinalIgnoreCase))
                    return index;
            }
        }

        if (previousSelectedIndex >= 0 && previousSelectedIndex < viewModels.Count)
            return previousSelectedIndex;

        return autoSelectFirstResult && viewModels.Count > 0 ? 0 : -1;
    }

    private IReadOnlyList<string> GetChangedResultKeys(IReadOnlyList<ResultViewModel> viewModels)
    {
        var newKeys = viewModels.Select(static viewModel => viewModel.IdentityKey).ToArray();
        var changedKeys = new List<string>();
        var sharedCount = Math.Min(_lastAnimatedResultKeys.Count, newKeys.Length);

        for (var index = 0; index < sharedCount; index++)
        {
            if (!string.Equals(_lastAnimatedResultKeys[index], newKeys[index], StringComparison.Ordinal))
                changedKeys.Add(newKeys[index]);
        }

        for (var index = sharedCount; index < newKeys.Length; index++)
            changedKeys.Add(newKeys[index]);

        _lastAnimatedResultKeys = newKeys;
        return changedKeys;
    }

    private void PreparePendingResultAnimations(IReadOnlyList<string> changedResultKeys)
    {
        _pendingAnimatedResultKeys.Clear();
        foreach (var key in changedResultKeys)
            _pendingAnimatedResultKeys.Add(key);
    }

    private async void OnQueryBoxKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = await TryHandleLauncherKeyAsync(e, preferTextEditing: true);
    }

    private async void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Handled)
            return;

        e.Handled = await TryHandleLauncherKeyAsync(e, preferTextEditing: ShouldPreferTextEditing(e));
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Handled)
            return;

        var preferTextEditing = ShouldPreferTextEditing(e);
        _ = Dispatcher.BeginInvoke(async () => e.Handled = await TryHandleLauncherKeyAsync(e, preferTextEditing), DispatcherPriority.Input);
    }

    private async Task<bool> TryHandleLauncherKeyAsync(System.Windows.Input.KeyEventArgs e, bool preferTextEditing)
    {
        if (TryHandleModeHotkey(e))
            return true;

        if (MatchesBinding("kb-mode-next", e))
        {
            ApplyLauncherMode(_modeRegistry.GetNextMode(_activeMode ?? _settings.DefaultMode));
            return true;
        }

        if (MatchesBinding("kb-mode-previous", e))
        {
            ApplyLauncherMode(_modeRegistry.GetPreviousMode(_activeMode ?? _settings.DefaultMode));
            return true;
        }

        if (MatchesBinding("kb-mode-complete", e))
        {
            CompleteModeFromQuery();
            return true;
        }

        if (MatchesBinding("kb-row-down", e))
        {
            MoveSelection(1);
            return true;
        }

        if (MatchesBinding("kb-row-up", e))
        {
            MoveSelection(-1);
            return true;
        }

        if (!ShouldDeferToTextBox("kb-row-right", e, preferTextEditing) && MatchesBinding("kb-row-right", e))
        {
            MoveSelection(1);
            return true;
        }

        if (!ShouldDeferToTextBox("kb-row-left", e, preferTextEditing) && MatchesBinding("kb-row-left", e))
        {
            MoveSelection(-1);
            return true;
        }

        if (!ShouldDeferToTextBox("kb-row-first", e, preferTextEditing) && MatchesBinding("kb-row-first", e))
        {
            MoveSelectionAbsolute(0);
            return true;
        }

        if (!ShouldDeferToTextBox("kb-row-last", e, preferTextEditing) && MatchesBinding("kb-row-last", e))
        {
            MoveSelectionAbsolute(_results.Count - 1);
            return true;
        }

        if (MatchesBinding("kb-row-page-down", e))
        {
            MoveSelection(Math.Max(1, _settings.Lines));
            return true;
        }

        if (MatchesBinding("kb-row-page-up", e))
        {
            MoveSelection(-Math.Max(1, _settings.Lines));
            return true;
        }

        if (!ShouldDeferToTextBox("kb-move-front", e, preferTextEditing) && MatchesBinding("kb-move-front", e))
        {
            MoveCaretTo(0);
            return true;
        }

        if (MatchesBinding("kb-move-end", e))
        {
            MoveCaretTo(QueryBox.Text.Length);
            return true;
        }

        if (MatchesBinding("kb-remove-char-back", e))
        {
            RemoveCharacter(backward: true);
            return true;
        }

        if (MatchesBinding("kb-remove-char-forward", e))
        {
            RemoveCharacter(backward: false);
            return true;
        }

        if (MatchesBinding("kb-remove-to-sol", e))
        {
            RemoveToBoundary(start: true);
            return true;
        }

        if (MatchesBinding("kb-remove-to-eol", e))
        {
            RemoveToBoundary(start: false);
            return true;
        }

        if (MatchesBinding("kb-clear-line", e))
        {
            ClearQueryText();
            return true;
        }

        if (MatchesBinding("kb-primary-paste", e) || MatchesBinding("kb-secondary-paste", e))
        {
            PasteClipboardText();
            return true;
        }

        if (MatchesBinding("kb-accept-entry", e))
        {
            await ExecuteSelectedAsync();
            return true;
        }

        if (MatchesBinding("kb-accept-alt", e))
        {
            await ExecuteSelectedAsync();
            return true;
        }

        if (MatchesBinding("kb-accept-custom", e))
        {
            await ExecuteCustomInputAsync();
            return true;
        }

        if (MatchesBinding("kb-delete-entry", e))
        {
            await DeleteSelectedAsync();
            return true;
        }

        if (MatchesBinding("kb-toggle-case-sensitivity", e))
        {
            _settings.CaseSensitive = !_settings.CaseSensitive;
            SearchCurrentQuery();
            return true;
        }

        for (var customKey = 1; customKey <= 5; customKey++)
        {
            if (MatchesBinding($"kb-custom-{customKey}", e))
            {
                await TriggerCustomHotKeyAsync(customKey);
                return true;
            }
        }

        if (MatchesBinding("kb-cancel", e))
        {
            BeginHideLauncher();
            return true;
        }

        return false;
    }

    private bool ShouldDeferToTextBox(string bindingName, System.Windows.Input.KeyEventArgs e, bool preferTextEditing)
    {
        if (!preferTextEditing)
            return false;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        return LauncherTextEditingKeyHelper.ShouldDeferToTextBox(
            bindingName,
            _settings.Keybindings.GetValueOrDefault(bindingName),
            key,
            Keyboard.Modifiers);
    }

    private bool ShouldPreferTextEditing(System.Windows.Input.KeyEventArgs e)
    {
        if (QueryBox.IsKeyboardFocusWithin)
            return true;

        return e.OriginalSource is DependencyObject source &&
               (ReferenceEquals(source, QueryBox) || IsDescendantOfQueryBox(source));
    }

    private bool IsDescendantOfQueryBox(DependencyObject source)
    {
        DependencyObject? current = source;
        while (current is not null)
        {
            if (ReferenceEquals(current, QueryBox))
                return true;

            current = current switch
            {
                Visual visual => VisualTreeHelper.GetParent(visual),
                System.Windows.Media.Media3D.Visual3D visual3D => VisualTreeHelper.GetParent(visual3D),
                _ => LogicalTreeHelper.GetParent(current)
            };
        }

        return false;
    }

    private static T? FindDescendant<T>(DependencyObject root)
        where T : DependencyObject
    {
        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
                return match;

            var nested = FindDescendant<T>(child);
            if (nested is not null)
                return nested;
        }

        return null;
    }

    private bool TryHandleModeHotkey(System.Windows.Input.KeyEventArgs e)
    {
        foreach (var hotkey in _settings.ModeHotkeys)
        {
            if (!KeyboardBindingMatcher.Matches(hotkey.Value, e))
                continue;

            ApplyLauncherMode(hotkey.Key);
            return true;
        }

        return false;
    }

    private bool MatchesBinding(string bindingName, System.Windows.Input.KeyEventArgs e)
    {
        var configured = _settings.Keybindings.GetValueOrDefault(bindingName);
        return KeyboardBindingMatcher.Matches(configured, e);
    }

    private async void OnResultDoubleClick(object sender, MouseButtonEventArgs e)
    {
        await ExecuteSelectedAsync();
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (!_settings.Launcher.CloseOnFocusLoss)
            return;

        if (Environment.TickCount64 < _suppressDeactivateUntilTicks)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (IsVisible)
                {
                    ForceForeground();
                    ScheduleSearchBoxFocus();
                }
            }, DispatcherPriority.Input);
            return;
        }

        BeginHideLauncher();
    }

    private void ArmHotkeyDeactivateGuard()
    {
        _suppressDeactivateUntilTicks = Environment.TickCount64 + 300;
    }

    private void StartOutsideClickWatcher()
    {
        if (!_settings.Launcher.CloseOnFocusLoss || _mouseHook != IntPtr.Zero)
            return;

        using var process = System.Diagnostics.Process.GetCurrentProcess();
        var module = process.MainModule;
        _mouseHook = SetWindowsHookEx(WhMouseLl, _mouseProc, GetModuleHandle(module?.ModuleName), 0);
    }

    private void StopOutsideClickWatcher()
    {
        if (_mouseHook == IntPtr.Zero)
            return;

        UnhookWindowsHookEx(_mouseHook);
        _mouseHook = IntPtr.Zero;
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && IsVisible && IsOutsideClickMessage(wParam))
        {
            var info = Marshal.PtrToStructure<MsllHookStruct>(lParam);
            Dispatcher.BeginInvoke(() =>
            {
                if (IsVisible && !IsPointInsideWindow(info.Point.X, info.Point.Y))
                    BeginHideLauncher();
            }, DispatcherPriority.Input);
        }

        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private bool IsPointInsideWindow(int screenX, int screenY)
    {
        var point = PointFromScreen(new System.Windows.Point(screenX, screenY));
        return point.X >= 0 && point.X <= ActualWidth && point.Y >= 0 && point.Y <= ActualHeight;
    }

    private static bool IsOutsideClickMessage(IntPtr message)
    {
        var value = message.ToInt32();
        return value is WmLButtonDown or WmRButtonDown or WmMButtonDown or WmXButtonDown;
    }

    private void MoveSelection(int delta)
    {
        if (_results.Count == 0)
            return;

        var next = ResultsList.SelectedIndex;
        if (next < 0)
            next = delta >= 0 ? 0 : _results.Count - 1;
        else
        {
            next += delta;
            if (_settings.Launcher.SelectionWrap)
            {
                if (next < 0)
                    next = _results.Count - 1;
                if (next >= _results.Count)
                    next = 0;
            }
            else
            {
                next = Math.Clamp(next, 0, _results.Count - 1);
            }
        }

        ResultsList.SelectedIndex = next;
        ResultsList.ScrollIntoView(ResultsList.SelectedItem);
    }

    private void MoveSelectionAbsolute(int index)
    {
        if (_results.Count == 0)
            return;

        ResultsList.SelectedIndex = Math.Clamp(index, 0, _results.Count - 1);
        ResultsList.ScrollIntoView(ResultsList.SelectedItem);
    }

    private void MoveCaretTo(int index)
    {
        var position = Math.Clamp(index, 0, QueryBox.Text.Length);
        QueryBox.CaretIndex = position;
        QueryBox.Select(position, 0);
        Keyboard.Focus(QueryBox);
    }

    private void RemoveCharacter(bool backward)
    {
        if (!string.IsNullOrEmpty(QueryBox.SelectedText))
        {
            var selectionStart = QueryBox.SelectionStart;
            QueryBox.SelectedText = string.Empty;
            MoveCaretTo(selectionStart);
            return;
        }

        if (backward)
        {
            if (QueryBox.CaretIndex <= 0)
                return;

            var removeIndex = QueryBox.CaretIndex - 1;
            QueryBox.Text = QueryBox.Text.Remove(removeIndex, 1);
            MoveCaretTo(removeIndex);
            return;
        }

        if (QueryBox.CaretIndex >= QueryBox.Text.Length)
            return;

        var caretIndex = QueryBox.CaretIndex;
        QueryBox.Text = QueryBox.Text.Remove(caretIndex, 1);
        MoveCaretTo(caretIndex);
    }

    private void ClearQueryText()
    {
        QueryBox.Text = string.Empty;
        MoveCaretTo(0);
    }

    private void RemoveToBoundary(bool start)
    {
        if (!string.IsNullOrEmpty(QueryBox.SelectedText))
        {
            var selectionStart = QueryBox.SelectionStart;
            QueryBox.SelectedText = string.Empty;
            MoveCaretTo(selectionStart);
            return;
        }

        var caretIndex = QueryBox.CaretIndex;
        if (start)
        {
            if (caretIndex <= 0)
                return;

            QueryBox.Text = QueryBox.Text.Remove(0, caretIndex);
            MoveCaretTo(0);
            return;
        }

        if (caretIndex >= QueryBox.Text.Length)
            return;

        QueryBox.Text = QueryBox.Text[..caretIndex];
        MoveCaretTo(QueryBox.Text.Length);
    }

    private void PasteClipboardText()
    {
        if (!System.Windows.Clipboard.ContainsText())
            return;

        var clipboardText = System.Windows.Clipboard.GetText();
        if (string.IsNullOrEmpty(clipboardText))
            return;

        var selectionStart = QueryBox.SelectionStart;
        var selectionLength = QueryBox.SelectionLength;
        if (selectionLength > 0)
            QueryBox.Text = QueryBox.Text.Remove(selectionStart, selectionLength);

        QueryBox.Text = QueryBox.Text.Insert(selectionStart, clipboardText);
        MoveCaretTo(selectionStart + clipboardText.Length);
    }

    private void CompleteModeFromQuery()
    {
        var needle = _rawQuery.Trim();
        if (string.IsNullOrWhiteSpace(needle))
            return;

        var mode = _modeRegistry.OrderedModes.FirstOrDefault(mode =>
            mode.DisplayName.StartsWith(needle, StringComparison.OrdinalIgnoreCase) ||
            mode.Id.StartsWith(needle, StringComparison.OrdinalIgnoreCase));
        if (mode is null)
            return;

        ApplyLauncherMode(mode.Id, clearQuery: true);
    }

    private async Task ExecuteSelectedAsync()
    {
        var context = new LauncherQueryContext(_rawQuery, _rawQuery.Trim(), _settings, _theme.Settings, _activeMode, _settings.MaxResults);
        var interactiveMode = _modeRegistry.GetMode(_activeMode) as ILauncherInteractiveMode;
        if (ResultsList.SelectedItem is not ResultViewModel selected)
        {
            if (interactiveMode is null || _currentNoCustom || string.IsNullOrWhiteSpace(_rawQuery))
                return;

            var customInteraction = await interactiveMode.SubmitCustomAsync(context, _rawQuery, CancellationToken.None);
            await ApplyInteractionResultAsync(customInteraction).ConfigureAwait(true);
            return;
        }

        if (interactiveMode is not null && selected.LauncherEntry is not null)
        {
            if (selected.LauncherEntry.NonSelectable)
                return;

            var interaction = await interactiveMode.ActivateEntryAsync(context, selected.LauncherEntry, CancellationToken.None);
            await ApplyInteractionResultAsync(interaction).ConfigureAwait(true);
            return;
        }

        var action = selected.Source.Action;
        if (action.RequiresConfirmation)
        {
            var response = System.Windows.MessageBox.Show(
                action.ConfirmationText ?? action.Title,
                "Invoke",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (response != MessageBoxResult.Yes)
                return;
        }

        try
        {
            if (action.Id.StartsWith("mode:", StringComparison.OrdinalIgnoreCase))
            {
                var targetMode = action.Id["mode:".Length..];
                ApplyLauncherMode(targetMode, clearQuery: false);
                return;
            }

            if (await TryHandleStructuredActionAsync(action).ConfigureAwait(true))
            {
                if (ShouldRecordAction(action))
                    RecordRecentLaunch(selected.Source);

                if (_settings.Launcher.CloseAfterAction && ShouldCloseAfterAction(action))
                    BeginHideLauncher();
                return;
            }

            await action.ExecuteAsync(CancellationToken.None);
            RecordRecentLaunch(selected.Source);
            if (_settings.Launcher.CloseAfterAction)
                BeginHideLauncher();
        }
        catch (Exception ex)
        {
            var error = ResultViewModel.FromResult(InvokeResult.Error("Launch", ex.Message), _theme, _iconChromeBackground, _iconChromeBorder);
            error.IsLast = true;
            UpdateResults([error]);
            SetSelectedIndexWithoutAnimation(0);
            UpdateResultsSurfaceState();
        }
    }

    private async Task ExecuteCustomInputAsync()
    {
        var context = new LauncherQueryContext(_rawQuery, _rawQuery.Trim(), _settings, _theme.Settings, _activeMode, _settings.MaxResults);
        var interactiveMode = _modeRegistry.GetMode(_activeMode) as ILauncherInteractiveMode;
        if (interactiveMode is not null && !_currentNoCustom && !string.IsNullOrWhiteSpace(_rawQuery))
        {
            var interaction = await interactiveMode.SubmitCustomAsync(context, _rawQuery, CancellationToken.None);
            await ApplyInteractionResultAsync(interaction).ConfigureAwait(true);
            return;
        }

        await ExecuteSelectedAsync();
    }

    private static bool ShouldRecordAction(InvokeAction action) =>
        action.Kind is InvokeActionKind.Execute or InvokeActionKind.OpenUrl or InvokeActionKind.OpenPath;

    private static bool ShouldCloseAfterAction(InvokeAction action) =>
        action.Kind is not (InvokeActionKind.SetQuery or InvokeActionKind.CopyText);

    private async Task<bool> TryHandleStructuredActionAsync(InvokeAction action)
    {
        switch (action.Kind)
        {
            case InvokeActionKind.Execute:
                return false;
            case InvokeActionKind.OpenUrl:
            case InvokeActionKind.OpenPath:
                if (string.IsNullOrWhiteSpace(action.Target))
                    return true;

                Process.Start(new ProcessStartInfo(action.Target)
                {
                    UseShellExecute = true
                });
                return true;
            case InvokeActionKind.CopyText:
                System.Windows.Clipboard.SetText(action.Target ?? string.Empty);
                return true;
            case InvokeActionKind.SetQuery:
                _rawQuery = action.Target ?? string.Empty;
                ApplyQueryDisplayText(_rawQuery);
                UpdatePromptVisibility(_rawQuery);
                SearchCurrentQuery();
                await Dispatcher.InvokeAsync(ScheduleSearchBoxFocus, DispatcherPriority.Input);
                return true;
            default:
                return false;
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (ResultsList.SelectedItem is not ResultViewModel selected || selected.LauncherEntry is null)
            return;

        var interactiveMode = _modeRegistry.GetMode(_activeMode) as ILauncherInteractiveMode;
        if (interactiveMode is null)
            return;

        var context = new LauncherQueryContext(_rawQuery, _rawQuery.Trim(), _settings, _theme.Settings, _activeMode, _settings.MaxResults);
        var interaction = await interactiveMode.DeleteEntryAsync(context, selected.LauncherEntry, CancellationToken.None);
        await ApplyInteractionResultAsync(interaction).ConfigureAwait(true);
    }

    private async Task TriggerCustomHotKeyAsync(int customKeyIndex)
    {
        if (!_currentUseHotKeys)
            return;

        var interactiveMode = _modeRegistry.GetMode(_activeMode) as ILauncherInteractiveMode;
        if (interactiveMode is null)
            return;

        var selectedEntry = (ResultsList.SelectedItem as ResultViewModel)?.LauncherEntry;
        var context = new LauncherQueryContext(_rawQuery, _rawQuery.Trim(), _settings, _theme.Settings, _activeMode, _settings.MaxResults);
        var interaction = await interactiveMode.HandleCustomKeyAsync(context, selectedEntry, customKeyIndex, CancellationToken.None);
        await ApplyInteractionResultAsync(interaction).ConfigureAwait(true);
    }

    private Task ApplyInteractionResultAsync(LauncherModeInteractionResult interaction)
    {
        if (interaction.CloseLauncher)
        {
            BeginHideLauncher();
            return Task.CompletedTask;
        }

        if (!interaction.KeepFilter)
        {
            _rawQuery = string.Empty;
            ApplyQueryDisplayText(string.Empty);
        }

        if (!string.IsNullOrWhiteSpace(interaction.SwitchMode))
            _activeMode = _modeRegistry.ResolveMode(interaction.SwitchMode);

        if (interaction.Snapshot is { } snapshot &&
            (string.IsNullOrWhiteSpace(interaction.SwitchMode) ||
             string.Equals(_activeMode, snapshot.ModeId, StringComparison.OrdinalIgnoreCase)))
        {
            ApplySnapshotState(snapshot.Prompt, snapshot.Message, snapshot.Entries, snapshot.ThemeOverlay, snapshot.UrgentIndices, snapshot.ActiveIndices, snapshot.KeepSelection, snapshot.NewSelection, snapshot.UseHotKeys, snapshot.NoCustom, snapshot.MarkupRows, snapshot.DisplayPrefix, snapshot.RawPrefix);
            return Task.CompletedTask;
        }

        SearchCurrentQuery();
        return Task.CompletedTask;
    }

    private void ApplySnapshotState(
        string? prompt,
        string? message,
        IReadOnlyList<LauncherModeEntry> entries,
        Invoke.Core.Rasi.RasiDocument? themeOverlay,
        IReadOnlySet<int>? urgentIndices,
        IReadOnlySet<int>? activeIndices,
        bool keepSelection,
        int? newSelection,
        bool useHotKeys,
        bool noCustom,
        bool markupRows,
        string? displayPrefix,
        string? rawPrefix)
    {
        _currentModeMessage = message;
        _currentNoCustom = noCustom;
        _currentUseHotKeys = useHotKeys;
        _currentMarkupRows = markupRows;
        _currentDisplayPrefix = displayPrefix;
        _currentRawPrefix = rawPrefix;
        ApplyModePrompt(prompt);
        ApplyModeOverlay(themeOverlay);
        RefreshModeSwitcher();
        ApplyQueryDisplayText(_rawQuery);

        var viewModels = entries
            .Select((entry, index) => ResultViewModel.FromEntry(
                entry,
                _activeMode,
                _settings,
                _theme,
                _iconChromeBackground,
                _iconChromeBorder,
                markupRows,
                (urgentIndices?.Contains(index) ?? false) || entry.Urgent,
                (activeIndices?.Contains(index) ?? false) || entry.Active))
            .ToArray();
        for (var index = 0; index < viewModels.Length; index++)
            viewModels[index].IsLast = index == viewModels.Length - 1;

        var changedResultKeys = GetChangedResultKeys(viewModels);
        var shouldAnimateResults = changedResultKeys.Count > 0;
        var selectedIdentityKey = keepSelection ? (ResultsList.SelectedItem as ResultViewModel)?.IdentityKey : null;
        var selectedIndex = keepSelection ? ResultsList.SelectedIndex : -1;
        UpdateResults(viewModels);
        var resolvedSelectedIndex = newSelection is { } explicitIndex
            ? Math.Clamp(explicitIndex, viewModels.Length == 0 ? -1 : 0, viewModels.Length == 0 ? -1 : viewModels.Length - 1)
            : ResolveSelectedIndex(viewModels, selectedIdentityKey, selectedIndex, _settings.Launcher.AutoSelectFirstResult);
        SetSelectedIndexWithoutAnimation(resolvedSelectedIndex);
        PreparePendingResultAnimations(changedResultKeys);
        UpdateResultsSurfaceState(animateResults: shouldAnimateResults);
    }

    private void UpdateResultsSurfaceState(bool animateResults = false)
    {
        var hasQuery = !string.IsNullOrWhiteSpace(_rawQuery);
        var hasResults = _results.Count > 0;
        var showSurface = _settings.Launcher.ShowStartPage || hasQuery || hasResults || _isSearching;
        var hasModeMessage = GetWidgetBool("message", "enabled", true) && !string.IsNullOrWhiteSpace(_currentModeMessage);
        var showStatusPanel = !hasResults && _theme.Settings.ShowStatusPanel && (hasQuery || _isSearching);

        ResultsChrome.Visibility = showSurface ? Visibility.Visible : Visibility.Collapsed;
        LoadingBar.Visibility = _showLoadingIndicator ? Visibility.Visible : Visibility.Collapsed;
        ResultsList.Visibility = hasResults ? Visibility.Visible : Visibility.Collapsed;
        StatusPanel.Visibility = showStatusPanel ? Visibility.Visible : Visibility.Collapsed;
        ModeMessageText.Visibility = hasModeMessage ? Visibility.Visible : Visibility.Collapsed;
        ModeMessageText.Text = _currentModeMessage ?? string.Empty;

        if (showStatusPanel)
            UpdateStatusPanel(hasQuery);

        if (animateResults && showSurface)
            AnimateResultsSurface();

        QueueOverflowIndicatorRefresh();
    }

    private void UpdateStatusPanel(bool hasQuery)
    {
        StatusTitleText.Text = GetThemeText(
            _isSearching ? "LauncherSearchingTitle"
                : hasQuery ? "LauncherEmptyTitle"
                : "LauncherWelcomeTitle",
            _isSearching ? "Searching..."
                : hasQuery ? "No matches"
                : "Start typing");

        StatusBodyText.Text = GetThemeText(
            _isSearching ? "LauncherSearchingBody"
                : hasQuery ? "LauncherEmptyBody"
                : "LauncherWelcomeBody",
            _isSearching
                ? "Looking across apps, files, commands, and web."
                : hasQuery
                    ? "Try a shorter query, a bang like !g, or arrow keys once results appear."
                    : "Search apps, files, commands, themes, and web shortcuts from one box.");

        StatusHintText.Text = GetThemeText(
            _isSearching ? "LauncherSearchingHint"
                : hasQuery ? "LauncherEmptyHint"
                : "LauncherWelcomeHint",
            _isSearching
                ? "Esc closes. Typing keeps refining."
                : hasQuery
                    ? "Enter launches top match when results exist."
                    : "Enter opens top result. Esc closes. Up and Down move.");
    }

    private string GetThemeText(string key, string fallback) =>
        Resources[key] as string ?? fallback;

    private void QueueOverflowIndicatorRefresh()
    {
        _ = Dispatcher.BeginInvoke(UpdateOverflowIndicators, DispatcherPriority.Loaded);
    }

    private void UpdateOverflowIndicators()
    {
        AttachResultsScrollViewer();

        var topEnabled = GetWidgetBool("overflow-indicator-top", "enabled", false);
        var bottomEnabled = GetWidgetBool("overflow-indicator-bottom", "enabled", false);
        var hasResults = ResultsList.Visibility == Visibility.Visible && _results.Count > 0;
        if (!hasResults || (!topEnabled && !bottomEnabled))
        {
            OverflowTopBadge.Visibility = Visibility.Collapsed;
            OverflowBottomBadge.Visibility = Visibility.Collapsed;
            return;
        }

        var columns = ResolveVisibleResultColumns();
        var visibleRows = ResolveCurrentVisibleResultRows();
        var totalRows = (int)Math.Ceiling((double)_results.Count / columns);
        var maxFirstVisibleRow = Math.Max(0, totalRows - visibleRows);

        var rowPitch = Math.Max(1, _theme.Settings.RowHeight + Math.Max(0, _theme.Settings.ResultGap));
        var firstVisibleRow = 0;
        if (_resultsScrollViewer is not null)
            firstVisibleRow = (int)Math.Round(_resultsScrollViewer.VerticalOffset / rowPitch, MidpointRounding.AwayFromZero);

        firstVisibleRow = Math.Clamp(firstVisibleRow, 0, maxFirstVisibleRow);

        var aboveCount = Math.Min(_results.Count, firstVisibleRow * columns);
        var visibleCapacity = Math.Min(_results.Count - aboveCount, visibleRows * columns);
        var belowCount = Math.Max(0, _results.Count - aboveCount - visibleCapacity);

        OverflowTopText.Text = $"+{aboveCount}";
        OverflowBottomText.Text = $"+{belowCount}";
        OverflowTopBadge.Visibility = topEnabled && aboveCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        OverflowBottomBadge.Visibility = bottomEnabled && belowCount > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private int ResolveCurrentVisibleResultRows()
    {
        var configuredRows = ResolveVisibleResultRows();
        if (_resultsScrollViewer is null)
            return configuredRows;

        var rowPitch = Math.Max(1, _theme.Settings.RowHeight + Math.Max(0, _theme.Settings.ResultGap));
        var viewportHeight = _resultsScrollViewer.ViewportHeight;
        if (viewportHeight <= 0)
            return configuredRows;

        var visibleRows = (int)Math.Floor((viewportHeight + Math.Max(0, _theme.Settings.ResultGap)) / rowPitch);
        return Math.Clamp(visibleRows, 1, configuredRows);
    }

    private void AttachResultsScrollViewer()
    {
        var scrollViewer = FindDescendant<ScrollViewer>(ResultsList);
        if (ReferenceEquals(_resultsScrollViewer, scrollViewer))
            return;

        if (_resultsScrollViewer is not null)
            _resultsScrollViewer.ScrollChanged -= OnResultsScrollChanged;

        _resultsScrollViewer = scrollViewer;

        if (_resultsScrollViewer is not null)
            _resultsScrollViewer.ScrollChanged += OnResultsScrollChanged;
    }

    private void OnResultsScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalChange == 0 && e.ExtentHeightChange == 0 && e.ViewportHeightChange == 0)
            return;

        UpdateOverflowIndicators();
    }

    private void AnimateResultsSurface()
    {
        if (!_theme.Settings.AnimateResults)
        {
            ResultsHost.Opacity = 1;
            ResultsHostTranslateTransform.Y = 0;
            return;
        }

        ResultsHost.BeginAnimation(OpacityProperty, null);
        ResultsHostTranslateTransform.BeginAnimation(TranslateTransform.YProperty, null);
        ResultsHost.Opacity = 0;
        ResultsHostTranslateTransform.Y = _theme.Settings.ResultsAnimationOffsetY;

        ResultsHost.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(1, TimeSpan.FromMilliseconds(_theme.Settings.ResultsAnimationDurationMilliseconds)));
        ResultsHostTranslateTransform.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(_theme.Settings.ResultsAnimationDurationMilliseconds),
                EasingFunction = CreateEasingFunction(_theme.Settings.ResultsAnimationEasing)
            });
    }

    private void PrepareOpenAnimationState()
    {
        SurfaceHost.BeginAnimation(OpacityProperty, null);
        SurfaceTranslateTransform.BeginAnimation(TranslateTransform.YProperty, null);
        SurfaceScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        SurfaceScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);

        if (!_theme.Settings.AnimateLauncherOpen)
        {
            SurfaceHost.Opacity = 1;
            SurfaceTranslateTransform.Y = 0;
            SurfaceScaleTransform.ScaleX = 1;
            SurfaceScaleTransform.ScaleY = 1;
            return;
        }

        SurfaceHost.Opacity = 0;
        SurfaceTranslateTransform.Y = _theme.Settings.LauncherOpenOffsetY;
        SurfaceScaleTransform.ScaleX = _theme.Settings.LauncherOpenScale;
        SurfaceScaleTransform.ScaleY = _theme.Settings.LauncherOpenScale;
    }

    private void BeginOpenAnimation()
    {
        if (!_theme.Settings.AnimateLauncherOpen)
            return;

        var duration = TimeSpan.FromMilliseconds(_theme.Settings.LauncherOpenDurationMilliseconds);
        var easing = CreateEasingFunction(_theme.Settings.LauncherOpenEasing);
        SurfaceHost.BeginAnimation(OpacityProperty, new DoubleAnimation(1, duration));
        SurfaceTranslateTransform.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation { To = 0, Duration = duration, EasingFunction = easing });
        SurfaceScaleTransform.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            new DoubleAnimation { To = 1, Duration = duration, EasingFunction = easing });
        SurfaceScaleTransform.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            new DoubleAnimation { To = 1, Duration = duration, EasingFunction = easing });
    }

    private void BeginHideLauncher()
    {
        if (!IsVisible || _isClosing)
            return;

        _isClosing = true;
        _focusRetryTimer?.Stop();
        StopOutsideClickWatcher();

        if (!_theme.Settings.AnimateLauncherClose)
        {
            Hide();
            return;
        }

        var duration = TimeSpan.FromMilliseconds(_theme.Settings.LauncherCloseDurationMilliseconds);
        var easing = CreateEasingFunction(_theme.Settings.LauncherCloseEasing);
        var fade = new DoubleAnimation(0, duration);
        fade.Completed += (_, _) =>
        {
            if (_isClosing)
                Hide();
        };

        SurfaceHost.BeginAnimation(OpacityProperty, fade);
        SurfaceTranslateTransform.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation
            {
                To = _theme.Settings.LauncherCloseOffsetY,
                Duration = duration,
                EasingFunction = easing
            });
        SurfaceScaleTransform.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            new DoubleAnimation
            {
                To = _theme.Settings.LauncherCloseScale,
                Duration = duration,
                EasingFunction = easing
            });
        SurfaceScaleTransform.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            new DoubleAnimation
            {
                To = _theme.Settings.LauncherCloseScale,
                Duration = duration,
                EasingFunction = easing
            });
    }

    private void RecordRecentLaunch(InvokeResult result)
    {
        if (!_settings.History.EnableRecentBoost || _settings.History.MaxRecentItems <= 0)
            return;

        var key = RecentLaunchScoring.BuildKey(result.Kind, result.Title, result.Subtitle, result.Action.Title);
        var existing = _settings.History.RecentLaunches
            .FirstOrDefault(entry => entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            existing = new RecentLaunchEntry
            {
                Key = key,
                Title = result.Title,
                Subtitle = result.Subtitle,
                Kind = result.Kind.ToString()
            };
            _settings.History.RecentLaunches.Add(existing);
        }

        existing.LastUsedUtc = DateTime.UtcNow;
        existing.LaunchCount = Math.Clamp(existing.LaunchCount + 1, 1, 10_000);
        _settings.History.RecentLaunches = _settings.History.RecentLaunches
            .OrderByDescending(entry => entry.LastUsedUtc)
            .Take(_settings.History.MaxRecentItems)
            .ToList();
        _configService.SaveSettings(_settings);
    }

    private void OnResultListItemLoaded(object sender, RoutedEventArgs e)
    {
        if (!_theme.Settings.AnimateResults || sender is not ListBoxItem item)
            return;

        if (item.DataContext is not ResultViewModel viewModel ||
            !_pendingAnimatedResultKeys.Remove(viewModel.IdentityKey))
        {
            return;
        }

        var group = EnsureMutableItemTransformGroup(item);
        if (group.Children.Count < 2)
            return;

        if (group.Children[1] is not TranslateTransform translate)
            return;

        item.BeginAnimation(OpacityProperty, null);
        translate.BeginAnimation(TranslateTransform.YProperty, null);

        item.Opacity = 0;
        translate.Y = _theme.Settings.ResultsAnimationOffsetY;

        var itemIndex = ResultsList.ItemContainerGenerator.IndexFromContainer(item);
        item.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(1, TimeSpan.FromMilliseconds(_theme.Settings.ResultsAnimationDurationMilliseconds)));
        translate.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(_theme.Settings.ResultsAnimationDurationMilliseconds),
                EasingFunction = CreateEasingFunction(_theme.Settings.ResultsAnimationEasing),
                BeginTime = TimeSpan.FromMilliseconds(Math.Max(0, itemIndex) * _theme.Settings.ResultsAnimationStaggerMilliseconds)
            });
    }

    private void OnResultsListLoaded(object sender, RoutedEventArgs e)
    {
        AttachResultsScrollViewer();
        QueueOverflowIndicatorRefresh();
    }

    private void OnResultsListSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged || e.HeightChanged)
            QueueOverflowIndicatorRefresh();
    }

    private void OnResultListItemPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem item)
            item.IsSelected = true;
    }

    private void OnResultsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selectedIndex = ResultsList.SelectedIndex;
        if (selectedIndex < 0)
        {
            _lastAnimatedSelectionIndex = -1;
            return;
        }

        if (selectedIndex == _lastAnimatedSelectionIndex)
            return;

        _lastAnimatedSelectionIndex = selectedIndex;

        if (_suppressSelectionAnimation || !_theme.Settings.AnimateSelection)
            return;

        foreach (var item in e.AddedItems.OfType<ResultViewModel>())
        {
            if (ResultsList.ItemContainerGenerator.ContainerFromItem(item) is ListBoxItem container)
                AnimateSelectedContainer(container);
        }
    }

    private void OnResultTextLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBlock textBlock)
            ApplyResultText(textBlock);
    }

    private void OnResultTextDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBlock textBlock)
            ApplyResultText(textBlock);
    }

    private void ApplyResultText(TextBlock textBlock)
    {
        if (textBlock.DataContext is not ResultViewModel viewModel)
            return;

        var isSubtitle = string.Equals(textBlock.Tag as string, "subtitle", StringComparison.OrdinalIgnoreCase);
        var value = isSubtitle ? viewModel.Subtitle : viewModel.Title;
        textBlock.Inlines.Clear();

        if (!viewModel.MarkupRows || string.IsNullOrWhiteSpace(value))
        {
            textBlock.Text = value;
            return;
        }

        textBlock.Text = string.Empty;
        AppendMarkupInlines(textBlock.Inlines, value);
    }

    private void AppendMarkupInlines(InlineCollection inlines, string markup)
    {
        var boldDepth = 0;
        var italicDepth = 0;
        var underlineDepth = 0;
        var colorStack = new Stack<MediaBrush?>();
        var buffer = new System.Text.StringBuilder();

        void Flush()
        {
            if (buffer.Length == 0)
                return;

            var run = new Run(WebUtility.HtmlDecode(buffer.ToString()));
            if (boldDepth > 0)
                run.FontWeight = FontWeights.Bold;
            if (italicDepth > 0)
                run.FontStyle = FontStyles.Italic;
            if (underlineDepth > 0)
                run.TextDecorations = TextDecorations.Underline;
            if (colorStack.TryPeek(out var foreground) && foreground is not null)
                run.Foreground = foreground;

            inlines.Add(run);
            buffer.Clear();
        }

        for (var index = 0; index < markup.Length; index++)
        {
            if (markup[index] != '<')
            {
                buffer.Append(markup[index]);
                continue;
            }

            var endIndex = markup.IndexOf('>', index + 1);
            if (endIndex < 0)
            {
                buffer.Append(markup[index]);
                continue;
            }

            var tag = markup[(index + 1)..endIndex].Trim();
            var closing = tag.StartsWith("/", StringComparison.Ordinal);
            var body = closing ? tag[1..].Trim() : tag;
            Flush();

            if (closing)
            {
                switch (body.ToLowerInvariant())
                {
                    case "b":
                    case "strong":
                        boldDepth = Math.Max(0, boldDepth - 1);
                        break;
                    case "i":
                    case "em":
                        italicDepth = Math.Max(0, italicDepth - 1);
                        break;
                    case "u":
                        underlineDepth = Math.Max(0, underlineDepth - 1);
                        break;
                    case "span":
                        if (colorStack.Count > 0)
                            colorStack.Pop();
                        break;
                }

                index = endIndex;
                continue;
            }

            var tagName = body.Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries)[0].ToLowerInvariant();
            switch (tagName)
            {
                case "b":
                case "strong":
                    boldDepth++;
                    break;
                case "i":
                case "em":
                    italicDepth++;
                    break;
                case "u":
                    underlineDepth++;
                    break;
                case "span":
                    colorStack.Push(ParseMarkupForeground(body));
                    break;
                case "br":
                    inlines.Add(new LineBreak());
                    break;
                default:
                    break;
            }

            index = endIndex;
        }

        Flush();
    }

    private MediaBrush? ParseMarkupForeground(string tagBody)
    {
        foreach (var attributeName in new[] { "foreground", "color", "fgcolor" })
        {
            var marker = attributeName + "=";
            var markerIndex = tagBody.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
                continue;

            var valueStart = markerIndex + marker.Length;
            if (valueStart >= tagBody.Length)
                return null;

            var quote = tagBody[valueStart];
            string raw;
            if (quote is '"' or '\'')
            {
                var valueEnd = tagBody.IndexOf(quote, valueStart + 1);
                if (valueEnd < 0)
                    return null;

                raw = tagBody[(valueStart + 1)..valueEnd];
            }
            else
            {
                var valueEnd = tagBody.IndexOf(' ', valueStart);
                raw = valueEnd < 0 ? tagBody[valueStart..] : tagBody[valueStart..valueEnd];
            }

            try
            {
                var brush = (MediaBrush)new BrushConverter().ConvertFromString(raw)!;
                if (brush.CanFreeze)
                    brush.Freeze();

                return brush;
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private void AnimateSelectedContainer(ListBoxItem container)
    {
        var group = EnsureMutableItemTransformGroup(container);
        if (group.Children.Count < 2)
            return;

        if (group.Children[0] is not ScaleTransform scale || group.Children[1] is not TranslateTransform translate)
            return;

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        translate.BeginAnimation(TranslateTransform.XProperty, null);

        var duration = TimeSpan.FromMilliseconds(_theme.Settings.SelectionAnimationDurationMilliseconds);
        var easing = CreateEasingFunction(_theme.Settings.SelectionAnimationEasing);

        scale.ScaleX = 0.992;
        scale.ScaleY = 0.992;
        translate.X = _theme.Settings.SelectionAnimationOffsetX;

        scale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            new DoubleAnimation { To = 1, Duration = duration, EasingFunction = easing });
        scale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            new DoubleAnimation { To = 1, Duration = duration, EasingFunction = easing });
        translate.BeginAnimation(
            TranslateTransform.XProperty,
            new DoubleAnimation { To = 0, Duration = duration, EasingFunction = easing });

        if (container.Template?.FindName("ItemChrome", container) is Border itemChrome)
        {
            itemChrome.BeginAnimation(OpacityProperty, null);
            itemChrome.Opacity = 0.9;
            itemChrome.BeginAnimation(
                OpacityProperty,
                new DoubleAnimation { To = 1, Duration = duration, EasingFunction = easing });
        }

        if (container.Template?.FindName("SelectionAccent", container) is Border selectionAccent)
        {
            var accentTranslate = EnsureMutableTranslateTransform(selectionAccent, initialX: -6);
            selectionAccent.BeginAnimation(OpacityProperty, null);
            accentTranslate.BeginAnimation(TranslateTransform.XProperty, null);

            selectionAccent.Opacity = 0;
            accentTranslate.X = -6;

            selectionAccent.BeginAnimation(
                OpacityProperty,
                new DoubleAnimation { To = 1, Duration = duration, EasingFunction = easing });
            accentTranslate.BeginAnimation(
                TranslateTransform.XProperty,
                new DoubleAnimation { To = 0, Duration = duration, EasingFunction = easing });
        }
    }

    private static TranslateTransform EnsureMutableTranslateTransform(UIElement element, double initialX = 0)
    {
        if (element.RenderTransform is TranslateTransform translate && !translate.IsFrozen)
            return translate;

        var mutableTranslate = new TranslateTransform(initialX, 0);
        element.RenderTransform = mutableTranslate;
        return mutableTranslate;
    }

    private static TransformGroup EnsureMutableItemTransformGroup(ListBoxItem item)
    {
        if (item.RenderTransform is TransformGroup group &&
            !group.IsFrozen &&
            group.Children.Count >= 2 &&
            group.Children[0] is ScaleTransform scale &&
            group.Children[1] is TranslateTransform translate &&
            !scale.IsFrozen &&
            !translate.IsFrozen)
        {
            return group;
        }

        var mutableGroup = new TransformGroup();
        mutableGroup.Children.Add(new ScaleTransform());
        mutableGroup.Children.Add(new TranslateTransform());
        item.RenderTransform = mutableGroup;
        return mutableGroup;
    }

    private static IEasingFunction? CreateEasingFunction(string? easing) =>
        easing?.Trim().ToLowerInvariant() switch
        {
            "linear" => null,
            "quadraticin" => new QuadraticEase { EasingMode = EasingMode.EaseIn },
            "quadraticout" => new QuadraticEase { EasingMode = EasingMode.EaseOut },
            "quadraticinout" => new QuadraticEase { EasingMode = EasingMode.EaseInOut },
            "cubicin" => new CubicEase { EasingMode = EasingMode.EaseIn },
            "cubicinout" => new CubicEase { EasingMode = EasingMode.EaseInOut },
            "backout" => new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.22 },
            "backinout" => new BackEase { EasingMode = EasingMode.EaseInOut, Amplitude = 0.18 },
            "circleout" => new CircleEase { EasingMode = EasingMode.EaseOut },
            "circleinout" => new CircleEase { EasingMode = EasingMode.EaseInOut },
            _ => new CubicEase { EasingMode = EasingMode.EaseOut }
        };

    private const int SwShownormal = 1;

    private static void ForceForegroundWindow(IntPtr handle)
    {
        var foregroundWindow = GetForegroundWindow();
        var currentThreadId = GetCurrentThreadId();
        var foregroundThreadId = foregroundWindow == IntPtr.Zero
            ? 0
            : GetWindowThreadProcessId(foregroundWindow, IntPtr.Zero);

        if (foregroundThreadId != 0 && foregroundThreadId != currentThreadId)
            AttachThreadInput(currentThreadId, foregroundThreadId, true);

        try
        {
            SetForegroundWindow(handle);
        }
        finally
        {
            if (foregroundThreadId != 0 && foregroundThreadId != currentThreadId)
                AttachThreadInput(currentThreadId, foregroundThreadId, false);
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SetActiveWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SetFocus(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out PointStruct lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(PointStruct pt, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct MsllHookStruct
    {
        public PointStruct Point;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PointStruct
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RectStruct
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public RectStruct MonitorArea;
        public RectStruct WorkArea;
        public uint Flags;
    }

    private sealed class ResultViewModel : INotifyPropertyChanged
    {
        private static readonly ConcurrentDictionary<string, Task<BitmapSource?>> DeferredIconTasks = new(StringComparer.OrdinalIgnoreCase);
        private BitmapSource? _iconImage;
        private Visibility _iconVisibility;
        private Visibility _fallbackIconVisibility;

        public event PropertyChangedEventHandler? PropertyChanged;

        public required InvokeResult Source { get; init; }
        public LauncherModeEntry? LauncherEntry { get; set; }
        public bool MarkupRows { get; set; }
        public bool IsUrgent { get; set; }
        public bool IsActive { get; set; }
        public bool IsNonSelectable { get; set; }
        public required string Title { get; init; }
        public required string Subtitle { get; init; }
        public required string IconGlyph { get; init; }
        public required string IdentityKey { get; set; }
        public required string? DeferredIconSource { get; init; }
        public BitmapSource? IconImage
        {
            get => _iconImage;
            private set
            {
                if (ReferenceEquals(_iconImage, value))
                    return;

                _iconImage = value;
                OnPropertyChanged(nameof(IconImage));
            }
        }
        public required BitmapImage FallbackIconImage { get; init; }
        public Visibility IconVisibility
        {
            get => _iconVisibility;
            private set
            {
                if (_iconVisibility == value)
                    return;

                _iconVisibility = value;
                OnPropertyChanged(nameof(IconVisibility));
            }
        }
        public Visibility FallbackIconVisibility
        {
            get => _fallbackIconVisibility;
            private set
            {
                if (_fallbackIconVisibility == value)
                    return;

                _fallbackIconVisibility = value;
                OnPropertyChanged(nameof(FallbackIconVisibility));
            }
        }
        public required Visibility OneLineVisibility { get; init; }
        public required Visibility TwoLineVisibility { get; init; }
        public required MediaBrush IconBackground { get; init; }
        public required MediaBrush IconBorder { get; init; }
        public required MediaBrush IconForeground { get; init; }
        public MediaBrush RowBackground { get; set; } = System.Windows.Media.Brushes.Transparent;
        public MediaBrush RowBorder { get; set; } = System.Windows.Media.Brushes.Transparent;
        public MediaBrush TitleForeground { get; set; } = System.Windows.Media.Brushes.Transparent;
        public MediaBrush SubtitleForeground { get; set; } = System.Windows.Media.Brushes.Transparent;
        public double RowOpacity { get; set; } = 1d;
        public required Thickness ResultGapMargin { get; init; }
        public bool IsLast { get; set; }
        public Thickness RowMargin => IsLast ? new Thickness(0) : ResultGapMargin;

        public static ResultViewModel FromEntry(LauncherModeEntry entry, string? modeId, InvokeSettings settings, AppTheme theme, MediaBrush iconBackground, MediaBrush iconBorder, bool markupRows, bool isUrgent, bool isActive)
        {
            var title = FormatModeEntryValue(settings.ResultTitleTemplates.GetValueOrDefault(modeId ?? string.Empty), entry, modeId, entry.DisplayText);
            var subtitle = FormatModeEntryValue(settings.ResultSubtitleTemplates.GetValueOrDefault(modeId ?? string.Empty), entry, modeId, entry.SecondaryText);
            var viewModel = FromResult(
                new InvokeResult(
                    title,
                    subtitle,
                    entry.Kind,
                    entry.Score,
                    entry.Action,
                    entry.Icon),
                theme,
                iconBackground,
                iconBorder);
            viewModel.LauncherEntry = entry;
            viewModel.IdentityKey = entry.IdentityKey;
            viewModel.MarkupRows = markupRows;
            viewModel.IsUrgent = isUrgent;
            viewModel.IsActive = isActive;
            viewModel.IsNonSelectable = entry.NonSelectable;
            viewModel.RowBackground = ResolveWidgetBrush(theme, "element", isUrgent, isActive, "background-color") ?? System.Windows.Media.Brushes.Transparent;
            viewModel.RowBorder = ResolveWidgetBrush(theme, "element", isUrgent, isActive, "border-color") ?? System.Windows.Media.Brushes.Transparent;
            viewModel.TitleForeground = ResolveWidgetBrush(theme, "element-text", isUrgent, isActive, "text-color") ?? theme.Foreground;
            viewModel.SubtitleForeground = ResolveWidgetBrush(theme, "element-text", isUrgent, isActive, "placeholder-color")
                ?? ResolveWidgetBrush(theme, "element-text", isUrgent, isActive, "text-color")
                ?? theme.MutedForeground;
            viewModel.RowOpacity = entry.NonSelectable ? 0.55 : 1d;
            return viewModel;
        }

        private static string FormatModeEntryValue(string? template, LauncherModeEntry entry, string? modeId, string fallback) =>
            ModeEntryFormatter.Format(
                template,
                fallback,
                entry.Text,
                entry.DisplayText,
                entry.SecondaryText,
                entry.Meta,
                entry.Info,
                entry.CompletionText,
                entry.Icon,
                entry.Kind.ToString(),
                modeId);

        public static ResultViewModel FromResult(InvokeResult result, AppTheme theme, MediaBrush iconBackground, MediaBrush iconBorder)
        {
            var iconImage = LoadResultIcon(result, out var deferredIconSource);
            var isOneLine = theme.Settings.ResultLayout.Equals("oneLine", StringComparison.OrdinalIgnoreCase);

            return new()
            {
                Source = result,
                LauncherEntry = null,
                MarkupRows = false,
                Title = result.Title,
                Subtitle = result.Subtitle,
                IdentityKey = BuildIdentityKey(result),
                DeferredIconSource = deferredIconSource,
                IconImage = iconImage,
                FallbackIconImage = LoadFallbackIcon(result.Kind),
                IconVisibility = iconImage is null ? Visibility.Collapsed : Visibility.Visible,
                FallbackIconVisibility = iconImage is null ? Visibility.Visible : Visibility.Collapsed,
                OneLineVisibility = isOneLine ? Visibility.Visible : Visibility.Collapsed,
                TwoLineVisibility = isOneLine ? Visibility.Collapsed : Visibility.Visible,
                IconBackground = iconBackground,
                IconBorder = iconBorder,
                IconForeground = result.Kind == ResultKind.Error ? theme.Foreground : theme.Accent,
                RowBackground = System.Windows.Media.Brushes.Transparent,
                RowBorder = System.Windows.Media.Brushes.Transparent,
                TitleForeground = theme.Foreground,
                SubtitleForeground = theme.MutedForeground,
                RowOpacity = 1d,
                ResultGapMargin = new Thickness(0, 0, 0, theme.Settings.ResultGap),
                IconGlyph = result.Kind switch
                {
                    ResultKind.App => "A",
                    ResultKind.File => "F",
                    ResultKind.Folder => "D",
                    ResultKind.Web => "W",
                    ResultKind.Install => "+",
                    ResultKind.Uninstall => "-",
                    ResultKind.Config => "*",
                    ResultKind.Command => ">",
                    ResultKind.Error => "!",
                    _ => "."
                }
            };
        }

        public void ApplyResolvedIcon(BitmapSource icon)
        {
            IconImage = icon;
            IconVisibility = Visibility.Visible;
            FallbackIconVisibility = Visibility.Collapsed;
        }

        private static string BuildIdentityKey(InvokeResult result) =>
            $"{result.Kind}|{result.Title}|{result.Subtitle}|{result.Action.Title}|{result.Icon}";

        private static MediaBrush? ResolveWidgetBrush(AppTheme theme, string widget, bool isUrgent, bool isActive, string propertyName)
        {
            foreach (var selector in BuildWidgetSelectorCandidates(widget, isUrgent, isActive))
            {
                if (!theme.Settings.WidgetProperties.TryGetValue(selector, out var properties) ||
                    !properties.TryGetValue(propertyName, out var raw) ||
                    string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                try
                {
                    var brush = (MediaBrush)new BrushConverter().ConvertFromString(raw)!;
                    if (brush.CanFreeze)
                        brush.Freeze();

                    return brush;
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        private static IEnumerable<string> BuildWidgetSelectorCandidates(string widget, bool isUrgent, bool isActive)
        {
            if (isUrgent && isActive)
            {
                yield return $"{widget} active urgent";
                yield return $"{widget} urgent active";
                yield return $"{widget}.active.urgent";
                yield return $"{widget}.urgent.active";
            }

            if (isUrgent)
            {
                yield return $"{widget} urgent";
                yield return $"{widget}.urgent";
            }

            if (isActive)
            {
                yield return $"{widget} active";
                yield return $"{widget}.active";
            }

            yield return widget;
            yield return "*";
        }

        public static Task<BitmapSource?> FetchDeferredIconAsync(string iconSource, HttpClient httpClient)
        {
            return DeferredIconTasks.GetOrAdd(iconSource, source => FetchDeferredIconCachedAsync(source, httpClient));
        }

        private static BitmapSource? LoadResultIcon(InvokeResult result, out string? deferredIconSource)
        {
            deferredIconSource = ResolveDeferredIconSource(result.Icon);
            return LoadIconUri(result.Icon) ?? LoadIconPath(result.Icon) ?? LoadShellIcon(result);
        }

        private static string? ResolveDeferredIconSource(string? icon)
        {
            if (string.IsNullOrWhiteSpace(icon))
                return null;

            if (TryResolveRemoteIconUri(icon, out var remoteUri))
            {
                var cacheKey = remoteUri.AbsoluteUri;
                return ShellIconCache.GetUri(cacheKey) is null ? cacheKey : null;
            }

            return null;
        }

        private static BitmapImage? LoadIconUri(string? icon)
        {
            if (!TryResolveRemoteIconUri(icon, out var uri))
                return null;

            var cacheKey = uri.AbsoluteUri;
            if (ShellIconCache.GetUri(cacheKey) is BitmapImage cachedImage)
                return cachedImage;

            if (!uri.IsFile)
                return null;

            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = uri;
                image.EndInit();
                image.Freeze();
                ShellIconCache.SetUri(cacheKey, image);
                return image;
            }
            catch
            {
                return null;
            }
        }

        private static BitmapSource? LoadIconPath(string? icon)
        {
            if (string.IsNullOrWhiteSpace(icon))
                return null;

            if (icon.StartsWith("favicon:", StringComparison.OrdinalIgnoreCase))
                return null;

            if (icon.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
                return LoadShellIconForPath(icon);

            var expanded = Environment.ExpandEnvironmentVariables(icon.Trim().Trim('"'));
            return System.IO.File.Exists(expanded) || System.IO.Directory.Exists(expanded)
                ? LoadShellIconForPath(expanded)
                : null;
        }

        private static BitmapImage LoadFallbackIcon(ResultKind kind)
        {
            return FallbackIcons.GetOrAdd(kind, static kind =>
            {
                var fileName = kind switch
                {
                    ResultKind.App => "app.png",
                    ResultKind.File => "file.png",
                    ResultKind.Folder => "folder.png",
                    ResultKind.Web => "web.png",
                    ResultKind.Install => "install.png",
                    ResultKind.Uninstall => "uninstall.png",
                    ResultKind.Config => "config.png",
                    ResultKind.Command => "command.png",
                    ResultKind.Error => "error.png",
                    _ => "command.png"
                };

                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri($"pack://application:,,,/Assets/Icons/{fileName}", UriKind.Absolute);
                image.EndInit();
                image.Freeze();
                return image;
            });
        }

        private static bool TryResolveRemoteIconUri(string? icon, out Uri uri)
        {
            uri = null!;
            if (string.IsNullOrWhiteSpace(icon))
                return false;

            var trimmed = icon.Trim();
            if (trimmed.StartsWith("favicon:", StringComparison.OrdinalIgnoreCase))
            {
                var siteValue = trimmed["favicon:".Length..].Trim();
                if (!Uri.TryCreate(siteValue, UriKind.Absolute, out var siteUri) ||
                    siteUri.Scheme is not ("http" or "https"))
                {
                    return false;
                }

                uri = new Uri($"{siteUri.Scheme}://{siteUri.Host}/favicon.ico", UriKind.Absolute);
                return true;
            }

            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var parsedUri))
                return false;

            if (parsedUri.Scheme is not ("http" or "https" or "file"))
                return false;

            uri = parsedUri;
            return true;
        }

        private static async Task<BitmapSource?> FetchDeferredIconCoreAsync(string iconSource, HttpClient httpClient)
        {
            try
            {
                if (!Uri.TryCreate(iconSource, UriKind.Absolute, out var uri))
                    return null;

                if (uri.IsFile)
                {
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.UriSource = uri;
                    image.EndInit();
                    image.Freeze();
                    ShellIconCache.SetUri(iconSource, image);
                    return image;
                }

                using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using var memory = new MemoryStream();
                await stream.CopyToAsync(memory).ConfigureAwait(false);
                memory.Position = 0;

                var imageFromBytes = new BitmapImage();
                imageFromBytes.BeginInit();
                imageFromBytes.CacheOption = BitmapCacheOption.OnLoad;
                imageFromBytes.StreamSource = memory;
                imageFromBytes.EndInit();
                imageFromBytes.Freeze();
                ShellIconCache.SetUri(iconSource, imageFromBytes);
                return imageFromBytes;
            }
            catch
            {
                return null;
            }
        }

        private static async Task<BitmapSource?> FetchDeferredIconCachedAsync(string iconSource, HttpClient httpClient)
        {
            var icon = await FetchDeferredIconCoreAsync(iconSource, httpClient).ConfigureAwait(false);
            if (icon is null)
                DeferredIconTasks.TryRemove(iconSource, out _);

            return icon;
        }

        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private static BitmapSource? LoadShellIcon(InvokeResult result)
        {
            if (result.Kind is not (ResultKind.App or ResultKind.File or ResultKind.Folder))
                return null;

            var path = result.Subtitle;
            if (string.IsNullOrWhiteSpace(path))
                return null;

            if (ShellIconCache.Get(path) is { } cachedIcon)
                return cachedIcon;

            var icon = LoadShellIconForPath(path);
            if (icon is not null)
                ShellIconCache.Set(path, icon);

            return icon;
        }

        private static BitmapSource? LoadShellIconForPath(string path)
        {
            if (System.IO.File.Exists(path) || System.IO.Directory.Exists(path))
            {
                var info = new ShFileInfo();
                var resultPtr = SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<ShFileInfo>(), ShgfiIcon | ShgfiLargeIcon);
                if (resultPtr == IntPtr.Zero || info.IconHandle == IntPtr.Zero)
                    return null;

                try
                {
                    var source = Imaging.CreateBitmapSourceFromHIcon(info.IconHandle, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(32, 32));
                    source.Freeze();
                    return source;
                }
                finally
                {
                    DestroyIcon(info.IconHandle);
                }
            }

            if (!path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
                return null;

            try
            {
                var parseResult = SHParseDisplayName(path, IntPtr.Zero, out var itemIdList, 0, out _);
                if (parseResult != 0 || itemIdList == IntPtr.Zero)
                    return null;

                try
                {
                    var info = new ShFileInfo();
                    var resultPtr = SHGetFileInfo(itemIdList, 0, ref info, (uint)Marshal.SizeOf<ShFileInfo>(), ShgfiPidl | ShgfiIcon | ShgfiLargeIcon);
                    if (resultPtr == IntPtr.Zero || info.IconHandle == IntPtr.Zero)
                        return null;

                    try
                    {
                        var source = Imaging.CreateBitmapSourceFromHIcon(info.IconHandle, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(32, 32));
                        source.Freeze();
                        return source;
                    }
                    finally
                    {
                        DestroyIcon(info.IconHandle);
                    }
                }
                finally
                {
                    CoTaskMemFree(itemIdList);
                }
            }
            catch
            {
                return null;
            }
        }

        private static readonly ConcurrentDictionary<ResultKind, BitmapImage> FallbackIcons = new();

        private const uint ShgfiIcon = 0x000000100;
        private const uint ShgfiLargeIcon = 0x000000000;
        private const uint ShgfiPidl = 0x000000008;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(string path, uint fileAttributes, ref ShFileInfo fileInfo, uint fileInfoSize, uint flags);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHParseDisplayName(string name, IntPtr bindingContext, out IntPtr itemIdList, uint sfgaoIn, out uint sfgaoOut);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(IntPtr itemIdList, uint fileAttributes, ref ShFileInfo fileInfo, uint fileInfoSize, uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr icon);

        [DllImport("ole32.dll")]
        private static extern void CoTaskMemFree(IntPtr ptr);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ShFileInfo
        {
            public IntPtr IconHandle;
            public int IconIndex;
            public uint Attributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string DisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string TypeName;
        }
    }
}
