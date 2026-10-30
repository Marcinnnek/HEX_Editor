# 04: Readable save-on-close prompt

**What to build:** Make the save-on-close question identify the affected image and present clear, theme-consistent actions in a properly sized dialog.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] The message names the dirty file, or identifies an untitled image, and includes a subtle warning icon that remains readable in Light and Dark themes.
- [x] `Save`, `Don't save`, and `Cancel` appear in one aligned row without clipping or overlap at normal and high display scaling.
- [x] Enter activates Save; Cancel, Escape, and the window X leave the image open with its edits intact.
- [x] Save invokes the existing Save As flow for an untitled image, and a canceled or failed save prevents closing.
- [x] Rendered WPF scenarios verify layout, the displayed image name, keyboard/default actions, X/Cancel behavior, and both themes.

## Answer

Implemented a named, theme-aware save prompt with a warning icon and aligned Save, Don't save, and Cancel actions. Save is the default; Escape and closing with X return Cancel. The existing close workflow still invokes Save As for untitled images and prevents closing when save is canceled or fails. Rendered WPF acceptance scenarios pass in Light and Dark themes and cover the image name, action row, keyboard defaults, Escape, and X.
