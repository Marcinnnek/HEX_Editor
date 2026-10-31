# 04: HEX inter-byte hit targets

**What to build:** Users can select the intended HEX byte by clicking in the whitespace adjacent to its glyph, including the space between neighboring bytes, without accidentally selecting a byte from the column's outer inset.

**Blocked by:** 02: HEX Data column fits row capacity.

**Status:** resolved

- [x] The midpoint of each inter-byte space divides the hit area: its left half selects the left byte, and its right half selects the right byte.
- [x] A click just outside a byte glyph but inside that byte's grid cell selects it; an outer column inset remains outside all byte hit targets.
- [x] Selection remains accurate at 8, 16, and 32 bytes per row and at the configured horizontal byte-spacing values.
- [x] Editor and both Compare panels have the same hit behavior in DataGrid and windowed views, without an unintended viewport jump or change to double-click editing.
- [x] Existing byte-grid geometry tests cover midpoint boundaries, and WPF acceptance checks a routed click in each view mode.
