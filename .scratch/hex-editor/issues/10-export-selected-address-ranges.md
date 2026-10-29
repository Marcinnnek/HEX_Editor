# 10: Export selected address ranges

**What to build:** Export only selected occupied segments or an explicit address range to Intel HEX or Motorola S-Record, preserving all selected absolute addresses and leaving holes unfilled.

**Blocked by:**
- 03: Export Intel HEX and S-Record images.
- 04: Virtualized single-image editor view.

**Status:** ready-for-agent

- [ ] Select one or more occupied segments or specify an absolute-address range for export.
- [ ] Export the selected data in Intel HEX or S-Record with an 8, 16, or 32 byte maximum data payload per record.
- [ ] Preserve occupied addresses and byte values and do not serialize missing addresses as implicit fill bytes.
- [ ] Reject unsupported output ranges with actionable errors.
- [ ] Re-import exported ranges and verify that only the selected address-to-byte mapping is present.
