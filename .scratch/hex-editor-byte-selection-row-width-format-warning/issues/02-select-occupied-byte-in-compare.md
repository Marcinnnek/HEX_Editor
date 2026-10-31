# 02: Select an occupied byte in Compare

**What to build:** The same byte-cell selection works in either Compare panel while keeping the clicked side and all existing comparison indicators clear.

**Blocked by:** 01 — Select an occupied byte in Editor.

**Status:** resolved

- [x] Clicking an occupied byte in either panel selects only that panel's address and outlines that byte in its HEX and ASCII columns.
- [x] Difference backgrounds, edited-byte outlines, and Find markers remain visible alongside the selection outline.
- [x] Address and outer-inset clicks still select corresponding rows in both panels without selecting a byte; blank addresses do not become byte selections.
- [x] Selection does not move the current viewport unexpectedly and works in both normal and large-image Compare layouts.
- [x] WPF acceptance scenarios cover clicks in A and B, simultaneous indicators, and preservation of Compare navigation.
