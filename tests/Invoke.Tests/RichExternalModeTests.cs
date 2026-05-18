using Invoke.Core.Actions;
using Invoke.Core.Modes;
using Invoke.Core.Plugins.Rich;
using Invoke.Core.Services;

namespace Invoke.Tests;

[TestClass]
public sealed class RichExternalModeTests
{
    [TestMethod]
    public async Task SearchAsync_MapsStructuredResponseMetadata()
    {
        using var workspace = new TestWorkspace();
        var scriptPath = workspace.GetPath("demo.ps1");
        File.WriteAllText(
            scriptPath,
            """
            Write-Output '{"displayPrefix":"Demo | ","rawPrefix":"/demo ","results":[{"id":"row-1","title":"Open docs","subtitle":"Open web docs","kind":"Web","icon":"favicon:https://example.com","action":{"kind":"open-url","url":"https://example.com/docs"}},{"id":"row-2","title":"Set query","subtitle":"Rewrite input","action":{"kind":"set-query","query":"/demo rewritten"}}]}'
            """);

        var manifest = new RichScriptManifest
        {
            Id = "demo",
            Name = "Demo",
            Entry = "demo.ps1",
            Triggers = ["/demo"]
        };

        using var mode = new RichExternalMode(new RichScriptDefinition(workspace.Path, manifest), new ProcessRunner());
        var snapshot = await mode.SearchAsync(new LauncherQueryContext("/demo hello", "hello", new(), new(), "combi", 8), CancellationToken.None);

        Assert.AreEqual("Demo | ", snapshot.DisplayPrefix);
        Assert.AreEqual("/demo ", snapshot.RawPrefix);
        Assert.HasCount(2, snapshot.Entries);
        Assert.AreEqual("row-1", snapshot.Entries[0].Id);
        Assert.AreEqual("id:row-1", snapshot.Entries[0].IdentityKey);
        Assert.AreEqual(InvokeActionKind.OpenUrl, snapshot.Entries[0].Action.Kind);
        Assert.AreEqual("https://example.com/docs", snapshot.Entries[0].Action.Target);
        Assert.AreEqual("row-2", snapshot.Entries[1].Id);
        Assert.AreEqual(InvokeActionKind.SetQuery, snapshot.Entries[1].Action.Kind);
        Assert.AreEqual("/demo rewritten", snapshot.Entries[1].Action.Target);
    }

    [TestMethod]
    public async Task SearchAsync_SupportsPrefixTokenTriggerMode()
    {
        using var workspace = new TestWorkspace();
        var scriptPath = workspace.GetPath("prefix.ps1");
        File.WriteAllText(
            scriptPath,
            "$raw = [Console]::In.ReadToEnd()\r\n" +
            "if ($raw -match '\"trigger\"\\s*:\\s*\"([^\"]+)\"') { $trigger = $Matches[1] } else { $trigger = \"\" }\r\n" +
            "if ($raw -match '\"terms\"\\s*:\\s*\"([^\"]*)\"') { $terms = $Matches[1] } else { $terms = \"\" }\r\n" +
            "Write-Output ('[{\"title\":\"trigger=' + $trigger + ' terms=' + $terms + '\"}]')\r\n");

        var manifest = new RichScriptManifest
        {
            Id = "prefix",
            Name = "Prefix",
            Entry = "prefix.ps1",
            Triggers = ["!"],
            TriggerMode = RichScriptManifest.PrefixTokenTriggerMode
        };

        using var mode = new RichExternalMode(new RichScriptDefinition(workspace.Path, manifest), new ProcessRunner());
        var snapshot = await mode.SearchAsync(new LauncherQueryContext("!yt lo-fi", "!yt lo-fi", new(), new(), "combi", 8), CancellationToken.None);

        Assert.HasCount(1, snapshot.Entries);
        Assert.AreEqual("trigger=!yt terms=lo-fi", snapshot.Entries[0].DisplayText);
    }

    [TestMethod]
    public async Task SearchAsync_RunsWhenScriptIsActiveMode()
    {
        using var workspace = new TestWorkspace();
        var scriptPath = workspace.GetPath("cliphist.ps1");
        File.WriteAllText(scriptPath, "Write-Output '[{\"title\":\"recent clip\"}]'");

        var manifest = new RichScriptManifest
        {
            Id = "cliphist",
            Name = "Cliphist",
            Entry = "cliphist.ps1",
            Triggers = ["/clip"]
        };

        using var mode = new RichExternalMode(new RichScriptDefinition(workspace.Path, manifest), new ProcessRunner());
        var snapshot = await mode.SearchAsync(new LauncherQueryContext("hello", "hello", new(), new(), "cliphist", 8), CancellationToken.None);

        Assert.HasCount(1, snapshot.Entries);
        Assert.AreEqual("recent clip", snapshot.Entries[0].DisplayText);
    }
}
