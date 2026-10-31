# 02: Copy selected bytes to the system clipboard

**What to build:** Ctrl+C copies the selected byte or byte range as plain-text uppercase hex values separated by spaces.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] Ctrl+C on one selected byte places exactly its two uppercase hex digits on the system clipboard, with no extra spaces.
- [x] Ctrl+C on a Shift+click range copies occupied byte values in ascending address order, with exactly one ASCII space between values.
- [x] A range crossing an address gap omits unoccupied addresses and inserts no placeholders or line breaks.
- [x] The output contains no addresses or ASCII rendering.
- [x] In Compare, Ctrl+C copies from the panel that owns the active selection; selecting or copying in one panel does not change the other panel's selection or comparison indicators.
- [x] Copying works in the Editor's normal and windowed layouts.
- [x] Existing Copy range and Paste editing behavior remains intact and distinct from system clipboard text copying.
- [x] WPF acceptance tests verify clipboard contents for one-byte and multi-byte selections, including a range crossing an address gap.
