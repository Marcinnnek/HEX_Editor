# Mouse Selection, File Opening, Scrolling, and Save Reliability: Grill Notes

Status: agreed

## User request

- Select byte ranges by dragging the mouse in addition to the existing Shift+click gesture.
- Copy selected values as HEX bytes when selecting in the HEX column and as characters when selecting in the ASCII column.
- Add drag and drop; the source and destination are still to be confirmed.
- Open a file passed to HexEditor at launch, including when launched through a Windows file association.
- Move the Editor and Compare views by one display row for each vertical scrollbar arrow press.
- Investigate unresponsiveness or hangs during Save As and verify ordinary Save.

## Verified repository facts

- The shared `HexViewControl` handles byte clicks and Shift+click ranges, but has no mouse movement or release logic for drag selection. Byte hit testing selects only occupied cells. A selection is shown in both the HEX and ASCII representations.
- The existing system clipboard command always produces uppercase, space-separated HEX byte tokens. It omits unoccupied addresses in a selected range. In Compare, the command uses the panel with the active selection.
- The ASCII column renders bytes `0x20` through `0x7E` as their ASCII characters and every other occupied byte as `.`. The user has not yet decided whether ASCII copying should use these displayed replacement dots or preserve control and non-ASCII byte values in another way.
- The previously agreed `.scratch/hex-editor-copy-and-global-row-grid/spec.md` explicitly excludes mouse-drag selection and ASCII copying. This new request extends that scope; its existing rules for occupied bytes and address gaps remain relevant unless changed by the user.
- The application startup ignores command-line arguments. Opening a file currently starts from the Open picker. No file-drop handler is present in the UI.
- Both Editor and Compare use the shared windowed vertical scrollbar, whose `SmallChange` is three rows. Mouse wheel movement also uses three rows, but is a separate interaction.
- Ordinary Save and Save As run through an asynchronous file operation with progress and cancellation. Saving from the dirty-document close flow uses a synchronous path. The writer uses a temporary file, flushes it to disk, then moves it to the destination.
- Both format writers report progress after every output data record (at most 16 data bytes in the Save path). The main window queues a dispatcher update for each background-thread report. A 10 MB input can therefore create a very large number of UI updates; this is a plausible cause of apparent unresponsiveness, not yet a reproduced diagnosis.
- The domain glossary defines memory image, occupied byte, and address gap. The UI behavior requested here does not yet require a new domain term. ADR 0003 caps a user-facing memory image at 100 million occupied bytes while supporting sparse 32-bit addresses.

## Decision tree: first frontier

1. Mouse drag range selection: views and behavior across display rows and address gaps.
2. Clipboard representation: whether the initiating column controls HEX versus ASCII copying for all selection gestures.
3. Drag and drop: what can be dragged, and where it may be dropped.
4. Launch path: whether file-association opening follows the same workflow as Open.
5. Save responsiveness: a concrete reproduction scenario and the desired feedback during long operations.

The one-row scrollbar arrow requirement is explicit. The impact on mouse-wheel and track-click behavior can be addressed after the user confirms whether those interactions should also change.

## Settled decisions: round 1

- Mouse dragging selects an address range across multiple display rows in Editor and either Compare panel. Unoccupied cells remain visually blank, matching the existing Shift+click range behavior.
- Ctrl+C uses the column where selection began: HEX produces the existing uppercase, space-separated byte tokens; ASCII produces characters. This applies to single-click and Shift+click selections as well as dragging.
- Drag and drop means dropping a file from Windows Explorer onto the application window. The drop should open the file through the normal Open workflow.
- Launching the application with a file path should also follow the normal Open workflow, including its existing format-mismatch warning.
- The reported save problem occurs during saving an approximately 10 MB file. Its exact format, destination, timing, and final result remain unknown.

## Decision tree: second frontier

1. Define copied ASCII output for nonprintable or non-ASCII byte values, and how address gaps affect it.
2. Define how dragging behaves when the pointer crosses the HEX/ASCII boundary or the Compare panel boundary, and whether edge scrolling extends the range.
3. Define where a file may be dropped and how to handle multiple paths in one drop or launch.
4. Clarify the 10 MB save scenario and what responsive feedback is expected.
5. Clarify whether only scrollbar arrows, or also wheel and page clicks, should change their step size.

## Settled decisions: round 2

- ASCII copying returns exactly the characters shown in the ASCII column: printable `0x20`–`0x7E` characters and `.` for other occupied bytes. It omits unoccupied addresses and adds no row breaks.
- Drag selection stays in its originating Compare panel. The originating HEX or ASCII column determines the clipboard representation even if the pointer crosses to the other column.
- While the mouse button remains down, dragging beyond the visible top or bottom edge auto-scrolls and extends the selected range.
- A single file dropped anywhere on the main window uses the Open workflow, even with a Compare tab active. The existing unsaved-change decision still applies.
- Dropping or launching with multiple file paths shows a clear message and opens none of them.
- Each vertical scrollbar arrow press moves one display row in Editor and Compare. Mouse-wheel behavior is unchanged.
- The reported problem was seen with Save As on an Intel HEX file of approximately 10 MB. Verify Save as well, and exercise both Intel HEX and Motorola S-Record. The destination, duration, and final outcome of the reported operation remain unknown.

## Decision tree: third frontier

1. Define range behavior when the drag passes through or ends over a blank address, row margin, or another Compare panel.
2. Decide whether the save responsiveness guarantee also covers saving from the unsaved-changes prompt on close or Open.
3. Specify the observable save responsiveness target and the reported Save As outcome if available.

## Settled decisions: round 3

- A drag starts only on an occupied byte in HEX or ASCII. Address-column dragging retains the existing row-selection behavior.
- When the pointer enters an address gap, outer margin, or the other Compare panel during an active drag, the selection retains its last valid endpoint. It extends again when the pointer reaches an occupied byte in the originating panel.
- Save responsiveness covers Save and Save As from the main commands and from unsaved-changes prompts shown during close or Open.
- In the reported Save As case, the destination file appears to have been written, but the application window remains frozen. A successful file write alone is not sufficient; the window must return to a responsive completed state.

## Diagnosis hypothesis

The per-record progress reports and unbounded dispatcher updates may leave a large backlog after file writing finishes. Reproduce and measure this with approximately 10 MB Intel HEX and S-Record files before treating it as the root cause. Also inspect the synchronous save-before-close path independently.

## Domain documentation

No new glossary entry is needed: the decisions concern UI interaction and clipboard representation of existing occupied bytes. No ADR is warranted: the behavior is user-visible, reversible, and does not record a surprising architectural trade-off.

## Final confirmation

The user confirmed the complete scope and emphasized preserving existing behavior while implementing it.

## Implementation outcome

The agreed behavior was implemented. See `spec.md` for verification results and remaining test-environment limitations.
