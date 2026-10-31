# 08: Relocate a selected memory segment through a dialog

**What to build:** Selecting a memory segment navigates to its start without moving it. Double-clicking the selected segment opens the Relocate dialog, prefilled with its current start address so the user can make a small address change. The dialog previews the new absolute range and applies a safe, reversible move after validation and any required overwrite confirmation.

**Blocked by:** None (can start immediately; issue 02 is resolved).

**Status:** ready-for-agent

- [x] A single click selects the memory segment and navigates to its start without changing data.
- [x] Double-clicking a memory segment opens Relocate Selected Segment with the current start address prefilled and ready to edit.
- [x] The dialog shows the selected segment's current start, end, and byte count, accepts a hexadecimal new start, and previews the new end.
- [x] Malformed, overflowing, or format-incompatible destinations are rejected before any data changes.
- [x] Occupied destination bytes block by default; overwriting requires a separate affirmative confirmation even when values are identical.
- [x] Canceling either dialog or declining overwrite leaves image, selection, and dirty state unchanged.
- [x] A successful move preserves segment bytes and length, refreshes affected views, marks the base modified, and is one Undo/Redo action.
- [x] Test relocation through the document/workspace seam.
- [x] Inspect the modal flow in WPF.

## Comments

- Merged relocation dialog and workspace operation on `integration` as `1cf1166`; the implementer reports the WPF project built and all 7 relocation tests passed. Live modal inspection remains outstanding.
- User refinement (2026-10-30): double-clicking a segment is a shortcut to relocation; the current address is the initial value so a small change can be made quickly.
