# 06: Compact HEX and ASCII row spacing

**What to build:** Make the minimum vertical-spacing preference produce genuinely dense HEX/ASCII rows in Editor and Compare while preserving readable navigation and byte selection.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] At vertical spacing 0, consecutive rendered HEX/ASCII text lines leave approximately 0–1 visible pixel at normal display scaling in Editor and both Compare panels.
- [x] Larger spacing values increase the rendered gap progressively; horizontal byte spacing remains independent.
- [x] Corresponding Compare rows stay vertically aligned, and clicks in HEX and ASCII target the intended occupied byte across compact and expanded settings.
- [x] Segment/Block list spacing is unaffected by this preference.
- [x] Rendered WPF scenarios measure the absolute zero-setting gap and alignment; existing tests expecting a 0.2-character-height baseline are updated to the confirmed target.

## Answer

Changed the shared byte-grid row baseline from `1.2 * LineHeight` to `LineHeight`, so vertical spacing 0 renders a contiguous text baseline and higher settings add their configured gap. Byte highlight/hit bounds and horizontal byte spacing are unchanged. Updated rendered spacing, row alignment, horizontal independence, and Compare hit-testing coverage.

Focused verification passed: `GridSpacingSettingsTests`, `CompareRowActivationTests`, and `CompareMouseClickAcceptanceTests` (17 tests).
