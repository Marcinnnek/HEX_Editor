# 09: Calculate and write checksums and CRC values

**What to build:** Calculate a checksum or CRC over a selected absolute-address range and optionally write the result to a chosen address through an explicit, reversible user action.

**Blocked by:** 06: Edit memory images and save changes.

**Status:** ready-for-agent

- [ ] Support additive checksums with 8-, 16-, or 32-bit width.
- [ ] Provide CRC-16/CCITT-FALSE and CRC-32/ISO-HDLC presets plus custom algorithm parameters, including polynomial, initialization, reflection, and final XOR.
- [ ] Require an explicit missing-address treatment; when filling holes, require the user to choose the fill byte.
- [ ] Display the result, width, parameters, and byte order used.
- [ ] Optionally write the result to a chosen address with explicit width and byte order, using undo/redo and modified-state tracking.
- [ ] Mark a previously displayed result stale when bytes in its input range change.
- [ ] Verify preset algorithms against published known vectors and verify range behavior with sparse data.
