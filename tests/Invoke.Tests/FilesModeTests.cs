using Invoke.Core.Config;
using Invoke.Core.Modes;
using Invoke.Core.Services;

namespace Invoke.Tests;

[TestClass]
public sealed class FilesModeTests
{
    [TestMethod]
    public async Task SearchAsync_ShowsHelpfulMessageWhenEverythingIpcMissing()
    {
        using var workspace = new TestWorkspace();
        var esPath = workspace.GetPath("es.exe");
        File.WriteAllText(esPath, string.Empty);
        var settings = new InvokeSettings
        {
            EverythingCliPath = esPath
        };

        using var mode = new FilesMode(new RecordingProcessRunner
        {
            CaptureResult = new ProcessResult(8, string.Empty, "Everything IPC not found")
        });
        var snapshot = await mode.SearchAsync(new LauncherQueryContext("ab", "ab", settings, new ThemeSettings(), "files", 10), CancellationToken.None);

        StringAssert.Contains(snapshot.Message, "Everything not running");
        Assert.HasCount(0, snapshot.Entries);
    }

    [TestMethod]
    public async Task SearchAsync_MapsEverythingResultsIntoEntries()
    {
        using var workspace = new TestWorkspace();
        var filePath = workspace.GetPath("docs-note.txt");
        var folderPath = workspace.GetPath("docs");
        File.WriteAllText(filePath, "hello");
        Directory.CreateDirectory(folderPath);
        var esPath = workspace.GetPath("es.exe");
        File.WriteAllText(esPath, string.Empty);

        var settings = new InvokeSettings
        {
            EverythingCliPath = esPath
        };

        using var mode = new FilesMode(new RecordingProcessRunner
        {
            CaptureResult = new ProcessResult(0, string.Join(Environment.NewLine, [filePath, folderPath]), string.Empty)
        });

        var snapshot = await mode.SearchAsync(new LauncherQueryContext("do", "do", settings, new ThemeSettings(), "files", 10), CancellationToken.None);

        Assert.AreEqual("Search files with Everything", snapshot.Message);
        var displayTexts = snapshot.Entries.Select(static entry => entry.DisplayText).ToArray();
        CollectionAssert.Contains(displayTexts, "docs");
        CollectionAssert.Contains(displayTexts, "docs-note.txt");
    }

    private sealed class RecordingProcessRunner : ProcessRunner
    {
        public ProcessResult CaptureResult { get; set; } = new(0, string.Empty, string.Empty);

        public override Task<ProcessResult> CaptureAsync(string fileName, string arguments, CancellationToken cancellationToken) =>
            Task.FromResult(CaptureResult);
    }
}
