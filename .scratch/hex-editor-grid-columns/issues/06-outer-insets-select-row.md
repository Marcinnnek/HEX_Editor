# 06: Outer insets select a row

**What to build:** Clicking the outer inset of Address, HEX Data, or ASCII selects the row with the same behavior as an Address click, while byte cells remain independently clickable.

**Blocked by:** 01: Address column and address-gap descriptions; 02: HEX Data column fits row capacity; 03: ASCII column fit and background; 05: Address click selects a row.

**Status:** resolved

- [x] The six-unit outer insets of all three columns select the row without selecting an adjacent byte or moving the viewport.
- [x] An occupied-row inset click highlights that row, sets the current address to its start, and clears the prior byte selection; Compare highlights the corresponding row in both panels.
- [x] An address-gap inset click only highlights the gap row and leaves the current address unchanged; Edit Row does not open.
- [x] The behavior matches Address clicks in Editor and both Compare panels, in DataGrid and windowed views, at 8, 16, and 32 bytes per row.
- [x] WPF acceptance coverage verifies the inset boundary separately from byte-cell hits and preserves existing scroll and double-click behavior.
