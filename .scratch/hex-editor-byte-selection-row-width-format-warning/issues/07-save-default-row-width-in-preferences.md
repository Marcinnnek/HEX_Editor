# 07: Save the default row width in Preferences

**What to build:** Users can persist a default 1–128-byte display width that new Editor images and Compare views use without altering already-open comparisons.

**Blocked by:** 04 — Use a custom 1–128-byte row width in Editor; 05 — Use a custom 1–128-byte row width in Compare.

**Status:** resolved

- [x] Preferences accepts a default whole-number width from 1 through 128 and retains quick presets 8, 16, and 32; a fresh installation continues to default to 16.
- [x] Save persists the choice and applies it to subsequently opened Editor images and newly created Compare views. Cancel and window close discard staged changes.
- [x] A successful Open applies the saved default even though it reuses the Editor tab; already-open Compare views keep their current width.
- [x] Invalid numeric input cannot be saved or corrupt the existing preference, and the saved value survives application restart.
- [x] Preferences-session and WPF scenarios verify Save/Cancel, a default such as 24, Open, new Compare, and existing Compare behavior.
