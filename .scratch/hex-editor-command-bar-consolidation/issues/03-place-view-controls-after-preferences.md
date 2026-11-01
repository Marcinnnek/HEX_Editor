# 03: Place view controls after Preferences

Status: resolved

**What to build:** Order the top command row as File, Edit, View, Tools, Preferences, Bytes per row, ASCII. Move the shared Bytes per row field and ASCII checkbox immediately after Preferences without changing their active-tab behavior.

**Blocked by:** 02.

- [x] Tools and Preferences precede Bytes per row and ASCII on the same top row.
- [x] All menus and both view controls remain reachable at the minimum supported window width.
- [x] Editor and Compare settings, validation, shortcuts, and command enabled states remain unchanged.
- [x] The command-bar acceptance test verifies the exact order and minimum-width reachability.

## Answer

Moved the shared view controls after Preferences in `MainWindow.xaml`. The focused WPF command-bar acceptance test passed at the minimum supported window width.
