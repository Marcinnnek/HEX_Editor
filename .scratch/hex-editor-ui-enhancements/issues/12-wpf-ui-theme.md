# 12: Apply a consistent WPF UI theme

**What to build:** The main window, menus, buttons, inputs, tabs, dialogs, and notifications use WPF UI's visual system while the HEX presentation retains monospaced alignment. The app follows Windows appearance by default and remembers a manual light/dark choice.

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

- [ ] Supported controls use a consistent WPF UI style in both light and dark modes, with readable contrast, visible focus, hover, and disabled states.
- [ ] Automatic mode responds to Windows appearance changes; manual light/dark mode persists across application restarts and can be reset to automatic.
- [ ] HEX byte columns stay monospaced and aligned across themes and DPI settings.
- [ ] Dialogs and notifications follow one consistent presentation pattern without changing their underlying data operations.
- [ ] Appearance preferences are stored in user data rather than beside the executable.
- [ ] Inspect keyboard focus, both themes, DPI scaling, and representative Editor/Compare screens in WPF; verify preference persistence through a public settings seam.
