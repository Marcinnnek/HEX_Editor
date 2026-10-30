# 03: Migrate Compare range operations

**What to build:** Make both Compare panels and difference navigation work with range-aware memory images while retaining current small-image comparison behavior. This is a separate migration batch of the expand–contract change.

**Blocked by:** 01: Expand memory image ranges.

**Status:** resolved

- [ ] The base and reference panels can read and display bytes from range-aware images without materializing the whole image.
- [ ] Unequal bytes, one-sided occupied bytes, address gaps, and difference counts remain correct near `0x00000000` and `0xFFFFFFFF`.
- [ ] Previous/Next Difference and Block selection remain responsive and preserve current navigation behavior for ordinary images.
- [ ] Existing public comparison and WPF interaction behavior remains usable before full-range Fill is enabled.
