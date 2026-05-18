namespace Invoke.Core.Modes;

public sealed class EmptyMode(string id, string displayName) : ILauncherMode
{
    public string Id => id;
    public string DisplayName => displayName;
    public int Priority => 0;

    public ValueTask InitializeAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public Task<LauncherModeSnapshot> SearchAsync(LauncherQueryContext context, CancellationToken cancellationToken) =>
        Task.FromResult(new LauncherModeSnapshot(Id, DisplayName, null, []));

    public void Dispose()
    {
    }
}
