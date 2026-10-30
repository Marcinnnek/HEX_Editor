# 02: Give the Editor a stable image document

**What to build:** Opening, editing, undoing, and saving the Editor image work through one document that owns its memory image, file association, metadata, saved state, and history. This prepares the image for use by several tabs without changing current Editor behavior.

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

- [ ] An open image document keeps stable identity when edits replace its in-memory image instance or Save As changes its path.
- [ ] Editor open, edit, Undo, Redo, Save, Save As, and close remain usable through the document-level workspace operation.
- [ ] Save As updates association only after a successful safe write; a failed or canceled operation leaves path and dirty state unchanged.
- [ ] Existing sparse address and format metadata behavior remains intact.
- [ ] Add workspace-seam tests for image identity, dirty state, history, and save outcomes; keep existing Editor workflows passing.
