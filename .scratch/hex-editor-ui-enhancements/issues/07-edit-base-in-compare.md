# 07: Edit the left base image from Compare

**What to build:** A valid double-click in the left Compare panel opens Edit Row for the shared base image, while the right reference remains read-only. Confirmed edits refresh all views and comparison results.

**Blocked by:** 01: Make Compare double-click safe; 04: Share a base image across Editor and Compare tabs; 06: Edit an occupied Editor row in one transaction.

**Status:** ready-for-human

- [x] A double-click on an occupied left byte or row opens Edit Row with the base filename and absolute address.
- [x] The right panel has no edit action and still shows the ticket 01 status message on double-click.
- [x] Confirmed left edits refresh difference highlighting, counts, segment lists, Editor, and other Compare tabs using that base.
- [x] Cancel or invalid input leaves both images and dirty state unchanged; Undo/Redo works from any view of the base.
- [x] Workspace-seam tests cover valid rows, absent bytes, unloaded rows, and both panels.
- [ ] WPF interaction checks cover valid rows, absent bytes, unloaded rows, and both panels.

## Comments

- Merged Compare editing on `integration` as `781b204`; 34 focused Compare/workspace tests passed. Live WPF interaction review remains outstanding.
