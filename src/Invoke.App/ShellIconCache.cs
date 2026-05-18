using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media.Imaging;

namespace Invoke.App;

internal static class ShellIconCache
{
    private const int MaxEntries = 256;
    private static readonly ConcurrentDictionary<string, BitmapSource> Icons = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentQueue<string> InsertionOrder = new();
    private static readonly ConcurrentDictionary<string, BitmapSource> UriIcons = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentQueue<string> UriInsertionOrder = new();
    private static readonly HashSet<string> PathScopedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".appref-ms",
        ".cpl",
        ".dll",
        ".exe",
        ".ico",
        ".lnk",
        ".msc",
        ".scr",
        ".url"
    };

    public static BitmapSource? Get(string path)
    {
        var cacheKey = BuildCacheKey(path);
        if (cacheKey is null)
            return null;

        return Icons.TryGetValue(cacheKey, out var icon)
            ? icon
            : null;
    }

    public static BitmapSource? GetUri(string uri) =>
        UriIcons.TryGetValue(uri, out var icon)
            ? icon
            : null;

    public static void Set(string path, BitmapSource icon)
    {
        var cacheKey = BuildCacheKey(path);
        if (cacheKey is null)
            return;

        if (Icons.TryAdd(cacheKey, icon))
        {
            InsertionOrder.Enqueue(cacheKey);
            Trim();
        }
    }

    public static void SetUri(string uri, BitmapSource icon)
    {
        if (string.IsNullOrWhiteSpace(uri))
            return;

        if (UriIcons.TryAdd(uri, icon))
        {
            UriInsertionOrder.Enqueue(uri);
            TrimUri();
        }
    }

    internal static string? BuildCacheKey(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var expanded = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        if (expanded.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
            return expanded;

        try
        {
            expanded = Path.GetFullPath(expanded);
        }
        catch
        {
        }

        if (Directory.Exists(expanded))
            return "dir";

        var extension = Path.GetExtension(expanded);
        if (string.IsNullOrWhiteSpace(extension))
            return File.Exists(expanded) ? "file" : expanded;

        if (PathScopedExtensions.Contains(extension))
            return expanded;

        return "ext:" + extension;
    }

    internal static void ClearForTests()
    {
        Icons.Clear();
        while (InsertionOrder.TryDequeue(out _))
        {
        }

        UriIcons.Clear();
        while (UriInsertionOrder.TryDequeue(out _))
        {
        }
    }

    internal static int CountForTests => Icons.Count;
    internal static int UriCountForTests => UriIcons.Count;

    private static void Trim()
    {
        while (Icons.Count > MaxEntries && InsertionOrder.TryDequeue(out var oldestKey))
            Icons.TryRemove(oldestKey, out _);
    }

    private static void TrimUri()
    {
        while (UriIcons.Count > MaxEntries && UriInsertionOrder.TryDequeue(out var oldestKey))
            UriIcons.TryRemove(oldestKey, out _);
    }
}
