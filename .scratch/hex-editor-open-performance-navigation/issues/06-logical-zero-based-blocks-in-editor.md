# 06: Logical zero-based Blocks in Editor

**What to build:** Editor shows one Block per maximal contiguous occupied memory segment, numbers Blocks from zero, and performs Block actions on the entire logical segment.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] Adjacent occupied technical ranges appear as one Block even when their combined size exceeds a storage chunk threshold; address gaps separate Blocks.
- [x] Editor Blocks are sorted by start address and labeled `Block 0`, `Block 1`, and so on; labels update after deletion, addition, connection, or relocation.
- [x] Existing one-line descriptions, complete addresses and lengths, and horizontal scrolling remain usable.
- [x] Single click jumps to the Block start; double-click relocation and Delete act on the whole Block and preserve their existing confirmation and Undo behavior.
- [x] Exporting a selected Block includes every occupied byte in that logical segment, not just one backing range.
- [x] Public image/workspace and rendered Editor tests cover multi-range Blocks, zero-based labels, navigation, relocation, Delete, and export.
