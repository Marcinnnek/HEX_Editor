# 06: Edit memory images and save changes

**What to build:** Make reversible edits to an image by absolute address, then save the modified contents safely while clearly protecting unsaved work.

**Blocked by:**
- 03: Export Intel HEX and S-Record images.
- 04: Virtualized single-image editor view.

**Status:** resolved

- [x] Edit individual byte values in hexadecimal form.
- [x] Select byte ranges and support Cut, Copy, Paste, and Delete/Remove; internal copy/paste preserves sparse offsets and holes.
- [x] Delete occupancy without shifting later addresses and support filling a selected range with a byte or repeating pattern.
- [x] Insert or replace bytes at explicit addresses with an explicit overlap decision.
- [x] Support undo and redo for data-changing edit operations and reflect modified state in the UI.
- [x] Save to the associated path when format and path are known, and Save As to a selected path and format.
- [x] Use a temporary-file and safe-replacement strategy; never silently overwrite an existing Save As target.
- [x] Prompt before discarding unsaved changes when closing an image or the application; a failed save must preserve in-memory changes and the previous file.

## Comments

- Added sparse segment editing for byte replacement, insertion, patterned fill, occupancy deletion, and undo/redo. Cut, Copy, and Paste keep segment offsets and holes; insertion and paste require an explicit choice when occupied addresses are involved.
- Added Save and Save As with format selection. Saves export to a same-directory temporary file, flush it to disk, then move it into place. Save As asks before replacing an existing target. Failed exports leave the original file, current image, and association unchanged.
- Added visible modified state and Save/Discard/Cancel prompts for Close Image and application close. Imports merged over an existing image remain marked modified until saved.
- Verification: `dotnet test tests/HexEditor.Tests/HexEditor.Tests.csproj --no-restore -m:1 -nodeReuse:false` passed (22 tests); `dotnet build HexEditor.sln --no-restore -m:1 -nodeReuse:false` passed with 0 warnings and 0 errors.
