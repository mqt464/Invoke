using System.Text.Json;
using Invoke.Core.Actions;
using Invoke.Core.Plugins.External;
using Invoke.Core.Plugins.Rich;
using Invoke.Core.Results;
using Invoke.Core.Services;

namespace Invoke.Core.Modes;

public sealed class RichExternalMode : ILauncherMode
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly RichScriptDefinition _definition;
    private readonly ProcessRunner _processRunner;
    private readonly SemaphoreSlim _sessionLock = new(1, 1);
    private PersistentSession? _session;

    public RichExternalMode(RichScriptDefinition definition, ProcessRunner processRunner)
    {
        _definition = definition;
        _processRunner = processRunner;
    }

    public string Id => _definition.Manifest.Id;
    public string DisplayName => _definition.Manifest.Name;
    public int Priority => _definition.Manifest.Priority;

    public ValueTask InitializeAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public async Task<LauncherModeSnapshot> SearchAsync(LauncherQueryContext context, CancellationToken cancellationToken)
    {
        if (!ShouldRun(context.RawQuery, context.ActiveMode, out var trigger, out var terms))
            return new LauncherModeSnapshot(Id, DisplayName, null, []);

        var request = new ExternalPluginRequest(context.RawQuery, trigger, terms, context.ActiveMode ?? context.Settings.DefaultMode, context.MaxResults);
        var output = _definition.Manifest.KeepAlive
            ? await SendPersistentAsync(request, cancellationToken).ConfigureAwait(false)
            : await SendOneShotAsync(request, cancellationToken).ConfigureAwait(false);

        if (output.ExitCode != 0)
        {
            return new LauncherModeSnapshot(
                Id,
                DisplayName,
                null,
                [new LauncherModeEntry($"{DisplayName} error", $"{DisplayName} error", output.Error.Length > 0 ? output.Error : output.Output, ResultKind.Error, -1000, new InvokeAction("noop", "Dismiss", _ => Task.CompletedTask), "error")]);
        }

        var response = DeserializeResponse(output.Output);
        return new LauncherModeSnapshot(
            Id,
            response.Prompt ?? DisplayName,
            response.Message,
            response.Entries,
            DisplayPrefix: response.DisplayPrefix,
            RawPrefix: response.RawPrefix);
    }

    private bool ShouldRun(string rawQuery, string? activeMode, out string trigger, out string terms)
    {
        trigger = string.Empty;
        terms = string.Empty;
        var trimmed = rawQuery.Trim();

        if (string.Equals(activeMode, Id, StringComparison.OrdinalIgnoreCase))
        {
            terms = trimmed;
            return true;
        }

        if (_definition.Manifest.TriggerMode.Equals(RichScriptManifest.PrefixTokenTriggerMode, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var configuredTrigger in _definition.Manifest.Triggers)
            {
                if (!trimmed.StartsWith(configuredTrigger, StringComparison.OrdinalIgnoreCase))
                    continue;

                var splitIndex = trimmed.IndexOf(' ');
                trigger = splitIndex < 0 ? trimmed : trimmed[..splitIndex];
                if (trigger.Length == 0)
                    continue;

                terms = splitIndex < 0 ? string.Empty : trimmed[(splitIndex + 1)..].Trim();
                return true;
            }
        }

        foreach (var configuredTrigger in _definition.Manifest.Triggers)
        {
            if (trimmed.Equals(configuredTrigger, StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith(configuredTrigger + " ", StringComparison.OrdinalIgnoreCase))
            {
                trigger = configuredTrigger;
                terms = trimmed.Length == configuredTrigger.Length ? string.Empty : trimmed[(configuredTrigger.Length + 1)..].Trim();
                return true;
            }
        }

        return _definition.Manifest.RunForQuery && !string.IsNullOrWhiteSpace(trimmed);
    }

    private async Task<ProcessResult> SendOneShotAsync(ExternalPluginRequest request, CancellationToken cancellationToken)
    {
        var executable = RichScriptProcessResolver.Resolve(_definition);
        return await _processRunner.CaptureAsync(
            executable.FileName,
            executable.Arguments,
            JsonSerializer.Serialize(request, JsonOptions),
            _definition.Directory,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<ProcessResult> SendPersistentAsync(ExternalPluginRequest request, CancellationToken cancellationToken)
    {
        await _sessionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var session = await GetOrStartSessionAsync().ConfigureAwait(false);
            await session.Writer.WriteLineAsync(JsonSerializer.Serialize(request, JsonOptions)).ConfigureAwait(false);
            await session.Writer.FlushAsync().ConfigureAwait(false);
            var output = await session.Reader.ReadLineAsync().ConfigureAwait(false);
            return output is null
                ? new ProcessResult(-1, string.Empty, $"{DisplayName} session closed unexpectedly.")
                : new ProcessResult(0, output, string.Empty);
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    private Task<PersistentSession> GetOrStartSessionAsync()
    {
        if (_session is { Process.HasExited: false } current)
            return Task.FromResult(current);

        _session?.Dispose();
        var executable = RichScriptProcessResolver.Resolve(_definition, "--session");
        var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(executable.FileName, executable.Arguments)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _definition.Directory,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException($"Failed to start {DisplayName}.");
        _session = new PersistentSession(process);
        return Task.FromResult(_session);
    }

    private ExternalResponse DeserializeResponse(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return new ExternalResponse(null, null, null, null, []);

        using var document = JsonDocument.Parse(output);
        if (document.RootElement.ValueKind == JsonValueKind.Object &&
            document.RootElement.TryGetProperty("results", out _))
        {
            var response = JsonSerializer.Deserialize<ExternalPluginResponse>(document.RootElement.GetRawText(), JsonOptions) ?? new ExternalPluginResponse();
            return new ExternalResponse(
                response.Prompt,
                response.Message,
                response.DisplayPrefix,
                response.RawPrefix,
                response.Results
                    .Where(static result => !string.IsNullOrWhiteSpace(result.Title))
                    .Select(ToEntry)
                    .ToArray());
        }

        var results = document.RootElement.ValueKind switch
        {
            JsonValueKind.Array => JsonSerializer.Deserialize<List<ExternalPluginResult>>(document.RootElement.GetRawText(), JsonOptions) ?? [],
            JsonValueKind.Object => JsonSerializer.Deserialize<ExternalPluginResult>(document.RootElement.GetRawText(), JsonOptions) is { } singleResult ? [singleResult] : [],
            _ => []
        };

        return new ExternalResponse(
            null,
            null,
            null,
            null,
            results
                .Where(static result => !string.IsNullOrWhiteSpace(result.Title))
                .Select(ToEntry)
                .ToArray());
    }

    private LauncherModeEntry ToEntry(ExternalPluginResult result)
    {
        return new LauncherModeEntry(
            result.Title,
            result.Title,
            result.Subtitle ?? string.Empty,
            ParseKind(result.Kind),
            result.Score ?? Priority,
            BuildInvokeAction(result),
            result.Icon,
            result.Title,
            result.Subtitle,
            Id: result.Id);
    }

    private InvokeAction BuildInvokeAction(ExternalPluginResult result)
    {
        var action = result.Action;
        var actionKind = ParseActionKind(action?.Kind);
        var command = action?.Command;
        var workingDirectory = action?.WorkingDirectory;
        if (command is not null && !Path.IsPathRooted(command))
        {
            var candidate = Path.Combine(_definition.Directory, command);
            if (File.Exists(candidate))
                command = candidate;
        }

        if (!string.IsNullOrWhiteSpace(workingDirectory) && !Path.IsPathRooted(workingDirectory))
            workingDirectory = Path.Combine(_definition.Directory, workingDirectory);

        var target = actionKind switch
        {
            InvokeActionKind.OpenUrl => action?.Url,
            InvokeActionKind.OpenPath => ResolveActionPath(action?.Path),
            InvokeActionKind.CopyText => action?.Text,
            InvokeActionKind.SetQuery => action?.Query,
            _ => null
        };

        return new InvokeAction(
            result.Id ?? $"rich:{Id}:{result.Title}",
            ResolveActionTitle(actionKind, action?.Title),
            command is null ? _ => Task.CompletedTask : _ => _processRunner.LaunchAsync(command, action?.Arguments, workingDirectory, action?.RunAsAdministrator ?? false),
            action?.RequiresConfirmation ?? false,
            action?.ConfirmationText,
            actionKind,
            target);
    }

    private string? ResolveActionPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        return Path.IsPathRooted(path)
            ? path
            : Path.Combine(_definition.Directory, path);
    }

    private static ResultKind ParseKind(string? kind) =>
        Enum.TryParse<ResultKind>(kind, true, out var parsed) ? parsed : ResultKind.Command;

    private static InvokeActionKind ParseActionKind(string? kind) =>
        kind?.Trim().ToLowerInvariant() switch
        {
            "execute" or "command" or null or "" => InvokeActionKind.Execute,
            "open-url" or "url" => InvokeActionKind.OpenUrl,
            "open-path" or "path" => InvokeActionKind.OpenPath,
            "copy" or "copy-text" => InvokeActionKind.CopyText,
            "set-query" or "query" => InvokeActionKind.SetQuery,
            _ => InvokeActionKind.Execute
        };

    private static string ResolveActionTitle(InvokeActionKind actionKind, string? title) =>
        !string.IsNullOrWhiteSpace(title)
            ? title
            : actionKind switch
            {
                InvokeActionKind.OpenUrl => "Open URL",
                InvokeActionKind.OpenPath => "Open path",
                InvokeActionKind.CopyText => "Copy text",
                InvokeActionKind.SetQuery => "Set query",
                _ => "Run"
            };

    public void Dispose()
    {
        _session?.Dispose();
        _session = null;
        _sessionLock.Dispose();
    }

    private sealed class PersistentSession : IDisposable
    {
        public PersistentSession(System.Diagnostics.Process process)
        {
            Process = process;
            Writer = process.StandardInput;
            Reader = process.StandardOutput;
        }

        public System.Diagnostics.Process Process { get; }
        public StreamWriter Writer { get; }
        public StreamReader Reader { get; }

        public void Dispose()
        {
            try { Writer.Dispose(); } catch { }
            try { Reader.Dispose(); } catch { }
            try
            {
                if (!Process.HasExited)
                {
                    Process.Kill(entireProcessTree: true);
                    Process.WaitForExit(2_000);
                }
            }
            catch
            {
            }

            Process.Dispose();
        }
    }

    private sealed record ExternalResponse(
        string? Prompt,
        string? Message,
        string? DisplayPrefix,
        string? RawPrefix,
        LauncherModeEntry[] Entries);
}
