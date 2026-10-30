# HEX Editor UI Polish

Status: resolved

## Problem Statement

Edit Row still takes extra vertical space, its byte values are not centered, and Tab moves down to the ASCII field before advancing to the next HEX byte. Entering another field does not select the existing value, so replacing a byte takes repeated deletion. The visible selected-byte label adds little value when the whole row is editable.

The Editor and Compare memory-segment lists show byte counts only in decimal. Their rows are too tall, and clickable headers allow sorting into an order that is less useful than ascending start address. A longer byte-count format also needs to remain readable in narrow Compare columns.

Preferences can reopen with a washed-out light appearance after switching Dark → Light → Dark, and other dialogs may not consistently follow the effective theme. At vertical HEX spacing 0, the visible gap between data lines is still about one and a half character heights, rather than a compact baseline.

## Solution

Make Edit Row a compact, content-height dialog whose byte columns are centered and keyboard navigation follows all editable HEX bytes before all editable ASCII bytes. Select an input's contents when focus first enters it, while allowing a later click to position the caret. Keep the initially selected byte focused and highlighted, but remove its redundant heading label.

Present memory segments in ascending start-address order in Editor and both Compare panels. Show each segment's byte count in hexadecimal and decimal, tighten row spacing, left-align values, and disable header sorting. Preserve the full count in narrow Compare panels by horizontal scrolling.

Make Preferences and every other dialog match the effective Light or Dark appearance, including already open dialogs when Follow Windows changes. Fix the Dark → Light → Dark sequence so Preferences reopens without a pale overlay. Make vertical HEX spacing 0 yield an approximately 0.2-character-height visible gap between lines in Editor and both Compare panels, with higher settings adding separation and byte hit testing remaining accurate.

## User Stories

1. As a firmware engineer, I want Edit Row to retain the active view's 8-, 16-, or 32-byte row width, so that its columns correspond to the data I was inspecting.
2. As a firmware engineer, I want all offsets centered in their byte columns, so that I can scan across the row quickly.
3. As a firmware engineer, I want original HEX values centered in their byte columns, so that they line up with proposed values.
4. As a firmware engineer, I want original ASCII values centered in their byte columns, so that they line up with proposed values.
5. As a firmware engineer, I want proposed HEX and ASCII values centered horizontally and vertically, so that their input boxes look consistent with the read-only rows.
6. As a firmware engineer, I want row labels left-aligned, so that each row remains easy to identify.
7. As a keyboard user, I want Tab to visit occupied Proposed HEX bytes in increasing offset order, so that I can edit a run of hexadecimal values without changing rows.
8. As a keyboard user, I want Tab to visit occupied Proposed ASCII bytes after the final HEX input, so that I can finish one representation before moving to the other.
9. As a keyboard user, I want Tab to skip unoccupied addresses, so that focus never stops on a byte that the memory image does not contain.
10. As a keyboard user, I want Shift+Tab to reverse the same sequence, so that I can correct a previous value predictably.
11. As a keyboard user, I want Tab after the final ASCII input to continue through the ordinary Windows focus order to OK, so that I can finish the dialog without a custom loop.
12. As a keyboard user, I want the full existing value selected when I Tab into a byte input, so that typing replaces it immediately.
13. As a mouse user, I want the full value selected when I first click into a different byte input, so that replacement is just as quick with a pointer.
14. As a mouse user, I want a later click in the already-focused field to place the caret, so that I can change just one hexadecimal digit.
15. As a firmware engineer, I want the selected byte to retain initial focus and highlighting, so that Edit Row opens at the byte I chose.
16. As a firmware engineer, I want the selected-byte heading removed, so that the compact dialog does not repeat context already shown by focus.
17. As a firmware engineer, I want Edit Row's height to fit its controls and remain fixed during normal use, so that I do not have to stretch the window to reach its actions.
18. As a user on a small or highly scaled display, I want emergency vertical and horizontal scrolling when needed, so that controls and byte values remain reachable without shrinking text excessively.
19. As a firmware engineer, I want Edit Row's existing validation, Cancel behavior, and single reversible commit preserved, so that layout changes do not alter memory-image editing semantics.
20. As a firmware engineer, I want every Editor and Compare memory-segment count shown like `0x15BC = 5564`, so that I can read the size in both hexadecimal and decimal.
21. As a firmware engineer, I want uppercase hexadecimal without unnecessary leading zeros and plain decimal digits, so that segment sizes use one predictable format.
22. As a firmware engineer, I want memory-segment rows separated by about half a character height, so that a list uses less vertical space without crowding its text.
23. As a firmware engineer, I want segment-list values left-aligned, so that start, end, and byte count can be compared down each column.
24. As a firmware engineer, I want segment lists ordered by increasing start address, so that the first entry is always the earliest occupied range.
25. As a firmware engineer, I want header clicks to leave segment order unchanged, so that an accidental click cannot rearrange the list.
26. As a firmware engineer, I want the same ordering after edits and Compare refreshes, so that list behavior remains stable while the memory image changes.
27. As a user with narrow Compare panels, I want to scroll horizontally to the full byte-count text on one line, so that no count is clipped or wrapped.
28. As a firmware engineer, I want segment selection and existing navigation to keep working after the list layout changes, so that compact rows remain useful.
29. As a user who chooses Light, I want Preferences and other dialogs to use a coherent Light appearance, so that they match the active application theme.
30. As a user who chooses Dark, I want Preferences and other dialogs to use a coherent Dark appearance, so that controls do not look washed out or mismatched.
31. As a user who switches Dark → Light → Dark, I want reopened Preferences to render Dark correctly, so that repeated theme changes do not leave stale visual state.
32. As a Follow Windows user, I want already open dialogs to update when Windows changes appearance, so that the entire visible application remains consistent without a restart.
33. As a user opening a dialog after a theme change, I want it to use the current effective theme, so that new windows do not use an earlier palette.
34. As a user of Preferences, I want sliders, radio buttons, text, backgrounds, disabled states, and focus to remain legible in both themes, so that the appearance controls are usable.
35. As a firmware engineer, I want vertical HEX spacing 0 to leave about 0.2 character height between visible data lines, so that Editor uses a genuinely compact baseline.
36. As a firmware engineer, I want both Compare panels to use the same compact baseline, so that corresponding absolute-address rows stay aligned.
37. As a firmware engineer, I want increasing vertical spacing to add a visible, monotonic gap, so that the setting remains meaningful above 0.
38. As a firmware engineer, I want HEX and ASCII clicks to keep selecting the intended occupied byte at every spacing value, so that denser rows do not make editing unsafe.
39. As a firmware engineer, I want horizontal byte spacing to remain independent of vertical row spacing, so that adjusting density in one direction does not change the other.
40. As a firmware engineer, I want saved spacing and appearance choices to remain active across open views and application restarts, so that this polish does not regress Preferences behavior.

## Implementation Decisions

- Retain the Windows-only WPF application and existing memory-image model. A *memory segment* remains a contiguous range of occupied addresses; this feature changes only presentation and interaction.
- Rework Edit Row's input order so all enabled HEX inputs precede all enabled ASCII inputs. Keep inputs for unoccupied addresses disabled and outside keyboard traversal. Preserve ordinary Windows focus behavior at the ends of the sequence.
- Select all text on first focus entry by keyboard or pointer, without preventing a subsequent click in the focused input from placing the caret. Keep the selected address internally for initial focus and visual emphasis, but remove its heading text.
- Center byte-column content while retaining left-aligned row headings. Compute a content-fitting dialog height within the available work area and prevent routine vertical resizing; allow scrolling only when the display cannot fit the controls.
- Share one byte-count presentation rule across Editor and Compare segment lists: `0x` prefix, uppercase unpadded hexadecimal, ` = `, then ungrouped decimal. Keep the numeric count in the model.
- Keep ascending start-address order from the memory-image model and disable interactive column sorting. Apply compact, left-aligned row presentation to all three segment lists without changing segment selection or navigation.
- Let a narrow Compare segment list scroll horizontally to show long values. Keep each byte count on one line and avoid truncation.
- Apply the effective application appearance consistently to all dialog surfaces and controls. Theme changes must update existing windows and newly created windows, including the Dark → Light → Dark sequence. Investigate native control styling and stale resources as possible causes, without assuming either is the root cause.
- Define vertical HEX spacing as added separation over a compact baseline. At 0, target an approximately 0.2-character-height visible text-line gap; higher settings increase separation. Apply the same calculation to Editor and both Compare panels and keep byte hit areas and horizontal spacing independent.
- Preserve existing Edit Row validation, staging, cancellation, undo behavior, Preferences persistence, and Compare base/reference editing rules.

## Testing Decisions

- Test observable behavior through the highest existing seam available. Use public editing-session, memory-image/list, comparison, and appearance/spacing operations for data and state; use WPF window scenarios for focus, layout, theme rendering, and hit testing. Avoid assertions on private handlers, exact implementation structure, or a particular control template.
- Extend existing Edit Row session tests for 8/16/32-byte rows, unoccupied addresses, validation, Cancel, and one reversible commit. Exercise a rendered Edit Row window to verify HEX-then-ASCII Tab order, reverse traversal, initial focus, select-all on first entry, caret placement on a second click, and normal movement to OK.
- Inspect rendered Edit Row at 8, 16, and 32 bytes per row, including a constrained work area. Check centered values, one horizontal row, content-fitting fixed height, and emergency scrolling without hidden actions.
- Verify the shared byte-count format with representative sizes, including one byte, a multi-digit count, and a long value. Exercise Editor and both Compare list displays to confirm left alignment, compact row distances, ascending start-address order, inert header clicks, and unchanged selection/navigation.
- Exercise narrow Compare panels at more than one width to confirm that full count text remains on one line and can be reached by horizontal scrolling.
- Extend appearance-session and Preferences tests for Dark → Light → Dark, manual modes, and Follow Windows updates. Inspect Preferences and representative other dialogs in rendered WPF for background, text, slider, radio-button, focus, and disabled-control contrast, both already open and newly opened after a theme change.
- Extend spacing settings and WPF row-layout tests to measure the *absolute* visible gap at 0 relative to character height, not merely the difference between two slider values. Verify monotonic increase, Editor/Compare alignment, independent horizontal spacing, and occupied-byte hit testing at compact and expanded values.
- Reuse the existing session, segment-list layout, appearance, and row-spacing test patterns as prior art. Run the full automated suite after implementation and perform focused visual acceptance at normal and high display scaling.

## Out of Scope

- Changing Intel HEX or Motorola S-Record import/export, memory-image occupancy, address, or segment-boundary semantics.
- Changing Edit Row's accepted HEX/ASCII input forms, adding bytes at unoccupied addresses, or changing its undo unit.
- Adding user sorting, filtering, grouping, or new metadata to memory-segment lists.
- New appearance modes, a redesigned Preferences information architecture, or theme customization controls.
- Replacing WPF, redesigning Compare synchronization, or changing executable packaging.
- Pixel-perfect matching to a particular screenshot or physical display; density targets are relative to rendered character height and must remain readable across DPI settings.

## Further Notes

- The user confirmed all interaction and layout decisions in `grill-notes.md`, including sparse-row Tab behavior, full count text in narrow Compare panels, and approximately 0.2-character-height spacing at 0.
- This spec supersedes the previous UI regression spec only where the user changed a UI decision: the visible **Selected byte** heading is removed while initial byte focus and highlighting remain.
- The existing memory-image model already supplies segments in ascending start-address order. The current segment lists bind a decimal count and allow default header sorting. The current zero vertical spacing removes added row margin but still leaves substantial row chrome. These are implementation facts and investigation leads, not prescribed fixes.
- The user approved public operations for editing, segment order, and settings, plus rendered WPF scenarios for keyboard/pointer navigation, layout, and appearance.
- Implementation is on `integration`; the final full test suite passed 167/167 after the code-review fixes. Representative rendered WPF acceptance was offscreen; no high-DPI screenshot was captured.
