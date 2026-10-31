# 11: Warn before Save to a mismatched extension

**What to build:** Every ordinary Save of a document whose known filename extension conflicts with its detected source format requires Save anyway or Cancel before changing the file.

**Blocked by:** 08 — Warn about a format mismatch on Open.

**Status:** resolved

- [x] Save compares the document's detected source format with its current known extension on every attempt, including after Open anyway.
- [x] The warning names the destination and formats and offers Save anyway and Cancel. Save anyway writes the detected source format to the current path.
- [x] Cancel leaves the document modified, the existing file unchanged, and the operation available to retry; a later Save warns again.
- [x] Matching or unknown extensions save through the existing workflow without a mismatch warning.
- [x] Public document/workspace and WPF scenarios verify both mismatch directions, repeated Save, and file integrity on Cancel.
