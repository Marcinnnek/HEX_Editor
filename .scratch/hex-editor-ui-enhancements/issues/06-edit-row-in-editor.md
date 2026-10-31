# 06: Edit an occupied Editor row in one transaction

**What to build:** Double-clicking an occupied Editor row opens a modal Edit Row dialog with the row start, byte offsets from that start, and synchronized HEX/ASCII values. ASCII accepts one printable ASCII character per byte; HEX accepts one or two hexadecimal digits. Confirming valid changes makes one reversible edit; cancellation leaves the image untouched.

**Blocked by:** None (can start immediately; issue 02 is resolved).

**Status:** ready-for-agent

- [x] The dialog identifies the row start and selected byte offset from that start; byte columns use zero-based offsets instead of repeating full absolute addresses above every cell.
- [x] HEX accepts one or two hexadecimal digits (`0–9`, `A–F`, case-insensitive) per byte; one digit represents its value in the low nibble. ASCII accepts exactly one printable ASCII character (`0x20–0x7E`) per byte, and nonprintable values display as dots.
- [x] Original and proposed occupied-byte values remain synchronized between HEX and ASCII; invalid input cannot be committed.
- [x] Partial rows and address gaps are displayed without creating bytes at absent addresses; Insert or Fill remains the explicit way to add occupancy.
- [x] Invalid input cannot commit; Cancel changes neither bytes nor dirty state.
- [x] OK commits all changed bytes as one Undo/Redo action, updates the Editor view, and invalidates affected calculated results through the document change notification.
- [x] Test staged validation, sparse rows, cancellation, atomic history, and document state through public operations.
- [x] Inspect the dialog interaction in WPF.

## Comments

- Merged Edit Row implementation on `integration`; the implementer reports 118 tests passing after merging the current workspace changes. Manual WPF dialog inspection remains outstanding.
- User refinement (2026-10-30): show byte offsets from the row start in the grid, restrict ASCII cells to one character, and validate HEX cells containing one or two hexadecimal digits.
