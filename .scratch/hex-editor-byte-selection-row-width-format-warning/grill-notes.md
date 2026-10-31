# Byte Selection, Row Width, and Format Warning: Grill Notes

Status: agreed

## User request

- Clicking a byte should select that byte, while clicking Address continues to select the row.
- A byte should respond to clicks within its grid cell, not just on the rendered glyphs.
- Preferences should store a default number of bytes per row.
- Besides presets 8, 16, and 32, the application should accept a custom row width up to 128 bytes.
- A recognized filename extension that disagrees with the detected Intel HEX or Motorola S-Record content should produce a message box.

## Verified code facts

- `HexViewControl` is shared by Editor and both Compare panels. A byte click emits an address range and clears row selection in Editor, but `HexByteRowControl` has no visual state for the selected byte or range.
- `ByteGridLayout.TryGetByteIndexAt` uses the glyph line height as its vertical hit boundary. Additional vertical row spacing is outside byte hit targets. HEX inter-byte spacing is divided between neighboring bytes; ASCII cells have no inter-byte spacing. Outer column insets select a row.
- Editor, Compare, Edit Row, and HEX column width calculations currently constrain the display row width to 8, 16, or 32. The Compare difference-row calculation uses power-of-two bit masking; other row mapping uses integer division.
- Editor and Compare each expose preset row widths. Preferences currently stages appearance, grid spacing, and highlight colors; it does not store a default row width.
- The import service detects Intel HEX from `:` records and S-Record from `S` records, independent of the filename extension. The Open picker recognizes `.hex`, `.ihx`, `.s19`, `.s28`, `.s37`, `.srec`, and `.mot`; no mismatch warning is currently shown.
- The current Open flow asks about unsaved changes before loading and only replaces the Editor image after a successful import. Compare and Merge have separate image-loading paths.

## Settled decisions (round 1)

- A byte click highlights exactly that byte in both its HEX and ASCII representations, sets the current address, and clears a prior whole-row highlight. Shift+click keeps range selection.
- A byte's complete rectangular grid cell is clickable, including the space around glyphs within its row height. The gap between rows remains noninteractive. Outer column margins still select the row.
- Preferences stores a default bytes-per-row value for new views; views already open retain their current setting until changed manually.
- Custom row widths are all integers from 1 through 128, alongside quick presets 8, 16, and 32, in Editor and Compare.
- After parsing a mismatched file successfully, show its filename, expected format, and detected format with Open anyway and Cancel. Cancel leaves the existing image unchanged.
- Check Open, Compare, and Merge for known extensions: `.hex` and `.ihx` imply Intel HEX; `.s19`, `.s28`, `.s37`, `.srec`, and `.mot` imply Motorola S-Record. Unknown extensions do not warn.

## Settled decisions (round 2)

- In Compare, a byte click selects the byte only in the clicked panel. The other panel retains its difference highlighting without a new byte selection.
- Byte or range selection uses a distinct visible outline while preserving difference background, edit outline, and Find indicator.
- Shift+click visibly selects occupied bytes throughout the range in both HEX and ASCII. Unoccupied addresses have no selected cells, and clicking one empty address does not select a byte.
- A successful Open resets the existing Editor view to the saved default row width. Already-open Compare views keep their own widths.
- Any row width from 1 through 128 uses global alignment: row starts are multiples of the selected width, including near gaps and the end of the 32-bit address space. The last row can be partial.
- Custom input accepts only decimal digits representing values 1 through 128. Temporary empty input is resolved in round 3.
- Changing row width preserves the current address and byte/range selection, and scrolls to the new row containing the selection start.
- For Open, retain the existing save-changes prompt before loading. After a successful parse, show the mismatch warning before replacing the image. Cancel retains the old image; a previously completed Save remains saved.
- After opening a mismatched file, every subsequent ordinary Save warns again before writing.

## Settled decisions (round 3)

- A numeric row-width field offers presets 8, 16, and 32 and accepts decimal digits only. It never commits a value outside 1–128. Temporary empty input is allowed while editing; Enter or blur restores the last valid value if left empty.
- Every ordinary Save of a file whose known extension conflicts with its detected content format shows Save anyway and Cancel. Cancel retains unsaved document changes and leaves the file untouched.
- Save As and Export also warn before writing when a chosen output format conflicts with a known destination extension.
- Canceling a mismatch warning during Compare or Merge aborts the entire new operation and leaves existing documents unchanged.
- At 128 bytes per row, Edit Row wraps the bytes over multiple visual lines rather than keeping one very wide line.

## Settled decisions (round 4)

- Edit Row keeps widths 1–32 on one visual line. At widths 33–128, it divides the row into consecutive groups of at most 32 bytes, each with its own offsets and HEX/ASCII values. For example, 80 bytes uses groups of 32, 32, and 16.
- Within each Edit Row group, Tab moves through its occupied HEX fields, then its occupied ASCII fields, before moving to the next group. Shift+Tab reverses that order. This supersedes the proposal to traverse every HEX field across all groups before ASCII.
- Edit Row grows to fit its groups up to the available screen height, using vertical scrolling only when needed.
- The main Editor and Compare grids keep each 1–128-byte display row on one line and use horizontal scrolling where the row is wider than the panel. Only Edit Row wraps.

## Final confirmation

The user confirmed the complete scope of byte-cell selection, default and custom 1–128-byte display rows, Edit Row wrapping, and extension/content mismatch warnings for reads and writes. The settled decisions above supersede earlier grid-column notes where they differ.

## Acceptance examples

- Clicking occupied byte `FF` anywhere inside its cell selects that address and draws a selection outline in HEX and ASCII; clicking Address selects the row instead. Clicking a blank address or the gap between rows does not select a byte.
- In Compare, clicking a byte in panel B selects it only in B. A difference background, edit outline, and Find marker remain visible. Shift+click selects occupied bytes in the chosen range without drawing selected cells in address gaps.
- With a custom width of 3, row starts follow `0, 3, 6…`; a byte at `0xFFFFFFFF` is supported in a partial last row. Changing from 16 to 3 keeps the current selected address in view.
- A saved default of 24 applies to a newly opened Editor image and a newly created Compare view. Existing Compare views keep their current widths. The Editor and Compare grids show one row per line and scroll horizontally when required.
- An 80-byte Edit Row displays three groups of 32, 32, and 16. Tab traverses editable HEX fields then editable ASCII fields within each group before the next group. The dialog scrolls vertically only if the groups exceed the available screen height.
- Opening an S-Record file named `firmware.hex` shows the expected and detected formats and offers Open anyway or Cancel. Cancel leaves the previous Editor image intact. The equivalent warning applies to new Compare and Merge inputs.
- After opening that file, each ordinary Save warns again. Save anyway writes S-Record to the existing path; Cancel leaves the document modified and the file untouched. Save As and Export warn before writing a known extension that conflicts with the chosen output format.

## Domain documentation decision

These rules concern display rows, selection, preferences, and filename checks around the existing memory-image formats. They introduce no new memory-image domain term, so `GLOSSARY.md` needs no change. No ADR is warranted: the decisions are user-visible and reversible without a surprising architectural trade-off.

