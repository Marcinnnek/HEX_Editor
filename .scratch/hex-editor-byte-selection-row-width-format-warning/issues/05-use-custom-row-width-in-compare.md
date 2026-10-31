# 05: Use a custom 1–128-byte row width in Compare

**What to build:** Both Compare panels support a shared custom display width from 1 through 128, including sparse images and difference navigation at non-power-of-two widths.

**Blocked by:** 04 — Use a custom 1–128-byte row width in Editor.

**Status:** resolved

- [ ] Compare keeps the 8, 16, and 32 presets and offers the same decimal-only 1–128 entry and invalid-input behavior as Editor.
- [ ] Both panels use globally aligned row starts, show corresponding address gaps and a partial final row correctly, and remain synchronized while scrolling.
- [ ] Differences-only rows and Previous/Next Difference navigation identify the correct rows for widths such as 3 and 24; no power-of-two assumption remains in visible behavior.
- [ ] Changing width preserves the active address, selected panel, and byte or range selection and keeps the selected row in view.
- [ ] Rows remain single-line with each panel's horizontal scrollbar, including at width 128.
- [ ] Public comparison-model and WPF acceptance scenarios cover non-power-of-two widths, both panels, sparse addresses, and navigation.
