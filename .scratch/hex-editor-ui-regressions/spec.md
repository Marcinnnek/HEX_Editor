Status: ready-for-agent

# HEX Editor UI Regression and Preferences Refinements

## Problem Statement

After the UI enhancements, Edit Row occupies too much space and displays doubled text behind editable values. Changing vertical HEX row spacing does not visibly change the grid. A single click on a data row in either Compare panel can make the application stop responding and then close, even with two ordinary HEX files. Dark and Follow Windows appearance modes do not render the interface correctly. The HEX grid spacing dialog lacks the expected window close control, Cancel action, and consistent styling. The Appearance menu also takes space in the main menu even though appearance belongs with the other preferences. The ASCII column needs a subtle visual distinction, and the Compare memory-segment lists are not positioned directly over their corresponding data grids in the reported layout.

## Solution

Make Edit Row a compact, readable dialog that shows one line of the active tab's 8, 16, or 32 bytes per row and never paints duplicate values behind the inputs. Let the dialog fit the available screen width by using compact cells; keep a horizontal scrollbar only for unusual display scaling or a work area too narrow for readable cells. Size its height to its content. Make vertical spacing visibly change the distance between data rows in both Editor and Compare while preserving byte alignment and hit testing.

Keep Compare as two side-by-side file columns, each with its memory-segment list immediately above its own HEX grid. A single click on a data row in either panel selects the intended address without a hang or application exit. Preserve safe double-click editing/read-only behavior and Previous/Next Difference navigation.

Move Follow Windows, Light, and Dark appearance choices from the main menu into Preferences beside horizontal and vertical spacing. Preferences uses the current theme and a standard close control. Save commits the staged choices; Cancel or the close control discards them. Light and Dark render the entire interface coherently, and Follow Windows updates the interface when the operating-system theme changes, without a restart. Give the ASCII data columns a subtle, theme-aware background that keeps comparison highlights legible.

## User Stories

1. As a firmware engineer, I want Edit Row to use the active tab's bytes-per-row setting, so that the dialog shows the same row width I am inspecting.
2. As a firmware engineer, I want 8-byte rows to appear as one line of eight byte cells, so that a short row stays compact.
3. As a firmware engineer, I want 16-byte rows to appear as one line of sixteen byte cells, so that I can compare each proposed value with its original value at a glance.
4. As a firmware engineer, I want 32-byte rows to appear as one line of thirty-two byte cells, so that Edit Row matches the main grid even at the widest setting.
5. As a firmware engineer, I want Edit Row cells to use only the width needed for readable HEX and ASCII values, so that the dialog does not waste horizontal space.
6. As a firmware engineer, I want Edit Row's height to follow its content, so that a large empty area does not obscure the workspace.
7. As a firmware engineer using a narrow or highly scaled display, I want horizontal scrolling only when all readable byte cells cannot fit, so that values never become illegible.
8. As a firmware engineer, I want each proposed HEX and ASCII value drawn once, so that no original or placeholder text looks like a second typed value.
9. As a firmware engineer, I want row start, selected byte address, zero-based offsets, original values, and proposed values to remain identifiable, so that the compact layout does not hide editing context.
10. As a firmware engineer, I want the existing one-character printable ASCII and one-or-two-digit hexadecimal validation to remain intact, so that compacting the dialog cannot change the meaning of an edit.
11. As a firmware engineer, I want Cancel or invalid input to leave my base image unchanged, so that a layout fix cannot introduce unintended edits.
12. As a firmware engineer, I want vertical spacing changes to visibly separate Editor data rows, so that the preference actually controls grid density.
13. As a firmware engineer, I want the same vertical spacing to apply to both Compare data panels, so that corresponding addresses remain aligned.
14. As a firmware engineer, I want horizontal and vertical spacing to remain independent, so that adjusting one does not silently change the other.
15. As a firmware engineer, I want spacing changes to preserve byte hit testing and selection, so that clicks still select the intended absolute address.
16. As a firmware engineer, I want my spacing choices to survive a restart, so that I do not have to reconfigure the grid.
17. As a firmware engineer, I want a single click on a data row in the left Compare panel to respond promptly, so that inspecting a base image cannot freeze or close the application.
18. As a firmware engineer, I want a single click on a data row in the right Compare panel to respond promptly, so that inspecting the reference image is equally safe.
19. As a firmware engineer, I want Compare selection and scrolling to stay aligned by absolute address after a click, so that the two panels remain comparable.
20. As a firmware engineer, I want the left-panel double-click Edit Row action and the right-panel read-only feedback to remain safe, so that the single-click repair does not break established behavior.
21. As a firmware engineer, I want Previous and Next Difference to keep working after selecting a row in either panel, so that I can continue navigating differences.
22. As a firmware engineer, I want a recoverable Compare interaction failure reported in the status area and diagnostics without losing my tabs or edits, so that I can continue working.
23. As a firmware engineer, I want each Compare file's memory-segment list directly above its own HEX data, so that I can tell which segments belong to which file.
24. As a firmware engineer, I want the two Compare columns to use their available width at normal and narrow window sizes, so that segment lists do not cluster in one half of the workspace.
25. As a firmware engineer, I want a subtle background on the ASCII data column in Editor, so that ASCII remains visually distinct from HEX values.
26. As a firmware engineer, I want the same ASCII distinction in both Compare panels, so that the two views remain consistent.
27. As a firmware engineer, I want ASCII shading and difference highlights to remain distinguishable in both light and dark themes, so that changed bytes remain easy to identify.
28. As a firmware engineer, I want Appearance choices inside Preferences, so that display settings are found in one place and the main menu is simpler.
29. As a firmware engineer, I want the main Appearance menu removed after the move, so that it does not duplicate Preferences.
30. As a firmware engineer, I want Preferences to show Follow Windows, Light, and Dark alongside grid spacing, so that I can review all display choices before saving.
31. As a firmware engineer, I want Save to apply and persist the staged Preferences choices together, so that a deliberate confirmation controls changes to my display.
32. As a firmware engineer, I want Cancel or the title-bar close control to discard staged Preferences changes, so that opening the dialog is harmless.
33. As a firmware engineer, I want Preferences to have a visible standard window close control and styling consistent with the active theme, so that it behaves like the rest of the Windows application.
34. As a firmware engineer, I want Dark mode to provide readable text, controls, grids, dialogs, status, focus, and selection throughout the application, so that I can work comfortably in a dark desktop environment.
35. As a firmware engineer, I want Follow Windows to use the current operating-system appearance and react to later changes without restarting, so that the application stays in step with the desktop.
36. As a firmware engineer, I want a manual Light or Dark choice to persist across restarts until I select another mode, so that my override remains predictable.

## Implementation Decisions

- Keep the Windows-only .NET 10 WPF and MVVM architecture and existing memory-image semantics. This follow-up changes interaction, layout, and appearance rather than stored firmware data.
- Use the active Editor or Compare tab's bytes-per-row value when opening Edit Row. Keep one horizontal line of byte columns and the existing zero-based offsets, original values, proposed values, selected absolute address, validation, and atomic commit behavior.
- Remove the duplicate proposed-value visual elements behind the editable fields. Size byte columns from readable text and input chrome, cap the dialog to the available work area, and allow horizontal scrolling only when the full line cannot fit without sacrificing legibility. Derive dialog height from content.
- Apply vertical spacing at the row container/layout level so changing it produces visible separation between rendered data rows, including in both Compare panels. Reuse one spacing setting for corresponding panels and keep rows aligned. Recalculate hit-test geometry when spacing changes.
- Keep horizontal byte spacing independent of vertical row spacing and persist both per user. Apply a saved change to already open Editor and Compare views.
- Preserve sparse row virtualization. Do not resolve a click or synchronize panels by enumerating an entire memory image or materializing address gaps.
- Treat a single data-row click in either Compare panel as a first-class interaction. Make selection, address navigation, and scroll synchronization bounded and safe under reentrant WPF events. Retain the established left base image editing and right reference image read-only rules for double-clicks.
- Contain recoverable Compare click, double-click, and difference-navigation failures at UI command boundaries; report operation, panel, and address or selection when available in status and error-only diagnostics. An error must not close the application or discard open documents.
- Give Compare's segment-list area the same two-column ownership and available width as the data area. Each list stays immediately above the corresponding file's grid while resizing.
- Add a subtle theme-aware background to ASCII data cells or their column in Editor and both Compare panels. Preserve readable text, focus/selection, and higher-priority per-byte difference highlights; separator spaces remain neutral.
- Move the existing appearance modes into Preferences and remove their former main-menu entry. Stage appearance and spacing in the dialog; Save applies and persists them, while Cancel or title-bar close discards them. Provide a standard visible close control and controls styled consistently with the active theme.
- Make Light, Dark, and Follow Windows affect all application surfaces, including custom HEX/ASCII drawing, dialogs, menus, tabs, and status. Follow Windows subscribes to operating-system appearance changes while active and updates without restart; a manual override remains in force until changed.

## Testing Decisions

- Prefer tests of observable behavior through existing public workspace, document, comparison-model, editing-session, settings, and view-model operations. Avoid assertions against private event handlers, exact control names, or theme resource structure. The user previously accepted this seam for edit and navigation behavior, with WPF acceptance scenarios for layout, window controls, and Preferences.
- Extend existing Edit Row and workspace tests to cover opening with 8, 16, and 32 bytes per row; correct offsets, valid input, invalid input, cancellation, and one atomic edit. Review the actual WPF dialog at each width for compact sizing, readability, and absence of doubled values.
- Exercise vertical spacing settings through persistence and public layout behavior, then verify in a running WPF application that visible Editor and both Compare row distances change after Save and remain aligned. Check horizontal spacing independence and byte hit testing.
- Reproduce the reported single-click failure in both Compare panels with the two named HEX files, without editing or saving those source files. Verify that the tab remains responsive, the process stays open, and existing base-image state is intact. Use smaller synthetic sparse and differing memory images for repeatable regression tests at the highest practical interaction seam.
- Exercise selection, scrolling, left and right double-clicks, and Previous/Next Difference after the single-click fix. Assert the active address and paired rows rather than internal event order; verify status and diagnostics for a recoverable failure.
- Inspect Compare at normal and narrow window widths to verify that each memory-segment list sits above its own data panel and uses the column width. Retain existing sparse-row and comparison-alignment tests as prior art.
- Inspect Light, Dark, and Follow Windows in a running WPF application, including Preferences, Edit Row, Compare, menus, focus, selection, status, and ASCII/difference colors. Switch the operating-system theme while Follow Windows is selected and verify that the app updates without restart. Restart to verify a manual override persists.
- Verify Preferences staging: changing appearance and both spacing sliders then pressing Cancel or X leaves current and persisted values unchanged; Save applies them together. Verify a visible standard title-bar close control.
- Run the existing full automated suite after the changes, and perform focused WPF acceptance on the reported interaction and visual regressions.

## Out of Scope

- Changing Intel HEX or Motorola S-Record parsing, writing, checksum, or address semantics.
- Editing the read-only reference image from Compare.
- Replacing the Windows WPF framework or redesigning document ownership and Undo/Redo.
- Adding new appearance modes, fonts, color customization controls, or pixel-perfect reproduction of the screenshots.
- Changing the self-contained executable packaging requirements, except for publishing a build for acceptance of these UI fixes.

## Further Notes

- This spec follows the earlier HEX Editor UI and Workflow Enhancements spec and retains its agreed editing, comparison, and release behavior unless refined here.
- The reported Compare reproduction uses `CMSIS-DAP-STLINK21.hex` as the base image and `STM32-DEMO.hex` as the reference image. A single click on a data row in either panel is sufficient to cause a hang followed by application exit in the user's current build. The exact failure mechanism has not yet been established.
- The Edit Row screenshot shows editable fields overlapping proposed-value text. Current code places both in the same visual cells; the fix should remove that overlap rather than obscure it with color or opacity.
- The reported Compare screenshot shows the segment lists clustered toward the left instead of spanning the corresponding left and right data columns. The two-column relationship was already agreed in the earlier spec.
- The supported testing split was confirmed by the user in the earlier specification cycle: public workspace/model operations for editing and navigation, and WPF acceptance scenarios for visual layout and preferences.
