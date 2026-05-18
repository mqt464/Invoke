using Invoke.Core.Config;
using Invoke.Core.Modes;
using Invoke.Core.Results;
using Invoke.Core.Services;

namespace Invoke.Tests;

[TestClass]
public sealed class BuiltInModesTests
{
    [TestMethod]
    public async Task DrunMode_BoostsFrequentlyLaunchedAppsToTop()
    {
        var settings = new InvokeSettings
        {
            History = new HistorySettings
            {
                EnableRecentBoost = true,
                ScoreBoost = 160,
                RecentLaunches =
                [
                    new RecentLaunchEntry
                    {
                        Key = RecentLaunchScoring.BuildKey(ResultKind.App, "Notepad", @"C:\Apps\Notepad.exe", "Launch app"),
                        Title = "Notepad",
                        Subtitle = @"C:\Apps\Notepad.exe",
                        Kind = nameof(ResultKind.App),
                        LaunchCount = 6,
                        LastUsedUtc = DateTime.UtcNow
                    }
                ]
            }
        };

        var runner = new RecordingProcessRunner();
        using var mode = new DrunMode(
            runner,
            [
                ("Calculator", @"C:\Apps\Calc.exe", false),
                ("Notepad", @"C:\Apps\Notepad.exe", false)
            ]);

        var snapshot = await mode.SearchAsync(new LauncherQueryContext(string.Empty, string.Empty, settings, new ThemeSettings(), "drun", 10), CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "Notepad", "Calculator" }, snapshot.Entries.Select(static entry => entry.DisplayText).ToArray());
    }

    private sealed class RecordingProcessRunner : ProcessRunner
    {
        public string? LastLaunchFileName { get; private set; }
        public string? LastLaunchArguments { get; private set; }

        public override Task LaunchAsync(string fileName, string? arguments = null, string? workingDirectory = null, bool runAsAdmin = false)
        {
            LastLaunchFileName = fileName;
            LastLaunchArguments = arguments;
            return Task.CompletedTask;
        }
    }
}
