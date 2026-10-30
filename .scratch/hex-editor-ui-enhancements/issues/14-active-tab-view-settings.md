# 14: Synchronize view and navigation settings with the active tab

**What to build:** Each Editor or Compare tab keeps its own bytes-per-row and Show ASCII choices. The primary View/Navigate controls show and change the active tab's settings, while Compare's local controls stay synchronized with them.

**Blocked by:** 04: Share a base image across Editor and Compare tabs; 13: Rebuild the Editor and Compare workspace layout.

**Status:** ready-for-agent

- [ ] Switching tabs restores each tab's independent 8/16/32 bytes-per-row and ASCII visibility settings without modifying another tab.
- [ ] The primary toolbar reflects the selected tab; changing either primary or Compare-local controls updates the same active tab state.
- [ ] Go to address acts on the active tab; in Compare, both panels remain aligned at the same absolute address.
- [ ] Changing row width, ASCII visibility, range filter, or tab selection preserves meaningful address and scroll position where possible.
- [ ] Public workspace/view-model tests cover tab switching, two-way setting changes, address navigation, and aligned sparse rows; inspect the WPF bindings.
