using Invoke.Core.Actions;
using Invoke.Core.Results;
using Invoke.Core.Services;

namespace Invoke.Core.Modes;

public sealed class RunMode : ILauncherMode
{
    private readonly ProcessRunner _processRunner;
    private readonly object _gate = new();
    private IReadOnlyList<string> _executables = [];
    private DateTimeOffset _loadedAt = DateTimeOffset.MinValue;

    public RunMode(ProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public string Id => "run";
    public string DisplayName => "run";
    public int Priority => 80;

    public ValueTask InitializeAsync(CancellationToken cancellationToken)
    {
        EnsureCache();
        return ValueTask.CompletedTask;
    }

    public Task<LauncherModeSnapshot> SearchAsync(LauncherQueryContext context, CancellationToken cancellationToken)
    {
        var entries = new List<LauncherModeEntry>();
        if (!string.IsNullOrWhiteSpace(context.Terms))
        {
            entries.Add(new LauncherModeEntry(
                $"Run {context.Terms}",
                $"Run {context.Terms}",
                "Execute command",
                ResultKind.Command,
                600,
                new InvokeAction($"run:{context.Terms}", "Run command", _ => _processRunner.LaunchAsync("cmd.exe", "/c " + context.Terms)),
                "terminal",
                context.Terms));
        }

        entries.AddRange(GetExecutables()
            .Select(executable =>
            {
                var score = string.IsNullOrWhiteSpace(context.Terms) ? 180 : Scoring.Match(executable, context.Terms, 480, context.Settings);
                return new LauncherModeEntry(
                    executable,
                    executable,
                    "Executable on PATH",
                    ResultKind.Command,
                    score,
                    new InvokeAction($"run:path:{executable}", "Run executable", _ => _processRunner.LaunchAsync(executable)),
                    "terminal",
                    executable);
            })
            .Where(static entry => entry.Score >= 0)
            .OrderByDescending(static entry => entry.Score)
            .Take(context.MaxResults));

        return Task.FromResult(new LauncherModeSnapshot(Id, context.Settings.DisplayNames.GetValueOrDefault(Id, DisplayName), null, entries.Take(context.MaxResults).ToArray()));
    }

    private void EnsureCache()
    {
        lock (_gate)
        {
            if (DateTimeOffset.UtcNow - _loadedAt < TimeSpan.FromMinutes(5))
                return;

            _executables = LoadExecutables();
            _loadedAt = DateTimeOffset.UtcNow;
        }
    }

    private IReadOnlyList<string> GetExecutables()
    {
        EnsureCache();
        lock (_gate)
            return _executables;
    }

    private static IReadOnlyList<string> LoadExecutables()
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        return pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(Directory.Exists)
            .SelectMany(folder =>
            {
                try
                {
                    return Directory.EnumerateFiles(folder, "*.exe", SearchOption.TopDirectoryOnly)
                        .Select(Path.GetFileNameWithoutExtension)
                        .Where(static name => !string.IsNullOrWhiteSpace(name))
                        .Cast<string>();
                }
                catch
                {
                    return [];
                }
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public void Dispose()
    {
    }
}
