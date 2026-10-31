# 11: Find in a full-range image

**What to build:** Keep Find bytes usable when a memory image contains all 4,294,967,296 addresses after a full-range Fill.

**Blocked by:** 07: Fill the full 32-bit address space; 09: Find bytes in Editor; 10: Find bytes in Compare.

**Status:** resolved

- [x] Editor Find can locate a pattern near the first, interior, and final address of a fully occupied image without materializing a second 4 GiB copy.
- [x] Compare Find can search a fully occupied base or reference image while preserving address-then-panel ordering.
- [x] Long scans show meaningful progress, keep the application responsive, and honor cancellation without changing the current selection.
- [x] Next, Previous, and wrap behavior remain correct at `0x00000000` and `0xFFFFFFFF`.
