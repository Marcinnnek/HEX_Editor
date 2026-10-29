# 02: S-Record import and overlap decisions

**What to build:** Open Motorola S-Record images alongside Intel HEX, detect the format from file contents, and make overlapping input data visible before it is applied. The user can choose whether incoming bytes replace earlier or existing bytes, retain them, or cancel the import.

**Blocked by:** 01: Sparse memory and Intel HEX import.

**Status:** resolved

- [x] Open supported Motorola S-Record types and address widths, including common S-record extensions.
- [x] Detect Intel HEX or S-Record from content rather than relying only on the filename extension.
- [x] Validate S-Record syntax, byte counts, addresses, checksums, and termination records, with line-numbered actionable diagnostics.
- [x] Summarize overlaps both between records in one file and when importing into an existing image; include identical and differing byte counts and affected address ranges.
- [x] Ask for a decision even when every overlapping value is identical; offer keep existing, overwrite with incoming, or cancel.
- [x] For overlapping records within one file, keeping retains the earlier record and overwriting selects the later record.
- [x] Import all non-overlapping bytes regardless of the overlap choice.
- [x] Verify that import results preserve absolute addresses and do not silently replace conflicting values.

## Comments

- Implemented S0-S3 data records, S5-S6 count records, and matching S7-S9 termination records, including 16-, 24-, and 32-bit address widths.
- Added content-based Intel HEX/S-Record detection and line-numbered S-Record validation errors.
- Added a prepared import preview with address ranges and identical/different overlap counts. The WPF prompt asks for keep, overwrite, or cancel even when overlap values are identical. Keep retains the first incoming record and existing bytes; overwrite uses the last incoming record. Cancel leaves the current image unchanged.
- Applied imports through a cursor merge over segments so occupied bytes are not expanded into per-byte tree entries.
- Verification: `dotnet test tests/HexEditor.Tests/HexEditor.Tests.csproj --no-restore -m:1 -nodeReuse:false` passed (11 tests); `dotnet build HexEditor.sln --no-restore -m:1 -nodeReuse:false` passed with 0 warnings and 0 errors.
