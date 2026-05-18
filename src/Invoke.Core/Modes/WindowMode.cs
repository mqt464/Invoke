using System.Diagnostics;
using System.Runtime.InteropServices;
using Invoke.Core.Actions;
using Invoke.Core.Results;
using Invoke.Core.Services;

namespace Invoke.Core.Modes;

public sealed class WindowMode : ILauncherMode
{
    public string Id => "window";
    public string DisplayName => "window";
    public int Priority => 95;

    public ValueTask InitializeAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public Task<LauncherModeSnapshot> SearchAsync(LauncherQueryContext context, CancellationToken cancellationToken)
    {
        var entries = EnumerateWindows()
            .Select(window => (window, score: string.IsNullOrWhiteSpace(context.Terms) ? 420 : Math.Max(Scoring.Match(window.Title, context.Terms, 520, context.Settings), Scoring.Match(window.ProcessName, context.Terms, 460, context.Settings))))
            .Where(static item => item.score >= 0)
            .OrderByDescending(static item => item.score)
            .ThenBy(static item => item.window.Title, StringComparer.CurrentCultureIgnoreCase)
            .Take(context.MaxResults)
            .Select(item => new LauncherModeEntry(
                item.window.Title,
                item.window.Title,
                item.window.Subtitle,
                ResultKind.Command,
                item.score,
                new InvokeAction(
                    $"window:{item.window.Handle}",
                    "Switch to window",
                    _ => ActivateWindowAsync(item.window.Handle)),
                item.window.IconPath ?? "window",
                item.window.Title))
            .ToArray();

        return Task.FromResult(new LauncherModeSnapshot(Id, context.Settings.DisplayNames.GetValueOrDefault(Id, DisplayName), null, entries));
    }

    public void Dispose()
    {
    }

    private static IReadOnlyList<WindowEntry> EnumerateWindows()
    {
        var windows = new List<WindowEntry>();
        EnumWindows((handle, _) =>
        {
            if (!IsCandidateWindow(handle))
                return true;

            var title = GetWindowTitle(handle);
            if (string.IsNullOrWhiteSpace(title))
                return true;

            GetWindowThreadProcessId(handle, out var processId);
            var processName = TryGetProcessName(processId);
            windows.Add(new WindowEntry(
                handle,
                title.Trim(),
                processId == 0 ? processName : $"{processName} (PID {processId})",
                processName,
                TryGetProcessIconPath(processId)));
            return true;
        }, IntPtr.Zero);

        return windows;
    }

    private static bool IsCandidateWindow(IntPtr handle)
    {
        if (handle == IntPtr.Zero || !IsWindowVisible(handle) || GetWindow(handle, GwOwner) != IntPtr.Zero)
            return false;

        if (IsIconic(handle))
            return true;

        var style = GetWindowLongPtr(handle, GwlExstyle).ToInt64();
        return (style & WsExToolwindow) == 0;
    }

    private static string GetWindowTitle(IntPtr handle)
    {
        var length = GetWindowTextLength(handle);
        if (length <= 0)
            return string.Empty;

        var builder = new System.Text.StringBuilder(length + 1);
        _ = GetWindowText(handle, builder, builder.Capacity);
        return builder.ToString();
    }

    private static string TryGetProcessName(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return "Window";
        }
    }

    private static string? TryGetProcessIconPath(uint processId)
    {
        if (processId == 0)
            return null;

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    private static Task ActivateWindowAsync(IntPtr handle)
    {
        if (IsIconic(handle))
            ShowWindow(handle, SwRestore);
        else
            ShowWindow(handle, SwShow);

        SetForegroundWindow(handle);
        return Task.CompletedTask;
    }

    private sealed record WindowEntry(IntPtr Handle, string Title, string Subtitle, string ProcessName, string? IconPath);

    private const int GwlExstyle = -20;
    private const long WsExToolwindow = 0x00000080L;
    private const uint GwOwner = 4;
    private const int SwRestore = 9;
    private const int SwShow = 5;

    private delegate bool EnumWindowsProc(IntPtr handle, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);
}
