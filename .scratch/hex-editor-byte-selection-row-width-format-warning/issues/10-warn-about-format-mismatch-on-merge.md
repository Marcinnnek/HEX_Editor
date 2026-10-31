# 10: Warn about a format mismatch on Merge

**What to build:** A newly loaded Merge input with a misleading known extension requires a user decision before a merge preview or result appears.

**Blocked by:** 08 — Warn about a format mismatch on Open.

**Status:** resolved

- [x] Newly loaded Merge inputs use the same content-versus-known-extension warning as Open, for either input-file flow.
- [x] Open anyway continues with the content-detected format and the chosen merge direction.
- [x] Cancel aborts the whole new Merge operation, creates no preview or partial result, and leaves existing documents and Undo history unchanged.
- [x] A currently open Editor image reused as the destination is not reimported solely to repeat this input warning.
- [x] Public merge-workflow and WPF scenarios cover mismatch, matching/unknown extensions, continue, and Cancel.
