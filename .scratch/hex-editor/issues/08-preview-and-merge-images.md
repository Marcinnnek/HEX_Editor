# 08: Preview and merge memory images

**What to build:** Preview the result of merging one image into another, resolve overlaps by address range, and apply the chosen result as a reversible edit.

**Blocked by:**
- 05: Side-by-side memory comparison.
- 06: Edit memory images and save changes.

**Status:** ready-for-agent

- [ ] Merge File A into File B or File B into File A by absolute address.
- [ ] Combine non-overlapping addresses without changing their values or locations.
- [ ] Summarize every overlap, including identical values, and show a preview before applying the merge.
- [ ] Provide a default keep-source or keep-destination choice and allow overrides for a defined address range.
- [ ] Never apply conflicting bytes silently; allow cancelling the merge.
- [ ] Apply the resolved merge and support undo and redo.
- [ ] Refresh the memory segment list and any open comparison after the merge.
