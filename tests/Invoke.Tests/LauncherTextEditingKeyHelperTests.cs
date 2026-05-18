using Invoke.App.Hotkeys;

namespace Invoke.Tests;

[TestClass]
public sealed class LauncherTextEditingKeyHelperTests
{
    [TestMethod]
    public void DefaultControlADeferToTextBox()
    {
        var handledByTextBox = LauncherTextEditingKeyHelper.ShouldDeferToTextBox(
            "kb-move-front",
            "Control+a",
            System.Windows.Input.Key.A,
            System.Windows.Input.ModifierKeys.Control);

        Assert.IsTrue(handledByTextBox);
    }

    [TestMethod]
    public void CustomBindingDoesNotDeferToTextBox()
    {
        var handledByTextBox = LauncherTextEditingKeyHelper.ShouldDeferToTextBox(
            "kb-move-front",
            "Control+Home",
            System.Windows.Input.Key.A,
            System.Windows.Input.ModifierKeys.Control);

        Assert.IsFalse(handledByTextBox);
    }

    [TestMethod]
    public void DefaultLeftArrowDeferToTextBox()
    {
        var handledByTextBox = LauncherTextEditingKeyHelper.ShouldDeferToTextBox(
            "kb-row-left",
            "Left",
            System.Windows.Input.Key.Left,
            System.Windows.Input.ModifierKeys.None);

        Assert.IsTrue(handledByTextBox);
    }
}
