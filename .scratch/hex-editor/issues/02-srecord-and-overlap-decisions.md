# 02: S-Record import and overlap decisions

**What to build:** Open Motorola S-Record images alongside Intel HEX, detect the format from file contents, and make overlapping input data visible before it is applied. The user can choose whether incoming bytes replace earlier or existing bytes, retain them, or cancel the import.

**Blocked by:** 01: Sparse memory and Intel HEX import.

**Status:** ready-for-agent

- [ ] Open supported Motorola S-Record types and address widths, including common S-record extensions.
- [ ] Detect Intel HEX or S-Record from content rather than relying only on the filename extension.
- [ ] Validate S-Record syntax, byte counts, addresses, checksums, and termination records, with line-numbered actionable diagnostics.
- [ ] Summarize overlaps both between records in one file and when importing into an existing image; include identical and differing byte counts and affected address ranges.
- [ ] Ask for a decision even when every overlapping value is identical; offer keep existing, overwrite with incoming, or cancel.
- [ ] For overlapping records within one file, keeping retains the earlier record and overwriting selects the later record.
- [ ] Import all non-overlapping bytes regardless of the overlap choice.
- [ ] Verify that import results preserve absolute addresses and do not silently replace conflicting values.
