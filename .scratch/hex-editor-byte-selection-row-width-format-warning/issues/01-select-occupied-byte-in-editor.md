# 01: Select an occupied byte in Editor

**What to build:** Clicking an occupied HEX or ASCII byte cell in Editor visibly selects that byte and updates the current address. Address and outer-inset clicks continue to select the whole row.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] A click anywhere inside an occupied byte's grid cell selects its address, even outside the rendered glyphs; adjacent HEX spacing follows the existing midpoint rule.
- [x] The selected byte has a distinct visible outline in both HEX and ASCII in the normal and large-image views.
- [x] Clicking a byte clears the previous whole-row highlight. Clicking Address or an outer column inset continues to select the row and clears byte selection.
- [x] Clicking an unoccupied address or a vertical gap between rows does not select a byte or imply that data exists there.
- [x] Existing byte navigation and double-click editing continue to work, with focused WPF acceptance coverage of the hit targets and selected state.
