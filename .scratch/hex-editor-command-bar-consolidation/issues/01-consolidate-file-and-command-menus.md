# 01: Consolidate file and command menus

Status: resolved

**What to build:** Put Compare images and Merge images directly after Close Image in File, remove their top-level menus, and remove the duplicate top-level Export menu. Keep all export actions in File and preserve command behavior, enabled states, and shortcuts.

**Blocked by:** None.

- [x] File contains Open, Save, Save As, Close Image, Compare images, Merge images, and the three Export commands in the agreed order.
- [x] Top-level Compare, Merge, and Export menus are absent.
- [x] Compare and Merge remain disabled while an Open replacement is loading.
- [x] Menu acceptance tests cover command reachability and order.

## Answer

Implemented in `971fb79`. The relevant menu and loading-state acceptance tests passed in separate WPF test runs.
