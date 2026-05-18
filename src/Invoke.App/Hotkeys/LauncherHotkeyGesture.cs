namespace Invoke.App.Hotkeys;

internal readonly record struct LauncherHotkeyGesture(uint Modifiers, uint VirtualKey)
{
    internal const uint ModAlt = 0x0001;
    internal const uint ModControl = 0x0002;
    internal const uint ModShift = 0x0004;
    internal const uint ModWin = 0x0008;

    public bool IsAltSpace => Modifiers == ModAlt && VirtualKey == 0x20;
    public bool HasAltModifier => (Modifiers & ModAlt) != 0;
    public bool HasWinModifier => (Modifiers & ModWin) != 0;
    public bool PrefersLowLevelHook => IsAltSpace;
    public bool RequiresReleaseNeutralizer => HasAltModifier || HasWinModifier;
}

internal static class LauncherHotkeyParser
{
    public static LauncherHotkeyGesture ParseOrDefault(string? hotkey) =>
        TryParse(hotkey, out var gesture)
            ? gesture
            : new LauncherHotkeyGesture(LauncherHotkeyGesture.ModAlt, 0x20);

    public static bool TryParse(string? hotkey, out LauncherHotkeyGesture gesture)
    {
        gesture = default;
        if (string.IsNullOrWhiteSpace(hotkey))
            return false;

        uint modifiers = 0;
        uint? virtualKey = null;

        foreach (var segment in hotkey.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (TryParseModifier(segment, out var modifier))
            {
                if ((modifiers & modifier) != 0)
                    return false;

                modifiers |= modifier;
                continue;
            }

            if (virtualKey is not null || !TryParseVirtualKey(segment, out var key))
                return false;

            virtualKey = key;
        }

        if (modifiers == 0 || virtualKey is null)
            return false;

        gesture = new LauncherHotkeyGesture(modifiers, virtualKey.Value);
        return true;
    }

    private static bool TryParseModifier(string segment, out uint modifier)
    {
        modifier = segment.ToUpperInvariant() switch
        {
            "ALT" => LauncherHotkeyGesture.ModAlt,
            "CTRL" or "CONTROL" => LauncherHotkeyGesture.ModControl,
            "SHIFT" => LauncherHotkeyGesture.ModShift,
            "WIN" or "WINDOWS" => LauncherHotkeyGesture.ModWin,
            _ => 0
        };

        return modifier != 0;
    }

    private static bool TryParseVirtualKey(string segment, out uint virtualKey)
    {
        virtualKey = 0;
        if (segment.Length == 1)
        {
            var character = char.ToUpperInvariant(segment[0]);
            if (character is >= 'A' and <= 'Z')
            {
                virtualKey = character;
                return true;
            }

            if (character is >= '0' and <= '9')
            {
                virtualKey = character;
                return true;
            }
        }

        if (TryParseFunctionKey(segment, out virtualKey))
            return true;

        virtualKey = segment.ToUpperInvariant() switch
        {
            "SPACE" => 0x20,
            "TAB" => 0x09,
            "ENTER" or "RETURN" => 0x0D,
            "ESC" or "ESCAPE" => 0x1B,
            "BACKSPACE" => 0x08,
            "DELETE" or "DEL" => 0x2E,
            "INSERT" or "INS" => 0x2D,
            "HOME" => 0x24,
            "END" => 0x23,
            "PAGEUP" or "PGUP" => 0x21,
            "PAGEDOWN" or "PGDN" => 0x22,
            "UP" => 0x26,
            "DOWN" => 0x28,
            "LEFT" => 0x25,
            "RIGHT" => 0x27,
            "OEM3" or "BACKTICK" or "GRAVE" => 0xC0,
            "OEM_MINUS" or "MINUS" => 0xBD,
            "OEM_PLUS" or "PLUS" => 0xBB,
            "OEM_COMMA" or "COMMA" => 0xBC,
            "OEM_PERIOD" or "PERIOD" or "DOT" => 0xBE,
            "OEM_2" or "SLASH" => 0xBF,
            "OEM_1" or "SEMICOLON" => 0xBA,
            "OEM_7" or "QUOTE" or "APOSTROPHE" => 0xDE,
            "OEM_4" or "LBRACKET" or "OPENBRACKET" => 0xDB,
            "OEM_6" or "RBRACKET" or "CLOSEBRACKET" => 0xDD,
            "OEM_5" or "BACKSLASH" => 0xDC,
            _ => 0
        };

        return virtualKey != 0;
    }

    private static bool TryParseFunctionKey(string segment, out uint virtualKey)
    {
        virtualKey = 0;
        if (!segment.StartsWith('F') ||
            !int.TryParse(segment.AsSpan(1), out var functionNumber) ||
            functionNumber is < 1 or > 24)
        {
            return false;
        }

        virtualKey = (uint)(0x70 + functionNumber - 1);
        return true;
    }
}
