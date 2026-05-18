using Invoke.App;

namespace Invoke.Tests;

[TestClass]
public sealed class MainWindowSystemMenuTests
{
    [TestMethod]
    public void ShouldSuppressSystemMenuMessage_OnlyWhenLauncherVisibleAndMenuActivationMessage()
    {
        Assert.IsFalse(MainWindow.ShouldSuppressSystemMenuMessage(false, 0x0112, (IntPtr)0xF100));
        Assert.IsTrue(MainWindow.ShouldSuppressSystemMenuMessage(true, 0x0112, (IntPtr)0xF100));
        Assert.IsTrue(MainWindow.ShouldSuppressSystemMenuMessage(true, 0x0112, (IntPtr)0xF123));
        Assert.IsTrue(MainWindow.ShouldSuppressSystemMenuMessage(true, 0x0106, IntPtr.Zero));
        Assert.IsFalse(MainWindow.ShouldSuppressSystemMenuMessage(true, 0x0104, IntPtr.Zero));
    }
}
