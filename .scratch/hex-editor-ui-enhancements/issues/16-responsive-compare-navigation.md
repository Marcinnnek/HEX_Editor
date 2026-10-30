# 16: Keep comparison and scrolling responsive

**What to build:** Both Compare panels remain usable during wheel and scrollbar navigation on representative sparse firmware images. Long compare and file operations expose progress and cancellation without freezing the interface.

**Blocked by:** 07: Edit the left base image from Compare; 13: Rebuild the Editor and Compare workspace layout; 14: Synchronize view and navigation settings with the active tab; 15: Replace permanent advanced panels with compact commands.

**Status:** ready-for-agent

- [ ] Wheel scrolling and dragging the scrollbar in either Compare panel do not freeze the UI or saturate the CPU through avoidable per-scroll full-image work.
- [ ] Navigation remains synchronized by absolute address for 8/16/32 bytes per row, both ASCII modes, and sparse high-address gaps.
- [ ] Opening a comparison of typical files up to 10 MB targets 15 seconds or less on the user's machine, with the shortest practical time preferred.
- [ ] Long import, compare, and export operations display meaningful progress and offer cancellation; cancellation leaves document and tab state valid.
- [ ] Viewer data and visual elements remain virtualized, and unoccupied address gaps are never materialized as dense rows.
- [ ] Measure behavior on representative 10 MB and sparse fixtures in both panels, and retain behavioral regression coverage at the comparison/view-model seam.
