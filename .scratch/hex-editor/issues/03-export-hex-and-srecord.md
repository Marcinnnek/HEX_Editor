# 03: Export Intel HEX and S-Record images

**What to build:** Export a loaded memory image as Intel HEX or Motorola S-Record with a user-selected maximum record payload. Export must preserve the address-to-byte mapping and produce files that can be imported again without loss.

**Blocked by:** 02: S-Record import and overlap decisions.

**Status:** ready-for-agent

- [ ] Export to Intel HEX with required extended-address records and an end-of-file record.
- [ ] Export to S-Record using address-width-compatible data record types and an appropriate termination record.
- [ ] Support maximum data payload sizes of 8, 16, or 32 bytes per record.
- [ ] Generate valid serialized record checksums during export; do not alter internal segment boundaries or fill address gaps.
- [ ] Reject unsupported output address ranges with a clear error.
- [ ] Re-import exported files and verify that their address-to-byte mapping matches the source image for both formats and all payload sizes.
