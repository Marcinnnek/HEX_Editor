# 05: Save and close the active image across tabs

**What to build:** Save and Save As target the active tab's base image. Closing one tab retains a shared image; Close Image closes every view of that image after one decision about unsaved changes.

**Blocked by:** 04: Share a base image across Editor and Compare tabs.

**Status:** resolved

- [x] Save or Save As from Compare writes its left base, never the right reference; successful Save As updates displayed base filenames and association.
- [x] A tab close button closes only that view; a modified image stays available while another view uses it.
- [x] Closing the last view of a modified image offers save, discard, or cancel exactly once.
- [x] File > Close Image closes all views of the active base after one unsaved-change decision and leaves unrelated documents open.
- [x] Workspace-seam tests cover successful and canceled save/close flows, including more than one Compare tab and a different active Editor image.

## Comments

- Merged on `integration` as `e442600`. The implementer reports the focused `EditorDocumentWorkspaceTests` passed after merging the current integration tip.
