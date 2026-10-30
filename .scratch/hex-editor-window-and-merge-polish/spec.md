# Window, Block List, and Merge Polish

Status: ready-for-agent

## Problem Statement

The main window title does not reliably identify the active Editor or Compare tab. Memory segments appear as tables even though the user needs a simple, clickable list of blocks. The save-on-close prompt is poorly arranged, does not name the affected image, and application close can miss dirty editable images held by other Compare tabs. HEX/ASCII rows remain too far apart at the minimum vertical spacing setting. Merge asks for two files even when the Editor already contains the intended destination image. The supplied application icon is not used by the executable and windows.

## Solution

Make the window title follow the active tab and its editable image's saved state. Show memory segments as one-line, horizontally scrollable block entries in Editor and both Compare panels, while retaining selection, navigation, relocation, and export actions. Present a clear, themed save prompt for each distinct dirty editable image when closing. Tighten the minimum HEX/ASCII row gap to approximately 0–1 visible pixel at normal display scaling. When Editor has an image, merge a selected source into that in-memory image with the existing preview and Undo; retain the two-file workflow when Editor is empty. Use the supplied icon in the Windows executable and application-owned windows.

## User Stories

1. As an Editor user, I want the main window title to show my open file's basename, so that I can identify the image from the title bar or taskbar.
2. As an Editor user, I want an asterisk next to the filename when the image has unsaved changes, so that I can see its saved state without opening a menu.
3. As an Editor user, I want the asterisk to disappear after a successful save, so that the title reflects the current state.
4. As an Editor user, I want Save As to update the filename in the title, so that it shows the current file association.
5. As an Editor user without an open image, I want the existing untitled title, so that an empty Editor is recognizable.
6. As a Compare user, I want the title to show both compared filenames, so that I can distinguish active comparisons.
7. As a Compare user, I want the title to mark file A when its editable base image is dirty, so that I can see which comparison has unsaved work.
8. As a user switching between Editor and Compare tabs, I want the title to follow the selected tab immediately, so that it never describes an inactive view.
9. As a firmware engineer, I want each listed Block to represent one contiguous memory segment, so that the list matches the occupied address ranges I can edit.
10. As a firmware engineer, I want Blocks numbered from 1 by increasing start address, so that their order is predictable.
11. As a firmware engineer, I want Block numbers to update after a relocation or merge changes the segments, so that the labels stay accurate.
12. As a firmware engineer, I want each Block to show its start address, end address, and length in one sentence, so that I can scan its complete range without reading table columns.
13. As a firmware engineer, I want length shown as uppercase hexadecimal and plain decimal, so that both representations are available at a glance.
14. As a firmware engineer, I want the same Block presentation in Editor and both Compare panels, so that the views use one visual language.
15. As a user with a narrow Editor or Compare panel, I want each Block description to remain on one line with horizontal scrolling, so that no address or length is clipped or wrapped.
16. As a firmware engineer, I want a single click on a Block to navigate to its start address, so that I can inspect that range quickly.
17. As a firmware engineer, I want an Editor Block double-click to open relocation, so that I can move the selected segment directly.
18. As a firmware engineer, I want Ctrl/Shift selection of Blocks to remain available for operations such as exporting several segments, so that the new list does not reduce my workflow.
19. As a Compare user, I want each file's Block list above its own data, so that a Block clearly belongs to the corresponding image.
20. As a user closing a dirty image, I want the save prompt to name the affected image, so that I know what I am deciding to save.
21. As a user closing a dirty image, I want a readable warning icon and one aligned row of Save, Don't save, and Cancel actions, so that the decision is clear.
22. As a keyboard user, I want Save to be the default prompt action, so that Enter performs the expected save.
23. As a user closing a save prompt with X, I want closing to be canceled, so that dismissing the prompt cannot discard changes.
24. As a user with several dirty editable images, I want application close to ask once for each distinct image, so that no image is silently lost and shared views do not create duplicate prompts.
25. As a user with a dirty Editor and dirty Compare base images, I want the Editor prompt first and the other base-image prompts afterward, so that the sequence is predictable.
26. As a user who chooses Cancel at any save prompt, I want the application to remain open, so that I can return to my work.
27. As a user who previously chose Don't save during an aborted close attempt, I want those edits to remain in memory, so that Cancel does not erase work.
28. As a user closing a Compare tab, I want its existing last-view save rule to remain, so that a shared editable base image is prompted only when its last view closes.
29. As a firmware engineer, I want vertical HEX/ASCII spacing set to zero to show approximately 0–1 visible pixel between text lines at normal display scaling, so that I can inspect more bytes at once.
30. As a firmware engineer, I want higher vertical-spacing settings to increase the gap progressively, so that the preference continues to work.
31. As a Compare user, I want both data panels aligned at every spacing value, so that corresponding addresses remain easy to compare.
32. As a firmware engineer, I want HEX and ASCII clicks to keep targeting the intended occupied byte after rows become denser, so that selection and editing remain reliable.
33. As a user with an image open in Editor, I want Merge to ask only for a source file, so that I do not have to select the destination again.
34. As a user with unsaved Editor edits, I want Merge to use the current in-memory image as its destination, so that those edits participate in the result.
35. As a user currently viewing Compare, I want Merge to target the Editor tab's document, so that the destination does not change with the selected tab.
36. As a user merging into Editor, I want only the source-to-Editor direction available, so that the preview cannot reverse the destination.
37. As a user merging into Editor, I want the existing preview and overlap choices, so that I can review the result before applying it.
38. As a user after a merge, I want the result in the same Editor document and available through Undo, so that I can reverse the operation.
39. As a user with unsaved Editor edits, I want to select that same file's path as the source and read its disk version, so that I can merge the in-memory image with the saved version.
40. As a user with no image open in Editor, I want the existing two-file Merge flow with either direction, so that I can still merge two files, even while a Compare tab is active.
41. As a Windows user, I want the supplied icon on the EXE, main window, taskbar, and application-owned dialogs, so that the application is recognizable throughout Windows.

## Implementation Decisions

- Keep the Windows-only WPF and MVVM architecture and the existing memory-image model. A **Block** is a presentation name for a memory segment; its boundaries continue to follow occupied addresses rather than file-record boundaries.
- Use the active workspace tab and its editable document to derive the window title. Editor uses `<basename><dirty marker> - HEX Editor`; Compare uses `Compare: <file A><dirty marker> ↔ <file B> - HEX Editor`. Refresh on tab selection and document state changes, including edits, save, and Save As.
- Present the existing memory-segment data as selectable one-line list entries rather than a sortable table. Format an entry as `Block 1 Starts at: 0x08000000 Ends at: 0x080015BB (Length: 0x15BC = 5564)`. Use uppercase hexadecimal and an ungrouped decimal length. Order and renumber from ascending start addresses after changes.
- Provide horizontal scrolling for the full entry in Editor and both Compare lists. Preserve single-click navigation, Editor double-click relocation, and multiselection used by export. Keep each Compare list associated with its own image.
- Give the save prompt a theme-consistent warning icon, readable document name, and aligned `Save`, `Don't save`, `Cancel` actions. Enter activates Save; X and Escape cancel. The prompt must preserve correct behavior for untitled images that require Save As.
- At application close, gather distinct dirty editable documents from Editor and Compare views, ordered with Editor first. Prompt once per document. A canceled or failed save aborts closing; prior Don't save decisions do not alter in-memory images if closing is aborted. Preserve the current last-view rule for closing one Compare tab.
- Interpret zero vertical spacing as a nearly contiguous rendered HEX/ASCII text-line baseline: approximately 0–1 visible pixel at normal display scaling. Make higher settings add visible spacing without changing horizontal byte spacing, byte hit testing, or alignment across Editor and Compare.
- When Editor holds an image, fix it as the Merge destination regardless of the active tab. Use its current memory image, ask only for a source file, and hide reverse direction. Preserve preview, overlap resolution, document identity, and Undo.
- If the selected source path equals the Editor document's path, load the source from disk rather than reusing the in-memory destination. Retain the existing two-file, two-direction flow when Editor has no image.
- Include the supplied ICO asset in the Windows executable and apply it to application-owned windows. Native Windows picker visuals remain controlled by Windows.

## Testing Decisions

- Test externally visible outcomes at the highest existing seam. Use public workspace and memory-image operations for document state and Merge outcomes, and rendered WPF scenarios for title, lists, save prompts, spacing, and icon. Avoid assertions about private event handlers or a specific control class.
- Test public workspace and close-workflow operations for distinct document order, shared Compare views, dirty-state enumeration, Save/Don't save/Cancel outcomes, cancellation, and retention of edits after an aborted close attempt. Use rendered WPF tests for the save prompt itself, including its default Save action and X/Cancel behavior. Do not automate the full MainWindow modal close sequence; the prior harness hung, and prompt ordering is covered at the public close-workflow seam.
- Exercise title changes in a rendered main window across Editor, multiple Compare tabs, edits, Save, Save As, and tab switches. Verify that the Compare marker reflects file A's actual editable base document.
- Exercise Editor and both Compare Block lists with multiple separated segments, including after relocation or merge. Verify text, numbering, order, horizontal scrolling in narrow panels, single-click navigation, Editor double-click relocation, and Ctrl/Shift selection for export. Assert behavior rather than the use of DataGrid or ListBox.
- Extend spacing tests to measure the absolute visible gap at zero and the monotonic increase at larger settings in a rendered Editor and both Compare panels. Verify HEX/ASCII byte hit testing and corresponding row alignment. The earlier 0.2-character-height zero-gap expectation must be updated to the confirmed 0–1-pixel target.
- Exercise Merge with a clean Editor image, unsaved Editor edits, an active Compare tab whose base document differs from Editor, an empty Editor, and a source path matching Editor's path. Verify that the matching-path source contains disk bytes, the fixed destination retains unsaved bytes until overlap choice, preview reflects both images, and applying the result is reversible through Undo.
- Verify the built executable has the intended icon resource, and inspect main and representative application-owned WPF windows for the icon in light and dark themes.
- Reuse the repository's existing workspace, Compare workflow, segment-list layout, row-spacing, and WPF interaction tests as prior art. Run the full automated suite after implementation and inspect the key window layouts at normal and high display scaling.

## Out of Scope

- Changing Intel HEX or Motorola S-Record parsing, export formats, address occupancy, or memory-segment boundaries.
- Changing Compare difference calculation, reference-image mutability, or the two-column Compare layout beyond the Block-list presentation.
- Adding filtering, grouping, or manual sorting to Block lists.
- Changing Edit Row behavior, theme selection, or Preferences persistence, except as needed to render the save prompt and icon consistently.
- Replacing WPF or changing the existing Merge overlap policies, preview semantics, and Undo unit.
- Rebranding native Windows file pickers, whose icon and title-bar rendering are controlled by the operating system.

## Further Notes

- The user confirmed the design in `grill-notes.md` and approved public workspace operations plus rendered WPF scenarios as the testing split.
- This spec supersedes the previous UI-polish specification only where the target for vertical spacing zero changed: the new target is approximately 0–1 visible pixel, not 0.2 character height.
- Current implementation facts: the title is derived from Editor state but not refreshed for active Compare tabs; segment lists use tables; application close checks only the active document; Merge always opens destination and source pickers; a same-path comparison loader can reuse in-memory data. These are investigation leads, not prescribed implementation details.
- The supplied root icon file is currently untracked user content and must be preserved as the source asset. Existing binary artifacts in the workspace are user changes and are outside this specification's source edits.
