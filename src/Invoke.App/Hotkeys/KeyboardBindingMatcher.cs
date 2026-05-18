using System.Windows.Input;

namespace Invoke.App.Hotkeys;

internal readonly record struct KeyboardBindingGesture(ModifierKeys Modifiers, Key Key);

internal static class KeyboardBindingMatcher
{
    public static bool Matches(string? binding, System.Windows.Input.KeyEventArgs e)
    {
        if (!TryParse(binding, out var gesture))
            return false;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        return key == gesture.Key && Keyboard.Modifiers == gesture.Modifiers;
    }

    public static bool TryParse(string? binding, out KeyboardBindingGesture gesture)
    {
        gesture = default;
        if (string.IsNullOrWhiteSpace(binding))
            return false;

        ModifierKeys modifiers = ModifierKeys.None;
        Key? key = null;

        foreach (var segment in binding.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (TryParseModifier(segment, out var modifier))
            {
                if ((modifiers & modifier) != 0)
                    return false;

                modifiers |= modifier;
                continue;
            }

            if (key is not null || !TryParseKey(segment, out var parsedKey))
                return false;

            key = parsedKey;
        }

        if (key is null)
            return false;

        gesture = new KeyboardBindingGesture(modifiers, key.Value);
        return true;
    }

    private static bool TryParseModifier(string segment, out ModifierKeys modifier)
    {
        modifier = segment.ToUpperInvariant() switch
        {
            "ALT" => ModifierKeys.Alt,
            "CTRL" or "CONTROL" => ModifierKeys.Control,
            "SHIFT" => ModifierKeys.Shift,
            "WIN" or "WINDOWS" => ModifierKeys.Windows,
            _ => ModifierKeys.None
        };

        return modifier != ModifierKeys.None;
    }

    private static bool TryParseKey(string segment, out Key key)
    {
        key = Key.None;
        if (segment.Length == 1)
        {
            var character = char.ToUpperInvariant(segment[0]);
            if (character is >= 'A' and <= 'Z')
            {
                key = Enum.Parse<Key>(character.ToString(), ignoreCase: true);
                return true;
            }

            if (character is >= '0' and <= '9')
            {
                key = Enum.Parse<Key>("D" + character, ignoreCase: true);
                return true;
            }
        }

        if (segment.StartsWith("F", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(segment.AsSpan(1), out var functionNumber) &&
            functionNumber is >= 1 and <= 24)
        {
            key = Enum.Parse<Key>("F" + functionNumber, ignoreCase: true);
            return true;
        }

        key = segment.ToUpperInvariant() switch
        {
            "SPACE" => Key.Space,
            "TAB" => Key.Tab,
            "ENTER" or "RETURN" => Key.Enter,
            "ESC" or "ESCAPE" => Key.Escape,
            "BACKSPACE" => Key.Back,
            "DELETE" or "DEL" => Key.Delete,
            "INSERT" or "INS" => Key.Insert,
            "HOME" => Key.Home,
            "END" => Key.End,
            "PAGEUP" or "PGUP" => Key.PageUp,
            "PAGEDOWN" or "PGDN" => Key.PageDown,
            "UP" => Key.Up,
            "DOWN" => Key.Down,
            "LEFT" => Key.Left,
            "RIGHT" => Key.Right,
            "OEM3" or "BACKTICK" or "GRAVE" => Key.Oem3,
            "OEM_MINUS" or "MINUS" => Key.OemMinus,
            "OEM_PLUS" or "PLUS" => Key.OemPlus,
            "OEM_COMMA" or "COMMA" => Key.OemComma,
            "OEM_PERIOD" or "PERIOD" or "DOT" => Key.OemPeriod,
            "OEM_2" or "SLASH" => Key.Oem2,
            "OEM_1" or "SEMICOLON" => Key.Oem1,
            "OEM_7" or "QUOTE" or "APOSTROPHE" => Key.Oem7,
            "OEM_4" or "LBRACKET" or "OPENBRACKET" => Key.Oem4,
            "OEM_6" or "RBRACKET" or "CLOSEBRACKET" => Key.Oem6,
            "OEM_5" or "BACKSLASH" => Key.Oem5,
            _ => Key.None
        };

        return key != Key.None;
    }
}
