# 12: Apply a consistent WPF UI theme

**What to build:** The main window, menus, buttons, inputs, tabs, dialogs, and notifications use WPF UI's visual system while the HEX presentation retains monospaced alignment. The app follows Windows appearance by default and remembers a manual light/dark choice.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] Supported controls use a consistent WPF UI style in both light and dark modes, with readable contrast, visible focus, hover, and disabled states.
- [x] Automatic mode responds to Windows appearance changes; manual light/dark mode persists across application restarts and can be reset to automatic.
- [x] HEX byte columns stay monospaced and aligned across themes and DPI settings.
- [x] Dialogs and notifications follow one consistent presentation pattern without changing their underlying data operations.
- [x] Appearance preferences are stored in user data rather than beside the executable.
- [x] Inspect keyboard focus, both themes, DPI scaling, and representative Editor/Compare screens in WPF; verify preference persistence through a public settings seam.

## Comments

- WPF UI 4.3.0 styles the supported controls and Fluent windows. Automatic mode uses `SystemThemeWatcher`; the Appearance menu persists Light, Dark, or Follow Windows under the user's local application data.
- Replaced application prompts with a themed modal presentation while retaining their existing Yes/No/Cancel results. HEX rows keep Consolas and DPI-aware text measurement; the data column has a readable minimum width.
- `dotnet test tests/HexEditor.Tests/HexEditor.Tests.csproj --no-restore` passed 106 tests after merging the integration tip. Offscreen WPF renders were inspected for a populated light Editor at 96 and 144 DPI and a dark Compare view at 96 DPI. A visible button accepted keyboard focus and exposed a focus style. Live Windows appearance switching and physical keyboard navigation remain manual acceptance checks.
