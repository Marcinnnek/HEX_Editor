# Copy Selection and Global Row Grid: Grill Notes

Status: agreed

## User request

- Ctrl+C should copy the selected byte or bytes to the system clipboard as uppercase `XX` tokens separated by spaces.
- A custom-width grid is globally aligned from address zero, including for Blocks that begin at a nonzero offset within a row.
- Compare appears correct for row alignment and needs verification.

## Verified code facts

- `HexImageViewModel.BuildRanges` floors each memory range relative to that range's own start, which does not provide global row alignment.
- `MemoryComparisonModel.BuildRanges` floors navigation addresses by the configured width, which appears to use global alignment. Verify rendered rows and sparse gaps before considering it complete.
- Editor and Compare already track byte and Shift+click range selection. The Editor's `ImageEditingSession.CopyRange` copies an internal sparse image for paste; the main-window `Copy range` command is an editing operation, not plain-text system clipboard copy.
- The root glossary defines an occupied byte and address gap. For clipboard text, the user chose to omit unoccupied addresses within the selected range.

## Settled decisions

- The global grid starts at address zero. For width `W`, row starts are integer multiples of `W`.
- A byte's cell is determined by its absolute address modulo `W`, even when its Block starts partway through a row.
- Preserve address gaps as visually blank cells; do not shift bytes left to make a Block start at a new row.
- Ctrl+C writes uppercase two-digit hex tokens separated by a single space. Do not include addresses, ASCII text, line breaks, or gap placeholders.
- When the selection spans an address gap, copy only occupied bytes, in ascending address order.
- In Compare, copy from the panel that owns the active byte/range selection.
- Verify Compare's row alignment against the same examples; current arithmetic floors absolute addresses by width and may need to use the confirmed regional origin instead.

## Alignment clarification

- The user confirmed that for width 17, a byte at `0xA0000002` belongs to row `0xA0000000`, and a byte at `0xA0001002` belongs to row `0xA0001001`.
- Therefore each 16 MiB address region has a fixed grid origin at its region start (lower 24 address bits zero). Rows advance by the configured width from that origin and do not reset for Blocks or source records within the region.
- In the region starting at zero, S-Record data at `0x0002`, `0x0012`, and `0x0022` uses the grid starting at `0x00000000`.
- This user-confirmed row-origin rule supersedes the earlier interpretation of strict numeric multiples of width across all 32-bit addresses.

## Not in scope

- Changing the existing internal copy/paste edit workflow unless needed to keep its command distinct from system clipboard copying.
- Adding mouse-drag range selection. Existing plain click selects one byte and Shift+click selects a range; the requested copy behavior applies to those selections.
