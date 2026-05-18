using System.Diagnostics;
using Invoke.Core.Plugins.Rich;

namespace Invoke.App;

internal sealed class RichScriptBackgroundHost : IDisposable
{
    private readonly Dictionary<string, HostedProcess> _processes = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public void Start(IEnumerable<RichScriptDefinition> scripts)
    {
        ThrowIfDisposed();

        foreach (var script in scripts.Where(static item => !string.IsNullOrWhiteSpace(item.Manifest.BackgroundEntry)))
            StartProcess(script);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        foreach (var hosted in _processes.Values)
            hosted.Dispose();

        _processes.Clear();
    }

    private void StartProcess(RichScriptDefinition definition)
    {
        if (_processes.ContainsKey(definition.Manifest.Id))
            return;

        var startInfo = RichScriptProcessResolver.CreateBackgroundStartInfo(definition);
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardOutput = false;
        startInfo.RedirectStandardError = false;
        startInfo.RedirectStandardInput = false;

        var process = Process.Start(startInfo);
        if (process is null)
            return;

        var hosted = new HostedProcess(definition, process);
        if (definition.Manifest.BackgroundRestartOnExit)
        {
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) =>
            {
                if (_disposed)
                    return;

                hosted.Dispose();
                _processes.Remove(definition.Manifest.Id);
                StartProcess(definition);
            };
        }

        _processes[definition.Manifest.Id] = hosted;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(RichScriptBackgroundHost));
    }

    private sealed class HostedProcess(RichScriptDefinition definition, Process process) : IDisposable
    {
        private bool _disposed;

        public RichScriptDefinition Definition { get; } = definition;
        public Process Process { get; } = process;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
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
}
