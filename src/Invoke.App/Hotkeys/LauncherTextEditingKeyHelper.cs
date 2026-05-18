using System.Windows.Input;

namespace Invoke.App.Hotkeys;

internal static class LauncherTextEditingKeyHelper
{
    public static bool ShouldDeferToTextBox(string bindingName, string? configuredBinding, Key key, ModifierKeys modifiers)
    {
        if (!TryGetDefaultTextEditingConflict(bindingName, out var defaultGesture))
            return false;

        if (configuredBinding is not null &&
            KeyboardBindingMatcher.TryParse(configuredBinding, out var configuredGesture) &&
            configuredGesture != defaultGesture)
        {
            return false;
        }

        return key == defaultGesture.Key && modifiers == defaultGesture.Modifiers;
    }

    private static bool TryGetDefaultTextEditingConflict(string bindingName, out KeyboardBindingGesture gesture)
    {
        gesture = bindingName switch
        {
            "kb-row-left" => new KeyboardBindingGesture(ModifierKeys.None, Key.Left),
            "kb-row-right" => new KeyboardBindingGesture(ModifierKeys.None, Key.Right),
            "kb-row-first" => new KeyboardBindingGesture(ModifierKeys.None, Key.Home),
            "kb-row-last" => new KeyboardBindingGesture(ModifierKeys.None, Key.End),
            "kb-move-front" => new KeyboardBindingGesture(ModifierKeys.Control, Key.A),
            _ => default
        };

        return gesture.Key != Key.None;
    }
}
