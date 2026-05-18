using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Invoke.App.Hotkeys;

internal sealed class LowLevelLauncherHotkeyHook : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int LlkHfInjected = 0x00000010;
    private const ushort VkF24 = 0x87;
    private const uint InputKeyboard = 1;
    private const uint KeyeventfKeyup = 0x0002;

    private readonly LowLevelKeyboardProc _proc;
    private readonly IntPtr _hook;
    private readonly LauncherChordTracker _tracker;

    public event EventHandler<LauncherRequestedEventArgs>? LauncherRequested;

    public LowLevelLauncherHotkeyHook(IReadOnlyList<TrackedHotkeyBinding> bindings)
    {
        _tracker = new LauncherChordTracker(bindings);
        _proc = HookCallback;
        using var process = Process.GetCurrentProcess();
        var module = process.MainModule;
        _hook = SetWindowsHookEx(WhKeyboardLl, _proc, GetModuleHandle(module?.ModuleName), 0);
        if (_hook == IntPtr.Zero)
            throw new InvalidOperationException("Failed to install low-level keyboard hook.");
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
            UnhookWindowsHookEx(_hook);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
            return CallNextHookEx(_hook, nCode, wParam, lParam);

        var keyboard = Marshal.PtrToStructure<KbdllHookStruct>(lParam);
        var vkCode = keyboard.VkCode;
        if ((keyboard.Flags & LlkHfInjected) != 0)
            return CallNextHookEx(_hook, nCode, wParam, lParam);

        var isDown = wParam == WmKeyDown || wParam == WmSysKeyDown;
        var isUp = wParam == WmKeyUp || wParam == WmSysKeyUp;
        var result = _tracker.Process(vkCode, isDown, isUp);

        if (result.SendNeutralizer)
            NeutralizePendingModifierTap();

        if (result.RequestLauncher)
            LauncherRequested?.Invoke(this, new LauncherRequestedEventArgs(result.InitialMode));

        return result.SuppressEvent
            ? new IntPtr(1)
            : CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private static void NeutralizePendingModifierTap()
    {
        Input[] inputs =
        [
            new Input
            {
                Type = InputKeyboard,
                Union = new InputUnion
                {
                    KeyboardInput = new KeybdInput
                    {
                        Vk = VkF24
                    }
                }
            },
            new Input
            {
                Type = InputKeyboard,
                Union = new InputUnion
                {
                    KeyboardInput = new KeybdInput
                    {
                        Vk = VkF24,
                        Flags = KeyeventfKeyup
                    }
                }
            }
        ];

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, [In] Input[] inputs, int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdllHookStruct
    {
        public int VkCode;
        public int ScanCode;
        public int Flags;
        public int Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KeybdInput KeyboardInput;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeybdInput
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }
}
