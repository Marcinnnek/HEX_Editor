# 15: Edited-byte colors in Editor

**What to build:** Highlight current unsaved occupied-byte changes in Editor and let users choose that color independently for Light and Dark.

**Blocked by:** 14: Configurable comparison colors.

**Status:** resolved

- [ ] An occupied byte is marked edited when its current value differs from the last saved image; newly added occupied bytes are marked too.
- [ ] Save or Undo that restores the saved value removes the marker; a removed address has no green Editor cell.
- [ ] Editor shows edited bytes distinctly from its normal selection and ASCII background.
- [ ] Preferences offers an edited-byte swatch and picker for each theme with the same Save, Cancel, X, and Reset behavior as comparison colors.
- [ ] The edited-byte state is verified through public document behavior rather than a history of touched addresses.
