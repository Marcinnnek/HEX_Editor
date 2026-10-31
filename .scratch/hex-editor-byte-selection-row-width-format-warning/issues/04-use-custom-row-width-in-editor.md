# 04: Use a custom 1–128-byte row width in Editor

**What to build:** Editor accepts every whole-number display width from 1 through 128 while keeping the current address, byte or range selection, navigation, and editing usable.

**Blocked by:** 03 — Show the Shift-selected byte range.

**Status:** resolved

- [ ] The Editor width control keeps presets 8, 16, and 32 and accepts decimal digits for any value 1–128. Invalid, out-of-range, or pasted nonnumeric input is not applied; empty text restores the last valid width on Enter or focus loss.
- [ ] Display rows begin at global multiples of the chosen width, preserve address gaps, and display a partial final row through `0xFFFFFFFF` without overflow.
- [ ] A width change preserves the current address and visible byte or range selection and brings its start into view without an unrelated scroll jump.
- [ ] The main HEX and ASCII grids remain one line per display row, recalculate column widths, and scroll horizontally when necessary in normal and large-image layouts.
- [ ] Edit Row can open and edit the occupied bytes of a custom-width row, including widths above 32; its final grouped presentation is covered by ticket 06.
- [ ] Public model/workspace tests and WPF scenarios cover widths 1, 3, 24, 80, and 128 alongside the existing presets.
