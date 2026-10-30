# 05: Close all dirty images safely

**What to build:** When the application closes, ask about every distinct dirty editable memory image rather than only the image in the active tab.

**Blocked by:** 04: Readable save-on-close prompt.

**Status:** resolved

- [x] Application close prompts once per distinct dirty editable document: the Editor document first, then editable base images of Compare tabs.
- [x] Two or more Compare views sharing one base image produce one prompt for that image; read-only reference images do not produce prompts.
- [x] Save persists the chosen document; Don't save allows closing without persisting it; Cancel at any prompt aborts the entire close attempt.
- [x] If a later prompt is canceled, edits in documents previously answered Don't save remain in memory while the application stays open.
- [x] A failed or canceled Save As aborts closing without losing that document's edits.
- [x] Closing a single Compare tab retains the existing rule that its base image is prompted only when the last view closes.
- [x] Public workspace/close-workflow tests cover distinct document identity and order, cancellation, and retained edits after an aborted close; rendered WPF tests cover the save prompt itself.

## Answer

Application close now gets the Editor document followed by the registered Compare base documents, filters to dirty editable documents, and deduplicates by document identity. The close workflow asks in that order and aborts on Cancel or failed Save/Save As; Don't save leaves the in-memory document unchanged. Existing individual Compare-tab close behavior remains in place and still prompts only for its last view. Public workspace/close-workflow tests cover ordering, shared bases, cancellation, and retained edits; rendered WPF tests cover the save prompt itself, including Cancel/X/default actions. A full MainWindow modal close harness is not part of the testing decision because the previous automation attempt hung.
