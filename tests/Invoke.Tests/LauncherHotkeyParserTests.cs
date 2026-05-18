using Invoke.App.Hotkeys;

namespace Invoke.Tests;

[TestClass]
public class LauncherHotkeyParserTests
{
    [TestMethod]
    public void ParseOrDefault_FallsBackToAltSpace_ForInvalidHotkey()
    {
        var gesture = LauncherHotkeyParser.ParseOrDefault("not-a-hotkey");

        Assert.IsTrue(gesture.IsAltSpace);
    }

    [TestMethod]
    public void TryParse_ParsesCtrlShiftSpace()
    {
        var parsed = LauncherHotkeyParser.TryParse("Ctrl+Shift+Space", out var gesture);

        Assert.IsTrue(parsed);
        Assert.AreEqual(
            LauncherHotkeyGesture.ModControl | LauncherHotkeyGesture.ModShift,
            gesture.Modifiers);
        Assert.AreEqual(0x20u, gesture.VirtualKey);
    }

    [TestMethod]
    public void TryParse_ParsesAliasTokensAndFunctionKeys()
    {
        var parsed = LauncherHotkeyParser.TryParse("Control+Windows+F12", out var gesture);

        Assert.IsTrue(parsed);
        Assert.AreEqual(
            LauncherHotkeyGesture.ModControl | LauncherHotkeyGesture.ModWin,
            gesture.Modifiers);
        Assert.AreEqual(0x7Bu, gesture.VirtualKey);
    }

    [TestMethod]
    public void TryParse_RejectsMissingModifier()
    {
        Assert.IsFalse(LauncherHotkeyParser.TryParse("Space", out _));
    }
}
