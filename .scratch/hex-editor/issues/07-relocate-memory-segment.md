# 07: Relocate an occupied memory segment

**What to build:** Move an occupied segment to a new base address while preserving all byte values and relative offsets, with clear handling for collisions and invalid destinations.

**Blocked by:** 04: Virtualized single-image editor view.

**Status:** ready-for-agent

- [ ] Select an occupied segment and specify its destination base address.
- [ ] Preserve byte order and every relative offset during relocation.
- [ ] Validate the destination against 32-bit overflow and occupied destination addresses before applying the move.
- [ ] Present an explicit choice for destination overlaps, including identical values, and allow cancelling the relocation.
- [ ] Refresh segment summaries and the visible address range after relocation.
- [ ] Support undo and redo for relocation.
