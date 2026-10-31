# 03: Show the Shift-selected byte range

**What to build:** Shift+click makes an address range visibly selected in Editor and the clicked Compare panel, including across display rows, while leaving address gaps visually empty.

**Blocked by:** 01 — Select an occupied byte in Editor; 02 — Select an occupied byte in Compare.

**Status:** resolved

- [x] Shift+click outlines every occupied byte in the selected absolute-address range in both HEX and ASCII.
- [x] Unoccupied addresses within the range have no selected byte cell, and the space between rows remains noninteractive.
- [x] In Compare, the range outline belongs only to the clicked panel; difference, edit, and Find indicators remain readable.
- [x] A new plain byte click replaces the visible range with one selected byte; Address or outer-inset clicks return to row selection.
- [x] Existing range-based commands retain their address semantics, with WPF scenarios covering multi-row ranges and sparse memory images.
