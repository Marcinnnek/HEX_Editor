# 10: Export selected address ranges

**What to build:** Export only selected occupied segments or an explicit address range to Intel HEX or Motorola S-Record, preserving all selected absolute addresses and leaving holes unfilled.

**Blocked by:**
- 03: Export Intel HEX and S-Record images.
- 04: Virtualized single-image editor view.

**Status:** resolved

- [x] Select one or more occupied segments or specify an absolute-address range for export.
- [x] Export the selected data in Intel HEX or S-Record with an 8, 16, or 32 byte maximum data payload per record.
- [x] Preserve occupied addresses and byte values and do not serialize missing addresses as implicit fill bytes.
- [x] Reject unsupported output ranges with actionable errors.
- [x] Re-import exported ranges and verify that only the selected address-to-byte mapping is present.

## Comments

- Added public export operations for selected segment indexes and inclusive absolute-address ranges. Range export clips occupied segments at the requested bounds and keeps holes absent; empty selections and reversed ranges report actionable errors.
- Added export controls for Intel HEX or Motorola S-Record, 8/16/32-byte record payloads, multiple selected segments, and explicit hexadecimal start/end bounds.
- Added public-service round-trip coverage for selected segments and clipped ranges in both formats, plus a MainViewModel workflow check that exports and re-imports only the chosen segment.
- Verification after merging the current integration branch: `dotnet test tests/HexEditor.Tests/HexEditor.Tests.csproj -m:1 -nodeReuse:false -v:minimal` passed (30 tests); `dotnet build HexEditor.sln --no-restore -m:1 -nodeReuse:false -v:minimal` passed with 0 warnings and 0 errors.
