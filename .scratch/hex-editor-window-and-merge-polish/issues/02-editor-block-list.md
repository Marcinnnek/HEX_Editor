# 02: Editor Block list

**What to build:** Replace the Editor memory-segment table with a simple, clickable, one-line Block list that preserves the actions users already perform on segments and establishes the presentation used by Compare.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] Each entry represents one contiguous memory segment and reads like `Block 1 Starts at: 0x08000000 Ends at: 0x080015BB (Length: 0x15BC = 5564)`.
- [x] Blocks are numbered from 1 by increasing start address; labels refresh after edits, relocation, or merge changes segment boundaries.
- [x] Hexadecimal values use uppercase digits; length has no unnecessary leading zeroes and its decimal form has no grouping separators.
- [x] Each full description stays on one line and remains reachable through horizontal scrolling when Editor is narrow.
- [x] A single click navigates to the Block start, a double-click opens relocation, and Ctrl/Shift selection continues to support exporting several segments.
- [x] Public memory-segment behavior and rendered WPF interactions verify formatting, ordering, scrolling, navigation, relocation, and multiselection.

## Answer

Replaced the Editor segment table with a one-line, horizontally scrollable Block list. The shared segment row now exposes the required numbered description, and the Editor refreshes it from ascending occupied segment addresses. Selection still navigates to the segment and double-click still opens relocation; extended selection remains wired into selected-segment export.

Rendered WPF coverage verifies numbering, the specified address and length formatting, no-wrap rendering, narrow-window horizontal reachability, navigation to a selected segment, and extended selection. Focused test command: `dotnet test tests/HexEditor.Tests/HexEditor.Tests.csproj --filter FullyQualifiedName~EditorSegmentListLayoutTests --no-restore`.
