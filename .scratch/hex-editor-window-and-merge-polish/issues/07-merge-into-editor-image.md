# 07: Merge into the Editor image

**What to build:** Treat an open Editor image as the fixed Merge destination, so the user chooses only a source file and reviews a reversible merge against the Editor's current in-memory bytes.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] With an image open in Editor, Merge requests one source file and uses the Editor document as destination even when a Compare tab is active or holds another base image.
- [x] Unsaved Editor changes participate in the preview and remain in the destination until the user's overlap choice resolves them.
- [x] The reverse merge direction is unavailable in the fixed-destination flow; existing preview, overlap decisions, and range overrides remain usable.
- [x] Applying the merge updates the same Editor document and is reversible through Undo.
- [x] Choosing the Editor document's own path as source reads the saved disk version, allowing a preview against unsaved in-memory edits.
- [x] With an empty Editor, Merge still asks for two files and allows either direction, including when Compare is the active tab.
- [x] Public workspace/merge operations and rendered WPF and loader acceptance tests cover fixed destination, same-path disk loading, destination identity, and Undo.

## Answer

Merge now uses the Editor document as a fixed in-memory destination whenever it contains bytes, and loads the chosen source directly from disk. The empty-Editor path continues to load two disk images and exposes both directions. The merge dialog keeps its existing overlap preview and range overrides, while fixed-destination mode removes the direction selector. Acceptance coverage exercises the rendered fixed-destination dialog, unsaved bytes, destination identity, Undo, and matching-path disk loading.

## Comments

- Automated test execution is currently blocked because MSBuild cannot access `C:\Users\Marcin\AppData\Local\Microsoft SDKs` in this environment.
