# 16: Layer edits and differences in Compare

**What to build:** Show an editable base byte's unsaved state and its comparison difference at the same time, using the configured appearance colors.

**Blocked by:** 15: Edited-byte colors in Editor.

**Status:** resolved

- [ ] A base byte that differs from the reference keeps its difference background and gains the edited-byte outline when it is also edited.
- [ ] Unequal, A-only, and B-only states continue to use their distinct configured colors in both themes.
- [ ] Save or Undo removes the edited outline when the base byte matches the saved image, without hiding a remaining comparison difference.
- [ ] Removed addresses have no invented Editor byte cell; the comparison still represents one-sided occupancy correctly.
- [ ] The reference image remains read-only and does not acquire an edited-byte state from base-image changes.
