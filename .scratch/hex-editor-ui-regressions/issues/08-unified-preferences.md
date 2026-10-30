# 08: Put appearance and grid spacing in one Preferences dialog

**What to build:** Preferences contains Follow Windows, Light, Dark, and both grid-spacing settings in a consistently styled window. Save commits the staged choices; Cancel or the visible standard close control discards them. The separate Appearance menu is removed.

**Blocked by:** 03: Make vertical HEX row spacing visibly effective; 06: Make Follow Windows react without restart.

**Status:** ready-for-agent

- [ ] The main menu offers Preferences and no longer shows a separate Appearance menu or duplicate appearance commands.
- [ ] Preferences shows the current appearance mode and independent horizontal byte and vertical row spacing controls.
- [ ] The dialog has a visible standard Windows title-bar close control, a Cancel action, and styling consistent with the current appearance.
- [ ] Adjusting controls stages choices; Cancel or X leaves active and persisted appearance and spacing unchanged.
- [ ] Save applies and persists the staged choices together to open views; reopening Preferences shows the saved values.
- [ ] A new profile keeps the established default spacing and Follow Windows behavior, and a saved manual appearance override survives restart.
- [ ] Exercise settings persistence through public operations and inspect Save, Cancel, X, Light, Dark, and Follow Windows in WPF.
