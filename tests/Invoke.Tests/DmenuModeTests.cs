using Invoke.Core.Dmenu;
using Invoke.Core.Modes;

namespace Invoke.Tests;

[TestClass]
public sealed class DmenuModeTests
{
    [TestMethod]
    public async Task SearchAsync_FiltersEntriesAndUsesPrompt()
    {
        var session = new DmenuSession
        {
            Prompt = "pick",
            Message = "choose",
            MarkupRows = true,
            SelectedRow = 2,
            UrgentRows = [1],
            ActiveRows = [2],
            Entries = ["alpha", "beta", "alphabet"]
        };

        using var mode = new DmenuMode(session, _ => Task.CompletedTask);
        var snapshot = await mode.SearchAsync(new LauncherQueryContext("alp", "alp", new(), new(), "dmenu", 10), CancellationToken.None);

        Assert.AreEqual("pick", snapshot.Prompt);
        Assert.AreEqual("choose", snapshot.Message);
        Assert.IsTrue(snapshot.MarkupRows);
        Assert.AreEqual(1, snapshot.NewSelection);
        CollectionAssert.AreEquivalent(Array.Empty<int>(), snapshot.UrgentIndices!.ToArray());
        CollectionAssert.AreEquivalent(new[] { 1 }, snapshot.ActiveIndices!.ToArray());
        CollectionAssert.AreEqual(new[] { "alpha", "alphabet" }, snapshot.Entries.Select(static entry => entry.DisplayText).ToArray());
    }

    [TestMethod]
    public async Task SubmitCustomAsync_WritesSelectionAndCloses()
    {
        var selected = string.Empty;
        var session = new DmenuSession
        {
            Prompt = "pick",
            Entries = ["alpha"]
        };

        using var mode = new DmenuMode(session, value =>
        {
            selected = value;
            return Task.CompletedTask;
        });

        var result = await mode.SubmitCustomAsync(new LauncherQueryContext("", "", new(), new(), "dmenu", 10), "custom", CancellationToken.None);

        Assert.AreEqual("custom", selected);
        Assert.IsTrue(result.CloseLauncher);
    }

    [TestMethod]
    public async Task SubmitCustomAsync_RespectsNoCustom()
    {
        var selected = string.Empty;
        var session = new DmenuSession
        {
            Prompt = "pick",
            NoCustom = true,
            Entries = ["alpha"]
        };

        using var mode = new DmenuMode(session, value =>
        {
            selected = value;
            return Task.CompletedTask;
        });

        var result = await mode.SubmitCustomAsync(new LauncherQueryContext("", "", new(), new(), "dmenu", 10), "custom", CancellationToken.None);

        Assert.AreEqual(string.Empty, selected);
        Assert.IsFalse(result.CloseLauncher);
    }

    [TestMethod]
    public async Task ActivateEntryAsync_UsesConfiguredFormat()
    {
        var selected = string.Empty;
        var session = new DmenuSession
        {
            Prompt = "pick",
            MarkupRows = true,
            Format = "ip",
            Entries = ["<b>alpha</b>", "beta"]
        };

        using var mode = new DmenuMode(session, value =>
        {
            selected = value;
            return Task.CompletedTask;
        });

        var snapshot = await mode.SearchAsync(new LauncherQueryContext("alp", "alp", new(), new(), "dmenu", 10), CancellationToken.None);
        await mode.ActivateEntryAsync(new LauncherQueryContext("alp", "alp", new(), new(), "dmenu", 10), snapshot.Entries[0], CancellationToken.None);

        Assert.AreEqual("0alpha", selected);
    }
}
