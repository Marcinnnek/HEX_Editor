# 03: Export Intel HEX and S-Record images

**What to build:** Export a loaded memory image as Intel HEX or Motorola S-Record with a user-selected maximum record payload. Export must preserve the address-to-byte mapping and produce files that can be imported again without loss.

**Blocked by:** 02: S-Record import and overlap decisions.

**Status:** resolved

- [x] Export to Intel HEX with required extended-address records and an end-of-file record.
- [x] Export to S-Record using address-width-compatible data record types and an appropriate termination record.
- [x] Support maximum data payload sizes of 8, 16, or 32 bytes per record.
- [x] Generate valid serialized record checksums during export; do not alter internal segment boundaries or fill address gaps.
- [x] Reject unsupported output address ranges with a clear error.
- [x] Re-import exported files and verify that their address-to-byte mapping matches the source image for both formats and all payload sizes.

## Comments

- Added public `MemoryImageFileService.Export` for Intel HEX and Motorola S-Record, with payload selection limited to 8, 16, or 32 bytes and clear argument errors for unsupported options.
- Intel HEX export splits records at 64 KiB boundaries, emits extended linear address records as needed, regenerates two's-complement checksums, and writes EOF. S-Record export chooses S1/S2/S3 and matching S9/S8/S7 records from the highest occupied address, regenerates one's-complement checksums, and writes the matching termination record.
- Export iterates occupied segments and does not write bytes into address gaps. Both formats represent the complete 32-bit address range supported by `MemoryImage` (Intel HEX extended linear addressing and S3 records), so there is no valid image address outside the supported output range; the memory model already rejects segments extending beyond `0xFFFFFFFF`.
- Added public-service round-trip coverage for both formats and all payload settings, including sparse segments, the Intel HEX 64 KiB boundary, and address `0xFFFFFFFF`. Re-import verifies generated checksums and address mapping.
- Verification: `dotnet test tests/HexEditor.Tests/HexEditor.Tests.csproj --no-restore -m:1 -nodeReuse:false` passed (12 tests); `dotnet build HexEditor.sln --no-restore -m:1 -nodeReuse:false` passed with 0 warnings and 0 errors.
