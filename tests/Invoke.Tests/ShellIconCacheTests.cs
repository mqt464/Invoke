using System.Windows.Media;
using System.Windows.Media.Imaging;
using Invoke.App;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Invoke.Tests;

[TestClass]
public sealed class ShellIconCacheTests
{
    [TestInitialize]
    public void Initialize()
    {
        ShellIconCache.ClearForTests();
    }

    [TestMethod]
    public void BuildCacheKey_UsesExtensionForRegularFiles()
    {
        using var workspace = new TestWorkspace();
        var first = workspace.GetPath("docs", "notes.txt");
        var second = workspace.GetPath("docs", "todo.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(first)!);
        File.WriteAllText(first, "one");
        File.WriteAllText(second, "two");

        var firstKey = ShellIconCache.BuildCacheKey(first);
        var secondKey = ShellIconCache.BuildCacheKey(second);

        Assert.AreEqual("ext:.txt", firstKey);
        Assert.AreEqual(firstKey, secondKey);
    }

    [TestMethod]
    public void BuildCacheKey_KeepsPathForExecutables()
    {
        using var workspace = new TestWorkspace();
        var first = workspace.GetPath("apps", "first.exe");
        var second = workspace.GetPath("apps", "second.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(first)!);
        File.WriteAllText(first, string.Empty);
        File.WriteAllText(second, string.Empty);

        var firstKey = ShellIconCache.BuildCacheKey(first);
        var secondKey = ShellIconCache.BuildCacheKey(second);

        Assert.AreNotEqual(firstKey, secondKey);
        Assert.AreEqual(Path.GetFullPath(first), firstKey);
    }

    [TestMethod]
    public void Set_TrimsCacheToBoundedSize()
    {
        for (var index = 0; index < 400; index++)
            ShellIconCache.Set($@"shell:AppsFolder\app{index}", CreateIcon());

        Assert.IsLessThanOrEqualTo(ShellIconCache.CountForTests, 256);
    }

    [TestMethod]
    public void SetUri_TrimsCacheToBoundedSize()
    {
        for (var index = 0; index < 400; index++)
            ShellIconCache.SetUri($"file:///icons/{index}.png", CreateIcon());

        Assert.IsLessThanOrEqualTo(ShellIconCache.UriCountForTests, 256);
    }

    private static BitmapSource CreateIcon()
    {
        var pixels = new byte[] { 255, 255, 255, 255 };
        var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, pixels, 4);
        bitmap.Freeze();
        return bitmap;
    }
}
