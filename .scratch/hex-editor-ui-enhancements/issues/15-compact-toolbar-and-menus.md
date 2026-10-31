# 15: Replace permanent advanced panels with compact commands

**What to build:** The main window shows only File, Compare/Merge, History, View, and Navigate as primary toolbar groups. Selection/Range, Edit, Relocate, Checksum, and Export remain available through menus and focused dialogs; commands that cannot fit move into More.

**Blocked by:** 05: Save and close the active image across tabs; 07: Edit the left base image from Compare; 08: Relocate a selected memory segment through a dialog; 09: Use focused dialogs for range, insert, and fill; 10: Calculate and write checksums through a dialog; 11: Export chosen occupied data with per-export options; 12: Apply a consistent WPF UI theme; 13: Rebuild the Editor and Compare workspace layout; 14: Synchronize view and navigation settings with the active tab.

**Status:** ready-for-human

- [x] Only the five agreed primary groups persist beneath the menu bar; former advanced groups and their parameter fields are removed from the permanent layout.
- [x] File, Edit, View, Compare, Merge, Tools, and Export commands remain discoverable in appropriate menus or context menus and invoke their working dialogs.
- [x] Commands are enabled only when the active document, tab, segment, selection, or result makes them valid; Save/Undo/Redo target the active base.
- [x] At narrow widths, primary toolbar groups collapse into More at defined window-width thresholds, keeping their commands available from the overflow menu.
- [x] Verify narrow-window More menu keyboard access and confirm that overflow does not force horizontal scrolling of the Editor or Compare workspace.
- [x] Preserve useful existing shortcuts and add conventional shortcuts where absent; visible focus and status feedback remain consistent.
- [x] Manually walk every migrated command in Editor and Compare, including narrow-window overflow, and test active-tab command routing through the workspace seam.

## Comments

- Merged compact toolbar and active-base command routing on `integration` as `37cbd97`. The implementer reports the active-base routing test passed; XAML parsing and `git diff --check` passed. Narrow-window, keyboard, and full command walkthrough acceptance remains outstanding.
- Review fix merged on `integration` as `5f0ed44`: View collapses below 900 px, History below 760 px, and Compare/Merge below 650 px, exposing the groups' commands through More. Integrated tests pass (132/132); manual keyboard and workspace-scroll checks remain outstanding.
