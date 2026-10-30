# 06: Compact HEX and ASCII row spacing

**What to build:** Make the minimum vertical-spacing preference produce genuinely dense HEX/ASCII rows in Editor and Compare while preserving readable navigation and byte selection.

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

- [ ] At vertical spacing 0, consecutive rendered HEX/ASCII text lines leave approximately 0–1 visible pixel at normal display scaling in Editor and both Compare panels.
- [ ] Larger spacing values increase the rendered gap progressively; horizontal byte spacing remains independent.
- [ ] Corresponding Compare rows stay vertically aligned, and clicks in HEX and ASCII target the intended occupied byte across compact and expanded settings.
- [ ] Segment/Block list spacing is unaffected by this preference.
- [ ] Rendered WPF scenarios measure the absolute zero-setting gap and alignment; existing tests expecting a 0.2-character-height baseline are updated to the confirmed target.
