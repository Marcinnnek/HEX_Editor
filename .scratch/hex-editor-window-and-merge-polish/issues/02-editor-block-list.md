# 02: Editor Block list

**What to build:** Replace the Editor memory-segment table with a simple, clickable, one-line Block list that preserves the actions users already perform on segments and establishes the presentation used by Compare.

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

- [ ] Each entry represents one contiguous memory segment and reads like `Block 1 Starts at: 0x08000000 Ends at: 0x080015BB (Length: 0x15BC = 5564)`.
- [ ] Blocks are numbered from 1 by increasing start address; labels refresh after edits, relocation, or merge changes segment boundaries.
- [ ] Hexadecimal values use uppercase digits; length has no unnecessary leading zeroes and its decimal form has no grouping separators.
- [ ] Each full description stays on one line and remains reachable through horizontal scrolling when Editor is narrow.
- [ ] A single click navigates to the Block start, a double-click opens relocation, and Ctrl/Shift selection continues to support exporting several segments.
- [ ] Public memory-segment behavior and rendered WPF interactions verify formatting, ordering, scrolling, navigation, relocation, and multiselection.
