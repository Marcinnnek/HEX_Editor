# 04: Virtualized single-image editor view

**What to build:** Browse an imported memory image in a responsive virtualized HEX view that presents absolute addresses and occupied bytes without materializing long gaps.

**Blocked by:** 02: S-Record import and overlap decisions.

**Status:** ready-for-agent

- [ ] Display Address, HEX Data, and ASCII columns using the image's absolute addresses.
- [ ] Support 8, 16, or 32 bytes per row and a Show ASCII option.
- [ ] Render printable bytes as characters, non-printable bytes with one consistent placeholder, and missing addresses as blank positions distinct from `00` and `FF`.
- [ ] Virtualize rows so layout work is limited to visible rows and a small buffer.
- [ ] Collapse gaps spanning more than one displayed row to a marker showing the skipped address range; keep shorter gaps aligned as blank positions.
- [ ] Navigate directly to an absolute address and use the occupied-segment list to jump to a segment start.
- [ ] Keep rendering and navigation practical for typical images up to 10 MB and widely separated address ranges without dense allocation.
