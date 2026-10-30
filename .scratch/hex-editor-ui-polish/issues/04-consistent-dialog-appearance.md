# 04: Keep dialogs consistent through appearance changes

**What to build:** Preferences and other dialogs use the current effective Light or Dark appearance, including after repeated manual changes and live Follow Windows updates.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [ ] After Dark → Light → Dark, reopening Preferences shows a coherent Dark background and controls without a pale or washed-out overlay.
- [ ] A saved Light choice yields Light dialogs; a saved Dark choice yields Dark dialogs; Follow Windows follows the current operating-system appearance.
- [ ] Preferences text, radio buttons, sliders, inputs, buttons, disabled states, and focus indicators remain legible and stylistically consistent in both themes.
- [ ] Other existing dialogs opened after a theme change use the effective theme, including their content and controls.
- [ ] A live Windows appearance change updates dialogs already open while Follow Windows is active, without requiring an application restart or leaving stale colors.
- [ ] Repeated switching does not alter appearance persistence, Preferences Save/Cancel/X behavior, editing state, or memory-image data.
- [ ] Verify appearance state through public settings/session behavior and inspect representative rendered WPF dialogs through the Dark → Light → Dark and Follow Windows sequences.

## Answer

Implemented in `12e1c6c`: loaded and open windows bind to the effective application palette, including repeated Dark/Light changes and Follow Windows. A rendered WPF Preferences scenario verifies Dark → Light → Dark and updates while open. The focused scenario passed 1/1 and the final full suite passed 167/167. Visual acceptance ran offscreen; no high-DPI screenshot was captured.
