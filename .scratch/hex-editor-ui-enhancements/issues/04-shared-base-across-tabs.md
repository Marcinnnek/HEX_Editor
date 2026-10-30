# 04: Share a base image across Editor and Compare tabs

**What to build:** Several Compare tabs can use one base document while each holds a different read-only reference. Editing, Undo, and Redo of the base update all its views; opening another Editor file preserves existing Compare tabs and their unsaved work.

**Blocked by:** 03: Compare the current base image with one new file.

**Status:** resolved

- [x] Compare from an active Compare tab opens a new tab using the same base document and asks only for a new reference file.
- [x] Every Editor or Compare view of that base shows edited bytes and refreshed comparison results without reloading from disk.
- [x] All views of one base share one Undo/Redo history; distinct base documents keep separate histories.
- [x] Opening another file in Editor leaves prior Compare tabs, reference images, base bytes, and dirty state intact.
- [x] Workspace-seam tests cover multiple references, an Editor switch, edits, Undo/Redo, and sparse difference updates.

## Comments

- Merged on `integration` as `fc614be` (`Share base documents across compare tabs`). The implementer reports the full suite passed: 112 tests.
