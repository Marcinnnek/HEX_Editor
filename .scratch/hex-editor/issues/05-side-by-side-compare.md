# 05: Side-by-side memory comparison

**What to build:** Compare two imported images in synchronized panels that align every byte by absolute address and show differences without shifting data across missing positions.

**Blocked by:**
- 02: S-Record import and overlap decisions.
- 04: Virtualized single-image editor view.

**Status:** ready-for-agent

- [ ] Show File A and File B in side-by-side panels, each with Address, HEX Data, and ASCII columns.
- [ ] Synchronize the address axis, vertical navigation, row width (8/16/32 bytes), and shared Show ASCII toggle.
- [ ] Keep corresponding bytes aligned at the same absolute address when images have different starts, sizes, or sparse gaps.
- [ ] Classify each address as Equal, Different, OnlyA, or OnlyB and highlight individual byte states; style missing values distinctly from unequal values.
- [ ] Keep absent bytes blank in their address positions and do not shift subsequent bytes.
- [ ] Navigate to an arbitrary address and the next or previous difference; clicking either panel identifies the same address in the other panel, including absent bytes.
- [ ] Offer All Regions, Differences Only, and selected-address-range views; collapse identical regions and large gaps without generating empty rows.
- [ ] Refresh comparison results after either image is edited.
