# 02: Make memory-segment lists compact and predictable

**What to build:** The Editor and both Compare memory-segment lists show readable hexadecimal and decimal byte counts in a compact, stable start-address order, including in narrow Compare columns.

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

- [ ] Every segment byte count uses uppercase unpadded hexadecimal and plain decimal in the form `0x15BC = 5564`, including small and large values.
- [ ] Start, end, and byte-count values are left-aligned in Editor and both Compare lists.
- [ ] Visible separation between segment rows is about half a character height, without clipping text or reducing the row's selection usability.
- [ ] Each list stays sorted by increasing segment start address, including after an edit or comparison refresh; clicking any column header does not reorder it.
- [ ] Narrow Compare panels retain the complete byte-count text on one line and make it reachable by horizontal scrolling, without truncation or wrapping.
- [ ] Segment selection, address navigation, Compare column ownership, and refresh behavior remain usable.
- [ ] Verify numeric formatting and ordering through public memory-image/list operations and inspect the three rendered lists at normal and narrow widths.
