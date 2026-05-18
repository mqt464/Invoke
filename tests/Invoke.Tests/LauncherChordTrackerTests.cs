using Invoke.App.Hotkeys;

namespace Invoke.Tests;

[TestClass]
public class LauncherChordTrackerTests
{
    [TestMethod]
    public void AltSpaceChord_RequestsLauncher_SuppressesSpace_AndLetsAltReleasePassThrough()
    {
        var tracker = new LauncherChordTracker();

        var altDown = tracker.Process(LauncherChordTracker.VkLMenu, isDown: true, isUp: false);
        var spaceDown = tracker.Process(LauncherChordTracker.VkSpace, isDown: true, isUp: false);
        var spaceUp = tracker.Process(LauncherChordTracker.VkSpace, isDown: false, isUp: true);
        var altUp = tracker.Process(LauncherChordTracker.VkLMenu, isDown: false, isUp: true);

        Assert.AreEqual(KeyProcessingResult.Pass, altDown);
        Assert.IsTrue(spaceDown.SuppressEvent);
        Assert.IsTrue(spaceDown.RequestLauncher);
        Assert.IsTrue(spaceDown.SendNeutralizer);
        Assert.AreEqual(KeyProcessingResult.Suppress, spaceUp);
        Assert.AreEqual(KeyProcessingResult.Pass, altUp);
    }

    [TestMethod]
    public void RepeatedSpaceWhileHoldingAlt_OnlyTriggersLauncherOnce()
    {
        var tracker = new LauncherChordTracker();

        tracker.Process(LauncherChordTracker.VkLMenu, isDown: true, isUp: false);
        var firstSpaceDown = tracker.Process(LauncherChordTracker.VkSpace, isDown: true, isUp: false);
        var repeatedSpaceDown = tracker.Process(LauncherChordTracker.VkSpace, isDown: true, isUp: false);

        Assert.IsTrue(firstSpaceDown.RequestLauncher);
        Assert.IsFalse(repeatedSpaceDown.RequestLauncher);
        Assert.IsFalse(repeatedSpaceDown.SendNeutralizer);
        Assert.AreEqual(KeyProcessingResult.Suppress, repeatedSpaceDown);
    }

    [TestMethod]
    public void PlainAltPassesThrough()
    {
        var tracker = new LauncherChordTracker();

        var altDown = tracker.Process(LauncherChordTracker.VkRMenu, isDown: true, isUp: false);
        var altUp = tracker.Process(LauncherChordTracker.VkRMenu, isDown: false, isUp: true);

        Assert.AreEqual(KeyProcessingResult.Pass, altDown);
        Assert.AreEqual(KeyProcessingResult.Pass, altUp);
    }

    [TestMethod]
    public void AltTabTrackedBinding_RequestsConfiguredMode_AndSuppressesTab()
    {
        var tracker = new LauncherChordTracker(
        [
            new TrackedHotkeyBinding(
                new LauncherHotkeyGesture(LauncherHotkeyGesture.ModAlt, 0x09),
                "window")
        ]);

        var altDown = tracker.Process(LauncherChordTracker.VkLMenu, isDown: true, isUp: false);
        var tabDown = tracker.Process(0x09, isDown: true, isUp: false);
        var tabUp = tracker.Process(0x09, isDown: false, isUp: true);
        var repeatedTabDown = tracker.Process(0x09, isDown: true, isUp: false);
        var altUp = tracker.Process(LauncherChordTracker.VkLMenu, isDown: false, isUp: true);

        Assert.AreEqual(KeyProcessingResult.Pass, altDown);
        Assert.IsTrue(tabDown.SuppressEvent);
        Assert.IsTrue(tabDown.RequestLauncher);
        Assert.IsTrue(tabDown.SendNeutralizer);
        Assert.AreEqual("window", tabDown.InitialMode);
        Assert.AreEqual(KeyProcessingResult.Suppress, tabUp);
        Assert.AreEqual(KeyProcessingResult.Suppress, repeatedTabDown);
        Assert.AreEqual(KeyProcessingResult.Pass, altUp);
    }

    [TestMethod]
    public void ExtraModifierPreventsTrackedBindingMatch()
    {
        var tracker = new LauncherChordTracker(
        [
            new TrackedHotkeyBinding(
                new LauncherHotkeyGesture(LauncherHotkeyGesture.ModAlt, 0x09),
                "window")
        ]);

        tracker.Process(LauncherChordTracker.VkLMenu, isDown: true, isUp: false);
        tracker.Process(LauncherChordTracker.VkLShift, isDown: true, isUp: false);
        var tabDown = tracker.Process(0x09, isDown: true, isUp: false);

        Assert.AreEqual(KeyProcessingResult.Pass, tabDown);
    }
}
