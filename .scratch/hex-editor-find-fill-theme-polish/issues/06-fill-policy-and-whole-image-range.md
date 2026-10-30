# 06: Fill policy and whole-image range

**What to build:** Let users choose whether Fill Range preserves or overwrites occupied bytes, derive the whole image's address interval, and inspect a large interval before applying it. This slice works for ranges supported by the current image model while full-range storage is migrated separately.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [ ] Fill Range offers Keep existing data and Overwrite existing data, with Keep selected by default.
- [ ] Keep writes only to address gaps; Overwrite writes to every address in the selected interval.
- [ ] A repeating pattern remains anchored to the interval's first address even when Keep skips occupied bytes.
- [ ] Get fill all region sets the inclusive lowest and highest occupied addresses, including intervening gaps; it is unavailable for an empty image while manual entry remains possible.
- [ ] The dialog shows the inclusive address count and warns when it exceeds 50,000,000 addresses; cancelling the warning leaves the image unchanged.
- [ ] A completed Fill is one Undo operation and does not partially change the image after validation or cancellation fails.
