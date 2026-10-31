# HEX Grid Columns: Grill Notes

Status: agreed

## Current request

- Keep the Address column width stable while scrolling, measure it from an eight-digit 32-bit address, center its addresses, and leave a small margin around the text.
- Size HEX Data and ASCII to their byte content, with a small margin so text does not touch neighboring columns or the table edge.

## Verified code facts

- `HexViewControl` supplies the grid in Editor and both Compare panels. It uses a DataGrid for smaller images and a separate windowed layout for large images.
- The DataGrid Address column is 100 device-independent pixels wide; the windowed Address column is also 100. Address text is currently left-aligned with no inset.
- The DataGrid HEX column sizes to cells but has a 360-pixel minimum; its ASCII column is fixed at 220 pixels. The windowed layout uses Auto columns with matching 360- and 220-pixel minimums.
- `HexByteRowControl` measures its HEX and ASCII content from the number of cells. The minimum and fixed column widths can therefore leave unused space for 8- and 16-byte rows.
- ASCII can be hidden. Both layouts support horizontal scrolling when the row is wider than the viewport. Each Compare panel has its own horizontal scroll area.
- Address-gap descriptions currently share the Address column and can be truncated there.
- The glossary defines an address gap, but no new domain term is needed for this presentation change. No ADR addresses this UI layout.

## Settled decisions (round 1)

- Apply the layout in Editor and both Compare panels, including the large-image windowed layout.
- Determine HEX Data and ASCII widths from the active Bytes per row setting (8, 16, or 32). Keep widths stable while scrolling past shorter final rows or address gaps.
- Center the Address header over the address values. Align HEX Data and ASCII headers with the beginning of their data.

## Settled decisions (round 2)

- Add approximately 6 WPF device-independent pixels of empty space on each side of all three columns. Existing inter-byte spacing remains a separate setting.
- Keep full-size text and one line per row; use each view's horizontal scrollbar when 32 bytes do not fit.
- Keep address-gap descriptions in Address, left-aligned and ellipsized, with the complete description available in a tooltip. Ordinary addresses remain centered.
- Disable manual DataGrid column resizing so its measured layout matches the large-image view.
- The user refined “fixed Address width”: it should stay stable while rows scroll, but derive from the width of an eight-digit 32-bit address rather than be a hard-coded constant. The later round resolved this as text width plus 12 units, without a 110-unit minimum.

## Settled decisions (round 3)

- Address width is the measured width of eight hexadecimal digits (`FFFFFFFF`) plus 12 WPF units for the two 6-unit insets. There is no 110-unit minimum. The measured width stays stable while scrolling and may grow with the font.
- Recalculate HEX Data and ASCII widths immediately when Bytes per row or byte spacing changes. Reset horizontal scroll to the left while preserving the vertical position.
- Extend the subtle ASCII background across the whole ASCII column, including its outer insets.
- Clicking just outside a glyph but within that byte's grid cell selects the byte. Clicking a column's outer margin selects only the row, without choosing an adjacent byte.

## Verified hit-testing facts

- `ByteGridLayout` currently excludes HEX inter-byte spacing from byte hit boxes, so clicks in that spacing do not select a byte.
- The small Compare DataGrid has full-row selection; the small Editor DataGrid has cell selection. The large windowed view has no row-selection state, so row highlighting is new behavior there.

## Settled decisions (round 4)

- Split the gap between adjacent HEX byte cells at its midpoint. Clicking either half selects the corresponding byte, including whitespace near the glyph; the outer margin remains outside all byte hit boxes.
- Clicking an outer margin on an occupied data row should highlight the row and set the current address to its start, without scrolling. In Compare, highlight the corresponding row in both panels.
- Clicking an address-gap row only highlights that row, without selecting a byte or opening Edit Row. The later round resolved that the current address remains unchanged.

## Settled decisions (round 5)

- An outer-margin click on an occupied row clears the previous byte selection, highlights one row, and sets the current address to that row's start without moving the viewport. In Compare, highlight the matching row in both panels.
- An outer-margin click on an address-gap row highlights the gap row but leaves the current address unchanged. It does not select a byte or open Edit Row.
- Clicking the Address column follows the same row-selection rule as clicking an outer margin.

## Final confirmation

The user confirmed the full scope across Editor and both Compare panels, in both DataGrid and large-image layouts. The latest decisions above supersede any earlier open questions in this interview.

## Acceptance examples

- At 8 bytes per row, the HEX and ASCII columns shrink to their configured byte capacity plus the agreed insets; a shorter final row does not change their widths.
- At 32 bytes per row in a narrow Compare panel, the row stays on one line and its own horizontal scrollbar reveals the rest. Changing Bytes per row or byte spacing recalculates widths, returns horizontal scroll to the left, and preserves vertical position.
- Clicking 1 pixel into either half of an inter-byte gap selects the corresponding byte. Clicking the outer inset or Address selects the row; for an occupied row it sets the current address to the row start without scrolling, while a gap row leaves the current address unchanged.
- Editor highlights the selected row; Compare highlights the corresponding row in both panels. A new row-only selection clears the previous byte selection.
- A long address-gap description is left-aligned and ellipsized in Address, with its full text available by tooltip. Ordinary addresses and the Address header are centered.

## Domain documentation decision

- These are grid presentation and interaction rules, not new memory-image terms. `GLOSSARY.md` therefore needs no new entry.
- No ADR is warranted: the layout and hit-testing rules are straightforward to revise and do not represent a surprising, hard-to-reverse architectural trade-off.
