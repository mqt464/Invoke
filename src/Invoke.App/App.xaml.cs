using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Invoke.App.Hotkeys;
using Invoke.Core.Config;
using Invoke.Core.Dmenu;
using Invoke.Core.Modes;
using Invoke.Core.Plugins.Rich;
using Invoke.Core.Rasi;
using Invoke.Core.Services;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace Invoke.App;

public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName = @"Global\Invoke.Launcher.SingleInstance";
    private const int TrayMenuMinItemWidth = 200;
    private const int TrayMenuHorizontalTextPadding = 28;

    private ConfigService? _configService;
    private InvokeSettings? _settings;
    private ThemeSettings? _themeSettings;
    private ProcessRunner? _processRunner;
    private HotkeyService? _hotkeyService;
    private LauncherModeRegistry? _modeRegistry;
    private MainWindow? _window;
    private FileSystemWatcher? _configWatcher;
    private DispatcherTimer? _configReloadTimer;
    private Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstanceMutex;
    private readonly HashSet<string> _pendingConfigReloads = new(StringComparer.OrdinalIgnoreCase);
    private Forms.NotifyIcon? _notifyIcon;
    private Forms.ToolStripMenuItem? _traySuspendItem;
    private Forms.ToolStripMenuItem? _trayStartupItem;
    private bool _isExitRequested;
    private bool _hotkeysSuspended;
    private DmenuSession? _dmenuSession;
    private string? _dmenuSessionPath;
    private readonly StartupRegistrationService _startupRegistrationService = new();
    private RichScriptBackgroundHost? _richScriptBackgroundHost;

    protected override void OnStartup(StartupEventArgs e)
    {
        _dmenuSessionPath = TryGetArgumentValue(e.Args, "--dmenu-session");
        if (_dmenuSessionPath is null)
        {
            _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out var isFirstInstance);
            _ownsSingleInstanceMutex = isFirstInstance;
            if (!isFirstInstance)
            {
                Shutdown();
                return;
            }
        }

        base.OnStartup(e);
        _configService = new ConfigService();
        _settings = _configService.LoadSettings();
        _themeSettings = _configService.LoadTheme(_settings.ThemeName);
        _processRunner = new ProcessRunner();
        _dmenuSession = LoadDmenuSession(_dmenuSessionPath);

        if (_dmenuSession is not null)
            ApplyDmenuRuntimeOverrides(_settings, _dmenuSession);

        _modeRegistry = BuildModeRegistry();
        StartRichScriptBackgroundProcesses();
        _window = new MainWindow(_modeRegistry, _configService, _settings, _themeSettings);
        if (!string.IsNullOrWhiteSpace(_dmenuSession?.WindowTitle))
            _window.Title = _dmenuSession.WindowTitle;
        _window.LauncherHidden += OnLauncherHidden;
        if (_dmenuSession is null)
            InitializeNotifyIcon();

        if (_dmenuSession is null)
        {
            _hotkeyService = new HotkeyService(_settings.ToHotkeySettings());
            _hotkeyService.LauncherRequested += (_, args) =>
                Dispatcher.BeginInvoke(() => ShowLauncherFromHotkey(args.InitialMode), DispatcherPriority.Input);
            _hotkeyService.Start();
            StartConfigWatcher();
        }
        else
        {
            _window.ActivateLauncher(initialMode: "dmenu");
            _window.PrefillQueryAndSearch(_dmenuSession.InitialQuery ?? string.Empty, "dmenu");
        }

        if (Environment.GetEnvironmentVariable("INVOKE_DEV_OPEN") == "1")
            ShowLauncher();
    }

    private void ShowLauncher()
    {
        if (_window is null)
            return;

        _window.ActivateLauncher();
    }

    private void ShowLauncherFromHotkey(string? initialMode = null)
    {
        if (_window is null)
            return;

        _window.ActivateLauncher(fromHotkey: true, initialMode: initialMode);
    }

    private void ExitApplication()
    {
        if (_isExitRequested)
            return;

        _isExitRequested = true;
        _notifyIcon?.Dispose();
        _notifyIcon = null;
        Shutdown();
    }

    private void InitializeNotifyIcon()
    {
        var trayMenuItemWidth = CalculateTrayMenuItemWidth();
        var notifyIcon = new Forms.NotifyIcon
        {
            Text = "Invoke",
            Visible = true,
            Icon = LoadTrayIcon()
        };

        notifyIcon.DoubleClick += (_, _) => Dispatcher.BeginInvoke(ShowLauncher, DispatcherPriority.Input);

        var contextMenu = new Forms.ContextMenuStrip();
        ApplyTrayMenuStyling(contextMenu);
        contextMenu.Items.Add(CreateMenuItem("Open Launcher", trayMenuItemWidth, (_, _) => Dispatcher.BeginInvoke(ShowLauncher, DispatcherPriority.Input), isDefault: true));
        _traySuspendItem = CreateMenuItem("Disable Hotkey", trayMenuItemWidth, (_, _) => Dispatcher.BeginInvoke(ToggleHotkeySuspension, DispatcherPriority.Normal));
        contextMenu.Items.Add(_traySuspendItem);
        _trayStartupItem = CreateMenuItem("Start With Windows", trayMenuItemWidth, (_, _) => Dispatcher.BeginInvoke(ToggleStartupRegistration, DispatcherPriority.Normal));
        contextMenu.Items.Add(_trayStartupItem);
        contextMenu.Items.Add(CreateMenuItem("Open Config Folder", trayMenuItemWidth, (_, _) => Dispatcher.BeginInvoke(OpenConfigFolder, DispatcherPriority.Background)));
        contextMenu.Items.Add(CreateMenuItem("Open Script Order File", trayMenuItemWidth, (_, _) => Dispatcher.BeginInvoke(OpenScriptOrderFile, DispatcherPriority.Background)));
        contextMenu.Items.Add(CreateMenuItem("Reload Config", trayMenuItemWidth, (_, _) => Dispatcher.BeginInvoke(ReloadConfigurationFromTray, DispatcherPriority.Background)));
        contextMenu.Items.Add(new Forms.ToolStripSeparator());
        contextMenu.Items.Add(CreateMenuItem("Exit", trayMenuItemWidth, (_, _) => Dispatcher.BeginInvoke(ExitApplication, DispatcherPriority.Normal)));
        contextMenu.Opening += (_, _) => RefreshTrayMenuState();
        notifyIcon.ContextMenuStrip = contextMenu;

        _notifyIcon = notifyIcon;
        RefreshTrayMenuState();
    }

    private Forms.ToolStripMenuItem CreateMenuItem(string text, int width, EventHandler onClick, bool isDefault = false)
    {
        var item = new TrayMenuItem(text, width, 26);
        item.Click += onClick;

        if (isDefault)
            item.Font = new Drawing.Font(item.Font, Drawing.FontStyle.Bold);

        return item;
    }

    private static int CalculateTrayMenuItemWidth()
    {
        var labels = new[]
        {
            "Open Launcher",
            "Disable Hotkey",
            "Enable Hotkey",
            "Start With Windows",
            "Open Config Folder",
            "Open Script Order File",
            "Reload Config",
            "Exit"
        };

        using var font = new Drawing.Font("Segoe UI", 9F, Drawing.FontStyle.Regular);
        var widestLabel = labels
            .Select(label => Forms.TextRenderer.MeasureText(label, font).Width)
            .Max();

        return Math.Max(TrayMenuMinItemWidth, widestLabel + TrayMenuHorizontalTextPadding);
    }

    private static void ApplyTrayMenuStyling(Forms.ContextMenuStrip contextMenu)
    {
        var background = Drawing.Color.FromArgb(18, 18, 20);
        var foreground = Drawing.Color.FromArgb(240, 240, 242);
        var selection = Drawing.Color.FromArgb(44, 44, 48);
        var border = Drawing.Color.FromArgb(58, 58, 64);
        var separator = Drawing.Color.FromArgb(46, 46, 50);

        contextMenu.ShowImageMargin = false;
        contextMenu.ShowCheckMargin = false;
        contextMenu.BackColor = background;
        contextMenu.ForeColor = foreground;
        contextMenu.Font = new Drawing.Font("Segoe UI", 9F, Drawing.FontStyle.Regular);
        contextMenu.Padding = new Forms.Padding(1, 0, 1, 0);
        contextMenu.Margin = Forms.Padding.Empty;
        contextMenu.RenderMode = Forms.ToolStripRenderMode.Professional;
        contextMenu.Renderer = new TrayMenuRenderer(background, selection, border, separator);
    }

    private void ToggleHotkeySuspension()
    {
        _hotkeysSuspended = !_hotkeysSuspended;

        if (_hotkeysSuspended)
            _hotkeyService?.Stop();
        else
            _hotkeyService?.Start();

        RefreshTrayMenuState();
    }

    private void RefreshTrayMenuState()
    {
        if (_traySuspendItem is not null)
            _traySuspendItem.Text = _hotkeysSuspended ? "Enable Hotkey" : "Disable Hotkey";

        if (_trayStartupItem is not null)
            _trayStartupItem.Checked = _startupRegistrationService.IsEnabled();

        if (_notifyIcon is not null)
            _notifyIcon.Text = _hotkeysSuspended ? "Invoke (Hotkey Suspended)" : "Invoke";
    }

    private void ToggleStartupRegistration()
    {
        try
        {
            _startupRegistrationService.SetEnabled(!_startupRegistrationService.IsEnabled());
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Invoke startup toggle failed: {ex}");
        }

        RefreshTrayMenuState();
    }

    private void OpenConfigFolder()
    {
        if (_configService is null)
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _configService.ConfigDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Invoke tray open config folder failed: {ex}");
        }
    }

    private void OpenScriptOrderFile()
    {
        if (_configService is null)
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _configService.ScriptOrderPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Invoke tray open script order failed: {ex}");
        }
    }

    private void ReloadConfigurationFromTray()
    {
        if (_configService is null)
            return;

        try
        {
            ReloadRuntime();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Invoke tray reload failed: {ex}");
        }
    }

    private static Drawing.Icon LoadTrayIcon()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(exePath))
            {
                var icon = Drawing.Icon.ExtractAssociatedIcon(exePath);
                if (icon is not null)
                    return icon;
            }
        }
        catch
        {
        }

        return Drawing.SystemIcons.Application;
    }

    private sealed class TrayMenuRenderer(Drawing.Color background, Drawing.Color selection, Drawing.Color border, Drawing.Color separator)
        : Forms.ToolStripProfessionalRenderer(new TrayMenuColorTable(background, selection, border, separator))
    {
        protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e)
        {
            var fill = e.Item.Selected && e.Item.Enabled ? selection : background;
            using var brush = new Drawing.SolidBrush(fill);
            e.Graphics.FillRectangle(brush, new Drawing.Rectangle(1, 1, e.Item.Width - 2, e.Item.Height - 2));
        }

        protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
        {
            Forms.TextRenderer.DrawText(
                e.Graphics,
                e.Text,
                e.TextFont,
                new Drawing.Rectangle(10, 0, e.Item.Width - 20, e.Item.Height),
                Drawing.Color.FromArgb(240, 240, 242),
                Forms.TextFormatFlags.Left | Forms.TextFormatFlags.VerticalCenter | Forms.TextFormatFlags.EndEllipsis);
        }

        protected override void OnRenderToolStripBorder(Forms.ToolStripRenderEventArgs e)
        {
            using var pen = new Drawing.Pen(border);
            var bounds = new Drawing.Rectangle(Drawing.Point.Empty, e.ToolStrip.Size);
            bounds.Width -= 1;
            bounds.Height -= 1;
            e.Graphics.DrawRectangle(pen, bounds);
        }

        protected override void OnRenderSeparator(Forms.ToolStripSeparatorRenderEventArgs e)
        {
            using var pen = new Drawing.Pen(separator);
            var y = e.Item.ContentRectangle.Top + (e.Item.ContentRectangle.Height / 2);
            e.Graphics.DrawLine(pen, 10, y, e.Item.Width - 10, y);
        }
    }

    private sealed class TrayMenuItem : Forms.ToolStripMenuItem
    {
        private readonly Drawing.Size _preferredSize;

        public TrayMenuItem(string text, int width, int height)
            : base(text)
        {
            _preferredSize = new Drawing.Size(width, height);
            AutoSize = false;
            Size = _preferredSize;
            Margin = Forms.Padding.Empty;
            Padding = new Forms.Padding(10, 4, 10, 4);
            TextAlign = Drawing.ContentAlignment.MiddleLeft;
        }

        public override Drawing.Size GetPreferredSize(Drawing.Size constrainingSize) => _preferredSize;
    }

    private sealed class TrayMenuColorTable(Drawing.Color background, Drawing.Color selection, Drawing.Color border, Drawing.Color separator)
        : Forms.ProfessionalColorTable
    {
        public override Drawing.Color ToolStripDropDownBackground => background;
        public override Drawing.Color MenuBorder => border;
        public override Drawing.Color MenuItemBorder => border;
        public override Drawing.Color MenuItemSelected => selection;
        public override Drawing.Color MenuItemSelectedGradientBegin => selection;
        public override Drawing.Color MenuItemSelectedGradientEnd => selection;
        public override Drawing.Color MenuItemPressedGradientBegin => background;
        public override Drawing.Color MenuItemPressedGradientMiddle => background;
        public override Drawing.Color MenuItemPressedGradientEnd => background;
        public override Drawing.Color ImageMarginGradientBegin => background;
        public override Drawing.Color ImageMarginGradientMiddle => background;
        public override Drawing.Color ImageMarginGradientEnd => background;
        public override Drawing.Color SeparatorDark => separator;
        public override Drawing.Color SeparatorLight => separator;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _notifyIcon?.Dispose();
        _notifyIcon = null;
        _configWatcher?.Dispose();
        _configReloadTimer?.Stop();
        _hotkeyService?.Dispose();
        _richScriptBackgroundHost?.Dispose();
        if (_ownsSingleInstanceMutex)
            _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    private void StartConfigWatcher()
    {
        if (_configService is null)
            return;

        _configReloadTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        _configReloadTimer.Tick += (_, _) => ReloadPendingConfigChanges();

        _configWatcher = new FileSystemWatcher(_configService.ConfigDirectory)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };
        _configWatcher.Changed += OnConfigFileChanged;
        _configWatcher.Created += OnConfigFileChanged;
        _configWatcher.Renamed += OnConfigFileRenamed;
    }

    private void OnConfigFileChanged(object sender, FileSystemEventArgs e)
    {
        Dispatcher.BeginInvoke(() => QueueConfigReload(e.FullPath));
    }

    private void OnConfigFileRenamed(object sender, RenamedEventArgs e)
    {
        Dispatcher.BeginInvoke(() => QueueConfigReload(e.FullPath));
    }

    private void QueueConfigReload(string path)
    {
        var fileName = Path.GetFileName(path);
        if (IsThemePath(path))
        {
            _pendingConfigReloads.Add("themes");
            _configReloadTimer?.Stop();
            _configReloadTimer?.Start();
            return;
        }

        if (IsScriptPath(path))
        {
            _pendingConfigReloads.Add("scripts");
            _configReloadTimer?.Stop();
            _configReloadTimer?.Start();
            return;
        }

        if (Path.GetDirectoryName(path)?.Equals(_configService?.ConfigDirectory, StringComparison.OrdinalIgnoreCase) != true)
        {
            return;
        }

        if (fileName is "config.rasi")
        {
            _pendingConfigReloads.Add(fileName);
        }
        else
        {
            return;
        }

        _configReloadTimer?.Stop();
        _configReloadTimer?.Start();
    }

    private void ReloadPendingConfigChanges()
    {
        _configReloadTimer?.Stop();
        if (_configService is null)
            return;

        var fileNames = _pendingConfigReloads.ToArray();
        _pendingConfigReloads.Clear();

        foreach (var fileName in fileNames)
        {
            try
            {
                switch (fileName)
                {
                    case "config.rasi":
                    case "themes":
                    case "scripts":
                        ReloadRuntime();
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Invoke config reload failed for {fileName}: {ex}");
            }
        }

        if (_pendingConfigReloads.Count > 0)
            _configReloadTimer?.Start();
        else
            _window?.RefreshFromConfig();
    }

    private void ReloadRuntime()
    {
        if (_configService is null || _processRunner is null)
            return;

        _settings = _configService.LoadSettings();
        _themeSettings = _configService.LoadTheme(_settings.ThemeName);
        _richScriptBackgroundHost?.Dispose();
        _modeRegistry?.Dispose();
        _modeRegistry = BuildModeRegistry();
        StartRichScriptBackgroundProcesses();

        _hotkeyService?.Dispose();
        _hotkeyService = new HotkeyService(_settings.ToHotkeySettings());
        _hotkeyService.LauncherRequested += (_, args) =>
            Dispatcher.BeginInvoke(() => ShowLauncherFromHotkey(args.InitialMode), DispatcherPriority.Input);
        if (!_hotkeysSuspended)
            _hotkeyService.Start();

        _window?.ApplyRuntime(_modeRegistry, _settings, _themeSettings);
        _window?.RefreshFromConfig();
    }

    private LauncherModeRegistry BuildModeRegistry()
    {
        if (_configService is null || _settings is null || _themeSettings is null || _processRunner is null)
            throw new InvalidOperationException("Runtime not initialized.");

        if (_dmenuSession is not null)
        {
            var dmenuMode = new DmenuMode(_dmenuSession, WriteDmenuSelectionAsync);
            return new LauncherModeRegistry(
                [new LauncherModeDefinition("dmenu", _dmenuSession.Prompt, dmenuMode)],
                _settings,
                _themeSettings);
        }

        var definitions = new List<LauncherModeDefinition>
        {
            new("drun", _settings.DisplayNames.GetValueOrDefault("drun", "drun"), new DrunMode(_processRunner)),
            new("run", _settings.DisplayNames.GetValueOrDefault("run", "run"), new RunMode(_processRunner)),
            new("files", _settings.DisplayNames.GetValueOrDefault("files", "files"), new FilesMode(_processRunner)),
            new("window", _settings.DisplayNames.GetValueOrDefault("window", "window"), new WindowMode()),
            new("combi", _settings.DisplayNames.GetValueOrDefault("combi", "combi"), new EmptyMode("combi", _settings.DisplayNames.GetValueOrDefault("combi", "combi")))
        };

        foreach (var script in _configService.LoadRichScripts())
            definitions.Add(new LauncherModeDefinition(script.Manifest.Id, script.Manifest.Name, new RichExternalMode(script, _processRunner), AlwaysEvaluate: true));

        return new LauncherModeRegistry(definitions, _settings, _themeSettings);
    }

    private static string? TryGetArgumentValue(IReadOnlyList<string> args, string name)
    {
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (args[index].Equals(name, StringComparison.OrdinalIgnoreCase))
                return args[index + 1];
        }

        return null;
    }

    private static DmenuSession? LoadDmenuSession(string? sessionPath)
    {
        if (string.IsNullOrWhiteSpace(sessionPath) || !File.Exists(sessionPath))
            return null;

        try
        {
            return DmenuSession.Load(sessionPath);
        }
        catch
        {
            return null;
        }
    }

    private static void ApplyDmenuRuntimeOverrides(InvokeSettings settings, DmenuSession session)
    {
        settings.Modes = ["dmenu"];
        settings.ModeEntries =
        [
            ModeEntry.BuiltIn("dmenu")
        ];
        settings.DefaultMode = "dmenu";
        settings.CombiModes = [];
        settings.DisplayNames["dmenu"] = session.Prompt;
        settings.ShowStartPage = true;
        settings.Launcher.ShowStartPage = true;
        settings.AutoSelectFirstResult = true;
        settings.Launcher.AutoSelectFirstResult = true;
        settings.CloseAfterAction = true;
        settings.Launcher.CloseAfterAction = true;
        settings.CloseOnFocusLoss = true;
        settings.Launcher.CloseOnFocusLoss = true;
        settings.Lines = Math.Max(1, session.Lines);
        settings.MaxResults = Math.Max(1, session.Entries.Count);
        settings.Search.MaxResults = settings.MaxResults;
        settings.Launcher.VisibleResults = settings.Lines;
        settings.CaseSensitive = !session.CaseInsensitive;
    }

    private async Task WriteDmenuSelectionAsync(string selection)
    {
        if (_dmenuSession is null || string.IsNullOrWhiteSpace(_dmenuSession.OutputPath))
            return;

        var directory = Path.GetDirectoryName(_dmenuSession.OutputPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(_dmenuSession.OutputPath, selection + Environment.NewLine).ConfigureAwait(false);
        await Dispatcher.BeginInvoke(Shutdown);
    }

    private void OnLauncherHidden()
    {
        if (_dmenuSession is not null)
            Shutdown();
    }

    private bool IsThemePath(string path)
    {
        if (_configService is null)
            return false;

        var relative = Path.GetRelativePath(_configService.ConfigDirectory, path);
        return relative.StartsWith("themes" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
               Path.GetExtension(path).Equals(".rasi", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsScriptPath(string path)
    {
        if (_configService is null)
            return false;

        var relative = Path.GetRelativePath(_configService.ConfigDirectory, path);
        if (!relative.StartsWith("scripts" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return false;

        var fileName = Path.GetFileName(path);
        if (fileName.Equals("order.txt", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("script.toml", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Path.GetExtension(path).ToLowerInvariant() is ".ps1" or ".cmd" or ".bat" or ".exe";
    }

    private void StartRichScriptBackgroundProcesses()
    {
        if (_configService is null || _dmenuSession is not null)
            return;

        _richScriptBackgroundHost = new RichScriptBackgroundHost();
        _richScriptBackgroundHost.Start(_configService.LoadRichScripts());
    }
}
