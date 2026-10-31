# Responsive HEX Grid Columns and Row Selection

Status: ready-for-agent

## Problem Statement

The Address, HEX Data, and ASCII columns leave too much unused space or place text against a column edge. Address values are not centered. The small-image DataGrid and large-image windowed layout use different sizing rules, so the same image can look different depending on its size. Existing byte hit testing leaves dead space between HEX bytes, while the windowed layout has no row-only selection when a user clicks an address or the outer margin.

## Solution

Use the same measured, stable column widths and six-unit outer insets in Editor and both Compare panels, in both small- and large-image layouts. Center ordinary addresses and the Address header. Size HEX Data and ASCII from the active 8/16/32 Bytes per row setting while retaining full-size, single-line text and horizontal scrolling when necessary. Make inter-byte spaces behave as part of the neighboring byte cells and make outer-margin and Address clicks select a row without moving the viewport. Preserve full address-gap descriptions through tooltips.

## User Stories

1. As an Editor user, I want the Address column to fit a 32-bit address with a small inset, so that addresses remain readable without wasted space.
2. As an Editor user, I want address values centered in their column, so that the address gutter looks consistent.
3. As an Editor user, I want the Address header centered above the addresses, so that the header describes the visible data clearly.
4. As a Compare user, I want the same Address width and alignment in both panels, so that corresponding rows are easy to scan.
5. As a user with a larger display font, I want Address width to grow with the measured text, so that eight-digit addresses remain complete.
6. As a user scrolling through an image, I want column widths to remain stable, so that shorter final rows and address gaps do not move column boundaries.
7. As a user viewing 8 bytes per row, I want HEX Data and ASCII to fit that row capacity, so that large empty column areas disappear.
8. As a user viewing 16 bytes per row, I want the columns sized for 16 bytes, so that the layout uses space efficiently.
9. As a user viewing 32 bytes per row, I want the columns sized for all 32 bytes, so that data remains on one line at its normal text size.
10. As a user changing Bytes per row, I want HEX Data and ASCII widths to update immediately, so that the grid reflects the new setting.
11. As a user changing horizontal byte spacing, I want HEX Data width to update immediately, so that the final byte is not clipped and unused width does not accumulate.
12. As a user changing row width or byte spacing while scrolled horizontally, I want the view to return to the left while keeping its vertical position, so that the Address column is visible again.
13. As a user in a narrow Editor window, I want horizontal scrolling instead of compressed or wrapped bytes, so that the byte grid stays legible.
14. As a Compare user in a narrow window, I want each panel to scroll horizontally within its own column, so that both images remain independently inspectable.
15. As a user, I want a small inset on both sides of Address, HEX Data, and ASCII, so that text and highlights do not touch neighboring columns or the table edge.
16. As a user, I want HEX Data and ASCII headers to align with the beginning of their data, so that the column boundaries are clear.
17. As a user, I want the subtle ASCII background to cover its whole column including the insets, so that it reads as one continuous area.
18. As a user who hides ASCII, I want its column to collapse completely, so that hidden content does not reserve space.
19. As a user clicking just beside a HEX byte glyph within its cell, I want that byte selected, so that minor pointer imprecision does not miss the target.
20. As a user clicking between two HEX bytes, I want the midpoint to decide which neighboring byte is selected, so that there is no dead zone between byte cells.
21. As a user clicking an outer column inset, I want to select the row rather than the nearest byte, so that I can distinguish row selection from byte selection.
22. As a user clicking the Address column, I want the same row selection behavior as an outer inset, so that the address gutter is useful for navigation.
23. As an Editor user selecting an occupied row, I want that row highlighted and the current address set to its start, so that I know which row is active.
24. As a Compare user selecting an occupied row, I want the matching row highlighted in both panels and the current address set to its start, so that both sides remain aligned without jumping.
25. As a user selecting a row, I want the previous byte selection cleared and the viewport to stay still, so that only one current selection is shown.
26. As a user clicking an address-gap row, I want only that row highlighted, so that the gap is identifiable without implying an occupied byte exists.
27. As a user clicking an address-gap row, I want the current address unchanged and Edit Row to stay closed, so that gap selection cannot be mistaken for data editing.
28. As a user inspecting a long address-gap description, I want the shortened text left-aligned with the complete description in a tooltip, so that a compact Address column does not hide information.
29. As a user switching between small and large images, I want the same widths, alignment, margins, hit targets, and selection behavior, so that the grid remains predictable.

## Implementation Decisions

- Apply one column-sizing rule to the shared HEX grid used by Editor and both Compare panels. The DataGrid and windowed layouts must expose the same observable widths and alignment.
- Measure the eight hexadecimal digits `FFFFFFFF` with the active address font. Address width equals that measured width plus 12 WPF device-independent units: six units of inset on each side. There is no 110-unit minimum. The width is stable while scrolling and recomputed when font metrics change.
- Compute HEX Data and ASCII widths for the active 8, 16, or 32 bytes per row, using their rendered byte geometry plus six-unit insets on both sides. A short final row and address gaps do not alter those widths. Inter-byte spacing remains controlled by the existing spacing setting.
- Align ordinary address values and the Address header to the center. Align HEX Data and ASCII headers with the start of the data after the left inset. Keep address-gap descriptions left-aligned, ellipsized when necessary, and available in full through a tooltip.
- Keep the current address representation and normal text size. Keep each logical data row on one line. When the combined widths exceed the viewport, expose horizontal scrolling within that Editor or Compare panel. Do not allow manual DataGrid column resizing.
- Recalculate data-column widths when Bytes per row or horizontal byte spacing changes. Reset only horizontal scroll to the left; preserve the current vertical position. Hiding ASCII collapses its width. The subtle ASCII background includes the insets.
- Treat the space between adjacent HEX byte glyph cells as part of the byte grid. The midpoint divides the hit area: the left half selects the left byte, and the right half selects the right byte. A click just outside a glyph but within that byte's cell still selects it.
- Outer column insets are outside all byte hit areas. An inset or Address click selects the row. On an occupied row, clear the prior byte selection, highlight that row, set the current address to its start, and do not scroll. Compare highlights the corresponding row on both sides.
- An address-gap row may be highlighted through the same row click, but it does not select a byte, change the current address, or open Edit Row.
- Keep existing byte editing, Find highlighting, difference and edited-byte colors, and Compare scrolling behavior intact while adding these row-selection visuals.

## Testing Decisions

- Test observable behavior rather than checking private layout calculations or exact control-tree implementation. The user confirmed two existing levels: WPF acceptance scenarios for rendered widths, scrolling, tooltip and selection behavior; byte-grid geometry tests for precise hit boundaries.
- Extend the existing Editor and Compare WPF acceptance scenarios. Check small DataGrid and large windowed views at 8, 16, and 32 bytes per row; include a short final row, an address gap, ASCII shown and hidden, a narrow viewport, and a larger font. Assert stable column widths, text visibility, alignment, and six-unit insets with a small rendering tolerance.
- Drive horizontal scrolling in Editor and both Compare panels. Changing Bytes per row or byte spacing must recalculate widths, return horizontal offset to zero, and leave the first visible vertical row unchanged.
- Drive clicks in ordinary byte cells, both sides of an inter-byte midpoint, outer insets, Address cells, and address-gap rows. Assert the visible row highlight, current address, cleared prior byte selection, synchronized Compare row highlight, and lack of scroll or Edit Row activation where specified.
- Test gap-description truncation and full tooltip text, and verify the ASCII background spans its insets in both light and dark themes.
- Use the existing Find and scrolling acceptance scenarios as regression coverage, particularly their checks that wide rows expose horizontal scrolling and that byte clicks do not move the viewport. Use the existing grid-spacing tests as prior art for deterministic byte hit geometry.

## Out of Scope

- Changes to memory-image data, 32-bit address semantics, file parsing, Block boundaries, comparison differences, or editing rules.
- Changes to the Edit Row dialog, application menus, vertical row spacing, font-size preferences, or theme colors.
- Wrapping a data row, shrinking byte text to fit a narrow panel, or preserving manual column-width overrides.

## Further Notes

- The initial suggestion of a 110-unit Address width was superseded by the confirmed measured-text-plus-12 rule, without a minimum.
- The glossary's term **address gap** applies to unoccupied ranges; selecting a gap row must not make it appear occupied. No new glossary term or ADR is required for this presentation change.
- The agreed interview decisions and current-code observations are recorded in the companion grill notes.
