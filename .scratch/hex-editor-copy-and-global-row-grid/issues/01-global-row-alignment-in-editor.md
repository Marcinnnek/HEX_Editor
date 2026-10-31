# 01: Align Editor rows to the global address grid

**What to build:** Make Editor rows begin on global multiples of the configured width, with each occupied byte displayed at its absolute-address offset in the row.

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

- [ ] For supported widths from 1 through 128, displayed row starts follow multiples of the width from address zero, including when a Block starts partway through a row.
- [ ] Keep cells for unoccupied addresses blank; do not shift bytes to the beginning of a Block or source record.
- [ ] At width 17, a Block beginning at `0xA0000002` appears on rows `0xA0000000`, `0xA0000011`, `0xA0000022`, and so on, with its first byte at offset 2.
- [ ] S-Record entries beginning at `0x0002`, `0x0012`, and `0x0022` follow global row boundaries rather than source-record boundaries.
- [ ] Address navigation, selection, and editing continue to use the same absolute byte addresses after alignment.
- [ ] Partial rows through `0xFFFFFFFF` remain valid, without address overflow.
- [ ] Compare's ordinary and differences-only row mappings are verified against global alignment at custom widths; correct them only if they diverge.
- [ ] Public Editor and Compare model tests cover the row labels, occupied cell offsets, sparse gaps, width changes, and high-address boundary.
