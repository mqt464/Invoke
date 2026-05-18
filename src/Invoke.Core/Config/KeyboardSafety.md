# Keyboard Safety

Invoke must not let `Alt + Space` fall through when app is using low-level fallback hook.

Windows and foreground apps may treat that chord as system-menu activation. Result can be stray menu focus, open window menu, or other input state glitches after launcher opens.

## Current Behavior

- `Alt + Space` opens Invoke.
- If standard hotkey registration fails, fallback hook watches only `Space` while `Alt` is held.
- When fallback hook fires, Invoke injects harmless `F24` press and release so Windows does not reinterpret released `Alt` key as menu activation.
- Plain `Alt` still passes through.
- Other `Alt` shortcuts still pass through.

## Intent

Fallback path exists to preserve launcher chord without stealing unrelated keyboard behavior. If this doc and implementation ever disagree, preserve normal Windows `Alt` behavior first, then restore launcher chord safely.
