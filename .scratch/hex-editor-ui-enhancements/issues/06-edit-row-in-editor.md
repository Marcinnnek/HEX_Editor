# 06: Edit an occupied Editor row in one transaction

**What to build:** Double-clicking an occupied Editor row opens a modal Edit Row dialog with absolute addresses and synchronized HEX/ASCII values. Confirming valid changes makes one reversible edit; cancellation leaves the image untouched.

**Blocked by:** 02: Give the Editor a stable image document.

**Status:** ready-for-agent

- [ ] The dialog identifies the row start and selected byte address and displays original and proposed occupied-byte values.
- [ ] HEX accepts exactly two valid digits for any byte value; direct ASCII accepts printable 0x20–0x7E, and nonprintable values display as dots.
- [ ] Partial rows and address gaps are displayed without creating bytes at absent addresses; Insert or Fill remains the explicit way to add occupancy.
- [ ] Invalid input cannot commit; Cancel changes neither bytes nor dirty state.
- [ ] OK commits all changed bytes as one Undo/Redo action, updates the Editor view, and invalidates affected calculated results through the document change notification.
- [ ] Test staged validation, sparse rows, cancellation, atomic history, and document state through public operations; inspect the dialog interaction in WPF.
