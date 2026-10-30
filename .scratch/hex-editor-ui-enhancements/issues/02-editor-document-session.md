# 02: Give the Editor a stable image document

**What to build:** Opening, editing, undoing, and saving the Editor image work through one document that owns its memory image, file association, metadata, saved state, and history. This prepares the image for use by several tabs without changing current Editor behavior.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] An open image document keeps stable identity when edits replace its in-memory image instance or Save As changes its path.
- [x] Editor open, edit, Undo, Redo, Save, Save As, and close remain usable through the document-level workspace operation.
- [x] Save As updates association only after a successful safe write; a failed or canceled operation leaves path and dirty state unchanged.
- [x] Existing sparse address and format metadata behavior remains intact.
- [x] Add workspace-seam tests for image identity, dirty state, history, and save outcomes; keep existing Editor workflows passing.

## Comments

- Added `EditorDocument` and `EditorDocumentWorkspace` as the Editor's image, history, association, metadata, and saved-state owner. `MainViewModel` delegates existing Editor operations to this workspace.
- Workspace tests cover stable identity through edit/Undo/Redo/Save As, failed and canceled saves, open failure, sparse import metadata, close, and document change notifications. The full test suite passes (100 tests).
