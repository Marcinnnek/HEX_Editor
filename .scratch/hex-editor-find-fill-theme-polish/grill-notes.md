# Find, Fill, and Appearance Polish — Design Interview

Status: confirmed by the user

## Confirmed decisions

- Remove the main toolbar's Go-to-address field and Go button. Keep Go to address in the View menu and preserve Ctrl+G.
- Remove the More button entirely. All commands it exposed remain reachable through menus, including at the minimum window width. This supersedes the More overflow requirement in the earlier UI enhancement spec.
- Label the new search command **Find bytes**. It searches byte sequences in the memory image, independent of Intel HEX or S-Record source-record boundaries.
- **Get fill all region** in Fill Range selects the inclusive address interval from the image's lowest occupied address to its highest occupied address. This interval includes address gaps between memory segments.
- **Keep existing data** writes the fill pattern only at unoccupied addresses. **Overwrite existing data** writes the pattern at every address in the range. Each fill is one Undo operation.
- Keep Ctrl+G for the Go-to-address dialog after removing the toolbar controls.
- Find bytes works in Editor and in both images of an active Compare tab. HEX patterns accept contiguous or space-separated complete byte pairs, case-insensitively. ASCII patterns use case-sensitive 7-bit ASCII. Matches may cross source-record boundaries but stop at address gaps.
- Align repeating fill patterns to the range's start address even when Keep existing data skips occupied addresses.
- Default Fill Range to Keep existing data when the dialog opens.
- Show the address count and warn before a large fill interval is applied; allow cancellation without changing the image.
- Do not add action buttons to the Editor or Compare block lists. Keep single-click navigation to the block start, so no separate Jump button is needed. Put Delete Block only in the existing Relocate memory segment dialog, reached from the Editor's current relocation command or block double-click. Compare keeps click-to-jump and a read-only reference image.
- Native title bars follow the effective Light/Dark theme in every open and newly opened application window, including live Follow Windows changes.
- Preferences has separate Light and Dark colors for unequal bytes, bytes present only in A, bytes present only in B, and edited bytes, plus Reset to defaults.
- An edited byte is an occupied byte whose value differs from the image's last saved version; a newly added occupied byte qualifies. Save or Undo that restores the saved value removes the edited highlight. Removed addresses have no green Editor cell.
- If an editable Compare byte is both edited and different from the reference, show the difference color as the background and the edited color as an outline.
- Preferences uses color swatches and a picker. Color changes affect the application only after Save; Cancel and X discard them. Reset restores defaults for the theme currently being edited.
- Find bytes has a nonmodal window with a pattern field, HEX/ASCII choice, Find Next, Find Previous, and Close. Navigation starts after/before the selected byte, or at the active image's start/end without selection, and wraps around. Only the current match is highlighted and its address/panel is shown in the Find window.
- In Compare, a match at the same address in A and B counts as two results, A before B; identify the matching panel and keep the views synchronized.
- Delete Block acts on exactly one selected Editor block; disable it for multiple selections. Preserve the existing single-click jump action.
- When a nonmodal Find window remains open across tab switches or image loads, subsequent navigation follows the newly active tab and clears the old match. Add Ctrl+F, F3, and Shift+F3 alongside View > Find bytes.
- Keep separate HEX and ASCII drafts in Find. Invalid patterns or no match produce inline feedback without moving the current selection.
- Confirm Delete Block with its start, end, and length. Cancel leaves the image unchanged; deletion is one Undo operation.
- A current Find match gets an independent visual indicator so that a difference background and edited-byte outline remain visible together.
- Preferences offers a separate Light/Dark palette selector that does not itself change the application's active theme.
- Show a fill-size warning above 50 MB, with cancellation before data changes. Long Fill, Save, and Export operations show progress, support cancellation, and avoid partial image or output changes. Disk-backed working storage is acceptable if needed; insufficient space produces a clear error without changing existing data.
- A manually entered Fill Range from `0x00000000` to `0xFFFFFFFF` must produce 4,294,967,296 occupied bytes, which remain saveable/exportable and undoable as one operation. See ADR 0002 for the storage constraint.
- Warn when the inclusive Fill Range contains more than 50,000,000 addresses, regardless of how many are already occupied.
- Closing Find removes its temporary match indicator but leaves the view and selection at the match. In Compare, Find results are ordered by `(address, panel A before B)` and wrap in that order.
- After Delete Block, select the nearest following block, or the preceding one if the last block was deleted, without jumping the view. Get fill all region is unavailable for an empty image; manual range entry stays available.
- Find scans remain responsive even for a fully occupied 4 GiB image, show progress, and can be cancelled without moving the current selection.
- Give bytes present only in A and bytes present only in B separate configurable colors in each theme.
- Highlight the complete current Find match across row boundaries in both HEX and ASCII columns.
- Preserve the last Find pattern and mode after closing its window so F3/Shift+F3 continue the search. Ctrl+F opens or focuses a single Find window.
- Any in-place image change invalidates the current Find indicator; subsequent navigation searches the current bytes.
- Delete in the Relocate memory segment dialog acts on the original selected block regardless of uncommitted destination-address text. After confirmation it deletes the block, closes the dialog, and remains undoable as one operation.
- Restore a visible selected indicator in Preferences' appearance radio buttons under Dark; keep the Light appearance readable.

## Existing behavior relevant to the design

- Fill Range currently overwrites occupied bytes and materializes a contiguous byte array for the entire selected interval. Very large sparse intervals can therefore require substantial memory, and intervals longer than `int.MaxValue` are rejected.
- Editor block selection already jumps to the block start, and double-click opens relocation. The Compare reference image is read-only.
- The current comparison palette distinguishes unequal bytes and bytes present in only one image. Editor bytes have no edited-byte highlight state yet.
- The application's WPF window background follows its theme, but the native Windows title bar does not currently receive an explicit dark/light setting.

## Open design branches

None.
