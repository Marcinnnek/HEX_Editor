# 01: Keep single clicks in both Compare panels safe

**What to build:** A single click on data in either Compare panel selects the intended address without freezing or closing the application. The panels remain aligned, and established double-click and difference-navigation actions still work.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [ ] A single click on an occupied data row or byte in the base panel responds without a hang or application exit.
- [ ] The same single-click interaction is safe in the read-only reference panel.
- [ ] Selection and scrolling stay aligned by absolute address without repeated full-image work or a feedback loop between panels.
- [ ] The reported `CMSIS-DAP-STLINK21.hex` and `STM32-DEMO.hex` comparison stays open and responsive after clicks in both panels; source files are not edited or saved during verification.
- [ ] Left-panel double-click editing, right-panel read-only feedback, and Previous/Next Difference remain usable after a single click.
- [ ] A recoverable interaction error reports the operation, panel, and address or selection when available in status and error diagnostics; open documents and unsaved edits survive.
- [ ] Cover observable selection and navigation through existing public workspace/model operations and reproduce the click in a WPF acceptance scenario.

## Answer

Compare handles single clicks, selection synchronization, scrolling, double clicks, and difference navigation through guarded event paths. WPF click tests cover both panels, editing and read-only feedback, and failure reporting. A one-off WPF test loaded read-only copies of the reported firmware pair and completed address-row clicks in both panels without changing either file. The desktop automation tool could not complete physical byte clicks against its temporary test window; this remains a manual acceptance check.
