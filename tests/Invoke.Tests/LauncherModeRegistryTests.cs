using Invoke.Core.Actions;
using Invoke.Core.Config;
using Invoke.Core.Modes;
using Invoke.Core.Results;

namespace Invoke.Tests;

[TestClass]
public sealed class LauncherModeRegistryTests
{
    [TestMethod]
    public async Task SearchAsync_UsesDefaultModeAndAddsSupplementalResults()
    {
        var settings = CreateSettings();
        using var registry = new LauncherModeRegistry(
            [
                new LauncherModeDefinition("drun", "drun", new StubMode("drun", [Entry("App", 150)])),
                new LauncherModeDefinition("helper", "helper", new StubMode("helper", [Entry("Plugin", 400)]), AlwaysEvaluate: true)
            ],
            settings,
            new ThemeSettings());

        var result = await registry.SearchAsync("powertoys", null, CancellationToken.None);

        Assert.AreEqual("drun", result.ActiveModeId);
        CollectionAssert.AreEqual(new[] { "Plugin", "App" }, result.Entries.Select(static entry => entry.DisplayText).ToArray());
        Assert.AreEqual("drun", result.Prompt);
    }

    [TestMethod]
    public async Task SearchAsync_CombiUsesBoostsAndPerModeLimits()
    {
        var settings = CreateSettings();
        settings.DefaultMode = "combi";
        settings.Modes = ["drun", "run", "combi"];
        settings.CombiModes = ["drun", "run"];
        settings.MaxResults = 5;
        settings.MaxResultsPerMode["run"] = 1;
        settings.CombiScoreBoosts["drun"] = 100;

        using var registry = new LauncherModeRegistry(
            [
                new LauncherModeDefinition("drun", "drun", new StubMode("drun", [Entry("Apps hit", 10)])),
                new LauncherModeDefinition("run", "run", new StubMode("run", [Entry("Run best", 80), Entry("Run second", 70)])),
                new LauncherModeDefinition("combi", "combi", new StubMode("combi", []))
            ],
            settings,
            new ThemeSettings());

        var result = await registry.SearchAsync("code", "combi", CancellationToken.None);

        Assert.AreEqual("combi", result.ActiveModeId);
        Assert.AreEqual("combi", result.Prompt);
        CollectionAssert.AreEqual(new[] { "Apps hit", "Run best" }, result.Entries.Select(static entry => entry.DisplayText).ToArray());
    }

    [TestMethod]
    public async Task SearchAsync_CombiAddsSupplementalAlwaysEvaluateResults()
    {
        var settings = CreateSettings();
        settings.DefaultMode = "combi";
        settings.Modes = ["drun", "combi"];
        settings.CombiModes = ["drun"];

        using var registry = new LauncherModeRegistry(
            [
                new LauncherModeDefinition("drun", "drun", new StubMode("drun", [Entry("Apps hit", 10)])),
                new LauncherModeDefinition("wiki", "Wiki", new StubMode("wiki", [Entry("!wiki result", 400)]), AlwaysEvaluate: true),
                new LauncherModeDefinition("combi", "combi", new StubMode("combi", []))
            ],
            settings,
            new ThemeSettings());

        var result = await registry.SearchAsync("!wiki machine learning", "combi", CancellationToken.None);

        Assert.AreEqual("combi", result.ActiveModeId);
        CollectionAssert.AreEqual(new[] { "!wiki result", "Apps hit" }, result.Entries.Select(static entry => entry.DisplayText).ToArray());
    }

    [TestMethod]
    public void GetNextAndPreviousMode_FollowsConfiguredOrder()
    {
        var settings = CreateSettings();
        settings.Modes = ["window", "drun", "run"];

        using var registry = new LauncherModeRegistry(
            [
                new LauncherModeDefinition("drun", "drun", new StubMode("drun", [])),
                new LauncherModeDefinition("run", "run", new StubMode("run", [])),
                new LauncherModeDefinition("window", "window", new StubMode("window", []))
            ],
            settings,
            new ThemeSettings());

        Assert.AreEqual("drun", registry.GetNextMode("window"));
        Assert.AreEqual("run", registry.GetPreviousMode("window"));
    }

    [TestMethod]
    public void OrderedModes_UsesOnlyConfiguredModesIncludingConfiguredRichExternalScripts()
    {
        var settings = CreateSettings();
        settings.Modes = ["drun", "cliphist"];

        using var registry = new LauncherModeRegistry(
            [
                new LauncherModeDefinition("drun", "drun", new StubMode("drun", [])),
                new LauncherModeDefinition("cliphist", "Cliphist", new StubMode("cliphist", [])),
                new LauncherModeDefinition("winget-plus", "Winget", new StubMode("winget-plus", []))
            ],
            settings,
            new ThemeSettings());

        CollectionAssert.AreEqual(
            new[] { "drun", "cliphist" },
            registry.OrderedModes.Select(static mode => mode.Id).ToArray());
    }

    private static InvokeSettings CreateSettings() =>
        new()
        {
            Modes = ["drun", "run", "window", "combi"],
            DefaultMode = "drun",
            CombiModes = ["drun", "run"],
            MaxResults = 8
        };

    private static LauncherModeEntry Entry(string title, double score) =>
        new(
            title,
            title,
            string.Empty,
            ResultKind.Command,
            score,
            new InvokeAction(title, "Run", _ => Task.CompletedTask));

    private sealed class StubMode : ILauncherMode
    {
        private readonly IReadOnlyList<LauncherModeEntry> _entries;

        public StubMode(string id, IReadOnlyList<LauncherModeEntry> entries)
        {
            Id = id;
            DisplayName = id;
            _entries = entries;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public int Priority => 0;

        public ValueTask InitializeAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public Task<LauncherModeSnapshot> SearchAsync(LauncherQueryContext context, CancellationToken cancellationToken) =>
            Task.FromResult(new LauncherModeSnapshot(Id, DisplayName, null, _entries));

        public void Dispose()
        {
        }
    }
}
