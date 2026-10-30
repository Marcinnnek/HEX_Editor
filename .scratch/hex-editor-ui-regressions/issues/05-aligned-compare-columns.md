# 05: Align each Compare memory-segment list with its data

**What to build:** Compare presents two usable file columns, each with that file's memory-segment list directly above its HEX/ASCII data instead of clustering both lists in one half of the workspace.

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

- [ ] The base image's memory-segment list is immediately above the base data panel, and the reference image's list is immediately above the reference data panel.
- [ ] Each list uses its corresponding column's available width at normal and narrow window sizes.
- [ ] Resizing the window or moving the divider keeps each list aligned with its own data panel.
- [ ] Segment selection and absolute-address navigation remain usable on both sides.
- [ ] Compare's settings, aligned rows, and difference-result area remain visible and usable.
- [ ] Inspect the layout and interactions in WPF at normal and narrow sizes, retaining existing public comparison/navigation tests as regression coverage.
