using System.Runtime.InteropServices;
using Invoke.Core.Actions;
using Invoke.Core.Results;
using Invoke.Core.Services;

namespace Invoke.Core.Modes;

public sealed class DrunMode : ILauncherMode
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(30);
    private static readonly EnumerationOptions EnumerationOptions = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = 0
    };

    private readonly ProcessRunner _processRunner;
    private readonly IReadOnlyList<AppEntry>? _appOverrides;
    private readonly object _gate = new();
    private IReadOnlyList<AppEntry> _apps = [];
    private DateTimeOffset _loadedAt = DateTimeOffset.MinValue;

    public DrunMode(ProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    internal DrunMode(ProcessRunner processRunner, IReadOnlyList<(string Name, string Path, bool IsShell)> appOverrides)
    {
        _processRunner = processRunner;
        _appOverrides = appOverrides
            .Select(static app => new AppEntry(app.Name, app.Path, app.IsShell ? AppEntryKind.Shell : AppEntryKind.File))
            .ToArray();
    }

    public string Id => "drun";
    public string DisplayName => "drun";
    public int Priority => 90;

    public ValueTask InitializeAsync(CancellationToken cancellationToken)
    {
        EnsureCache();
        return ValueTask.CompletedTask;
    }

    public Task<LauncherModeSnapshot> SearchAsync(LauncherQueryContext context, CancellationToken cancellationToken)
    {
        var results = GetApps()
            .Select(app => CreateEntry(app, context))
            .Where(static entry => entry is not null)
            .Select(static entry => entry!)
            .OrderByDescending(static item => item.Score)
            .ThenBy(static item => item.DisplayText, StringComparer.CurrentCultureIgnoreCase)
            .Take(context.MaxResults)
            .ToArray();

        return Task.FromResult(new LauncherModeSnapshot(Id, context.Settings.DisplayNames.GetValueOrDefault(Id, DisplayName), null, results));
    }

    private LauncherModeEntry? CreateEntry(AppEntry app, LauncherQueryContext context)
    {
        var baseScore = string.IsNullOrWhiteSpace(context.Terms)
            ? 220
            : Scoring.Match(app.Name, context.Terms, 500, context.Settings);
        if (baseScore < 0)
            return null;

        var title = ModeEntryFormatter.Format(
            context.Settings.ResultTitleTemplates.GetValueOrDefault(Id),
            app.Name,
            app.Name,
            app.Name,
            app.Path,
            null,
            null,
            app.Name,
            "app",
            ResultKind.App.ToString(),
            Id);
        var subtitle = ModeEntryFormatter.Format(
            context.Settings.ResultSubtitleTemplates.GetValueOrDefault(Id),
            app.Path,
            app.Name,
            app.Name,
            app.Path,
            null,
            null,
            app.Name,
            "app",
            ResultKind.App.ToString(),
            Id);
        var score = baseScore + RecentLaunchScoring.GetBoost(context.Settings.History, ResultKind.App, title, subtitle, "Launch app");

        return new LauncherModeEntry(
            app.Name,
            app.Name,
            app.Path,
            ResultKind.App,
            score,
            new InvokeAction(
                $"app:{app.Path}",
                "Launch app",
                _ => app.Kind == AppEntryKind.Shell
                    ? _processRunner.LaunchAsync("explorer.exe", app.Path)
                    : _processRunner.LaunchAsync(app.Path)),
            "app",
            app.Name);
    }

    private void EnsureCache()
    {
        lock (_gate)
        {
            if (DateTimeOffset.UtcNow - _loadedAt <= CacheLifetime)
                return;

            _apps = LoadApps();
            _loadedAt = DateTimeOffset.UtcNow;
        }
    }

    private IReadOnlyList<AppEntry> GetApps()
    {
        if (_appOverrides is not null)
            return _appOverrides;

        EnsureCache();
        lock (_gate)
            return _apps;
    }

    private static IReadOnlyList<AppEntry> LoadApps()
    {
        var folders = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps")
        };

        var shortcutApps = folders
            .Where(Directory.Exists)
            .SelectMany(EnumerateFiles)
            .Where(path => path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".appref-ms", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            .Select(path => new AppEntry(Path.GetFileNameWithoutExtension(path), path, AppEntryKind.File));

        return shortcutApps
            .Concat(LoadShellApps())
            .GroupBy(static app => app.Name, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
    }

    private static IEnumerable<string> EnumerateFiles(string folder)
    {
        try
        {
            return Directory.EnumerateFiles(folder, "*.*", EnumerationOptions);
        }
        catch
        {
            return [];
        }
    }

    private static IEnumerable<AppEntry> LoadShellApps()
    {
        object? shellApplication = null;
        object? appsFolder = null;
        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType is null)
                return [];

            shellApplication = Activator.CreateInstance(shellType);
            if (shellApplication is null)
                return [];

            dynamic shell = shellApplication;
            appsFolder = shell.NameSpace("shell:AppsFolder");
            if (appsFolder is null)
                return [];

            var apps = new List<AppEntry>();
            foreach (var item in ((dynamic)appsFolder).Items())
            {
                string name = item.Name;
                string path = item.Path;
                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(path))
                    apps.Add(CreateShellAppEntry(name, path));
            }

            return apps;
        }
        catch
        {
            return [];
        }
        finally
        {
            ReleaseComObject(appsFolder);
            ReleaseComObject(shellApplication);
        }
    }

    private static AppEntry CreateShellAppEntry(string name, string path)
    {
        if (Path.IsPathFullyQualified(path))
            return new AppEntry(name, path, AppEntryKind.File);

        return new AppEntry(name, path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase) ? path : $@"shell:AppsFolder\{path}", AppEntryKind.Shell);
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
            Marshal.FinalReleaseComObject(value);
    }

    public void Dispose()
    {
    }

    private sealed record AppEntry(string Name, string Path, AppEntryKind Kind);

    private enum AppEntryKind
    {
        File,
        Shell
    }
}
