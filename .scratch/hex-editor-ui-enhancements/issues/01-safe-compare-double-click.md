# 01: Make Compare double-click safe

**What to build:** Double-clicking either Compare panel never terminates the application. Invalid targets are ignored safely, and the read-only reference panel explains its behavior in the status bar.

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

- [ ] Double-clicking a header, separator, address gap, empty area, or unrealized row causes no edit and no unhandled exception.
- [ ] Double-clicking the right reference panel shows a brief read-only status message without a modal dialog.
- [ ] A valid left occupied row is recognized without crashing; opening its editor is delivered by ticket 07.
- [ ] Recoverable failures include useful operation and address context in diagnostics without logging byte buffers.
- [ ] Exercise valid and invalid hit targets through the highest available interaction seam and manually check the WPF event route.
