# 01: Make Compare double-click safe

**What to build:** Double-clicking either Compare panel never terminates the application. Invalid targets are ignored safely, and the read-only reference panel explains its behavior in the status bar.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] Double-clicking a header, separator, address gap, empty area, or unrealized row causes no edit and no unhandled exception.
- [x] Double-clicking the right reference panel shows a brief read-only status message without a modal dialog.
- [x] A valid left occupied row is recognized without crashing; opening its editor is delivered by ticket 07.
- [x] Recoverable failures include useful operation and address context in diagnostics without logging byte buffers.
- [x] Exercise valid and invalid hit targets through the highest available interaction seam and manually check the WPF event route.

## Comments

- Added public Compare row activation behavior with occupied-byte validation and reference read-only feedback. The WPF preview mouse route handles the second press on both panels, resolves only realized rows and valid byte hits on the base side, and reports failures with side, row index, address, exception type, and HResult. No byte values are logged.
- Four focused tests cover occupied base rows, reference feedback, invalid sparse targets, and HEX separator/outside hit testing. The full MSTest suite passed: 98 tests. Reviewed the WPF event registration, hit-test path, and main status-bar binding in source. Interactive mouse validation remains for UI acceptance.
