# 07: Relocate an occupied memory segment

**What to build:** Move an occupied segment to a new base address while preserving all byte values and relative offsets, with clear handling for collisions and invalid destinations.

**Blocked by:** 04: Virtualized single-image editor view.

**Status:** resolved

- [x] Select an occupied segment and specify its destination base address.
- [x] Preserve byte order and every relative offset during relocation.
- [x] Validate the destination against 32-bit overflow and occupied destination addresses before applying the move.
- [x] Present an explicit choice for destination overlaps, including identical values, and allow cancelling the relocation.
- [x] Refresh segment summaries and the visible address range after relocation.
- [x] Support undo and redo for relocation.

## Comments

- Added a relocation preview that reports each destination collision, including identical values. Applying the preview supports keeping existing destination bytes, overwriting them, or cancelling; overflow is rejected before the move.
- Added a selected-segment relocation action to the editor. Successful relocations refresh the segment list and address view and use the editor's shared undo/redo history.
- Verification: all 32 tests passed and the full solution build passed with no warnings.
