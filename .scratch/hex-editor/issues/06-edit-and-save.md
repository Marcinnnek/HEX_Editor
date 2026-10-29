# 06: Edit memory images and save changes

**What to build:** Make reversible edits to an image by absolute address, then save the modified contents safely while clearly protecting unsaved work.

**Blocked by:**
- 03: Export Intel HEX and S-Record images.
- 04: Virtualized single-image editor view.

**Status:** ready-for-agent

- [ ] Edit individual byte values in hexadecimal form.
- [ ] Select byte ranges and support Cut, Copy, Paste, and Delete/Remove; internal copy/paste preserves sparse offsets and holes.
- [ ] Delete occupancy without shifting later addresses and support filling a selected range with a byte or repeating pattern.
- [ ] Insert or replace bytes at explicit addresses with an explicit overlap decision.
- [ ] Support undo and redo for data-changing edit operations and reflect modified state in the UI.
- [ ] Save to the associated path when format and path are known, and Save As to a selected path and format.
- [ ] Use a temporary-file and safe-replacement strategy; never silently overwrite an existing Save As target.
- [ ] Prompt before discarding unsaved changes when closing an image or the application; a failed save must preserve in-memory changes and the previous file.
