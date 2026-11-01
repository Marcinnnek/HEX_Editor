# Main Command Bar Consolidation

## Intent

Give the Editor and Compare workspaces one compact command row. Keep file operations and view settings accessible without a second toolbar or duplicate Compare controls.

## Agreed behavior

- The top row orders its controls as File, Edit, View, Bytes per row, ASCII, Tools, and Preferences.
- The Bytes per row field operates on the active Editor or Compare tab. It retains the 8, 16, and 32 presets and accepts decimal values from 1 through 128.
- The ASCII checkbox operates on the active tab. View keeps its 8/16/32 bytes-per-row commands and Show ASCII command.
- File places Compare images and Merge images immediately after Close Image. Their existing workflows and enabled states remain intact.
- File retains Export Image, Export Selected Segments, and Export Address Range. The duplicate top-level Export menu is removed.
- The separate toolbar containing Open, Save, Compare, Merge, Undo, Redo, Bytes per row, and ASCII is removed. The commands remain in menus with their existing shortcuts.
- The Compare panel no longer repeats Bytes per row and Show ASCII. Its view mode, range, address, and difference controls remain.
- The file-operation progress and cancel panel remains available during long operations.

## Verification

- At the minimum supported window width, the top-row controls and both trailing menus remain reachable.
- The top-row field updates the active Editor or Compare tab, preserves the Compare selection when row width changes, and restores the current value after invalid input.
- The top-row ASCII checkbox follows the active tab and updates both Compare panels together.
- File commands retain their enabled states during file loading, and each Export command appears once.

## Relationship to earlier specs

This decision supersedes the permanent five-group toolbar layout in `.scratch/hex-editor-ui-enhancements/spec.md` and its compact-toolbar issue. It preserves the active-tab view-setting behavior agreed there.
