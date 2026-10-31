# 13: Warn before Export to a mismatched extension

**What to build:** Export checks its chosen output format against a known destination extension before writing any exported memory image data.

**Blocked by:** 08 — Warn about a format mismatch on Open.

**Status:** resolved

- [x] Full-image, selected-segment, and address-range exports all use the same format-versus-known-extension warning, while matching or unknown extensions proceed normally.
- [x] The warning identifies the destination, extension-implied format, and chosen output format with Save anyway and Cancel.
- [x] Cancel creates no new file and leaves any existing target unchanged. Save anyway exports the chosen format with the existing export options.
- [x] Public export-workflow and WPF scenarios cover every export variant, both mismatch directions, and cancellation without a partial file.
