# 01: Write bounded diagnostics outside the release directory

**What to build:** When a recoverable application error occurs, HEX Editor records a useful diagnostic entry in user data so firmware engineers can investigate failures without exposing firmware contents or requiring a writable executable directory.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] Error entries identify the operation and the affected address or selection when available.
- [x] Diagnostics are stored in the user's local application data and do not require files beside the executable.
- [x] Diagnostic entries omit firmware byte contents, full file paths, routine successes, and telemetry.
- [x] Log rotation retains no more than five files of at most 1 MiB each.
- [x] A missing or unwritable log location does not prevent application startup or memory-image workflows.
- [x] Verify representative recoverable errors produce useful entries and that rotation and privacy limits hold.

## Comments

- Merged on `integration` as `ec9ea30`; the implementer reports all 4 focused diagnostics tests passed.
