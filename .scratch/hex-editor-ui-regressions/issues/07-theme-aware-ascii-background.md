# 07: Distinguish ASCII data with a subtle theme-aware background

**What to build:** ASCII data has a slight background tint in Editor and both Compare panels that remains readable in Light and Dark and does not obscure changed-byte highlights.

**Blocked by:** 04: Render a readable Dark appearance throughout the app.

**Status:** resolved

- [x] The ASCII data column is subtly distinct from Address and HEX Data in Editor and both Compare panels.
- [x] The tint follows Light and Dark appearance without reducing text, focus, or selection contrast.
- [x] Comparison-difference highlights remain visible on ASCII bytes; separator spaces remain neutral.
- [x] The tint does not change memory-image values, byte alignment, row sizing, or hit testing.
- [x] Inspect both themes and representative changed/unchanged ASCII bytes in WPF; retain existing comparison highlight tests.

## Answer

The custom renderer now paints a subtle theme-aware ASCII background in Editor and both Compare panels. Byte highlights are drawn above the tint and separator spaces remain outside their highlight bounds. Rendering and comparison highlight tests passed.
