# 02: HEX Data column fits row capacity

**What to build:** In Editor and both Compare panels, HEX Data uses only the width needed for the active 8-, 16-, or 32-byte row and its configured byte spacing, while retaining readable one-line data and horizontal scrolling when the panel is narrow.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] HEX Data has six WPF units of outer inset on both sides, and its header aligns with the first byte after the left inset.
- [x] Width follows the active Bytes per row and horizontal byte spacing settings immediately; shorter final rows and address gaps do not change it during scrolling.
- [x] At 32 bytes per row in a narrow Editor or Compare panel, full-size bytes remain on one line and can be reached with that panel's horizontal scrollbar.
- [x] Changing Bytes per row or byte spacing resets horizontal scroll to the left without moving the current vertical row.
- [x] Editor and both Compare panels behave the same in DataGrid and windowed views; manual DataGrid resizing cannot override the computed width.
- [x] WPF acceptance coverage checks all row capacities, spacing changes, width stability, and horizontal overflow.
