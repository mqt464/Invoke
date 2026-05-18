namespace Invoke.Tests;

internal sealed class TestWorkspace : IDisposable
{
    public TestWorkspace()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Invoke.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string GetPath(params string[] segments) =>
        segments.Aggregate(Path, System.IO.Path.Combine);

    public void Dispose()
    {
        if (Directory.Exists(Path))
            Directory.Delete(Path, recursive: true);
    }
}
