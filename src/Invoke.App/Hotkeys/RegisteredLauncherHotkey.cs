using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Invoke.App.Hotkeys;

internal sealed class RegisteredLauncherHotkey : NativeWindow, IDisposable
{
    private const int HotkeyId = 0x5A11;
    private const int WmHotkey = 0x0312;
    private const uint ModNorepeat = 0x4000;

    private bool _registered;

    public event EventHandler? LauncherRequested;

    private RegisteredLauncherHotkey()
    {
        CreateHandle(new CreateParams());
    }

    public static RegisteredLauncherHotkey? TryCreate(LauncherHotkeyGesture gesture)
    {
        var window = new RegisteredLauncherHotkey();
        if (window.TryRegister(gesture))
            return window;

        window.Dispose();
        return null;
    }

    public void Dispose()
    {
        if (_registered && Handle != IntPtr.Zero)
        {
            UnregisterHotKey(Handle, HotkeyId);
            _registered = false;
        }

        if (Handle != IntPtr.Zero)
            DestroyHandle();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotkey && m.WParam.ToInt32() == HotkeyId)
            LauncherRequested?.Invoke(this, EventArgs.Empty);

        base.WndProc(ref m);
    }

    private bool TryRegister(LauncherHotkeyGesture gesture)
    {
        _registered = RegisterHotKey(Handle, HotkeyId, gesture.Modifiers | ModNorepeat, gesture.VirtualKey);
        return _registered;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
