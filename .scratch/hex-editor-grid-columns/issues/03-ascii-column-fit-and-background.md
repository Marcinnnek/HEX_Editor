# 03: ASCII column fit and background

**What to build:** In Editor and both Compare panels, ASCII fits the active row capacity with compact insets and a continuous subtle background, while remaining readable and scrollable in narrow views.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] ASCII width fits 8, 16, or 32 characters plus six WPF units on each side, independent of shorter final rows and address gaps.
- [x] The ASCII header aligns with the first character after the left inset, and the subtle ASCII background covers the complete column including both insets in light and dark themes.
- [x] Changing Bytes per row updates width immediately, resets horizontal scroll to the left, and preserves the vertical row.
- [x] Hiding ASCII collapses the column completely. When shown in a narrow view, text remains full-size on one line and is reachable through horizontal scrolling.
- [x] Editor and both Compare panels behave the same in DataGrid and windowed views; manual DataGrid resizing cannot override the computed width.
- [x] WPF acceptance coverage checks widths, visibility, background continuity, and overflow.
