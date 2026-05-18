using System.Windows.Input;
using Invoke.App.Hotkeys;

namespace Invoke.Tests;

[TestClass]
public sealed class KeyboardBindingMatcherTests
{
    [TestMethod]
    public void TryParse_AcceptsPlainKeysAndModifierChords()
    {
        Assert.IsTrue(KeyboardBindingMatcher.TryParse("Return", out var enter));
        Assert.AreEqual(Key.Enter, enter.Key);
        Assert.AreEqual(ModifierKeys.None, enter.Modifiers);

        Assert.IsTrue(KeyboardBindingMatcher.TryParse("Alt+Right", out var nextMode));
        Assert.AreEqual(Key.Right, nextMode.Key);
        Assert.AreEqual(ModifierKeys.Alt, nextMode.Modifiers);

        Assert.IsTrue(KeyboardBindingMatcher.TryParse("Control+j", out var controlJ));
        Assert.AreEqual(Key.J, controlJ.Key);
        Assert.AreEqual(ModifierKeys.Control, controlJ.Modifiers);
    }

    [TestMethod]
    public void TryParse_AcceptsShiftTab()
    {
        Assert.IsTrue(KeyboardBindingMatcher.TryParse("Shift+Tab", out var gesture));
        Assert.AreEqual(Key.Tab, gesture.Key);
        Assert.AreEqual(ModifierKeys.Shift, gesture.Modifiers);
    }

    [TestMethod]
    public void TryParse_RejectsInvalidBindings()
    {
        Assert.IsFalse(KeyboardBindingMatcher.TryParse("", out _));
        Assert.IsFalse(KeyboardBindingMatcher.TryParse("Ctrl+Alt", out _));
        Assert.IsFalse(KeyboardBindingMatcher.TryParse("Ctrl+Foo", out _));
    }
}
