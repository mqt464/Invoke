using Invoke.Core.Actions;
using Invoke.Core.Config;
using Invoke.Core.Results;
using Invoke.Core.Services;

namespace Invoke.Core.Modes;

public sealed class FilesMode : ILauncherMode
{
    private const string IpcNotFound = "Everything IPC not found";
    private static readonly TimeSpan EverythingStartTimeout = TimeSpan.FromSeconds(3);

    private readonly ProcessRunner _processRunner;
    private readonly SemaphoreSlim _everythingStartGate = new(1, 1);
    private bool _attemptedEverythingStart;

    public FilesMode(ProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public string Id => "files";
    public string DisplayName => "files";
    public int Priority => 85;

    public ValueTask InitializeAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public async Task<LauncherModeSnapshot> SearchAsync(LauncherQueryContext context, CancellationToken cancellationToken)
    {
        var message = "Search files with Everything";
        if (string.IsNullOrWhiteSpace(context.Terms))
            return new LauncherModeSnapshot(Id, context.Settings.DisplayNames.GetValueOrDefault(Id, DisplayName), message, []);

        if (context.Terms.Trim().Length < 2)
            return new LauncherModeSnapshot(Id, context.Settings.DisplayNames.GetValueOrDefault(Id, DisplayName), "Type at least 2 characters for file search", []);

        var esPath = ResolveEsPath(context.Settings);
        if (esPath is null)
        {
            return new LauncherModeSnapshot(
                Id,
                context.Settings.DisplayNames.GetValueOrDefault(Id, DisplayName),
                "Everything CLI not found",
                [MissingEverythingEntry()]);
        }

        var args = $"-n {context.MaxResults} {Quote(context.Terms)}";
        var result = await SearchEverythingAsync(context.Settings, esPath, args, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            return new LauncherModeSnapshot(
                Id,
                context.Settings.DisplayNames.GetValueOrDefault(Id, DisplayName),
                BuildErrorMessage(result),
                []);
        }

        var entries = result.Output
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(path => CreateEntry(path, context))
            .Where(static entry => entry is not null && entry.Score >= 0)
            .Select(static entry => entry!)
            .Take(context.MaxResults)
            .ToArray();

        return new LauncherModeSnapshot(Id, context.Settings.DisplayNames.GetValueOrDefault(Id, DisplayName), message, entries);
    }

    public void Dispose()
    {
        _everythingStartGate.Dispose();
    }

    private LauncherModeEntry? CreateEntry(string path, LauncherQueryContext context)
    {
        if (File.Exists(path))
        {
            var name = Path.GetFileName(path);
            return new LauncherModeEntry(
                path,
                name,
                path,
                ResultKind.File,
                Scoring.Match(name, context.Terms, 420, context.Settings),
                new InvokeAction($"file:{path}", "Open file", _ => _processRunner.LaunchAsync(path)),
                path,
                path);
        }

        if (Directory.Exists(path))
        {
            var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            name = string.IsNullOrWhiteSpace(name) ? path : name;
            return new LauncherModeEntry(
                path,
                name,
                path,
                ResultKind.Folder,
                Scoring.Match(name, context.Terms, 400, context.Settings),
                new InvokeAction($"folder:{path}", "Open folder", _ => _processRunner.LaunchAsync(path)),
                path,
                path);
        }

        return null;
    }

    private LauncherModeEntry MissingEverythingEntry() =>
        new(
            "Everything not installed",
            "Open Everything download page",
            "Bundle es.exe/Everything.exe or set everything-cli-path.",
            ResultKind.Config,
            320,
            new InvokeAction("everything:download", "Open Everything download page", _ => _processRunner.LaunchAsync("https://www.voidtools.com/downloads/")),
            "config");

    private string? ResolveEsPath(InvokeSettings settings)
        => ResolveFirstExistingPath(EnumerateEsCandidates(settings));

    private string? ResolveEverythingPath(InvokeSettings settings)
        => ResolveFirstExistingPath(EnumerateEverythingCandidates(settings));

    private IEnumerable<string> EnumerateEsCandidates(InvokeSettings settings)
    {
        var configuredCliPath = ExpandConfiguredPath(settings.EverythingCliPath);
        if (configuredCliPath is not null)
            yield return configuredCliPath;

        var configuredEverythingPath = ExpandConfiguredPath(settings.EverythingPath);
        if (configuredEverythingPath is not null)
            yield return Path.Combine(Path.GetDirectoryName(configuredEverythingPath) ?? string.Empty, "es.exe");

        foreach (var candidate in EnumerateBundledCandidates("es.exe"))
            yield return candidate;

        foreach (var candidate in EnumeratePathExecutableCandidates("es.exe"))
            yield return candidate;
    }

    private IEnumerable<string> EnumerateEverythingCandidates(InvokeSettings settings)
    {
        var configuredEverythingPath = ExpandConfiguredPath(settings.EverythingPath);
        if (configuredEverythingPath is not null)
            yield return configuredEverythingPath;

        var configuredCliPath = ExpandConfiguredPath(settings.EverythingCliPath);
        if (configuredCliPath is not null)
            yield return Path.Combine(Path.GetDirectoryName(configuredCliPath) ?? string.Empty, "Everything.exe");

        foreach (var candidate in EnumerateBundledCandidates("Everything.exe"))
            yield return candidate;

        foreach (var folder in ProgramFileFolders())
        {
            yield return Path.Combine(folder, "Everything", "Everything.exe");
            yield return Path.Combine(folder, "voidtools", "Everything", "Everything.exe");
        }

        foreach (var candidate in EnumeratePathExecutableCandidates("Everything.exe"))
            yield return candidate;
    }

    private static string? ResolveFirstExistingPath(IEnumerable<string> candidates)
    {
        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static string? ExpandConfiguredPath(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : Environment.ExpandEnvironmentVariables(path);

    private static IEnumerable<string> EnumerateBundledCandidates(string fileName)
    {
        yield return Path.Combine(AppContext.BaseDirectory, fileName);
        yield return Path.Combine(AppContext.BaseDirectory, "tools", "Everything", fileName);
        yield return Path.Combine(Environment.CurrentDirectory, "tools", "Everything", fileName);
    }

    private static IEnumerable<string> EnumeratePathExecutableCandidates(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var folder in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            yield return Path.Combine(folder, fileName);
    }

    private static IEnumerable<string> ProgramFileFolders()
    {
        var folders = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs")
        };

        return folders.Where(static folder => !string.IsNullOrWhiteSpace(folder));
    }

    private async Task<ProcessResult> SearchEverythingAsync(InvokeSettings settings, string esPath, string args, CancellationToken cancellationToken)
    {
        var result = await _processRunner.CaptureAsync(esPath, args, cancellationToken).ConfigureAwait(false);
        if (!IsEverythingIpcMissing(result))
            return result;

        if (!await TryStartEverythingAsync(settings, cancellationToken).ConfigureAwait(false))
            return result;

        var startedAt = DateTimeOffset.UtcNow;
        do
        {
            await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            result = await _processRunner.CaptureAsync(esPath, args, cancellationToken).ConfigureAwait(false);
            if (!IsEverythingIpcMissing(result))
                return result;
        }
        while (DateTimeOffset.UtcNow - startedAt < EverythingStartTimeout);

        return result;
    }

    private async Task<bool> TryStartEverythingAsync(InvokeSettings settings, CancellationToken cancellationToken)
    {
        var everythingPath = ResolveEverythingPath(settings);
        if (everythingPath is null)
            return false;

        await _everythingStartGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_attemptedEverythingStart)
                return true;

            await _processRunner.LaunchAsync(everythingPath, "-startup").ConfigureAwait(false);
            _attemptedEverythingStart = true;
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            _everythingStartGate.Release();
        }
    }

    private static bool IsEverythingIpcMissing(ProcessResult result)
    {
        if (result.ExitCode != 8)
            return false;

        var text = string.Join(Environment.NewLine, result.Output, result.Error);
        return text.Contains(IpcNotFound, StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Error 8", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildErrorMessage(ProcessResult result)
    {
        if (IsEverythingIpcMissing(result))
            return "Everything not running. Invoke tried to start it automatically.";

        return string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
}
