# 02: Make Edit Row compact and match the active row width

**What to build:** Edit Row shows one readable line of the active tab's 8, 16, or 32 bytes per row, without doubled proposed-value text or unnecessary dialog space. Existing staged editing behavior remains intact.

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

- [ ] Opening Edit Row from Editor or the editable Compare panel uses that tab's current 8-, 16-, or 32-byte row width.
- [ ] Byte offsets, original HEX/ASCII, and proposed HEX/ASCII remain identifiable on one horizontal line; the row start and selected absolute address remain visible.
- [ ] Each proposed value appears once, with no original or placeholder text painted behind an input.
- [ ] The dialog uses compact readable cells and a content-sized height; it shows the full row at typical display scaling and uses horizontal scrolling only when the available work area cannot fit it legibly.
- [ ] One printable ASCII character and one or two hexadecimal digits remain the accepted input forms; invalid input, Cancel, and absent bytes preserve the base image and dirty state.
- [ ] A valid confirmation remains one reversible edit, with synchronized HEX/ASCII representations.
- [ ] Exercise row width and edit outcomes through the public workspace/editing seam and inspect the WPF dialog at all three widths.
