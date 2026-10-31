# 09: Warn about a format mismatch on Compare

**What to build:** A newly loaded Compare input with a misleading known extension requires a user decision before a comparison is created.

**Blocked by:** 08 — Warn about a format mismatch on Open.

**Status:** resolved

- [x] Each newly loaded Compare input is checked against its content-detected format using the same known-extension rules and warning language as Open.
- [x] Open anyway creates the comparison from the detected format without changing the data or reference/base roles.
- [x] Cancel aborts the entire new Compare operation, creates no tab, and leaves existing Editor and Compare documents unchanged.
- [x] An already-open image reused as a base is not needlessly reimported; matching or unknown extensions continue without a warning.
- [x] Public workflow and WPF scenarios cover mismatched inputs and cancellation in the current-image and two-file Compare flows.
