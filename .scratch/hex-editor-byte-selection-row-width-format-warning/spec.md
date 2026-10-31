# Byte Selection, Flexible Display Rows, and Format Mismatch Warnings

Status: resolved

## Problem Statement

The Editor and Compare views identify a clicked byte only when the pointer lands on its rendered text. The current address can change without a visible byte-level selection, while clicking Address visibly selects a whole row. This makes precise navigation and range selection difficult, especially when a byte is also highlighted as a comparison difference, an edit, or a Find match.

The display row width is limited to 8, 16, or 32 bytes and cannot be saved as a default. Users need any integer width from 1 through 128 while retaining the existing quick presets, row navigation, Edit Row workflow, and usable horizontal scrolling. Existing comparison row logic assumes power-of-two widths in at least one path.

The importer detects Intel HEX and Motorola S-Record from file content, but a misleading known filename extension produces no warning. Users need a chance to cancel a mismatched Open, Compare, or Merge, and they need equivalent warnings before writing a format under a conflicting known extension.

## Solution

Treat each occupied byte as a visible, clickable grid cell in Editor and Compare. Clicking a byte selects its address and outlines that byte in both HEX and ASCII in the clicked panel. Shift+click outlines the occupied bytes in the selected address range. Clicking Address or an outer column inset continues to select the row. Difference, edit, and Find markers remain visible alongside the selection outline.

Allow a decimal display width from 1 through 128 bytes per row, with 8, 16, and 32 as quick presets. Preferences stores the width used for newly opened images and newly created Compare views. Width changes keep the current address and selection in view. The main grids retain one display row per line and scroll horizontally when necessary. Edit Row groups widths above 32 into successive lines of no more than 32 bytes.

Detect the actual input format from parsed content. When a recognized filename extension implies the other format, show the filename, expected format, and detected format, with Open anyway and Cancel before accepting the image. Apply the same rule before Save, Save As, and Export when the known destination extension conflicts with the format that will be written.

## User Stories

1. As an Editor user, I want a click inside an occupied HEX byte cell to select that byte, so that I can navigate without aiming at its glyphs.
2. As an Editor user, I want a click inside an occupied ASCII byte cell to select the same address, so that both columns offer equivalent navigation.
3. As an Editor user, I want the selected byte outlined in HEX and ASCII, so that I can see exactly which byte is active.
4. As an Editor user, I want a byte click to replace the previous whole-row highlight, so that a row selection cannot be mistaken for a byte selection.
5. As an Editor user, I want an Address click to continue selecting the whole row, so that row navigation remains available.
6. As an Editor user, I want a click in an outer column inset to continue selecting the row, so that empty column margins have a predictable action.
7. As an Editor user, I want inter-byte whitespace within the grid to select the adjacent byte according to its cell, so that every byte has a practical hit target.
8. As an Editor user, I want the vertical area of a byte cell to be clickable, so that clicking just above or below its text still selects it.
9. As an Editor user, I want the space between rows to remain noninteractive, so that a near miss cannot select a different byte.
10. As an Editor user, I want a blank address cell to remain unselected, so that the interface does not imply that an unoccupied byte exists.
11. As an Editor user, I want Shift+click to visibly outline all occupied bytes in my address range in HEX and ASCII, so that I can inspect the selected span.
12. As an Editor user, I want unoccupied addresses inside that span to remain visually blank, so that an address gap is not represented as data.
13. As a Compare user, I want a byte click in panel A or B to select only that panel's byte, so that the source of the selection is clear.
14. As a Compare user, I want the other panel's difference colors to remain visible after selecting a byte, so that the comparison is still readable.
15. As a Compare user, I want a selected byte's outline to coexist with its difference background, edit outline, and Find marker, so that none of those states is hidden.
16. As a Compare user, I want the same cell hit targets in both panels and in small and large image views, so that selection is consistent.
17. As a user, I want to set a default bytes-per-row value in Preferences, so that new images open with my preferred density.
18. As a user, I want the default to accept every whole number from 1 through 128, so that I can choose an exact display width.
19. As a user, I want the 8, 16, and 32 presets to remain easy to select, so that common widths require little effort.
20. As a user, I want a numeric-only width field in Editor and Compare, so that I can enter a custom decimal width without changing formats or settings dialogs.
21. As a user, I want an out-of-range value to leave the current width unchanged, so that an invalid entry cannot break the view.
22. As a user, I want an empty width field to restore the last valid value when I commit or leave it, so that clearing text temporarily is safe.
23. As a user, I want Preferences Save to persist my new default and Cancel or window close to discard it, so that the existing staged-settings behavior remains predictable.
24. As an Editor user, I want a successfully opened image to use the saved default, so that Open starts with my chosen width even though it reuses the Editor tab.
25. As a Compare user, I want each newly created comparison to use the saved default, so that both panels start with the same row width.
26. As a Compare user, I want an already open comparison to retain its current width when the default changes, so that my current inspection is not disturbed.
27. As a user, I want changing width in an open view to preserve my current address and byte or range selection, so that I do not lose my place.
28. As a user, I want the view to show the newly aligned row containing the selection start after a width change, so that the selected address remains visible.
29. As a user, I want display rows aligned to global multiples of the selected width, so that the same address maps consistently in Editor and Compare, including across memory segments.
30. As a user, I want a partial final row near `0xFFFFFFFF` to display safely, so that valid 32-bit addresses remain accessible at any width.
31. As a user, I want 1–128-byte rows to stay on one line in the main grids, so that horizontal scrolling reveals data without changing the row's meaning.
32. As an Edit Row user, I want widths up to 32 to retain a single-line layout, so that the common workflow remains compact.
33. As an Edit Row user, I want widths above 32 divided into consecutive groups of at most 32 bytes, so that the dialog remains readable.
34. As an Edit Row user, I want each group to show its own offsets and original and proposed HEX/ASCII values, so that I can relate each input to the correct address.
35. As an Edit Row user, I want Tab to traverse editable HEX fields then editable ASCII fields within each group before the next group, so that keyboard movement follows the visible layout.
36. As an Edit Row user, I want Shift+Tab to reverse that order and skip unoccupied addresses, so that backward movement is equally predictable.
37. As an Edit Row user, I want vertical scrolling only when the grouped dialog exceeds the screen height, so that smaller dialogs fit their content.
38. As a user opening a file, I want a warning when a known extension disagrees with the parsed format, so that I can inspect a misleading filename before accepting it.
39. As a user opening a mismatched file, I want Open anyway or Cancel after parsing, so that I can decide whether to keep the current Editor image.
40. As a user replacing an edited image, I want the existing save-changes prompt to run before loading and the format warning before replacement, so that my current image remains protected.
41. As a Compare user, I want a mismatch warning for each newly loaded file, so that a misleading input cannot silently enter a comparison.
42. As a Merge user, I want the same warning for newly loaded inputs, so that I can stop a merge based on a misleading filename.
43. As a Compare or Merge user, I want Cancel to abort the entire new operation without changing existing documents, so that a warning never leaves a partial result.
44. As a user, I want unknown extensions to rely on detected content without a mismatch warning, so that files with custom names remain usable.
45. As a user saving an image under a conflicting known extension, I want Save anyway or Cancel before each ordinary Save, so that I can avoid repeating the mismatch unintentionally.
46. As a user canceling Save, I want the document to remain modified and the target file untouched, so that I can choose another path or format later.
47. As a user using Save As or Export, I want a warning before writing a chosen format under a conflicting known extension, so that output filenames reflect their content.

## Implementation Decisions

- The shared HEX view owns row-versus-byte hit testing and visual selection in Editor and both Compare panels. Both normal and large-image layouts use the same byte-cell semantics.
- The byte selection state is separate from comparison-difference, edited-byte, and Find states. Render an independently visible selection outline without replacing their existing indicators.
- The selected byte or range is represented by absolute addresses. Only occupied bytes within that range receive byte outlines; address gaps and blank cells remain unselected.
- A click within the complete byte cell selects its occupied address. Inter-byte whitespace belongs to the neighboring cells as previously agreed. Vertical row spacing and outer column insets remain outside byte cells.
- Address and outer-inset clicks retain whole-row selection. A byte click clears that row highlight and sets the current address. In Compare, selection belongs to the clicked panel only.
- Accept integer display widths from 1 through 128 in the Editor model, Compare model, Edit Row session, view geometry, and any navigation that derives row addresses. Replace any power-of-two-only row calculation with width-independent arithmetic.
- Align row starts to multiples of the current width from address zero. Preserve sparse address gaps and handle the partial final row within the 32-bit address space without overflow.
- Keep the 8, 16, and 32 shortcuts. Editor and Compare offer decimal-only custom entry; an invalid or out-of-range entry is not applied. A temporarily empty field restores its last valid value on Enter or focus loss.
- Preferences stages and persists the default display width together with its existing choices. Saving updates the default for future views; canceling does not. Preserve the existing default of 16 when no preference has been stored.
- A successful Open applies the stored default to the replacement Editor image. New Compare views start at the stored default; already-open Compare views are unaffected.
- An in-place width change preserves the current address and selection and positions the view at the newly aligned row containing the selection start. The main Editor and Compare grids remain single-line and scroll horizontally as required.
- Edit Row keeps one visual group for widths 1–32. For 33–128, split the row into ordered groups of at most 32 bytes, preserving byte offsets and original/proposed HEX and ASCII values. Keyboard order is HEX then ASCII within each group, then the next group; reverse for Shift+Tab. Unoccupied fields are skipped. Cap dialog height at the screen's available height and scroll vertically when needed.
- The parsed content remains the authority for Intel HEX versus Motorola S-Record. Compare the detected format with the known filename extension only after a successful parse. The message reports the filename, the extension-implied format, and the detected format.
- Treat `.hex` and `.ihx` as Intel HEX extensions and `.s19`, `.s28`, `.s37`, `.srec`, and `.mot` as S-Record extensions, case-insensitively. Other extensions have no implied format and show no mismatch warning.
- Open retains its existing save-changes prompt before import. Show Open anyway or Cancel after a successful import but before replacing the Editor image. A completed earlier Save is not undone by Cancel.
- Compare and Merge apply the warning to newly loaded inputs. Cancel aborts the entire new operation without adding a comparison, opening a merge preview, or changing existing documents.
- Every ordinary Save of a mismatched known-extension document shows Save anyway or Cancel before writing. Save anyway uses the image's detected source format, including when the extension still disagrees. Cancel leaves the document modified and the file unchanged.
- Save As and Export compare the chosen output format with the known destination extension and warn before creating or replacing the target file. Unknown extensions do not warn.

## Testing Decisions

- Test observable user behavior and public operations, rather than control templates, private state, or a particular storage layout. Reuse the existing model/workspace and WPF acceptance seams; introduce a new seam only if an agreed behavior cannot be exercised there.
- At the public model/workspace seam, verify widths 1, 3, 8, 16, 24, 32, 80, and 128; global row alignment; address gaps; a partial final row at `0xFFFFFFFF`; difference-row navigation at non-power-of-two widths; preservation of selection and current address after width changes; default preference persistence and staged Save/Cancel behavior; and Edit Row operations for grouped widths.
- At the public import/export seam, verify content-based format detection, all recognized extension mappings, unknown extensions, mismatched read and write decisions, cancellation without a partial image or file, and preservation of detected format after Open anyway.
- At the WPF seam, verify click targets in HEX and ASCII for normal and large-image layouts, row selection by Address/insets, byte and Shift-range outlines, blank-address behavior, and simultaneous visibility of selection, difference, edit, and Find states in both Compare panels.
- At the WPF seam, verify presets and custom numeric entry, invalid paste or out-of-range values, in-place width changes, one-line main grids with horizontal scrolling, grouped Edit Row layout and Tab order, and vertical scrolling when its content exceeds the screen.
- At the WPF seam, verify warning text and Open anyway/Cancel or Save anyway/Cancel flows across Open, Compare, Merge, Save, Save As, and Export. Confirm that Cancel preserves the existing document and never changes an output file.
- Use the existing row geometry and selection tests, Editor/Compare view-model tests, Edit Row session and window tests, Preferences session tests, file-service tests, and Open/Compare acceptance tests as prior art. Keep focused regression scenarios for the 1–128 width boundary and mismatched extensions.
- Check that large-image scrolling and the existing occupied-byte limit remain correct when the display width increases. This feature does not change the limit or create bytes in address gaps.

## Out of Scope

- Changing the underlying Intel HEX or S-Record syntax or inferring format from a filename instead of content.
- Changing the 32-bit address range or the maximum number of occupied bytes in a memory image.
- Persisting an individual open tab's current width as a separate per-file setting; only the default is persisted.
- Wrapping rows in the main Editor or Compare grids.
- Selecting or synthesizing bytes at unoccupied addresses.
- Replacing the existing difference, edit, or Find color preferences with selection colors.

## Further Notes

- The related grid-column decisions already define Address and outer-inset row selection, inter-byte whitespace ownership, stable column widths, and horizontal scrolling. This spec extends those interactions with byte-level selection and custom width support.
- A file named `firmware.hex` containing valid S-Record data is a valid import with a misleading filename. The warning concerns the mismatch; it is not a parse error.
- The domain glossary's terms *memory image*, *occupied byte*, *address gap*, *base image*, and *reference image* apply throughout. No new glossary entry or ADR is required for these presentation and workflow rules.
