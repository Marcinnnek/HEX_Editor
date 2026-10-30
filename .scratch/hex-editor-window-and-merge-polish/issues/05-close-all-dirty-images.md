# 05: Close all dirty images safely

**What to build:** When the application closes, ask about every distinct dirty editable memory image rather than only the image in the active tab.

**Blocked by:** 04: Readable save-on-close prompt.

**Status:** ready-for-agent

- [ ] Application close prompts once per distinct dirty editable document: the Editor document first, then editable base images of Compare tabs.
- [ ] Two or more Compare views sharing one base image produce one prompt for that image; read-only reference images do not produce prompts.
- [ ] Save persists the chosen document; Don't save allows closing without persisting it; Cancel at any prompt aborts the entire close attempt.
- [ ] If a later prompt is canceled, edits in documents previously answered Don't save remain in memory while the application stays open.
- [ ] A failed or canceled Save As aborts closing without losing that document's edits.
- [ ] Closing a single Compare tab retains the existing rule that its base image is prompted only when the last view closes.
- [ ] Workspace-level and rendered WPF scenarios verify prompt order, document identity, cancellation, and shared-view behavior.
