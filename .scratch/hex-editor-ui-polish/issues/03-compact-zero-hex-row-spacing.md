# 03: Make zero vertical HEX spacing genuinely compact

**What to build:** Vertical spacing 0 produces closely spaced, readable HEX/ASCII data rows in Editor and both Compare panels; larger saved values add visible separation without shifting byte hit targets.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] At vertical spacing 0, the visible gap between consecutive data text lines is approximately 0.2 of the rendered character height, rather than the current roughly 1.5-character gap.
- [x] Editor and both Compare panels use the same row-density baseline, and corresponding Compare addresses remain vertically aligned.
- [x] Increasing vertical spacing increases the visible row gap monotonically; changing vertical spacing does not alter horizontal byte spacing.
- [x] Already open views apply the saved choice, and the choice survives application restart.
- [x] HEX and ASCII clicks still resolve to the intended occupied byte at compact and expanded settings; row gaps, headers, and unoccupied addresses are not treated as bytes.
- [x] Sparse-row virtualization, scrolling, selection, and comparison navigation remain responsive.
- [x] Verify settings through public behavior and measure the absolute rendered gap relative to character height in WPF, alongside alignment and hit testing.

## Answer

Implemented in `a9f2846`: vertical spacing 0 uses a 0.2-character-height baseline and larger settings add separation while byte hit areas remain limited to rendered text. Editor and both Compare views share the calculation. The final full suite passed 167/167.
