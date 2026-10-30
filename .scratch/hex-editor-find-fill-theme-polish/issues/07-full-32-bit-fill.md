# 07: Fill the full 32-bit address space

**What to build:** Let a user fill every address from `0x00000000` through `0xFFFFFFFF`, save or export the resulting occupied bytes, and undo the edit as one operation.

**Blocked by:** 05: Retire the single-array segment contract; 06: Fill policy and whole-image range.

**Status:** resolved

- [ ] A Fill from zero through `0xFFFFFFFF` produces exactly 4,294,967,296 occupied bytes, with correct pattern values at the beginning, interior, and final address.
- [ ] Keep and Overwrite follow the chosen policy throughout that range, including existing occupied bytes and gaps.
- [ ] The operation warns above 50,000,000 addresses, shows progress, remains responsive, and can be cancelled without committing a partial image.
- [ ] The completed Fill is one Undo operation; Undo restores the prior image and Redo restores the filled image.
- [ ] Intel HEX and S-Record Save/Export can stream the resulting image with progress and cancellation, preserving existing output on failure.
- [ ] Disk-backed working storage is acceptable; insufficient space fails clearly without changing the current image.
