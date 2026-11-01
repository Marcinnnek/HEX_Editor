# Mouse Range Selection, Contextual Copy, File Opening, Scrolling, and Responsive Save

Status: resolved

## Problem Statement

Byte ranges can currently be selected with Shift+click but not by dragging the mouse. Ctrl+C always copies HEX tokens, even if the user selected a byte in the ASCII column. Files dropped from Explorer are ignored, and a file path supplied when Windows launches HexEditor through a file association is ignored. A vertical scrollbar arrow moves three rows rather than one in Editor and Compare. Save As can leave the window frozen after an approximately 10 MB Intel HEX file appears to have been written. Ordinary Save and both output formats also need verification.

## Solution

Add mouse-drag range selection to the shared byte grid in Editor and both Compare panels. Preserve the existing address-based selection and sparse-image behavior. Track the column where a selection begins so Ctrl+C copies uppercase HEX tokens from HEX or the displayed characters from ASCII. Open a single Explorer-dropped file or startup file path through the existing Open workflow. Make each vertical scrollbar arrow press move exactly one display row. Reproduce and fix the Save As freeze, and make every Save and Save As entry point remain responsive through completion or cancellation.

## User Stories

1. As an Editor user, I can select occupied bytes across multiple rows by pressing on a byte and dragging, so I can select a range without Shift+click.
2. As a Compare user, I can drag-select within either panel, so I can copy from the intended image without selecting the other panel.
3. As a user selecting in HEX, I can press Ctrl+C to receive uppercase `XX` tokens separated by single spaces, so I can paste byte values elsewhere.
4. As a user selecting in ASCII, I can press Ctrl+C to receive the characters shown in that column, so I can paste readable text elsewhere.
5. As a user dragging beyond the top or bottom of the visible grid, I can keep extending the selection while the grid scrolls, so long ranges remain practical.
6. As a user dropping one firmware file from Explorer onto the main window, I can open it using the same safety and error checks as Open, regardless of the active tab.
7. As a user launching HexEditor through a Windows file association, I can see the supplied file opened, rather than an empty window.
8. As a user pressing a vertical scrollbar arrow in Editor or Compare, I move exactly one display row per press.
9. As a user saving an approximately 10 MB image, I can see progress, cancel safely, and continue using the window after completion for Save As and Save in either supported format.
10. As a user choosing Save in an unsaved-changes prompt during close or Open, I receive the same responsive save behavior as the main Save command.

## Interaction Decisions

- A drag begins only when the initial mouse press hits an occupied byte in the HEX or ASCII column. Plain click and Shift+click retain their existing selection gestures. Address-column interaction retains row selection.
- Selection endpoints are absolute occupied-byte addresses. Dragging may cross display rows and address gaps. Only occupied cells within the inclusive address range are outlined; no value is synthesized in a gap.
- When the pointer passes over a blank address, margin, or the other Compare panel, retain the last valid endpoint. Resume extending or shrinking the range on reaching an occupied byte in the originating panel. Releasing the mouse ends the drag and leaves the selection visible.
- Dragging beyond the visible top or bottom of the originating grid auto-scrolls that grid and updates the range as occupied bytes become reachable. Stop auto-scrolling when the drag ends.
- In Compare, selection and copying belong to the panel where selection began. The other panel keeps its own difference highlighting. The same interaction applies to normal and windowed large-image views.
- The column where selection starts fixes the Ctrl+C representation, including when a drag or Shift+click crosses into the other column. An initial click in HEX chooses HEX; an initial click in ASCII chooses ASCII.
- HEX copying preserves the existing uppercase, two-digit token format with one ASCII space between occupied bytes. ASCII copying emits exactly the column's displayed character for each occupied byte: `0x20` through `0x7E` as characters, all other byte values as `.`. Copy occupied bytes in ascending address order, omit gaps, and add no row breaks or placeholders.
- System clipboard text copying remains separate from the existing internal sparse-image Copy range and Paste editing operations.

## File Opening and Scrolling Decisions

- Drop one file from Windows Explorer anywhere on the main window to invoke the same document-replacement workflow as Open. This also applies while a Compare tab is active. Preserve the existing unsaved-changes decision, content-based format detection, extension mismatch warning, progress, cancellation, and error handling.
- A single path passed at process startup invokes that same Open workflow. This allows a user-configured Windows file association to open its target file. Paths containing spaces or non-ASCII characters must work.
- If a drop or launch supplies multiple paths, show a clear message and open none. A directory or invalid file must not silently replace a document.
- Each press of the vertical scrollbar's up or down arrow moves one display row in Editor and both Compare panels. Mouse-wheel and scrollbar-track/page behavior remain unchanged.

## Save Reliability Decisions

- Reproduce the reported case: Save As of an approximately 10 MB Intel HEX file appears to write the destination but leaves the window frozen. Determine whether the freeze occurs during writing, flushing, progress dispatch, or post-save UI work before changing the implementation.
- Verify Save As and Save with approximately 10 MB Intel HEX and Motorola S-Record inputs. Cover saves from the main commands and unsaved-changes prompts during close or Open, including a first save requiring a destination path.
- The window must keep processing input and show meaningful progress during a long save. Cancellation must remain usable and preserve the prior destination and document association when the save is canceled. A completed save must dismiss progress, update the document's path and modified state as appropriate, and return the window to normal interaction.
- Retain atomic output behavior: write to a temporary file, then replace the destination only after a complete successful write. Verify the saved file by reopening it and comparing occupied addresses, values, and applicable format metadata.
- The current writers report progress after every data record, and the window queues a UI update for each report. This is a diagnosis hypothesis for the freeze, not an established root cause. Bound UI update work if measurements confirm it. Also address the separate synchronous save-before-close path if it blocks the UI.

## Verification

- Exercise click, Shift+click, and forward and reverse drags across rows, gaps, view edges, HEX/ASCII columns, and Compare panels. Confirm that row selection and byte outlines remain distinct and that selection coexists with difference, edit, and Find markers.
- Check clipboard contents for both starting columns, printable bytes, control bytes, high-bit bytes, address gaps, single-byte selections, and ranges crossing rows. Confirm that Compare copies only from the originating panel.
- Drop one valid file while Editor and Compare are active; test dirty-document Save/Don't save/Cancel, malformed input, extension mismatch, and multiple-path rejection. Launch with a file path containing spaces and non-ASCII characters and verify the file is opened.
- Verify one display row per vertical arrow press in Editor and both Compare panels at different row widths. Confirm mouse-wheel and page behavior are unchanged.
- Measure UI response, progress completion, cancellation, and final file integrity for the reported 10 MB Save As scenario and matching Save scenarios in both formats. Include save-on-close and save-before-Open paths. Preserve the existing atomic-write and cancellation tests.

## Scope and Prior Decisions

- This spec extends `.scratch/hex-editor-copy-and-global-row-grid/spec.md`, which explicitly left mouse-drag selection and ASCII copying out of its earlier scope. Its existing rules for occupied bytes, address gaps, and HEX clipboard text still apply.
- The root `GLOSSARY.md` already defines memory image, occupied byte, and address gap. No new domain term or ADR is warranted for these user-interface decisions.
- The current work does not add internal drag-and-drop byte editing, change the HEX or S-Record file formats, create bytes in gaps, or alter mouse-wheel scrolling.

## Confirmation

The user confirmed the complete scope and requested that existing behavior not regress.

## Implementation and Verification

- Implemented mouse-captured byte dragging in the shared Editor/Compare grid, contextual HEX/ASCII clipboard copying, one-row vertical scrollbar arrows, file drops, and startup-path opening through the existing Open workflow.
- Coalesced file-operation progress on a 50 ms UI timer and routed Save and Save As from unsaved-changes prompts through the same asynchronous progress and cancellation path as the main commands.
- Acceptance tests cover contextual copying in Editor and Compare, one-row arrows in Editor and Compare, a startup-style path with spaces and non-ASCII characters, approximately 10 MB Save As plus ordinary Save in both formats, output reimport, and Save/Don't save during close.
- The full test process reached more than 280 passing tests but later terminated in a WPF cross-thread resource exception. The affected Compare tests passed when run separately. System clipboard assertions were intermittently empty during larger runs and passed in focused runs after allowing a brief clipboard read delay. Manual mouse validation through Windows automation was unavailable after the helper lost the test window.
