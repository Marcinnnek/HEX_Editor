# 01: Active tab window title

**What to build:** Show the active Editor or Compare image names and editable-image saved state in the main window title, updating immediately as the user switches tabs, edits, saves, or uses Save As.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] Editor shows the current file basename and a `*` for unsaved changes, followed by ` - HEX Editor`; an empty Editor retains its untitled title.
- [x] Saving clears the marker, and Save As updates the name without requiring a tab switch.
- [x] Compare shows both filenames as `Compare: fileA.hex ↔ fileB.hex - HEX Editor`, with `*` beside file A when its editable base image is dirty.
- [x] Switching among Editor and multiple Compare tabs immediately displays the selected tab's title, even when their editable documents differ.
- [x] Rendered WPF scenarios verify the title through tab changes, edits, save, and Save As without relying on private handlers.

## Answer

Implemented active-tab window titles for Editor and Compare views. The title follows Editor or the selected Compare workspace, marks the active editable document as dirty, and refreshes on edits, saves, Save As, and Compare file A renames. Added a rendered WPF acceptance scenario covering the untitled title, Editor and Compare states, multiple Compare tabs, tab switching, edits, Save, and Save As.

Verified with `dotnet test tests/HexEditor.Tests/HexEditor.Tests.csproj --no-restore --filter FullyQualifiedName~ActiveWindowTitleAcceptanceTests` and `dotnet test tests/HexEditor.Tests/HexEditor.Tests.csproj --no-restore --filter FullyQualifiedName~CompareViewModelTests`.
