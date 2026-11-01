# 02: Share view controls across Editor and Compare

Status: resolved

**What to build:** Remove the toolbar under the menu and use a single top-row Bytes per row field and ASCII checkbox for the active Editor or Compare tab. Remove the duplicate controls from Compare while retaining its other settings.

**Blocked by:** None.

- [x] Open, Save, Compare, Merge, Undo, and Redo remain available through menus after their toolbar buttons are removed.
- [x] Bytes per row accepts presets 8, 16, and 32 and custom decimal values from 1 through 128 on the active tab.
- [x] ASCII follows the active tab and updates both Compare panels together.
- [x] Compare retains its view mode, range, address, and difference navigation controls.
- [x] View retains the 8/16/32 commands and Show ASCII command.
- [x] Acceptance tests cover active-tab routing, invalid row widths, and Compare address preservation.

## Answer

Implemented in `971fb79`. The initial top-row position is superseded by ticket 03; the shared control behavior remains in place.
