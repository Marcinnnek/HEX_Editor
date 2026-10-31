# 05: Limit Merge and relocation results

**What to build:** Merge and relocation commit only valid images at or below the 100,000,000-occupied-byte allowance, without partial changes.

**Blocked by:** 03: Limit loaded images to 100 million occupied bytes.

**Status:** resolved

- [x] Merge checks the occupied-address union of the chosen result, rather than adding source byte counts; two valid inputs whose result exceeds the limit are rejected atomically.
- [x] Overlapping source bytes follow the existing merge decisions and count once in the resulting image.
- [x] Relocation preserves the whole logical segment, rejects addresses beyond `0xFFFFFFFF`, and verifies any resulting occupancy before commit.
- [x] Rejected Merge or relocation leaves image bytes, dirty state, and Undo/Redo history unchanged; accepted operations retain their existing single-Undo behavior.
- [x] Public merge and relocation tests cover distinct, overlapping, exact-boundary, over-limit, and highest-address cases.
