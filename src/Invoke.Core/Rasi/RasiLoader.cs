namespace Invoke.Core.Rasi;

public sealed class RasiLoader
{
    public RasiDocument LoadFile(string path)
    {
        return LoadFile(path, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    private RasiDocument LoadFile(string path, HashSet<string> visitedPaths)
    {
        var fullPath = Path.GetFullPath(path);
        if (!visitedPaths.Add(fullPath))
            return new RasiDocument();

        if (!File.Exists(fullPath))
            return new RasiDocument();

        var document = RasiParser.Parse(File.ReadAllText(fullPath));
        var merged = new RasiDocument();
        foreach (var import in document.Imports)
        {
            var importPath = ResolveRelativePath(Path.GetDirectoryName(fullPath) ?? AppContext.BaseDirectory, import);
            merged.Merge(LoadFile(importPath, visitedPaths));
        }

        if (!string.IsNullOrWhiteSpace(document.ThemeReference))
        {
            var themePath = ResolveRelativePath(Path.GetDirectoryName(fullPath) ?? AppContext.BaseDirectory, document.ThemeReference);
            merged.Merge(LoadFile(themePath, visitedPaths));
        }

        merged.Merge(document);
        return merged;
    }

    public static string ResolveRelativePath(string baseDirectory, string path)
    {
        var trimmed = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        if (Path.IsPathRooted(trimmed))
            return trimmed;

        return Path.GetFullPath(Path.Combine(baseDirectory, trimmed));
    }
}
