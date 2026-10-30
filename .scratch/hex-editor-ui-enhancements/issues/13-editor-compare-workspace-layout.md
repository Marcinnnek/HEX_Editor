# 13: Rebuild the Editor and Compare workspace layout

**What to build:** The Editor shows its memory-segment list above a full-width HEX table. Compare shows two aligned panels, each with its segment list and HEX table, plus the settings row and result area from the agreed visual reference in WPF UI style.

**Blocked by:** 12: Apply a consistent WPF UI theme.

**Status:** ready-for-agent

- [ ] Editor uses the agreed vertical memory-segment list and full-width Address, HEX Data, ASCII layout.
- [ ] Compare retains two side-by-side panels with block lists above aligned HEX grids and a visible difference-result area.
- [ ] Segment selection and absolute-address navigation remain usable in both workspaces.
- [ ] Missing bytes stay visibly absent and distinct from stored 00/FF; row and byte alignment survive resizing and ASCII visibility changes.
- [ ] Virtualization remains active and sparse address gaps do not create dense rows or excessive visual elements.
- [ ] Inspect layout at normal and narrow sizes and exercise existing view-model behavior for alignment and navigation.
