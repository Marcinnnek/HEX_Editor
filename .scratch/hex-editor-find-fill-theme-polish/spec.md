# Find, Fill, Block Deletion, and Appearance Polish

Status: resolved

## Problem Statement

The main toolbar contains a redundant Go-to-address field and a More button that duplicates menu commands. There is no way to find a HEX or ASCII byte sequence in a memory image. Fill Range always overwrites occupied bytes, cannot derive the whole image's address interval, and cannot fill the full 32-bit address space. A memory segment can be relocated but has no direct deletion action in the relocation dialog. Dark appearance leaves native title bars light, hides the selected indicator in Preferences' appearance radio buttons, and makes comparison differences and edited bytes difficult to distinguish. Users cannot choose the highlight colors for each appearance mode.

## Solution

Keep Go to address in View with Ctrl+G and remove its toolbar controls and More. Add a nonmodal Find bytes window that searches HEX or ASCII byte sequences in the active Editor or both images of the active Compare tab. Extend Fill Range with a whole-image interval shortcut, a choice between preserving and overwriting occupied bytes, a warning above 50 million addresses, and cancellable, atomic support for an actual 4 GiB fill. Put Delete Block in the existing Relocate memory segment dialog. Make native title bars and appearance controls legible in Dark, distinguish comparison differences from edits, and let users save separate highlight palettes for Light and Dark.

## User Stories

1. As a firmware engineer, I want the Go-to-address field and Go button removed from the main toolbar, so that the toolbar has fewer redundant controls.
2. As a firmware engineer, I want Go to address in View, so that I can still open its address dialog.
3. As a keyboard user, I want Ctrl+G to open Go to address, so that removing its toolbar controls does not slow navigation.
4. As a firmware engineer, I want the More button removed, so that the main toolbar contains only primary controls.
5. As a user of a narrow window, I want every command formerly under More to remain available through menus, so that I can reach it at the minimum window width.
6. As a firmware engineer, I want Find bytes in View, so that I can locate a byte sequence in a memory image.
7. As a keyboard user, I want Ctrl+F to open or focus one Find bytes window, so that repeated use does not create duplicate windows.
8. As a firmware engineer, I want to choose HEX or ASCII in Find bytes, so that I can search using either representation.
9. As a firmware engineer, I want HEX search to accept both `DEADBEEF` and `DE AD BE EF`, so that spacing does not change the searched bytes.
10. As a firmware engineer, I want HEX search to ignore letter case and require complete byte pairs, so that malformed half-byte patterns cannot produce misleading matches.
11. As a firmware engineer, I want ASCII search to match case-sensitive 7-bit ASCII, so that typed characters map predictably to byte values.
12. As a firmware engineer, I want Find to reject non-ASCII characters in ASCII mode, so that no implicit text encoding changes the pattern.
13. As a firmware engineer, I want Find to preserve a separate draft for each mode, so that switching between HEX and ASCII does not erase either pattern.
14. As a firmware engineer, I want a match to cross source-record and displayed-row boundaries, so that file formatting does not interrupt byte-sequence search.
15. As a firmware engineer, I want an address gap to stop a match, so that absent bytes are never treated as stored data.
16. As an Editor user, I want Find to search the active editable image, so that results refer to the image I am inspecting.
17. As a Compare user, I want Find to search both the base and reference images, so that I can locate the pattern on either side.
18. As a Compare user, I want matches at the same address in A and B counted separately, so that neither image's result is hidden.
19. As a Compare user, I want results ordered by address, then A before B, so that Next and Previous have a stable sequence.
20. As a Compare user, I want a result to identify its panel and keep the two views synchronized, so that I can understand its position in the comparison.
21. As a firmware engineer, I want Find Next to begin after the selected byte and Find Previous before it, so that I can continue from my current position.
22. As a firmware engineer, I want Find to begin at the active image's start or end when nothing is selected, so that an initial search has a predictable origin.
23. As a firmware engineer, I want Next and Previous to wrap at the ends of their result order, so that repeated navigation reaches every match.
24. As a keyboard user, I want F3 and Shift+F3 to repeat the last Next and Previous search even after closing Find, so that I can navigate without reopening the window.
25. As a firmware engineer, I want Find to highlight the entire current match in both HEX and visible ASCII columns, including across rows, so that I can recognize every matched byte.
26. As a firmware engineer, I want Find to show the current match's address and panel and to highlight only that match, so that the result remains easy to locate.
27. As a firmware engineer, I want invalid patterns or no match reported inside Find without changing my selection, so that a failed search does not disrupt navigation.
28. As a firmware engineer, I want closing Find to remove its temporary match indicator but leave my view and selection at the result, so that the navigation remains useful.
29. As a firmware engineer, I want an open Find window to follow tab switches, image loads, and in-place image changes, so that later searches use current bytes and discard stale indicators.
30. As a firmware engineer, I want long searches, including in a fully occupied 4 GiB image, to show progress and support cancellation without freezing the window or moving my selection, so that I can stop an expensive scan safely.
31. As a firmware engineer, I want Get fill all region in Fill Range to enter the lowest and highest occupied addresses of the image, so that I can select its complete address interval quickly.
32. As a firmware engineer, I want that interval to include address gaps between memory segments, so that I can choose whether Fill creates data in those gaps.
33. As a firmware engineer, I want Get fill all region unavailable for an empty image while manual range entry remains available, so that the shortcut never invents image bounds.
34. As a firmware engineer, I want Keep existing data selected by default, so that Fill does not overwrite occupied bytes unless I choose to.
35. As a firmware engineer, I want Keep existing data to write the pattern only at unoccupied addresses, so that existing values survive the operation.
36. As a firmware engineer, I want Overwrite existing data to write the pattern at every address in the interval, so that I can deliberately replace stored values.
37. As a firmware engineer, I want the repeating pattern aligned to the start address even when Keep skips occupied bytes, so that each address receives a predictable pattern position.
38. As a firmware engineer, I want the inclusive address count shown before Fill and a cancellable warning when it exceeds 50,000,000 addresses, so that I can recognize an expensive operation before it changes the image.
39. As a firmware engineer, I want to fill `0x00000000` through `0xFFFFFFFF` with an actual occupied byte at every address, so that Fill supports the full 32-bit address space.
40. As a firmware engineer, I want a full-range Fill to be saveable and exportable as Intel HEX or S-Record and undoable as one edit, so that a large operation remains a normal document change.
41. As a firmware engineer, I want long Fill, Save, and Export operations to show progress and support cancellation without partial document or output changes, so that I can safely stop them.
42. As a firmware engineer, I want an insufficient-space failure to preserve the current image and any existing output file, so that a failed large operation does not lose data.
43. As an Editor user, I want one selected block to offer Delete inside the Relocate memory segment dialog, so that I can remove that memory segment without a separate range entry.
44. As an Editor user, I want Delete disabled when multiple blocks are selected, so that it is clear which segment would be removed.
45. As an Editor user, I want Delete to target the original selected block regardless of uncommitted destination text, so that changing a relocation field cannot change the deletion target.
46. As an Editor user, I want a confirmation naming the block's start, end, and length, so that I can check the deletion target.
47. As an Editor user, I want Cancel to leave the image unchanged and confirmed Delete to close the dialog and be reversible with one Undo, so that I retain control of the operation.
48. As an Editor user, I want the nearest following block selected after deletion, or the preceding block when the last was deleted, without an automatic jump, so that I can continue working in the list.
49. As an Editor or Compare user, I want a single click on a block to keep jumping to its start, so that no extra Jump button is needed.
50. As a Compare user, I want the reference image to remain read-only and the block lists to gain no action buttons, so that deletion is confined to the editable Editor image.
51. As a Dark appearance user, I want native title bars in every open and newly opened application window to be dark, so that dialogs match the active appearance.
52. As a Follow Windows user, I want title bars to update when the system theme changes without restarting, so that all windows follow the effective appearance.
53. As a Preferences user, I want the selected appearance radio button to show a clear indicator in Dark as it does in Light, so that I can see the active choice.
54. As an Editor user, I want an occupied byte that differs from the last saved image to use an edited-byte highlight, so that I can see current unsaved changes.
55. As an Editor user, I want a newly added occupied byte highlighted and the highlight removed after Save or Undo restores the saved value, so that the marker reflects the current document state.
56. As an Editor user, I want removed addresses to remain absent rather than show a green byte cell, so that the image does not imply storage where there is none.
57. As a Compare user, I want unequal bytes and bytes present only in A or only in B to have distinct, legible colors in Dark, so that each comparison state is visible.
58. As a Compare user, I want a byte that is both edited and different from the reference to retain the difference background and show an edited outline, so that I can see both states.
59. As a Find user, I want a separate match indicator when a byte is also edited or different, so that all three states remain visible.
60. As a Preferences user, I want separate Light and Dark colors for unequal bytes, A-only bytes, B-only bytes, and edited bytes, so that highlights work with each appearance.
61. As a Preferences user, I want swatches and a color picker plus a separate palette selector, so that I can edit either theme's colors without switching the active application theme.
62. As a Preferences user, I want Save to apply color changes, Cancel or X to discard them, and Reset to restore defaults for the selected palette, so that experimenting with colors is reversible.

## Implementation Decisions

- Keep this a Windows-only WPF application, consistent with the existing desktop ADR. Use the glossary's memory image, memory segment/Block, occupied byte, address gap, source record, byte sequence, comparison difference, base image, reference image, and edited byte meanings.
- Remove the main toolbar's Go-to-address field, Go button, and More button. Preserve View > Go to address and Ctrl+G. Keep commands that More duplicated reachable through menus at the minimum supported window width. This intentionally supersedes the earlier More overflow requirement.
- Add one nonmodal Find bytes window with a HEX/ASCII selector, separate draft patterns, Find Next, Find Previous, Close, inline errors/status, and a single-window focus behavior. View > Find bytes and Ctrl+F open or focus it; F3 and Shift+F3 reuse the last valid pattern and mode.
- Parse HEX as complete byte pairs with optional inter-byte spaces and case-insensitive digits. Interpret ASCII input as case-sensitive 7-bit byte values and reject non-ASCII input. Match consecutive occupied addresses across source-record and row boundaries; never cross an address gap.
- Search the active Editor image or both images of the active Compare tab. Order Compare results by address then A before B, including two results for a match at the same address on both sides. Search direction starts beyond the current selected byte or at the relevant image boundary, wraps, and keeps the Compare views synchronized with the matching panel identified.
- Highlight only the current complete match in HEX and visible ASCII. Closing Find removes the temporary match indicator without moving the selection. Tab or image changes, edits, Undo, Save, and Merge invalidate the old indicator; subsequent searches use the current active image. Invalid patterns, no match, and cancelled scans leave navigation unchanged.
- Keep large scans asynchronous and cancellable with progress. The Find window and application remain responsive for an image whose occupied span covers the entire 32-bit address space.
- Extend Fill Range with Get fill all region, using the minimum and maximum occupied addresses inclusive, including intervening gaps. Disable the shortcut for an empty image while retaining manual entry. Present Keep existing data and Overwrite existing data, defaulting to Keep. Pattern position is determined from the interval start, including addresses skipped by Keep.
- Compute Fill interval length with a 64-bit count; show the inclusive count and warn before execution when it exceeds 50,000,000 addresses, regardless of current occupancy. The warning can cancel without a change. A manually entered full range from zero through `0xFFFFFFFF` must create 4,294,967,296 occupied bytes and remain one Undo transaction.
- Follow ADR 0002: revise memory-image storage and related contracts so that a contiguous 4 GiB occupied range does not require one CLR byte array or an `int` byte count. Use bounded or logical storage and streaming operations, with disk-backed working storage when needed. This affects edits, comparison, rendering, import, save, and export where they assume a segment fits in one array.
- Keep long Fill, Save, and Export operations responsive, with progress and cancellation. Publish a new image or destination file only after success. Preserve the previous image and existing output after cancellation, failure, or insufficient working space. Intel HEX and S-Record output must cover the full 32-bit range when the image does.
- Put Delete only in the existing Relocate memory segment dialog. It targets exactly one selected Editor Block and the original segment boundaries, independent of destination-field text. Confirm with start, end, and length; on confirmation close the dialog and commit one undoable deletion. Select the nearest following block, falling back to the previous block, without an automatic navigation. Preserve single-click block navigation and existing relocation behavior; add no block-list buttons or deletion of the read-only reference image.
- Apply the effective Light/Dark appearance to native title bars of all existing and new windows, including live Follow Windows changes. Make the selected indicator in appearance radio buttons visible in Dark.
- Compare current occupied bytes against the document's last saved image to determine edited-byte state. Added bytes count as edited; removed addresses have no Editor cell. Keep comparison difference state separate from edited state. Render a difference background, an edited outline, and an independent Find indicator when all are present.
- Persist separate Light and Dark highlight colors for unequal bytes, A-only bytes, B-only bytes, and edited bytes. Preferences provides swatches, a color picker, a palette selector independent of the active appearance, and Reset for the palette being edited. Apply changes only on Save; Cancel and X leave current colors unchanged.

## Testing Decisions

- Test observable behavior through the highest existing public seams: workspace/document and memory-image operations for Find, Fill, Delete, edited-byte state, Undo, and cancellation; public file operations for save/export and atomic failure behavior. Prefer these over tests of private storage shapes or event handlers.
- Use existing workspace, image-editing, and file-service acceptance tests as prior art for preserving occupied bytes, filling gaps, full-range address arithmetic, one-step Undo, save/export, and cancellation without partial output.
- Verify the logical full 32-bit fill through public operations and byte samples at the beginning, interior, and final address, including pattern alignment and Undo. Exercise streaming output and cancellation at representative boundaries without generating a multi-gigabyte file in every automated run.
- Exercise search over sparse images, source-record and displayed-row boundaries, address gaps, both Compare sides at one address, forward/backward wrap, changed images, and cancellation through public search/workspace behavior.
- Use WPF acceptance scenarios, following existing dialog, Compare interaction, and Preferences tests, for menu reachability, keyboard shortcuts, Find and Fill dialogs, Delete confirmation, title bars, dark radio indicators, highlight layering, color swatches, and Save/Cancel/Reset behavior.
- Assert visible and navigational outcomes rather than specific controls, storage types, brush keys, or implementation events. Check that dark colors make difference, edit, and search states distinguishable and that existing Light behavior remains readable.

## Out of Scope

- Searching raw Intel HEX or S-Record source records as text, or matching across absent addresses.
- Adding Jump or Delete buttons to block lists, changing single-click block navigation, or editing/deleting the Compare reference image.
- Supporting non-ASCII character encodings in ASCII Find.
- Showing a green Editor cell for a removed address.
- Applying unsaved color choices globally while Preferences remains open.
- Changing the Windows-only WPF platform decision.

## Further Notes

- The confirmed design decisions are recorded in the accompanying grill notes; the glossary defines the domain terms used here. ADR 0002 records the full-range storage constraint.
- A full-range fill contains exactly `2^32` occupied bytes and may produce an output file much larger than 4 GiB. The 50 MB warning is defined as more than 50,000,000 addresses, not 50 MiB, and is based on the inclusive interval length rather than the count of new bytes.
- Existing Fill Range currently rejects intervals longer than `int.MaxValue` and normalizes adjacent memory segments into one array. Chunking only the Fill loop would therefore be insufficient to meet the full-range story.
