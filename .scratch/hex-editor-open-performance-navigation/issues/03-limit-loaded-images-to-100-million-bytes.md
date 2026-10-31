# 03: Limit loaded images to 100 million occupied bytes

**What to build:** Every image loaded for Editor, Compare, or Merge is accepted or rejected by its final count of unique occupied byte addresses, independently of encoded file length or address span.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] A loaded image with exactly 100,000,000 unique occupied addresses is allowed; one with 100,000,001 is rejected with a clear message and no partial replacement.
- [x] Duplicate or overlapping source records count each final occupied address once while preserving the existing overlap decision.
- [x] Sparse images may occupy any addresses from `0x00000000` through `0xFFFFFFFF`; unoccupied gaps do not consume the allowance.
- [x] There is no separate encoded text-file size cap; a long duplicate-heavy file remains eligible and cancellable while its unique occupancy stays within the limit.
- [x] Editor, Compare base, and Compare reference images each receive an independent allowance; Merge inputs are validated on load.
- [x] Public file-service and workspace tests cover both formats, duplicates, exact boundary, over-limit rejection, distant addresses, and the highest address.
