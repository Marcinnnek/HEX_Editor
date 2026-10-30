Status: ready-for-agent

# HEX Editor UI and Workflow Enhancements

## Problem Statement

The Windows HEX Editor exposes many advanced controls in permanent toolbar rows, leaving too little space for the Editor and Compare workspaces. The Compare command asks for two files even when an editable memory image is already open. Compare row activation and difference navigation have been reported to terminate the application, and row editing, relocation, and export options are difficult to discover or use safely. The current application also treats the Editor as the owner of a single active image, which cannot represent the agreed behavior when several Compare tabs share one base image and the Editor opens another image. Users need a coherent WPF UI interface, responsive sparse-image navigation, explicit data-changing dialogs, configurable HEX grid spacing, and a self-contained Windows executable.

## Solution

Present five compact primary toolbar groups above an Editor/Compare tab workspace styled with WPF UI. Place Selection/Range, Edit, Relocate, Checksum, Export, and Preferences commands in menus and focused dialogs. Use the supplied screenshot as a layout reference: the Editor has a compact memory-segment list above a full-width HEX table, while Compare has two side-by-side columns; each column places one file's segment list above that same file's HEX table. Keep workspace settings and the result area available. Commands that exceed a narrow toolbar width move into a More menu. The main window exposes the standard Windows minimize, maximize, and close controls.

Represent each open editable base image as one document with its own bytes, path, format metadata, saved state, and Undo/Redo history. Editor and Compare tabs may refer to the same document; each Compare tab also owns a read-only reference image and its own view settings. Commands act on the active tab's base image. A change to that document refreshes every view that uses it. Opening a new Editor image does not discard earlier Compare tabs. Closing a tab closes one view; Close Image closes all views of the active image after one unsaved-change decision.

Make the left Compare panel editable through a modal Edit Row dialog and keep the right panel read-only. In Edit Row, display zero-based byte offsets from the row start, limit ASCII input to one character, and validate HEX input as one or two hexadecimal digits. Add a Relocate dialog with explicit collision approval, opened by double-clicking a selected memory segment and prefilled with its current start address. Add focused range/fill/checksum dialogs, an export dialog that asks for format, scope, target path, and maximum data bytes per record, and a Preferences window with independently configurable horizontal byte spacing and vertical row spacing. Keep comparison separators neutral when neighboring bytes are highlighted. Export writes a copy without altering the active document association or dirty state. Compare row activation and previous/next difference navigation must report recoverable errors without terminating the application. Publish one self-contained Windows x64 executable and validate that it launches without adjacent application DLLs.

## User Stories

1. As a firmware engineer, I want File, Compare/Merge, History, View, and Navigate in a compact primary toolbar, so that the workspace has room for memory data.
2. As a firmware engineer, I want advanced operations in menus and contextual commands, so that they remain discoverable without occupying a permanent second toolbar row.
3. As a firmware engineer, I want overflowing primary commands under More when the window is narrow, so that I can still reach them without horizontally scrolling the workspace.
4. As a firmware engineer, I want the Editor memory-segment list above a full-width HEX table, so that I can inspect blocks and bytes in one vertical workspace.
5. As a firmware engineer, I want Compare to retain side-by-side segment lists, aligned HEX tables, settings, and a result area, so that I can inspect both images together.
6. As a firmware engineer, I want both workspaces styled consistently with WPF UI, so that menus, controls, dialogs, and notifications feel like one application.
7. As a firmware engineer, I want the app to follow the Windows light/dark theme by default, so that it fits my desktop appearance.
8. As a firmware engineer, I want a persisted manual light/dark override, so that my chosen appearance survives a restart.
9. As a keyboard user, I want visible focus, disabled states, readable contrast, and standard shortcuts, so that I can operate the app without a mouse.
10. As a firmware engineer, I want monospaced HEX bytes to remain aligned under the new theme, so that addresses and byte positions stay readable.
11. As a firmware engineer with an open base image, I want Compare to ask only for a reference file, so that I do not select the same file twice.
12. As a firmware engineer with unsaved edits, I want Compare to use the current in-memory base image, so that I see differences from my actual work.
13. As a firmware engineer with no open image, I want the first file chosen for Compare to open in Editor, so that it becomes the editable base image.
14. As a firmware engineer who cancels the second picker, I want the newly chosen base file to remain in Editor without a Compare tab, so that my first selection is not lost.
15. As a firmware engineer in Compare, I want Compare again to open a new tab using the same base image and ask for another reference, so that I can compare one working image against several files.
16. As a firmware engineer, I want opening a different file in Editor to leave earlier Compare tabs and their edits intact, so that I can switch tasks without losing work.
17. As a firmware engineer, I want all Editor and Compare views of one base image to show edits immediately, so that tabs never disagree about its bytes.
18. As a firmware engineer, I want one Undo/Redo history per base image, so that an edit made in one tab can be undone from another tab showing that image.
19. As a firmware engineer, I want each tab to retain its own bytes-per-row and Show ASCII settings, so that switching tabs restores my preferred view.
20. As a firmware engineer, I want the main View and Navigate controls to reflect the active tab and match the Compare workspace controls, so that repeated controls never show conflicting values.
21. As a firmware engineer, I want Compare rows aligned by absolute address with absent bytes left blank, so that sparse data never shifts into a false position.
22. As a firmware engineer, I want both Compare panels to respond together to bytes-per-row and ASCII visibility changes, so that corresponding addresses remain aligned.
23. As a firmware engineer, I want wheel and scrollbar navigation in both Compare panels to remain responsive for typical images up to 10 MB, so that scrolling does not freeze the computer.
24. As a firmware engineer, I want long import, compare, and export work to expose progress and cancellation, so that I can regain control of the application.
25. As a firmware engineer, I want a double-click on an occupied Editor row to open Edit Row, so that I can change bytes with their absolute addresses visible.
26. As a firmware engineer, I want Edit Row to show the row start, selected byte address, HEX values, and printable ASCII, so that I know exactly what I am changing.
27. As a firmware engineer, I want HEX and ASCII edits synchronized inside the dialog, so that either representation shows the proposed byte values.
28. As a firmware engineer, I want HEX input to accept exactly two hexadecimal digits for any byte value, so that invalid values cannot be committed.
29. As a firmware engineer, I want direct ASCII editing limited to printable bytes 0x20 through 0x7E and other bytes shown as dots, so that nonprintable bytes are never changed accidentally.
30. As a firmware engineer, I want absent bytes in partial or sparse rows to remain absent unless I explicitly insert or fill them, so that editing does not create unintended data.
31. As a firmware engineer, I want OK to commit all changes from one Edit Row dialog as one Undo/Redo action, so that I can reverse the edit predictably.
32. As a firmware engineer, I want Cancel or invalid input to leave the base image and dirty state unchanged, so that an abandoned edit is harmless.
33. As a firmware engineer, I want a valid double-click on the left Compare panel to edit the base image, so that I can correct a difference in context.
34. As a firmware engineer, I want a double-click on the right reference panel to show a short read-only message in the status bar, so that I understand why no editor opens.
35. As a firmware engineer, I want double-clicking headers, gaps, empty space, or unloaded rows to be safe, so that Compare never crashes from an invalid target.
36. As a firmware engineer, I want the comparison result and displayed calculated checksums refreshed or marked stale after an edit, so that I do not rely on old results.
37. As a firmware engineer, I want clicking a memory segment to select it without moving it, so that navigation cannot modify the image.
38. As a firmware engineer, I want Relocate to open a dialog showing the selected segment's current address range and size, so that I can review what will move.
39. As a firmware engineer, I want to enter a hexadecimal new start address and see the computed end address, so that I can judge the destination before committing.
40. As a firmware engineer, I want malformed, overflowing, or unsupported relocation destinations rejected, so that addresses cannot wrap or exceed the chosen output format.
41. As a firmware engineer, I want an occupied destination blocked by default and overwriting separately confirmed even when byte values match, so that collisions never silently replace data.
42. As a firmware engineer, I want a successful relocation to preserve segment bytes and length, update all views, mark the image modified, and support Undo/Redo, so that the move is safe and reversible.
43. As a firmware engineer, I want Select Range, Fill, Edit/Insert, and Checksum to collect their parameters in focused dialogs, so that hidden toolbar fields are not required.
44. As a firmware engineer, I want export to ask for entire image, selected segments, or address range, so that I can choose exactly which occupied bytes to write.
45. As a firmware engineer, I want each export to show the target path and Intel HEX or Motorola S-Record format, so that I can verify the destination before writing.
46. As a firmware engineer, I want each export to offer 8, 16, or 32 data bytes per record, so that serialized record size matches my downstream tools.
47. As a firmware engineer, I want 16 bytes per record as the initial default and my last choice remembered after restart, so that repeat exports are convenient without hiding the choice.
48. As a firmware engineer, I want export to preserve occupied addresses, gaps, checksums, and required control records, so that reimport produces the intended image.
49. As a firmware engineer, I want Export Image to write a copy while keeping the active path and dirty state, so that export cannot accidentally change my working document.
50. As a firmware engineer, I want Save As to change the active file association only after a successful safe write, so that the working document follows the saved file.
51. As a firmware engineer in Compare, I want Save and Save As to target the left base image, so that I can save edits without leaving the comparison.
52. As a firmware engineer, I want a tab close button to close only that view and retain a modified image used by other tabs, so that shared work stays open.
53. As a firmware engineer, I want the last view of a modified image to ask whether to save, discard, or cancel, so that I can prevent data loss.
54. As a firmware engineer, I want Close Image to close all views of the active image after one unsaved-change decision, so that I can remove a document without answering the same question repeatedly.
55. As a firmware engineer, I want recoverable errors to identify the operation and address or selection involved, so that I can correct the input without restarting.
56. As a firmware engineer, I want a single Windows x64 executable that launches without adjacent application DLLs or a separately installed .NET desktop runtime, so that deployment is straightforward.
57. As a firmware engineer, I want the main window to show standard minimize, maximize, and close controls, so that I can manage it like other Windows applications.
58. As a firmware engineer, I want Edit Row to label bytes by zero-based offset from the row start, so that I can identify a cell without repeating the full address above it.
59. As a firmware engineer, I want each ASCII cell to accept one printable ASCII character and each HEX cell one or two hexadecimal digits, so that edits map unambiguously to bytes.
60. As a firmware engineer, I want Preferences to control horizontal byte spacing and vertical row spacing independently and remember both choices, so that I can tune the grid density.
61. As a firmware engineer, I want comparison highlighting to color the differing byte while leaving separator spaces neutral, so that only data cells appear changed.
62. As a firmware engineer, I want a single click on a memory segment to select and navigate to its start and a double-click to open relocation with the current address prefilled, so that a small address adjustment is quick and safe.
63. As a firmware engineer, I want Previous/Next Difference to move in address order from the selected byte or the address field when no row is selected, so that navigation finds the nearest difference in the requested direction.
64. As a firmware engineer, I want Compare activation and difference-navigation errors logged and reported in the status bar without closing the app, so that a failed action does not lose my workspace.

## Implementation Decisions

- Preserve Windows-only .NET 10 WPF and MVVM, as established by the existing desktop architecture decision. Add WPF UI resources and controls where supported while retaining a monospaced, virtualized HEX presentation.
- Introduce one workspace-level state owner for open image documents, Editor and Compare tabs, active-tab routing, and tab closure. An image document has stable identity independent of its path or a particular immutable memory-image instance; it owns editing history, source format metadata, saved state, and change notifications. This corrects the current single-active-image and path-matching limitations.
- Keep a read-only reference image per Compare tab. All Compare tabs that use the same base document observe its changes and share its Undo/Redo history. A new Editor image creates or activates a different document without replacing base documents still used by Compare tabs.
- Keep viewing preferences per tab. The main toolbar routes View and Navigate changes to the active tab, and Compare's local controls bind to that same tab state. Both Compare panels use one absolute-address row model and remain synchronized after filtering, width changes, and navigation.
- Route Open, Compare, Save, Save As, Export, Edit, Merge, Undo/Redo, and Close through the active document or tab as applicable. Compare from an existing Compare tab reuses that tab's base document and opens a new tab with a newly selected reference.
- When Compare begins without a base document, load its first file into Editor before requesting the reference. Cancelling the second picker leaves that Editor document open and creates no Compare tab. File parsing failures do not replace or discard previously open documents.
- Keep read-only reference operations disabled. A right-panel double-click posts a brief status message. Row hit testing rejects headers, separators, absent bytes, and unrealized rows before opening Edit Row; command boundaries catch and report unexpected failures with structured context.
- Use a modal Edit Row interaction with a staged proposed-value buffer. Validate all occupied-byte HEX/ASCII changes before one atomic commit. Missing bytes are noneditable in this dialog; Insert or Fill is required to create occupancy. The base document publishes one change notification and one Undo/Redo transaction for the commit.
- Use the existing relocation preview and overlap decision behavior behind a focused dialog. An occupied destination, including identical values, requires a separate affirmative overwrite confirmation. Cancellation and validation failure leave the image unchanged.
- Move advanced parameter collection from permanent controls into range, edit/insert, fill, checksum, relocation, and export dialogs. Keep operations available in menus and useful context menus, with enabled state based on the active selection and document.
- Use existing format readers/writers and atomic file-write behavior. Export Image is a copy operation and never changes document association or saved state. Save As changes association only after successful write. Export dialogs choose scope, format, destination, and 8/16/32 maximum data bytes per record; view row width remains independent. Persist the most recent export record size, defaulting to 16 on a new profile.
- Keep long file operations asynchronous with progress and cancellation. Preserve sparse row virtualization and avoid allocating address gaps or eagerly rendering all comparison rows. The UI must stay responsive during wheel and scrollbar navigation; for typical files up to 10 MB, comparison opening should target no more than 15 seconds on the user's machine.
- Store user grid-spacing preferences in per-user data under `%LOCALAPPDATA%\HexEditor`; use slightly tighter horizontal and vertical spacing by default. Keep comparison separators neutral regardless of the state of adjacent byte cells.
- Preserve standard Windows main-window minimize, maximize, and close controls.
- Provide automatic Windows theme selection plus a persisted manual light/dark override. Theme, export-size preference, defaults, and logs must not require files adjacent to the distributed executable.
- Publish a self-contained Windows x64 single-file executable with any necessary native components embedded for extraction. Do not enable trimming without compatibility evidence. Verify the released executable as an isolated file on a compatible clean Windows machine.

## Testing Decisions

- Test externally observable behavior through public workspace operations and document/tab state. Use this as the primary seam for open, compare, edit, save, export, relocation, and difference navigation; avoid tests bound to private event handlers, exact control names, or theme resource structure. Verify visual layout, standard window controls, and Preferences through WPF acceptance scenarios.
- Extend existing memory-image, editing-session, comparison-model, view-model, relocation, and file-service tests as prior art. Existing tests already cover sparse addresses, absolute-address comparison, view widths, Undo/Redo, relocation, Save As, and export/reimport; new cases should exercise shared document identity and multi-tab behavior through the workspace seam.
- Verify Compare with an open dirty base, Compare without a base, second-picker cancellation, Compare from Compare, opening a different Editor image, and two Compare tabs sharing edits and one history. Assert bytes, dirty state, tab ownership, and reference read-only behavior.
- Verify Edit Row staging with HEX and ASCII changes, invalid input, partial rows, absent bytes, Cancel, one atomic Undo/Redo action, and refreshed comparison/checksum state. Verify safe command behavior for headers, empty areas, and virtualized rows.
- Verify Edit Row uses zero-based byte offsets from the row start, accepts one or two hexadecimal digits per HEX cell and exactly one printable ASCII character (`0x20–0x7E`), rejects all invalid values, and keeps representations synchronized.
- Verify relocation success, overflow/format bounds, collision denial, explicit overwrite approval including identical bytes, cancellation, segment refresh, and Undo/Redo through public operations.
- Verify export scope, both formats, 8/16/32 record sizes, address gaps, metadata, checksum/control records, round-trip import, persistent default, and unchanged document path/dirty state. Verify Save As changes association only after successful atomic write.
- Verify tab close versus Close Image with multiple views and dirty images, one prompt per affected image, and cancellation preserving all state.
- Exercise active-tab View/Navigate synchronization and per-tab preferences through the workspace seam. Review WPF UI layout, overflow, focus, dialogs, dark/light themes, and right-panel status feedback with UI acceptance scenarios.
- Verify previous/next difference navigation with two different files, selected and unselected rows, address-field starting positions, sparse gaps, and no further differences. Inject or reproduce recoverable navigation/activation failures and verify diagnostics plus status feedback without closing the window.
- Review Compare as two columns, with each file's segment list immediately above its own data panel; verify the Editor segment list remains only slightly wider than its address content. Review standard window controls and Preferences persistence after restart.
- Verify horizontal and vertical grid spacing are independently adjustable and persisted, and that separator spaces stay neutral beside highlighted bytes.
- Measure wheel and scrollbar interaction in both Compare panels on representative files up to 10 MB and sparse high-address fixtures. Inspect responsiveness, CPU use, cancellation, and the agreed 15-second comparison-opening target without depending on internal timing of individual methods.
- Verify a Release publish from an isolated output directory. On a clean compatible Windows x64 machine without a separately installed desktop runtime, copy only the executable and confirm startup and the main workflows; retain this as a release acceptance check if that machine is unavailable during development.

## Out of Scope

- Linux, macOS, Windows x86, and Windows ARM64 releases.
- Editing the right/reference image from Compare.
- Direct typing into the main HEX grid; editing occurs through Edit Row or other explicit operation dialogs.
- Pixel-for-pixel reproduction of the screenshot's colors, icons, fonts, or exact dimensions.
- Redesign of the sparse memory format, new firmware container formats, direct device flashing, or disassembly.
- Trimming the published application or requiring that its runtime never extract internal files to a temporary location.

## Further Notes

- This specification translates the agreed UI and workflow enhancement requirements, revision 1.2, into implementation work. The refinements agreed on 2026-10-30 cover Edit Row input and addressing, Compare layout and error recovery, configurable grid spacing, segment relocation interaction, and standard main-window controls. The earlier HEX/S-Record requirements remain in force unless explicitly refined here.
- Current implementation already has WPF Editor and Compare views, a virtualized HEX control, file workflows, comparison and relocation models, and MSTest coverage. The main architectural gap is document ownership across several tabs; some existing UI controls read parameters directly from permanent toolbar fields.
- Follow the agreed priority: prevent the Compare double-click crash first; then implement shared comparison/editing and relocation workflows, compact layout and WPF UI styling, export dialog, and isolated single-file release validation.
