using Invoke.Core.Config;

namespace Invoke.App.Hotkeys;

public sealed class HotkeyService : IDisposable
{
    private readonly HotkeySettings _settings;
    private readonly List<RegisteredBinding> _registeredHotkeys = [];
    private LowLevelLauncherHotkeyHook? _launcherHotkeyHook;
    private bool _started;
    private long _lastLauncherRequestTicks;

    public event EventHandler<LauncherRequestedEventArgs>? LauncherRequested;

    public bool IsRunning => _started && (_registeredHotkeys.Count > 0 || _launcherHotkeyHook is not null);

    public HotkeyService(HotkeySettings settings)
    {
        _settings = settings;
    }

    public void Start()
    {
        if (_started)
            return;

        var fallbackBindings = new List<TrackedHotkeyBinding>();
        if (LauncherHotkeyParser.TryParse(_settings.LauncherHotkey, out var launcherGesture))
            RegisterBindingOrFallback(launcherGesture, null, fallbackBindings);

        foreach (var entry in _settings.ModeHotkeys)
        {
            if (!LauncherHotkeyParser.TryParse(entry.Value, out var gesture))
                continue;

            RegisterBindingOrFallback(gesture, entry.Key, fallbackBindings);
        }

        if (fallbackBindings.Count > 0)
        {
            _launcherHotkeyHook = new LowLevelLauncherHotkeyHook(fallbackBindings);
            _launcherHotkeyHook.LauncherRequested += HandleLowLevelLauncherRequested;
        }

        _started = true;
    }

    public void Reload()
    {
        Stop();
        Start();
    }

    public void Stop()
    {
        foreach (var binding in _registeredHotkeys)
        {
            binding.Hotkey.LauncherRequested -= binding.Handler;
            binding.Hotkey.Dispose();
        }
        _registeredHotkeys.Clear();

        if (_launcherHotkeyHook is not null)
        {
            _launcherHotkeyHook.LauncherRequested -= HandleLowLevelLauncherRequested;
            _launcherHotkeyHook.Dispose();
            _launcherHotkeyHook = null;
        }

        _started = false;
    }

    public void Dispose()
    {
        Stop();
    }

    private void RegisterBindingOrFallback(LauncherHotkeyGesture gesture, string? initialMode, ICollection<TrackedHotkeyBinding> fallbackBindings)
    {
        if (gesture.PrefersLowLevelHook || !TryRegisterBinding(gesture, initialMode))
            fallbackBindings.Add(new TrackedHotkeyBinding(gesture, initialMode));
    }

    private bool TryRegisterBinding(LauncherHotkeyGesture gesture, string? initialMode)
    {
        var registeredHotkey = RegisteredLauncherHotkey.TryCreate(gesture);
        if (registeredHotkey is null)
            return false;

        EventHandler handler = (_, _) => RaiseLauncherRequested(initialMode);
        registeredHotkey.LauncherRequested += handler;
        _registeredHotkeys.Add(new RegisteredBinding(registeredHotkey, handler));
        return true;
    }

    private void HandleLowLevelLauncherRequested(object? sender, LauncherRequestedEventArgs e)
    {
        RaiseLauncherRequested(e.InitialMode);
    }

    private void RaiseLauncherRequested(string? initialMode)
    {
        var now = Environment.TickCount64;
        if (now - _lastLauncherRequestTicks < 150)
            return;

        _lastLauncherRequestTicks = now;
        LauncherRequested?.Invoke(this, new LauncherRequestedEventArgs(initialMode));
    }

    private sealed record RegisteredBinding(RegisteredLauncherHotkey Hotkey, EventHandler Handler);
}
