# 05: Address click selects a row

**What to build:** Clicking a row's Address gives a clear row-only selection in Editor and Compare without jumping the viewport or pretending an address gap contains a byte.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] Clicking Address on an occupied row highlights that row, sets the current address to the row start, and clears the previous byte selection without scrolling.
- [x] In Compare, the corresponding row is highlighted in both panels while the panels retain their current vertical positions.
- [x] Clicking Address on an address-gap row highlights the gap row but does not select a byte, change the current address, or open Edit Row.
- [x] A new row-only selection replaces the previous row highlight; ordinary byte clicks, Find, difference navigation, and double-click editing continue to work.
- [x] WPF acceptance coverage exercises Editor and both Compare panels in DataGrid and windowed views, including occupied and gap rows.
