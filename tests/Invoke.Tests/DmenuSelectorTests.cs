using Invoke.Core.Dmenu;

namespace Invoke.Tests;

[TestClass]
public sealed class DmenuSelectorTests
{
    [TestMethod]
    public void Filter_PrefersExactAndPrefixMatches()
    {
        var entries = new[] { "beta", "alphabet", "alpha" };

        var filtered = DmenuSelector.Filter(entries, "alpha", caseSensitive: true);

        CollectionAssert.AreEqual(new[] { "alpha", "alphabet" }, filtered.ToArray());
    }

    [TestMethod]
    public void SelectBest_HonorsCaseInsensitiveFlag()
    {
        var entries = new[] { "PowerShell", "pwsh", "paint" };

        var selected = DmenuSelector.SelectBest(entries, "powershell", caseSensitive: false);

        Assert.AreEqual("PowerShell", selected);
    }
}
