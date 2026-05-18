# Invoke TODO

## Phase 1 - Scaffold
- [x] Create solution/project files for desktop app, core library, and tests.
- [x] Add initial README with run/build notes.
- [x] Add config, plugin, query, and result primitives.

## Phase 2 - Launcher Shell
- [x] Add WPF launcher window opened by `Alt + Space`.
- [x] Add keyboard-first result navigation and action execution.
- [x] Add safe plain-Win behavior that opens only taskbar/search surface without stuck modifier risk.
- [x] Add tray icon and startup toggle UI.

## Phase 3 - Search + Commands
- [x] Add app search provider.
- [x] Add Everything-backed file search provider using `es.exe`.
- [x] Auto-start Everything and retry when `es.exe` reports missing IPC.
- [x] Add bang provider with built-in data and user overrides.
- [x] Add winget install/uninstall provider.
- [x] Add `/config` provider.
- [x] Add custom shell command provider.
- [ ] Replace built-in bang seed list with vendored maintained bang dataset.
- [ ] Add Everything SDK/IPC path before `es.exe` fallback.

## Phase 4 - Customization
- [x] Add JSON config files in user config folder.
- [x] Add JSON themes file with colors, font, radius, opacity, spacing, geometry, and custom resources.
- [x] Add live theme reload.
- [x] Add plugin ordering editor.

## Phase 5 - Packaging
- [x] Add installer/portable packaging notes.
- [ ] Add WiX or MSIX packaging project.
- [x] Add portable zip publish script.
- [ ] Bundle or install Everything during packaging so file search works after Invoke install.
- [x] Add startup registration support.

## Phase 6 - Tests
- [x] Add parser tests.
- [x] Add ranking/manager tests.
- [ ] Add hotkey integration tests.
- [ ] Add UI automation tests.
- [ ] Add installer smoke tests.
