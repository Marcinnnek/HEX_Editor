# 12: Warn before Save As to a mismatched extension

**What to build:** Save As checks the user-selected output format against the chosen destination extension and warns before creating or replacing a mismatched file.

**Blocked by:** 08 — Warn about a format mismatch on Open.

**Status:** resolved

- [x] Known Intel HEX and S-Record extensions use the same mapping as Open; matching and unknown extensions proceed without the mismatch warning.
- [x] A mismatch warning identifies the destination, extension-implied format, and chosen output format with Save anyway and Cancel.
- [x] Cancel leaves the current document and any existing target file unchanged; no partial file is created. Save anyway writes the chosen format and updates the document through the normal Save As workflow.
- [x] WPF and public save-workflow scenarios cover new and existing target paths, both mismatch directions, and cancellation.
