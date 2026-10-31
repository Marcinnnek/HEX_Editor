# 01: Open replaces the Editor document

**What to build:** Opening Intel HEX or S-Record replaces the Editor image after the user's save decision. A read failure or cancellation leaves the previous document in place, and opening the same path reloads its disk contents.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] Open presents Save / Don't save / Cancel only when the Editor document has unsaved changes; Cancel or failed Save prevents the new read.
- [x] The selected file is prepared without importing its bytes into the current image, and the previous document remains until the new one has loaded and passed validation.
- [x] A failed or canceled load preserves the previous image and its in-memory edits, including after Don't save; any Save that completed remains reflected in its saved state.
- [x] Opening the current path reads the disk version again and follows the same save decision.
- [x] Successful Open activates the new Editor image with its source path and format; it does not append bytes or create a merge Undo action.
- [x] Public workspace and rendered Open scenarios cover clean, dirty, canceled, failed, and same-path replacement.
