# 09: Find bytes in Editor

**What to build:** Add a nonmodal Find bytes workflow for the active Editor image, with HEX and ASCII patterns and repeatable navigation.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [ ] View > Find bytes and Ctrl+F open or focus a single Find window with HEX/ASCII choice, separate drafts, Find Next, Find Previous, and Close.
- [ ] HEX accepts complete byte pairs with or without spaces and ignores letter case; ASCII accepts case-sensitive 7-bit characters and rejects non-ASCII input.
- [ ] Matches span source-record and displayed-row boundaries but never cross an address gap.
- [ ] Next and Previous start beyond the selected byte or at the image boundary, wrap, and select the complete current match in HEX and visible ASCII.
- [ ] F3 and Shift+F3 repeat the last valid search after closing Find; closing removes only the temporary match indicator.
- [ ] Invalid input, no result, and cancellation give inline feedback without moving the selection. Image loads, edits, Undo, Save, and Merge invalidate stale matches.
- [ ] Search remains responsive and cancellable with progress on sizable ordinary images.
