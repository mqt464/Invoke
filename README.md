<p align="center">
  <img src="docs/assets/invoke-logo.png" alt="Invoke logo" width="96" />
</p>

<h1 align="center">Invoke</h1>

<p align="center">Keyboard-first Windows launcher for apps, commands, files, windows, and script-powered actions.</p>

Invoke is native Windows launcher built with WPF on `.NET 10`. It is designed around plain-text configuration, rofi-style `.rasi` themes, built-in search modes, and rich external scripts that can turn launcher into front end for your own workflows.

## What Invoke is for

Invoke works well when you want one keyboard surface for:

- launching installed apps
- running commands from `PATH`
- jumping to files and folders with Everything
- switching open windows
- exposing custom tools through script-backed results
- driving `dmenu`-style selection flows on Windows

## Features

- Built-in modes: `drun`, `run`, `files`, `window`, and `combi`
- Config-first runtime under `%AppData%\Invoke`
- `.rasi` themes with widget-level layout and color control
- Rich external scripts with `script.toml` manifests
- Trigger prefixes like `/note` or token prefixes like `!yt`
- Persistent script sessions for stateful tools
- Background helper processes for scripts
- Recent-launch score boosting
- Live reload for config, themes, and scripts
- Tray menu for launcher access, config reload, startup toggle, and hotkey suspension
- `dmenu` compatibility through `Invoke.Cli`

## Requirements

- Windows 11 recommended
- `.NET 10` SDK for local development
- `Everything` plus `es.exe` for `files` mode

Invoke looks for `es.exe` beside app, in `tools\Everything`, on `PATH`, or through `everything-cli-path`. If Everything IPC is unavailable, Invoke can try to start `Everything.exe` automatically and retry search.

## Build and run

```powershell
dotnet build .\Invoke.sln
dotnet run --project .\src\Invoke.App\Invoke.App.csproj
```

If `dotnet` on `PATH` points at runtime-only install:

```powershell
.\.dotnet\dotnet.exe build .\Invoke.sln
```

Shortcut:

```powershell
.\dev.ps1
```

## First-run layout

Invoke seeds:

```text
%AppData%\Invoke\
  config.rasi
  themes\
    default.rasi
  scripts\
    order.txt
```

If `INVOKE_PORTABLE=1`, config is stored beside executable in `config\`.

## Example config

```rasi
configuration {
  modes: "drun,run,files,window,combi";
  default-mode: "drun";
  combi-modes: "drun,run,files,window";
  theme: "default";
  launcher-hotkey: "Alt+Space";
  everything-cli-path: "C:/Tools/Everything/es.exe";
  everything-path: "C:/Tools/Everything/Everything.exe";
  mode-window-hotkey: "Alt+1";
  mode-run-hotkey: "Alt+2";
  kb-mode-next: "Alt+Right";
  kb-mode-previous: "Alt+Left";
  lines: 8;
  columns: 1;
}
```

Only minimal keys are active by default: `kb-accept-entry`, `kb-cancel`, `kb-row-up`, and `kb-row-down`. Additional `kb-*` bindings only apply when configured.

## Use cases by mode

- `drun`: launch installed apps and shortcuts
- `run`: execute commands and `PATH` executables
- `files`: search files and folders through Everything
- `window`: switch focus to open windows
- `combi`: merge results from multiple providers into one default search

## Rich scripts

Each script gets folder under `%AppData%\Invoke\scripts`.

```text
%AppData%\Invoke\scripts\echo\
  script.toml
  echo.ps1
```

Example manifest:

```toml
id = "echo"
name = "Echo"
kind = "external"
entry = "echo.ps1"
priority = 70
triggers = ["/echo"]
```

Rich scripts can return structured actions like:

- execute command
- open URL
- open path
- copy text
- rewrite query

## Dmenu

Invoke also ships `Invoke.Cli`:

```powershell
"alpha`nbeta`ngamma" | .\src\Invoke.Cli\bin\Debug\net10.0-windows\Invoke.Cli.exe dmenu -filter be
"alpha|beta|gamma" | .\src\Invoke.Cli\bin\Debug\net10.0-windows\Invoke.Cli.exe dmenu -sep "|" -dump
```

## Documentation site

Full docs now live in Docusaurus site under [`website`](website).

Local docs dev:

```powershell
cd .\website
npm install
npm start
```

Useful docs entry points:

- [What Invoke is](website/docs/overview.mdx)
- [Install and run](website/docs/getting-started/install-and-run.mdx)
- [Modes](website/docs/usage/modes.mdx)
- [Themes](website/docs/customization/themes.mdx)
- [Rich scripts](website/docs/extensions/rich-scripts.mdx)
- [Deploy docs to GitHub Pages](website/docs/reference/deploy-docs.mdx)
