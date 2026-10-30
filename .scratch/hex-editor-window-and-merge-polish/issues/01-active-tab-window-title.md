# 01: Active tab window title

**What to build:** Show the active Editor or Compare image names and editable-image saved state in the main window title, updating immediately as the user switches tabs, edits, saves, or uses Save As.

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

- [ ] Editor shows the current file basename and a `*` for unsaved changes, followed by ` - HEX Editor`; an empty Editor retains its untitled title.
- [ ] Saving clears the marker, and Save As updates the name without requiring a tab switch.
- [ ] Compare shows both filenames as `Compare: fileA.hex ↔ fileB.hex - HEX Editor`, with `*` beside file A when its editable base image is dirty.
- [ ] Switching among Editor and multiple Compare tabs immediately displays the selected tab's title, even when their editable documents differ.
- [ ] Rendered WPF scenarios verify the title through tab changes, edits, save, and Save As without relying on private handlers.
