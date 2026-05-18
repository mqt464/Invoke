using Invoke.Core.Config;

namespace Invoke.Tests;

[TestClass]
public sealed class RasiConfigServiceTests
{
    [TestMethod]
    public void Constructor_SeedsRasiFirstRuntimeLayout()
    {
        using var workspace = new TestWorkspace();
        var service = new ConfigService(workspace.Path);

        Assert.IsTrue(File.Exists(service.ConfigPath));
        Assert.IsTrue(File.Exists(workspace.GetPath("themes", "default.rasi")));
        Assert.IsTrue(Directory.Exists(workspace.GetPath("scripts")));
        Assert.IsTrue(File.Exists(service.ScriptOrderPath));
        var defaultTheme = File.ReadAllText(workspace.GetPath("themes", "default.rasi"));
        StringAssert.Contains(defaultTheme, "text: \"Applications:\";");
        StringAssert.Contains(defaultTheme, "background-color: #10131B;");
        StringAssert.Contains(defaultTheme, "font-family: \"Consolas\";");
        StringAssert.Contains(defaultTheme, "placeholder: \"Type to filter\";");
        StringAssert.Contains(defaultTheme, "overflow-indicator-bottom {");
    }

    [TestMethod]
    public void Constructor_RefreshesUntouchedLegacyDefaultTheme()
    {
        using var workspace = new TestWorkspace();
        var themesDirectory = workspace.GetPath("themes");
        Directory.CreateDirectory(themesDirectory);
        File.WriteAllText(
            workspace.GetPath("themes", "default.rasi"),
            """
            * {
              background-color: #111318;
              text-color: #F2F5F8;
              accent-color: #7DD3FC;
            }

            window {
              width: 720px;
              background-color: #111318;
              text-color: #F2F5F8;
              border-color: #2A3038;
              border-radius: 8px;
            }

            mainbox {
              spacing: 10px;
            }

            inputbar {
              padding: 14 20;
            }

            prompt {
              enabled: true;
              text: "Search";
              text-color: #9AA6B2;
            }

            listview {
              lines: 8;
              columns: 1;
              spacing: 0;
            }

            element {
              padding: 9 18;
              border-radius: 7px;
              height: 52px;
            }

            element selected {
              background-color: #1B2730;
              border-color: #7DD3FC;
            }

            element-icon {
              size: 30px;
            }

            message {
              enabled: true;
              text-color: #9AA6B2;
            }

            mode-switcher {
              enabled: true;
              show-icons: true;
            }
            """);

        _ = new ConfigService(workspace.Path);
        var defaultTheme = File.ReadAllText(workspace.GetPath("themes", "default.rasi"));

        StringAssert.Contains(defaultTheme, "text: \"Applications:\";");
        Assert.IsFalse(defaultTheme.Contains("background-color: #111318;", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Constructor_RefreshesUntouchedPreviousDefaultTheme()
    {
        using var workspace = new TestWorkspace();
        var themesDirectory = workspace.GetPath("themes");
        Directory.CreateDirectory(themesDirectory);
        File.WriteAllText(
            workspace.GetPath("themes", "default.rasi"),
            """
            * {
              background-color: #0F1722;
              text-color: #F4F7FB;
              accent-color: #6FD0C2;
            }

            window {
              width: 760px;
              background-color: #0F1722;
              text-color: #F4F7FB;
              border-color: #263448;
              border: 1px;
              border-radius: 18px;
              padding: 10;
            }

            mainbox {
              spacing: 10;
              children: [inputbar,listview];
            }

            inputbar {
              padding: 12 16;
              background-color: #141D29;
              border-color: #314154;
              text-color: #F4F7FB;
              border: 1px;
              border-radius: 14px;
              spacing: 10;
              children: [prompt,entry];
            }

            prompt {
              enabled: true;
              text: "Applications:";
              text-color: #A6B4C7;
              font-family: "Segoe UI Variable";
            }

            entry {
              placeholder: "";
              text-color: #F4F7FB;
              font-family: "Segoe UI Variable";
            }

            listview {
              lines: 8;
              columns: 1;
              spacing: 6;
              padding: 0 0 6 0;
              scrollbar: false;
            }

            element {
              padding: 12 14;
              border: 1px;
              border-color: #263448;
              border-radius: 14px;
              height: 56px;
            }

            element selected {
              background-color: #1A2938;
              border-color: #6FD0C2;
            }

            element-text {
              text-color: #E7ECF5;
              placeholder-color: #8897AA;
              font-family: "Segoe UI Variable";
            }

            element-text selected {
              text-color: #FAFCFF;
              placeholder-color: #B7C4D3;
            }

            element-icon {
              size: 26px;
            }

            message {
              enabled: false;
              padding: 16 4 16 2;
              text-color: #8897AA;
              font-family: "Segoe UI Variable";
            }

            mode-switcher {
              enabled: false;
              padding: 4 0 0 0;
              show-icons: true;
            }

            overflow-indicator {
              padding: 8 3 8 3;
              border-radius: 999;
              background-color: #142031;
              border-color: #263448;
              text-color: #F4F7FB;
            }

            overflow-indicator-top {
              enabled: true;
            }

            overflow-indicator-bottom {
              enabled: true;
            }
            """);

        _ = new ConfigService(workspace.Path);
        var defaultTheme = File.ReadAllText(workspace.GetPath("themes", "default.rasi"));

        StringAssert.Contains(defaultTheme, "background-color: #10131B;");
        StringAssert.Contains(defaultTheme, "font-family: \"Consolas\";");
        Assert.IsFalse(defaultTheme.Contains("background-color: #0F1722;", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Constructor_RefreshesUntouchedCurrentDefaultTheme()
    {
        using var workspace = new TestWorkspace();
        var themesDirectory = workspace.GetPath("themes");
        Directory.CreateDirectory(themesDirectory);
        File.WriteAllText(
            workspace.GetPath("themes", "default.rasi"),
            """
            * {
              background-color: #11111B;
              text-color: #E8EAF2;
              accent-color: #6D7394;
            }

            window {
              width: 760px;
              background-color: #11111B;
              text-color: #E8EAF2;
              border-color: #2C3146;
              border: 1px;
              border-radius: 4px;
              padding: 0;
            }

            mainbox {
              spacing: 0;
              children: [inputbar,listview];
            }

            inputbar {
              padding: 8 12;
              background-color: #5E6485;
              border-color: #5E6485;
              text-color: #F5F6FB;
              border: 0;
              border-radius: 0;
              spacing: 8;
              children: [prompt,entry];
            }

            prompt {
              enabled: true;
              text: "Applications:";
              text-color: #F5F6FB;
              font-family: "Cascadia Mono";
            }

            entry {
              placeholder: "";
              text-color: #F5F6FB;
              font-family: "Cascadia Mono";
            }

            listview {
              lines: 8;
              columns: 1;
              spacing: 0;
              padding: 10 0 10 0;
              scrollbar: false;
            }

            element {
              padding: 10 16;
              border: 0;
              border-radius: 0;
              height: 52px;
            }

            element selected {
              background-color: #1A1C29;
              border-color: #6D7394;
            }

            element-text {
              text-color: #C7CBDA;
              placeholder-color: #7E8397;
              font-family: "Cascadia Mono";
            }

            element-text selected {
              text-color: #F5F6FB;
              placeholder-color: #B8BCCB;
            }

            element-icon {
              size: 24px;
            }

            message {
              enabled: false;
              padding: 12 16 12 16;
              text-color: #7E8397;
              font-family: "Cascadia Mono";
            }

            mode-switcher {
              enabled: false;
              padding: 0;
              show-icons: true;
            }

            overflow-indicator {
              padding: 8 16 8 16;
              border-radius: 0;
              background-color: #1A1C29;
              border-color: #6D7394;
              text-color: #F5F6FB;
            }

            overflow-indicator-top {
              enabled: true;
            }

            overflow-indicator-bottom {
              enabled: true;
            }
            """);

        _ = new ConfigService(workspace.Path);
        var defaultTheme = File.ReadAllText(workspace.GetPath("themes", "default.rasi"));

        StringAssert.Contains(defaultTheme, "background-color: #10131B;");
        StringAssert.Contains(defaultTheme, "placeholder: \"Type to filter\";");
        Assert.IsFalse(defaultTheme.Contains("background-color: #11111B;", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Constructor_KeepsCustomizedDefaultTheme()
    {
        using var workspace = new TestWorkspace();
        var themesDirectory = workspace.GetPath("themes");
        Directory.CreateDirectory(themesDirectory);
        var customTheme =
            """
            * {
              background-color: #000000;
            }

            prompt {
              text: "My Apps";
            }
            """;
        File.WriteAllText(workspace.GetPath("themes", "default.rasi"), customTheme);

        _ = new ConfigService(workspace.Path);
        var defaultTheme = File.ReadAllText(workspace.GetPath("themes", "default.rasi"));

        Assert.AreEqual(
            customTheme.Replace("\r\n", "\n", StringComparison.Ordinal).Trim(),
            defaultTheme.Replace("\r\n", "\n", StringComparison.Ordinal).Trim());
    }

    [TestMethod]
    public void Constructor_RefreshesUntouchedSeedConfig()
    {
        using var workspace = new TestWorkspace();
        File.WriteAllText(
            workspace.GetPath("config.rasi"),
            """
            configuration {
              modes: "drun,run,files,window,combi";
              default-mode: "drun";
              combi-modes: "drun,run,files,window";
              theme: "default";
              launcher-hotkey: "Alt+Space";
              matching: "fuzzy";
              sorting-method: "normal";
              case-sensitive: false;
              cycle: true;
              sidebar-mode: true;
              show-icons: true;
              font: "Segoe UI Variable 15";
              location: "north";
              xoffset: 0;
              yoffset: 0;
              lines: 8;
              columns: 1;
              search-delay: 35;
              loading-delay: 1200;
              close-on-focus-loss: true;
              close-after-action: true;
              clear-query-on-hide: true;
              auto-select: true;
              show-start-page: true;
              display-drun: "drun";
              display-run: "run";
              display-files: "files";
              display-window: "window";
              display-combi: "combi";
              result-title-drun: "{display}";
              result-subtitle-drun: "";
              result-title-files: "{display}";
              result-subtitle-files: "{secondary}";
              kb-accept-entry: "Return";
              kb-cancel: "Escape";
              kb-row-up: "Up";
              kb-row-down: "Down";
              kb-mode-next: "Alt+Right";
              kb-mode-previous: "Alt+Left";
            }
            """);

        _ = new ConfigService(workspace.Path);
        var config = File.ReadAllText(workspace.GetPath("config.rasi"));

        StringAssert.Contains(config, "display-drun: \"drun\";");
        StringAssert.Contains(config, "display-window: \"window\";");
        Assert.IsFalse(config.Contains("launcher-hotkey:", StringComparison.Ordinal));
        Assert.IsFalse(config.Contains("result-title-drun:", StringComparison.Ordinal));
        Assert.IsFalse(config.Contains("result-subtitle-drun:", StringComparison.Ordinal));
        Assert.IsFalse(config.Contains("kb-mode-next:", StringComparison.Ordinal));
        Assert.IsFalse(config.Contains("display-drun: \"Apps\";", StringComparison.Ordinal));
    }

    [TestMethod]
    public void LoadSettings_ProjectsRasiConfigurationIntoRuntimeModel()
    {
        using var workspace = new TestWorkspace();
        var service = new ConfigService(workspace.Path);

        File.WriteAllText(
            service.ConfigPath,
            """
            configuration {
              modes: "apps,run,files,windows,combi,cliphist";
              default-mode: "cliphist";
              combi-modes: "apps,files,cliphist";
              theme: "oxide";
              launcher-hotkey: "Ctrl+Space";
              matching: "normal";
              sorting-method: "fzf";
              case-sensitive: true;
              cycle: false;
              sidebar-mode: false;
              show-icons: false;
              font: "JetBrains Mono 14";
              location: "south";
              xoffset: 24;
              yoffset: -12;
              lines: 12;
              columns: 2;
              search-delay: 55;
              loading-delay: 1500;
              close-on-focus-loss: false;
              close-after-action: false;
              clear-query-on-hide: false;
              auto-select: false;
              show-start-page: false;
              everything-cli-path: "C:\tools\Everything\es.exe";
              everything-path: "C:\tools\Everything\Everything.exe";
              display-drun: "Apps";
              display-files: "Files";
              display-window: "Windows";
              mode-window-hotkey: "Alt+2";
              kb-mode-next: "Control+j";
            }
            """);

        var settings = service.LoadSettings();

        CollectionAssert.AreEqual(new[] { "drun", "run", "files", "window", "combi", "cliphist" }, settings.Modes);
        CollectionAssert.AreEqual(new[] { "drun", "files", "cliphist" }, settings.CombiModes);
        Assert.AreEqual("cliphist", settings.DefaultMode);
        Assert.AreEqual("oxide", settings.ThemeName);
        Assert.AreEqual("Ctrl+Space", settings.LauncherHotkey);
        Assert.AreEqual("normal", settings.Matching);
        Assert.AreEqual("fzf", settings.SortingMethod);
        Assert.IsTrue(settings.CaseSensitive);
        Assert.IsFalse(settings.Cycle);
        Assert.IsFalse(settings.SidebarMode);
        Assert.IsFalse(settings.ShowIcons);
        Assert.AreEqual("JetBrains Mono 14", settings.Font);
        Assert.AreEqual("south", settings.Location);
        Assert.AreEqual(24d, settings.XOffset);
        Assert.AreEqual(-12d, settings.YOffset);
        Assert.AreEqual(12, settings.Lines);
        Assert.AreEqual(2, settings.Columns);
        Assert.AreEqual(12, settings.MaxResults);
        Assert.AreEqual(55, settings.DebounceMilliseconds);
        Assert.AreEqual(1500, settings.LoadingIndicatorDelayMilliseconds);
        Assert.IsFalse(settings.CloseOnFocusLoss);
        Assert.IsFalse(settings.CloseAfterAction);
        Assert.IsFalse(settings.ClearQueryOnHide);
        Assert.IsFalse(settings.AutoSelectFirstResult);
        Assert.IsFalse(settings.ShowStartPage);
        Assert.AreEqual("Apps", settings.DisplayNames["drun"]);
        Assert.AreEqual("Files", settings.DisplayNames["files"]);
        Assert.AreEqual("Windows", settings.DisplayNames["window"]);
        Assert.AreEqual("Alt+2", settings.ModeHotkeys["window"]);
        Assert.AreEqual("Control+j", settings.Keybindings["kb-mode-next"]);
        Assert.AreEqual(@"C:\tools\Everything\es.exe", settings.EverythingCliPath);
        Assert.AreEqual(@"C:\tools\Everything\Everything.exe", settings.EverythingPath);
        Assert.AreEqual("bottomCenter", settings.Placement.Anchor);
    }

    [TestMethod]
    public void LoadSettings_UsesMinimalDefaultKeybindingsAndNoLauncherHotkeyWhenUnset()
    {
        using var workspace = new TestWorkspace();
        var service = new ConfigService(workspace.Path);

        File.WriteAllText(
            service.ConfigPath,
            """
            configuration {
              modes: "drun,run,files,window,combi";
              default-mode: "drun";
              combi-modes: "drun,run";
              theme: "default";
            }
            """);

        var settings = service.LoadSettings();

        Assert.AreEqual(string.Empty, settings.LauncherHotkey);
        Assert.AreEqual("Return", settings.Keybindings["kb-accept-entry"]);
        Assert.AreEqual("Escape", settings.Keybindings["kb-cancel"]);
        Assert.AreEqual("Up", settings.Keybindings["kb-row-up"]);
        Assert.AreEqual("Down", settings.Keybindings["kb-row-down"]);
        Assert.IsFalse(settings.Keybindings.ContainsKey("kb-mode-next"));
        Assert.IsFalse(settings.Keybindings.ContainsKey("kb-move-front"));
    }

    [TestMethod]
    public void LoadSettings_PreservesConfiguredExternalModeIds()
    {
        using var workspace = new TestWorkspace();
        var service = new ConfigService(workspace.Path);
        File.WriteAllText(
            service.ConfigPath,
            """
            configuration {
              modes: "apps,run,files,windows,combi,cliphist,winget-plus";
              default-mode: "cliphist";
              combi-modes: "apps,files,winget-plus";
            }
            """);

        var settings = service.LoadSettings();

        CollectionAssert.AreEqual(new[] { "drun", "run", "files", "window", "combi", "cliphist", "winget-plus" }, settings.Modes);
        Assert.AreEqual("cliphist", settings.DefaultMode);
        CollectionAssert.AreEqual(new[] { "drun", "files", "winget-plus" }, settings.CombiModes);
    }

    [TestMethod]
    public void LoadTheme_MapsRasiWidgetsIntoThemeSettings()
    {
        using var workspace = new TestWorkspace();
        var service = new ConfigService(workspace.Path);
        var themePath = workspace.GetPath("themes", "oxide.rasi");
        File.WriteAllText(
            themePath,
            """
            * {
              background-color: #101820;
              text-color: #F8F8F2;
              accent-color: #FF9900;
            }

            window {
              width: 900px;
              border-color: #223344;
              border-radius: 16px;
            }

            inputbar {
              padding: [11, 22];
              background-color: #0F141B;
              border-color: #334455;
              border: 2px;
              border-radius: 12px;
              orientation: vertical;
              spacing: 9;
              children: [entry,prompt];
            }

            prompt {
              enabled: false;
              text: "Run";
              text-color: #8899AA;
            }

            listview {
              spacing: 6;
            }

            element {
              padding: [7, 15];
              height: 48px;
              border-radius: 10px;
            }

            element selected {
              background-color: #1C2733;
              border-color: #44CC88;
            }

            element-icon {
              size: 28px;
            }

            message {
              enabled: false;
              text: "Help text";
              text-color: #778899;
            }

            mode-switcher {
              enabled: false;
              show-icons: false;
              orientation: vertical;
              spacing: 12;
            }

            element-text {
              text-color: #FAFAFA;
              placeholder-color: #556677;
            }

            mainbox {
              children: [inputbar,mode-switcher,message,listview];
              orientation: horizontal;
            }
            """);

        var theme = service.LoadTheme("oxide");

        Assert.AreEqual("oxide", theme.Name);
        Assert.AreEqual("#101820", theme.Background);
        Assert.AreEqual("#F8F8F2", theme.Foreground);
        Assert.AreEqual("#8899AA", theme.MutedForeground);
        Assert.AreEqual("#44CC88", theme.Accent);
        Assert.AreEqual("#1C2733", theme.SelectedBackground);
        Assert.AreEqual("#223344", theme.Border);
        Assert.AreEqual(900d, theme.WindowWidth);
        Assert.AreEqual(16d, theme.CornerRadius);
        Assert.AreEqual(48d, theme.RowHeight);
        Assert.AreEqual(10d, theme.ResultCornerRadius);
        Assert.AreEqual(6d, theme.ResultGap);
        Assert.AreEqual(28d, theme.IconSize);
        Assert.AreEqual(22d, theme.SearchHorizontalPadding);
        Assert.AreEqual(11d, theme.SearchVerticalPadding);
        Assert.AreEqual(15d, theme.ResultHorizontalPadding);
        Assert.AreEqual(7d, theme.ResultVerticalPadding);
        Assert.AreEqual("Run", theme.PromptText);
        Assert.AreEqual("Help text", theme.MessageText);
        Assert.IsFalse(theme.ShowPrompt);
        Assert.IsFalse(theme.ShowModeSwitcher);
        Assert.IsFalse(theme.ShowIcons);
        Assert.IsTrue(theme.WidgetProperties.ContainsKey("window"));
        Assert.AreEqual("#0F141B", theme.WidgetProperties["inputbar"]["background-color"]);
        Assert.AreEqual("2px", theme.WidgetProperties["inputbar"]["border"]);
        Assert.AreEqual("vertical", theme.WidgetProperties["inputbar"]["orientation"]);
        Assert.AreEqual("entry prompt", theme.WidgetProperties["inputbar"]["children"]);
        Assert.AreEqual("vertical", theme.WidgetProperties["mode-switcher"]["orientation"]);
        Assert.AreEqual("12", theme.WidgetProperties["mode-switcher"]["spacing"]);
        Assert.AreEqual("false", theme.WidgetProperties["message"]["enabled"]);
        Assert.AreEqual("#FAFAFA", theme.WidgetProperties["element-text"]["text-color"]);
        Assert.AreEqual("inputbar mode-switcher message listview", theme.WidgetProperties["mainbox"]["children"]);
        Assert.AreEqual("horizontal", theme.WidgetProperties["mainbox"]["orientation"]);
    }

    [TestMethod]
    public void LoadTheme_ResolvesEnvVarAndInheritValues()
    {
        using var workspace = new TestWorkspace();
        var service = new ConfigService(workspace.Path);
        var themePath = workspace.GetPath("themes", "vars.rasi");
        Environment.SetEnvironmentVariable("INVOKE_THEME_ACCENT", "#123456");
        File.WriteAllText(
            themePath,
            """
            * {
              accent-color: env(INVOKE_THEME_ACCENT);
              text-color: #EEEEEE;
            }

            prompt {
              text-color: inherit;
            }

            message {
              text-color: var(accent-color, #999999);
            }
            """);

        var theme = service.LoadTheme("vars");

        Assert.AreEqual("#123456", theme.Accent);
        Assert.AreEqual("#EEEEEE", theme.WidgetProperties["prompt"]["text-color"]);
        Assert.AreEqual("#123456", theme.WidgetProperties["message"]["text-color"]);
        Assert.AreEqual("#EEEEEE", theme.MutedForeground);
    }

    [TestMethod]
    public void LoadRichScripts_ReadsTomlManifest()
    {
        using var workspace = new TestWorkspace();
        _ = new ConfigService(workspace.Path);

        var scriptDirectory = workspace.GetPath("scripts", "notes");
        Directory.CreateDirectory(scriptDirectory);
        File.WriteAllText(
            System.IO.Path.Combine(scriptDirectory, "script.toml"),
            """
            id = "notes"
            name = "Notes"
            kind = "external"
            entry = "notes.cmd"
            arguments = "--json"
            priority = 42
            keep_alive = true
            triggers = ["/note", "/scratch"]
            trigger_mode = "prefix-token"
            run_for_query = true
            background_entry = "watch-notes.ps1"
            background_arguments = "--watch"
            background_restart_on_exit = true
            """);

        var script = new ConfigService(workspace.Path).LoadRichScripts().Single(static item => item.Manifest.Id == "notes");

        Assert.AreEqual("Notes", script.Manifest.Name);
        Assert.AreEqual("notes.cmd", script.Manifest.Entry);
        Assert.AreEqual("--json", script.Manifest.Arguments);
        Assert.AreEqual(42, script.Manifest.Priority);
        Assert.IsTrue(script.Manifest.KeepAlive);
        Assert.IsTrue(script.Manifest.RunForQuery);
        Assert.AreEqual("watch-notes.ps1", script.Manifest.BackgroundEntry);
        Assert.AreEqual("--watch", script.Manifest.BackgroundArguments);
        Assert.IsTrue(script.Manifest.BackgroundRestartOnExit);
        CollectionAssert.AreEqual(new[] { "/note", "/scratch" }, script.Manifest.Triggers);
        Assert.AreEqual("prefix-token", script.Manifest.TriggerMode);
    }

    [TestMethod]
    public void LoadRichScripts_UsesScriptOrderFile()
    {
        using var workspace = new TestWorkspace();
        var service = new ConfigService(workspace.Path);

        var betaDirectory = workspace.GetPath("scripts", "beta");
        Directory.CreateDirectory(betaDirectory);
        File.WriteAllText(
            System.IO.Path.Combine(betaDirectory, "script.toml"),
            """
            id = "beta"
            name = "Beta"
            kind = "external"
            entry = "beta.cmd"
            priority = 50
            """);

        var alphaDirectory = workspace.GetPath("scripts", "alpha");
        Directory.CreateDirectory(alphaDirectory);
        File.WriteAllText(
            System.IO.Path.Combine(alphaDirectory, "script.toml"),
            """
            id = "alpha"
            name = "Alpha"
            kind = "external"
            entry = "alpha.cmd"
            priority = 1
            """);

        File.WriteAllText(service.ScriptOrderPath, "beta\nalpha\n");

        var scripts = service.LoadRichScripts();

        var orderedIds = scripts.Select(static script => script.Manifest.Id).Take(5).ToArray();
        Assert.AreEqual("beta", orderedIds[0]);
        Assert.AreEqual("alpha", orderedIds[1]);
        Assert.HasCount(2, orderedIds);
    }
}
