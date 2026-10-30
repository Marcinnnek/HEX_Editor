# 04: Readable save-on-close prompt

**What to build:** Make the save-on-close question identify the affected image and present clear, theme-consistent actions in a properly sized dialog.

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

- [ ] The message names the dirty file, or identifies an untitled image, and includes a subtle warning icon that remains readable in Light and Dark themes.
- [ ] `Save`, `Don't save`, and `Cancel` appear in one aligned row without clipping or overlap at normal and high display scaling.
- [ ] Enter activates Save; Cancel, Escape, and the window X leave the image open with its edits intact.
- [ ] Save invokes the existing Save As flow for an untitled image, and a canceled or failed save prevents closing.
- [ ] Rendered WPF scenarios verify layout, the displayed image name, keyboard/default actions, X/Cancel behavior, and both themes.
