# 07: Merge into the Editor image

**What to build:** Treat an open Editor image as the fixed Merge destination, so the user chooses only a source file and reviews a reversible merge against the Editor's current in-memory bytes.

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

- [ ] With an image open in Editor, Merge requests one source file and uses the Editor document as destination even when a Compare tab is active or holds another base image.
- [ ] Unsaved Editor changes participate in the preview and remain in the destination until the user's overlap choice resolves them.
- [ ] The reverse merge direction is unavailable in the fixed-destination flow; existing preview, overlap decisions, and range overrides remain usable.
- [ ] Applying the merge updates the same Editor document and is reversible through Undo.
- [ ] Choosing the Editor document's own path as source reads the saved disk version, allowing a preview against unsaved in-memory edits.
- [ ] With an empty Editor, Merge still asks for two files and allows either direction, including when Compare is the active tab.
- [ ] Public workspace/merge operations and a rendered picker workflow verify both modes, same-path disk loading, destination identity, and Undo.
