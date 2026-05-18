using System.Diagnostics;
using Invoke.Core.Services;

namespace Invoke.Tests;

[TestClass]
public sealed class ProcessRunnerTests
{
    [TestMethod]
    public async Task LaunchAsync_AllowsShellLaunchWithoutProcessHandle()
    {
        var runner = new NullStartProcessRunner();

        await runner.LaunchAsync(@"C:\Users\Admin\AppData\Roaming\Invoke");

        Assert.AreEqual(@"C:\Users\Admin\AppData\Roaming\Invoke", runner.StartInfo?.FileName);
        Assert.IsTrue(runner.StartInfo?.UseShellExecute);
    }

    [TestMethod]
    public async Task CaptureAsync_CancellationReturnsQuickly()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await new ProcessRunner().CaptureAsync(
                "powershell.exe",
                "-NoProfile -Command Start-Sleep -Seconds 30",
                cts.Token);
            Assert.Fail("Expected cancellation.");
        }
        catch (OperationCanceledException)
        {
        }

        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
    }

    private sealed class NullStartProcessRunner : ProcessRunner
    {
        public ProcessStartInfo? StartInfo { get; private set; }

        protected override Process? Start(ProcessStartInfo startInfo)
        {
            StartInfo = startInfo;
            return null;
        }
    }
}
