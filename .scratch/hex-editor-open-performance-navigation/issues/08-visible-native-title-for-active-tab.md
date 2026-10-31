# 08: Visible native title for the active tab

**What to build:** The actual Windows main-window caption shows the active Editor file or both files in the active Compare tab and updates with document state.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] Editor shows `<basename><dirty marker> - HEX Editor`, including the asterisk for unsaved changes; an empty Editor retains the existing untitled title.
- [x] Compare shows both basenames and the editable base image's dirty marker in the approved title format.
- [x] The caption updates after tab changes, edits, Undo/Redo when saved state changes, Save, Save As, and Open.
- [x] The filename is visibly rendered in the native system title bar, not merely stored in an internal title property.
- [x] Rendered main-window scenarios verify the visible caption across Editor, Compare, dirty/clean transitions, Save As, and an empty Editor.
