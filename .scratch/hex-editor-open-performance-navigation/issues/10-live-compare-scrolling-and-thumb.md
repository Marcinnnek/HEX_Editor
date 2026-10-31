# 10: Live Compare scrolling and a usable thumb

**What to build:** Both Compare data panels provide the same live wheel and vertical-thumb navigation as Editor while preserving synchronized comparison behavior.

**Blocked by:** 09: Live Editor scrolling and a usable thumb.

**Status:** resolved

- [x] Wheel movement and thumb dragging update visible rows continuously in both panels, including before drag release.
- [x] Each panel remains responsive with up to 100,000,000 occupied bytes per image, ASCII visible, and row widths 8, 16, and 32.
- [x] Visible row refresh targets roughly 100 ms and each vertical thumb remains at least approximately 30 pixels high at ordinary scaling.
- [x] Existing address alignment and synchronization continue to work when either panel is scrolled.
- [x] Rendered WPF scenarios exercise both sides and measure visible updates and thumb size.
