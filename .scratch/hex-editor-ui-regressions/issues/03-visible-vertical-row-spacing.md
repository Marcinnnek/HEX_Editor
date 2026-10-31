# 03: Make vertical HEX row spacing visibly effective

**What to build:** Saving a vertical spacing choice visibly changes the distance between data rows in Editor and both Compare panels, while byte selection and comparison alignment remain accurate.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] Changing vertical row spacing produces a visible change between Editor data rows without relying on a text-only value change in Preferences.
- [x] Both Compare panels apply the same saved vertical spacing and corresponding absolute-address rows stay aligned.
- [x] Changing vertical spacing leaves horizontal byte spacing unchanged, and changing horizontal spacing leaves vertical spacing unchanged.
- [x] Saved spacing applies to already open views and survives an application restart.
- [x] Clicks in HEX and ASCII cells still resolve to the intended occupied byte after spacing changes; gaps and headers are not treated as bytes.
- [x] Sparse-row virtualization and scrolling remain responsive at the available spacing values.
- [x] Verify public settings/layout behavior and inspect visible row distance, alignment, and hit testing in WPF.

## Answer

The saved vertical spacing now changes the measured and rendered gap between HEX rows in Editor and both Compare panels. WPF layout verification confirmed an eight-pixel change and aligned Compare rows; layout and hit-testing tests passed.
