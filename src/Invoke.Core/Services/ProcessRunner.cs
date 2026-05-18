using System.Diagnostics;

namespace Invoke.Core.Services;

public class ProcessRunner
{
    public virtual async Task<ProcessResult> CaptureAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        return await CaptureAsync(fileName, arguments, null, null, cancellationToken).ConfigureAwait(false);
    }

    public virtual async Task<ProcessResult> CaptureAsync(
        string fileName,
        string arguments,
        string? standardInput,
        string? workingDirectory,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = standardInput is not null,
            WorkingDirectory = workingDirectory ?? string.Empty,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to start {fileName}.");
        using var cancellationRegistration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
        });

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        if (standardInput is not null)
        {
            await process.StandardInput.WriteAsync(standardInput.AsMemory(), cancellationToken).ConfigureAwait(false);
            process.StandardInput.Close();
        }

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return new ProcessResult(process.ExitCode, await outputTask.ConfigureAwait(false), await errorTask.ConfigureAwait(false));
    }

    public virtual Task LaunchAsync(string fileName, string? arguments = null, string? workingDirectory = null, bool runAsAdmin = false)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments ?? string.Empty)
        {
            UseShellExecute = true,
            WorkingDirectory = workingDirectory ?? string.Empty,
            Verb = runAsAdmin ? "runas" : string.Empty
        };

        Start(startInfo);
        return Task.CompletedTask;
    }

    protected virtual Process? Start(ProcessStartInfo startInfo) => Process.Start(startInfo);
}

public sealed record ProcessResult(int ExitCode, string Output, string Error);
