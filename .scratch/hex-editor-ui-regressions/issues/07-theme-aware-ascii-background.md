# 07: Distinguish ASCII data with a subtle theme-aware background

**What to build:** ASCII data has a slight background tint in Editor and both Compare panels that remains readable in Light and Dark and does not obscure changed-byte highlights.

**Blocked by:** 04: Render a readable Dark appearance throughout the app.

**Status:** ready-for-agent

- [ ] The ASCII data column is subtly distinct from Address and HEX Data in Editor and both Compare panels.
- [ ] The tint follows Light and Dark appearance without reducing text, focus, or selection contrast.
- [ ] Comparison-difference highlights remain visible on ASCII bytes; separator spaces remain neutral.
- [ ] The tint does not change memory-image values, byte alignment, row sizing, or hit testing.
- [ ] Inspect both themes and representative changed/unchanged ASCII bytes in WPF; retain existing comparison highlight tests.
