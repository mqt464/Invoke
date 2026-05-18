using Invoke.Core.Dmenu;

namespace Invoke.Tests;

[TestClass]
public sealed class DmenuSessionTests
{
    [TestMethod]
    public void SaveAndLoad_RoundTripsSession()
    {
        using var workspace = new TestWorkspace();
        var path = workspace.GetPath("dmenu.json");
        var session = new DmenuSession
        {
            Prompt = "pick",
            Message = "hello",
            WindowTitle = "Invoke Menu",
            InitialQuery = "be",
            CaseInsensitive = true,
            NoCustom = true,
            MarkupRows = true,
            Format = "iq",
            Separator = "|",
            SelectedRow = 1,
            PreselectedText = "beta",
            UrgentRows = [0],
            ActiveRows = [1],
            Lines = 7,
            Entries = ["alpha", "beta"],
            OutputPath = workspace.GetPath("out.txt")
        };

        session.Save(path);
        var reloaded = DmenuSession.Load(path);

        Assert.AreEqual("pick", reloaded.Prompt);
        Assert.AreEqual("hello", reloaded.Message);
        Assert.AreEqual("Invoke Menu", reloaded.WindowTitle);
        Assert.AreEqual("be", reloaded.InitialQuery);
        Assert.IsTrue(reloaded.CaseInsensitive);
        Assert.IsTrue(reloaded.NoCustom);
        Assert.IsTrue(reloaded.MarkupRows);
        Assert.AreEqual("iq", reloaded.Format);
        Assert.AreEqual("|", reloaded.Separator);
        Assert.AreEqual(1, reloaded.SelectedRow);
        Assert.AreEqual("beta", reloaded.PreselectedText);
        CollectionAssert.AreEqual(new[] { 0 }, reloaded.UrgentRows);
        CollectionAssert.AreEqual(new[] { 1 }, reloaded.ActiveRows);
        Assert.AreEqual(7, reloaded.Lines);
        CollectionAssert.AreEqual(new[] { "alpha", "beta" }, reloaded.Entries);
        Assert.AreEqual(workspace.GetPath("out.txt"), reloaded.OutputPath);
    }
}
