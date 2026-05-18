using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using TextChangedEventArgs = System.Windows.Controls.TextChangedEventArgs;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfRectangle = System.Windows.Shapes.Rectangle;

namespace Invoke.App.Controls;

public sealed class SmoothCaretTextBox : WpfTextBox
{
    private const string CaretPartName = "PART_SmoothCaret";

    private WpfRectangle? _caret;
    private TranslateTransform? _caretTransform;
    private DispatcherTimer? _typingBlinkPauseTimer;

    static SmoothCaretTextBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(SmoothCaretTextBox),
            new FrameworkPropertyMetadata(typeof(SmoothCaretTextBox)));
    }

    public static readonly DependencyProperty CaretAnimationDurationMillisecondsProperty =
        DependencyProperty.Register(
            nameof(CaretAnimationDurationMilliseconds),
            typeof(int),
            typeof(SmoothCaretTextBox),
            new PropertyMetadata(85));

    public static readonly DependencyProperty CaretBlinkMillisecondsProperty =
        DependencyProperty.Register(
            nameof(CaretBlinkMilliseconds),
            typeof(int),
            typeof(SmoothCaretTextBox),
            new PropertyMetadata(1350, OnCaretBlinkPropertyChanged));

    public static readonly DependencyProperty CaretTypingBlinkPauseMillisecondsProperty =
        DependencyProperty.Register(
            nameof(CaretTypingBlinkPauseMilliseconds),
            typeof(int),
            typeof(SmoothCaretTextBox),
            new PropertyMetadata(650));

    public static readonly DependencyProperty CaretWidthProperty =
        DependencyProperty.Register(
            nameof(CaretWidth),
            typeof(double),
            typeof(SmoothCaretTextBox),
            new PropertyMetadata(1.5));

    public static readonly DependencyProperty CaretCornerRadiusProperty =
        DependencyProperty.Register(
            nameof(CaretCornerRadius),
            typeof(double),
            typeof(SmoothCaretTextBox),
            new PropertyMetadata(0.75));

    public static readonly DependencyProperty CaretMinHeightProperty =
        DependencyProperty.Register(
            nameof(CaretMinHeight),
            typeof(double),
            typeof(SmoothCaretTextBox),
            new PropertyMetadata(18.0));

    public static readonly DependencyProperty CaretAnimationEasingProperty =
        DependencyProperty.Register(
            nameof(CaretAnimationEasing),
            typeof(string),
            typeof(SmoothCaretTextBox),
            new PropertyMetadata("CubicOut"));

    public int CaretAnimationDurationMilliseconds
    {
        get => (int)GetValue(CaretAnimationDurationMillisecondsProperty);
        set => SetValue(CaretAnimationDurationMillisecondsProperty, value);
    }

    public int CaretBlinkMilliseconds
    {
        get => (int)GetValue(CaretBlinkMillisecondsProperty);
        set => SetValue(CaretBlinkMillisecondsProperty, value);
    }

    public int CaretTypingBlinkPauseMilliseconds
    {
        get => (int)GetValue(CaretTypingBlinkPauseMillisecondsProperty);
        set => SetValue(CaretTypingBlinkPauseMillisecondsProperty, value);
    }

    public double CaretWidth
    {
        get => (double)GetValue(CaretWidthProperty);
        set => SetValue(CaretWidthProperty, value);
    }

    public double CaretCornerRadius
    {
        get => (double)GetValue(CaretCornerRadiusProperty);
        set => SetValue(CaretCornerRadiusProperty, value);
    }

    public double CaretMinHeight
    {
        get => (double)GetValue(CaretMinHeightProperty);
        set => SetValue(CaretMinHeightProperty, value);
    }

    public string CaretAnimationEasing
    {
        get => (string)GetValue(CaretAnimationEasingProperty);
        set => SetValue(CaretAnimationEasingProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _caret = GetTemplateChild(CaretPartName) as WpfRectangle;

        if (_caret is not null)
        {
            _caretTransform = GetMutableCaretTransform(_caret);
            _caret.RenderTransformOrigin = new System.Windows.Point(0, 0);
            _caret.IsHitTestVisible = false;
            StartBlinkAnimation();
        }

        QueueCaretUpdate(animate: false);
    }

    protected override void OnGotKeyboardFocus(System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        PauseBlinkTemporarily();
        QueueCaretUpdate(animate: false);
    }

    protected override void OnLostKeyboardFocus(System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        UpdateCaretVisibility();
    }

    protected override void OnTextChanged(TextChangedEventArgs e)
    {
        base.OnTextChanged(e);
        PauseBlinkTemporarily();
        QueueCaretUpdate(animate: true);
    }

    protected override void OnSelectionChanged(RoutedEventArgs e)
    {
        base.OnSelectionChanged(e);
        PauseBlinkTemporarily();
        QueueCaretUpdate(animate: true);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        QueueCaretUpdate(animate: false);
    }

    private void QueueCaretUpdate(bool animate)
    {
        if (!IsLoaded)
            return;

        Dispatcher.BeginInvoke(
            () => UpdateCaretPosition(animate),
            DispatcherPriority.Render);
    }

    private void UpdateCaretPosition(bool animate)
    {
        if (_caret is null || _caretTransform is null)
            return;

        if (_caretTransform.IsFrozen)
            _caretTransform = GetMutableCaretTransform(_caret);

        UpdateCaretVisibility();
        if (_caret.Visibility != Visibility.Visible)
            return;

        var rect = GetCaretRect();
        if (rect == Rect.Empty)
            return;

        _caret.Height = Math.Max(CaretMinHeight, rect.Height);
        _caret.Width = CaretWidth;
        _caret.RadiusX = CaretCornerRadius;
        _caret.RadiusY = CaretCornerRadius;

        if (animate)
        {
            AnimateAxis(TranslateTransform.XProperty, rect.X);
            AnimateAxis(TranslateTransform.YProperty, rect.Y);
        }
        else
        {
            _caretTransform.BeginAnimation(TranslateTransform.XProperty, null);
            _caretTransform.BeginAnimation(TranslateTransform.YProperty, null);
            _caretTransform.X = rect.X;
            _caretTransform.Y = rect.Y;
        }
    }

    private Rect GetCaretRect()
    {
        var index = Math.Clamp(CaretIndex, 0, Text.Length);

        Rect rect;
        if (Text.Length == 0)
        {
            rect = Rect.Empty;
        }
        else if (index >= Text.Length)
        {
            rect = GetRectFromCharacterIndex(Text.Length - 1, trailingEdge: true);
        }
        else
        {
            rect = GetRectFromCharacterIndex(index, trailingEdge: false);
        }

        if (rect == Rect.Empty && index > 0)
            rect = GetRectFromCharacterIndex(index - 1, trailingEdge: true);

        if (rect != Rect.Empty && !double.IsInfinity(rect.X) && !double.IsNaN(rect.X))
            return rect;

        var fallbackHeight = Math.Max(CaretMinHeight, FontSize * 1.25);
        var fallbackY = Math.Max(0, (ActualHeight - fallbackHeight) / 2);
        return new Rect(Padding.Left, fallbackY, CaretWidth, fallbackHeight);
    }

    private void AnimateAxis(DependencyProperty property, double to)
    {
        var animation = new DoubleAnimation
        {
            To = to,
            Duration = TimeSpan.FromMilliseconds(CaretAnimationDurationMilliseconds),
            EasingFunction = CreateEasingFunction(CaretAnimationEasing),
            FillBehavior = FillBehavior.HoldEnd
        };

        _caretTransform?.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private static TranslateTransform GetMutableCaretTransform(WpfRectangle caret)
    {
        if (caret.RenderTransform is TranslateTransform transform && !transform.IsFrozen)
            return transform;

        transform = new TranslateTransform();
        caret.RenderTransform = transform;
        return transform;
    }

    private void UpdateCaretVisibility()
    {
        if (_caret is null)
            return;

        _caret.Visibility = IsKeyboardFocused && SelectionLength == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void StartBlinkAnimation()
    {
        if (_caret is null)
            return;

        var blink = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(CaretBlinkMilliseconds),
            RepeatBehavior = RepeatBehavior.Forever
        };

        blink.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromPercent(0)));
        blink.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromPercent(0.48)));
        blink.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromPercent(0.5)));
        blink.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromPercent(0.98)));
        blink.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromPercent(1)));

        _caret.BeginAnimation(OpacityProperty, blink, HandoffBehavior.SnapshotAndReplace);
    }

    private void PauseBlinkTemporarily()
    {
        if (_caret is null)
            return;

        _caret.BeginAnimation(OpacityProperty, null);
        _caret.Opacity = 1;

        _typingBlinkPauseTimer ??= new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(CaretTypingBlinkPauseMilliseconds)
        };
        _typingBlinkPauseTimer.Interval = TimeSpan.FromMilliseconds(CaretTypingBlinkPauseMilliseconds);

        _typingBlinkPauseTimer.Stop();
        _typingBlinkPauseTimer.Tick -= OnTypingBlinkPauseElapsed;
        _typingBlinkPauseTimer.Tick += OnTypingBlinkPauseElapsed;
        _typingBlinkPauseTimer.Start();
    }

    private void OnTypingBlinkPauseElapsed(object? sender, EventArgs e)
    {
        _typingBlinkPauseTimer?.Stop();
        StartBlinkAnimation();
    }

    private static void OnCaretBlinkPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SmoothCaretTextBox textBox && textBox._caret is not null)
            textBox.StartBlinkAnimation();
    }

    private static IEasingFunction CreateEasingFunction(string? easing)
    {
        return easing?.Trim().ToLowerInvariant() switch
        {
            "linear" => null!,
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
    }
}
