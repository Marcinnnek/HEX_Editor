# 04: Limit resulting images after editing

**What to build:** Direct edits, Fill, Insert, Paste, and other byte-writing commands cannot leave an editable memory image with more than 100,000,000 occupied byte addresses.

**Blocked by:** 03: Limit loaded images to 100 million occupied bytes.

**Status:** resolved

- [x] Each supported byte-writing operation checks the prospective occupied count of the resulting image before commit.
- [x] Fill and other overwrites of already occupied addresses are allowed when the final count stays within the allowance, even if the requested interval is large.
- [x] Fill with Keep existing data counts only newly occupied addresses and preserves its existing pattern alignment and Undo behavior.
- [x] An operation that would exceed the limit reports the boundary and leaves bytes, dirty state, and Undo/Redo history unchanged.
- [x] The full 32-bit address space remains available for sparse edits, including an occupied byte at `0xFFFFFFFF`.
- [x] Public editing-session and workspace tests cover exact-boundary acceptance, one-byte overflow, overwrite versus insertion, Paste, Fill, byte edits, and other user-facing writes.
