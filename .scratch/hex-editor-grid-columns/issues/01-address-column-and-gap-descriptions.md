# 01: Address column and address-gap descriptions

**What to build:** In Editor and both Compare panels, ordinary 32-bit addresses and the Address header are centered in a stable column sized to the measured eight-digit address plus a small inset. An address-gap row keeps its full description accessible despite that compact width.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] The Address column is the rendered width of `FFFFFFFF` plus 12 WPF units, with six-unit insets on both sides and no 110-unit minimum; it grows when the active font requires more width.
- [x] Ordinary addresses and the Address header are centered. The width does not change when scrolling past shorter rows or address gaps.
- [x] Long address-gap descriptions are left-aligned, ellipsized within the column, and available in full through a tooltip.
- [x] The same presentation works in Editor and both Compare panels, in small DataGrid and large windowed views; manual resizing does not override the measured width.
- [x] WPF acceptance coverage verifies measured width, alignment, truncation, and tooltip behavior.
