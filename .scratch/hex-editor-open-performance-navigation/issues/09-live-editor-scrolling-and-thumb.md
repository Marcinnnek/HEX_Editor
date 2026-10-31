# 09: Live Editor scrolling and a usable thumb

**What to build:** Editor data rows respond promptly to the mouse wheel and vertical scrollbar drag, with a thumb large enough to grab on large images.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] Wheel movement updates the visible HEX/ASCII rows continuously without long pauses on representative images up to 100,000,000 occupied bytes.
- [x] Dragging the vertical scrollbar thumb updates rows before mouse release; refresh latency targets roughly 100 ms.
- [x] The vertical thumb remains at least approximately 30 pixels high at ordinary display scaling, including for large images.
- [x] Scrolling remains responsive with ASCII visible and each supported 8/16/32-byte row setting.
- [x] Rendered WPF performance and interaction scenarios measure visible row changes, drag behavior, latency, and thumb size rather than internal virtualization controls.
