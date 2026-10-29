# 09: Calculate and write checksums and CRC values

**What to build:** Calculate a checksum or CRC over a selected absolute-address range and optionally write the result to a chosen address through an explicit, reversible user action.

**Blocked by:** 06: Edit memory images and save changes.

**Status:** resolved

- [x] Support additive checksums with 8-, 16-, or 32-bit width.
- [x] Provide CRC-16/CCITT-FALSE and CRC-32/ISO-HDLC presets plus custom algorithm parameters, including polynomial, initialization, reflection, and final XOR.
- [x] Require an explicit missing-address treatment; when filling holes, require the user to choose the fill byte.
- [x] Display the result, width, parameters, and byte order used.
- [x] Optionally write the result to a chosen address with explicit width and byte order, using undo/redo and modified-state tracking.
- [x] Mark a previously displayed result stale when bytes in its input range change.
- [x] Verify preset algorithms against published known vectors and verify range behavior with sparse data.

## Comments

- Implemented the public `HexEditor.Tools.ChecksumCalculator` seam, including additive 8/16/32-bit sums, the two CRC presets, configurable 8/16/32-bit CRC parameters, explicit sparse-hole treatment, and byte-order-aware output formatting.
- Added calculate/write controls to the editor. Writes use the existing edit history and modified-state tracking; changed covered bytes mark the displayed calculation stale and stale values cannot be written.
- Verified CRC presets against the `123456789` check values (`0x29B1` and `0xCBF43926`), plus sparse filling, custom CRC parameters, byte order, stale tracking, and undo/redo.
- Validation: `dotnet test HexEditor.sln --no-restore` passed (32 tests); Release solution build passed with zero warnings and zero errors.
