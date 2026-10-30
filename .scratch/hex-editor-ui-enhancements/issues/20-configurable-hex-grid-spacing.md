# 20: Configure HEX grid spacing in Preferences

**What to build:** Add a Preferences window reachable from the main menu. It provides independent horizontal byte spacing and vertical row spacing controls, persists both values per user, and uses slightly tighter spacing than the current default. Comparison separator spaces remain neutral when adjacent bytes are highlighted.

**Blocked by:** None (can start immediately; Editor and Compare views already exist).

**Status:** ready-for-agent

- [ ] Add a Preferences command to the main menu and open a focused settings window.
- [ ] Provide independent controls for horizontal spacing between bytes and vertical spacing between rows.
- [ ] Apply changes to Editor and Compare views and persist them under `%LOCALAPPDATA%\HexEditor`, with sensible defaults on first launch.
- [ ] Use slightly tighter spacing than the current grid by default.
- [ ] Keep spacing and separator backgrounds neutral when neighboring comparison bytes are highlighted.
- [ ] Verify independent adjustment, application across views, first-launch defaults, and persistence after restart.

## Comments

- User decision (2026-10-30): expose both spacing settings from a Preferences menu item and provide a settings window; horizontal and vertical spacing are separate settings and persist.
- User decision (2026-10-30): highlighted comparison bytes must not color the spaces between bytes.
