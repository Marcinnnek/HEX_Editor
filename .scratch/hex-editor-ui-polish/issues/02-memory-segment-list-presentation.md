# 02: Make memory-segment lists compact and predictable

**What to build:** The Editor and both Compare memory-segment lists show readable hexadecimal and decimal byte counts in a compact, stable start-address order, including in narrow Compare columns.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [ ] Every segment byte count uses uppercase unpadded hexadecimal and plain decimal in the form `0x15BC = 5564`, including small and large values.
- [ ] Start, end, and byte-count values are left-aligned in Editor and both Compare lists.
- [ ] Visible separation between segment rows is about half a character height, without clipping text or reducing the row's selection usability.
- [ ] Each list stays sorted by increasing segment start address, including after an edit or comparison refresh; clicking any column header does not reorder it.
- [ ] Narrow Compare panels retain the complete byte-count text on one line and make it reachable by horizontal scrolling, without truncation or wrapping.
- [ ] Segment selection, address navigation, Compare column ownership, and refresh behavior remain usable.
- [ ] Verify numeric formatting and ordering through public memory-image/list operations and inspect the three rendered lists at normal and narrow widths.

## Answer

Implemented in `5f111af` and refined in `9543692`/`6e89d00`: all three lists share byte-count formatting and compact left-aligned styles, keep ascending order with sorting disabled, and Compare supports horizontal scrolling for long counts. Rendered row-gap checks target about half a character height. Focused list tests passed 3/3; the final full suite passed 167/167.
