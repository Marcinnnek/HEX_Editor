# 11: Find navigates to a visible Editor match

**What to build:** A successful Editor Find Next, Find Previous, or F3 puts the found byte sequence visibly in the data grid instead of only reporting its address.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] The first match row becomes the first visible row when possible; near the image end it is placed as high as real content allows without artificial blank rows.
- [x] At least the starting HEX byte is horizontally visible, including for a match beyond the previous horizontal viewport.
- [x] The entire current byte sequence remains highlighted in HEX and ASCII across row boundaries, including when it extends beyond the viewport.
- [x] Next, Previous, F3, and wrapped searches all navigate consistently while preserving existing pattern parsing and search order.
- [x] Public Find-workflow and rendered Editor scenarios verify the selected match, viewport placement, horizontal visibility, and highlighting.
