# 17: Layer Find with byte highlights

**What to build:** Keep the current Find match visible when its bytes are also comparison differences or edited bytes.

**Blocked by:** 09: Find bytes in Editor; 10: Find bytes in Compare; 16: Layer edits and differences in Compare.

**Status:** resolved

- [x] The complete current match has a separate visible indicator in HEX and visible ASCII, including across displayed rows.
- [x] In Compare, a byte can simultaneously show its difference background, edited outline, and Find indicator without losing any state.
- [x] Only the current match receives the Find indicator; closing Find removes it while leaving the underlying difference/edit colors and navigation intact.
- [x] The same layering remains readable in Light and Dark, including with user-selected highlight colors.
