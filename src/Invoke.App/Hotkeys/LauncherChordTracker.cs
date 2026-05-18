namespace Invoke.App.Hotkeys;

internal sealed class LauncherChordTracker
{
    internal const int VkMenu = 0x12;
    internal const int VkLMenu = 0xA4;
    internal const int VkRMenu = 0xA5;
    internal const int VkControl = 0x11;
    internal const int VkLControl = 0xA2;
    internal const int VkRControl = 0xA3;
    internal const int VkShift = 0x10;
    internal const int VkLShift = 0xA0;
    internal const int VkRShift = 0xA1;
    internal const int VkLWin = 0x5B;
    internal const int VkRWin = 0x5C;
    internal const int VkSpace = 0x20;

    private readonly IReadOnlyList<TrackedHotkeyBinding> _bindings;
    private bool _altDown;
    private bool _controlDown;
    private bool _shiftDown;
    private bool _winDown;
    private TrackedHotkeyBinding? _activeBinding;

    public LauncherChordTracker()
        : this([new TrackedHotkeyBinding(new LauncherHotkeyGesture(LauncherHotkeyGesture.ModAlt, 0x20), null)])
    {
    }

    public LauncherChordTracker(IReadOnlyList<TrackedHotkeyBinding> bindings)
    {
        _bindings = bindings;
    }

    public KeyProcessingResult Process(int vkCode, bool isDown, bool isUp)
    {
        if (TryGetModifier(vkCode, out var modifier))
        {
            if (isDown)
            {
                SetModifierState(modifier, isPressed: true);
                return KeyProcessingResult.Pass;
            }

            if (isUp)
            {
                SetModifierState(modifier, isPressed: false);
                if (_activeBinding is { } activeBinding &&
                    (activeBinding.Gesture.Modifiers & modifier) != 0)
                {
                    _activeBinding = null;
                }

                return KeyProcessingResult.Pass;
            }
        }

        if (isDown)
        {
            if (_activeBinding is { } activeBinding && activeBinding.Gesture.VirtualKey == (uint)vkCode)
                return KeyProcessingResult.Suppress;

            var matchedBinding = _bindings.FirstOrDefault(binding =>
                binding.Gesture.VirtualKey == (uint)vkCode &&
                binding.Gesture.Modifiers == GetActiveModifiers());
            if (matchedBinding is null)
                return KeyProcessingResult.Pass;

            _activeBinding = matchedBinding;
            return new KeyProcessingResult(
                SuppressEvent: true,
                RequestLauncher: true,
                SendNeutralizer: matchedBinding.Gesture.RequiresReleaseNeutralizer,
                InitialMode: matchedBinding.InitialMode);
        }

        if (isUp &&
            _activeBinding is { } releasedBinding &&
            releasedBinding.Gesture.VirtualKey == (uint)vkCode)
        {
            return KeyProcessingResult.Suppress;
        }

        return KeyProcessingResult.Pass;
    }

    private static bool TryGetModifier(int vkCode, out uint modifier)
    {
        modifier = vkCode switch
        {
            VkMenu or VkLMenu or VkRMenu => LauncherHotkeyGesture.ModAlt,
            VkControl or VkLControl or VkRControl => LauncherHotkeyGesture.ModControl,
            VkShift or VkLShift or VkRShift => LauncherHotkeyGesture.ModShift,
            VkLWin or VkRWin => LauncherHotkeyGesture.ModWin,
            _ => 0
        };

        return modifier != 0;
    }

    private void SetModifierState(uint modifier, bool isPressed)
    {
        if (modifier == LauncherHotkeyGesture.ModAlt)
            _altDown = isPressed;
        else if (modifier == LauncherHotkeyGesture.ModControl)
            _controlDown = isPressed;
        else if (modifier == LauncherHotkeyGesture.ModShift)
            _shiftDown = isPressed;
        else if (modifier == LauncherHotkeyGesture.ModWin)
            _winDown = isPressed;
    }

    private uint GetActiveModifiers()
    {
        uint modifiers = 0;
        if (_altDown)
            modifiers |= LauncherHotkeyGesture.ModAlt;
        if (_controlDown)
            modifiers |= LauncherHotkeyGesture.ModControl;
        if (_shiftDown)
            modifiers |= LauncherHotkeyGesture.ModShift;
        if (_winDown)
            modifiers |= LauncherHotkeyGesture.ModWin;
        return modifiers;
    }
}

internal sealed record TrackedHotkeyBinding(LauncherHotkeyGesture Gesture, string? InitialMode);

internal readonly record struct KeyProcessingResult(
    bool SuppressEvent,
    bool RequestLauncher,
    bool SendNeutralizer,
    string? InitialMode = null)
{
    public static KeyProcessingResult Pass { get; } = new(false, false, false, null);

    public static KeyProcessingResult Suppress { get; } = new(true, false, false, null);
}
