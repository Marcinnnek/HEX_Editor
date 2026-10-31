# 01: Align Editor rows to the global address grid

**What to build:** Make Editor rows use one width-based grid anchored at each 16 MiB address-region start, with each occupied byte displayed at its absolute-address offset in that grid.

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

- [ ] For supported widths from 1 through 128, each address region uses its 16 MiB start as one fixed grid origin; row starts advance by the configured width from that origin.
- [ ] Keep cells for unoccupied addresses blank; do not shift bytes to the beginning of a Block or source record.
- [ ] At width 17, a Block beginning at `0xA0000002` appears on rows `0xA0000000`, `0xA0000011`, `0xA0000022`, and so on, with its first byte at offset 2; `0xA0001002` is in the row starting at `0xA0001001`.
- [ ] S-Record entries beginning at `0x0002`, `0x0012`, and `0x0022` follow one grid anchored at `0x00000000`, not source-record boundaries.
- [ ] Address navigation, selection, and editing continue to use the same absolute byte addresses after alignment.
- [ ] Partial rows through `0xFFFFFFFF` remain valid, without address overflow.
- [ ] Compare's ordinary and differences-only row mappings use the same address-region origin in both panels; correct them if they diverge.
- [ ] Public Editor and Compare model tests cover the row labels, occupied cell offsets, sparse gaps, width changes, and high-address boundary.
