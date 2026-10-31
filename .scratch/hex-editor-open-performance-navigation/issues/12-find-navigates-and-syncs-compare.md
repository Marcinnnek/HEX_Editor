# 12: Find navigates and synchronizes Compare

**What to build:** A Find result in either Compare image appears visibly in its panel, and the other panel tracks the corresponding address or gap.

**Blocked by:** 11: Find navigates to a visible Editor match.

**Status:** resolved

- [x] Find Next, Find Previous, and F3 navigate the panel that owns the result and place its first match row at the top where possible.
- [x] The result's first HEX cell is horizontally visible and the full sequence is highlighted in HEX and ASCII across rows.
- [x] The opposite panel synchronizes to the corresponding address even when it contains an address gap; no byte is invented in that gap.
- [x] Near the image end, the result is placed as high as existing content allows, without synthetic rows or loss of the current-match marker.
- [x] Public comparison-Find and rendered two-panel scenarios cover results on A and B, sparse gaps, wrapping, and viewport placement.
