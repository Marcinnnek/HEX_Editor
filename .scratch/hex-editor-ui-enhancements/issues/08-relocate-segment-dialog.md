# 08: Relocate a selected memory segment through a dialog

**What to build:** Selecting a memory segment only selects it. The Relocate command opens a focused dialog that previews the new absolute range and applies a safe, reversible move after validation and any required overwrite confirmation.

**Blocked by:** 02: Give the Editor a stable image document.

**Status:** ready-for-agent

- [ ] The dialog shows the selected segment's current start, end, and byte count, accepts a hexadecimal new start, and previews the new end.
- [ ] Malformed, overflowing, or format-incompatible destinations are rejected before any data changes.
- [ ] Occupied destination bytes block by default; overwriting requires a separate affirmative confirmation even when values are identical.
- [ ] Canceling either dialog or declining overwrite leaves image, selection, and dirty state unchanged.
- [ ] A successful move preserves segment bytes and length, refreshes affected views, marks the base modified, and is one Undo/Redo action.
- [ ] Test relocation through the document/workspace seam and inspect the modal flow in WPF.
