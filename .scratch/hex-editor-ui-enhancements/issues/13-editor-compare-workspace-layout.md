# 13: Rebuild the Editor and Compare workspace layout

**What to build:** The Editor shows its memory-segment list above a full-width HEX table. Compare shows two aligned panels, each with its segment list and HEX table, plus the settings row and result area from the agreed visual reference in WPF UI style.

**Blocked by:** None (can start immediately; issue 12 is resolved).

**Status:** ready-for-agent

- [x] Editor uses the agreed vertical memory-segment list and full-width Address, HEX Data, ASCII layout.
- [x] Compare retains two side-by-side panels with block lists above aligned HEX grids and a visible difference-result area.
- [x] Compare has two columns: the first column stacks File 1's segment list above File 1's data, and the second stacks File 2's segment list above File 2's data.
- [ ] In Editor, size the segment list just wider than its address content instead of stretching it across the workspace.
- [x] Segment selection and absolute-address navigation remain usable in both workspaces.
- [x] Missing bytes stay visibly absent and distinct from stored 00/FF; row and byte alignment survive resizing and ASCII visibility changes.
- [x] Virtualization remains active and sparse address gaps do not create dense rows or excessive visual elements.
- [x] Exercise existing view-model behavior for alignment and navigation.
- [ ] Inspect layout at normal and narrow sizes.

## Comments

- Merged on `integration` as `3063b19`. The implementer reports the WPF UI build succeeded and all 116 tests passed after merging integration tip `ec9ea30`. Visual review at normal and narrow sizes remains outstanding.
- User clarification (2026-10-30): Compare remains two columns side by side; each file's segment list is directly above that same file's data. The Editor's segment list should be only slightly wider than the addresses it displays.
