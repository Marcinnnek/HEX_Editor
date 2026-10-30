# 03: Make vertical HEX row spacing visibly effective

**What to build:** Saving a vertical spacing choice visibly changes the distance between data rows in Editor and both Compare panels, while byte selection and comparison alignment remain accurate.

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

- [ ] Changing vertical row spacing produces a visible change between Editor data rows without relying on a text-only value change in Preferences.
- [ ] Both Compare panels apply the same saved vertical spacing and corresponding absolute-address rows stay aligned.
- [ ] Changing vertical spacing leaves horizontal byte spacing unchanged, and changing horizontal spacing leaves vertical spacing unchanged.
- [ ] Saved spacing applies to already open views and survives an application restart.
- [ ] Clicks in HEX and ASCII cells still resolve to the intended occupied byte after spacing changes; gaps and headers are not treated as bytes.
- [ ] Sparse-row virtualization and scrolling remain responsive at the available spacing values.
- [ ] Verify public settings/layout behavior and inspect visible row distance, alignment, and hit testing in WPF.
