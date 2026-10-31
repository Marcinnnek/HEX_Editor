# 02: Migrate Editor range operations

**What to build:** Make the Editor display and edit a memory image through range-aware access while preserving its current behavior for ordinary files. This is one bounded migration batch of the expand–contract change.

**Blocked by:** 01: Expand memory image ranges.

**Status:** resolved

- [x] Editor rows and the Block list can display occupied ranges and address gaps through the expanded image representation, including bytes near both ends of the 32-bit space.
- [x] Existing byte edits, Edit Row, insertion, deletion, relocation, Merge, Undo, and Redo still produce the same observable results for small images.
- [x] Editing and navigation do not require one contiguous byte array for every memory segment.
- [x] The Editor remains usable and the existing public editor acceptance behavior stays green before full-range Fill is enabled.
