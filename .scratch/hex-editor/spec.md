Status: ready-for-agent

# HEX / S-Record Compare and Editor

## Problem Statement

Firmware images stored as Intel HEX and Motorola S-Record files describe bytes at absolute addresses, but ordinary text or binary file tools present them as sequential records or byte offsets. That makes sparse regions, missing bytes, address alignment, and safe comparison or editing difficult to reason about. Users need a Windows desktop tool that preserves address occupancy and contents while inspecting, comparing, editing, merging, and exporting these images.

## Solution

Build a Windows-only .NET 10 desktop application using WPF and MVVM. It will import Intel HEX and Motorola S-Record files into a format-independent sparse memory image, operate on absolute addresses, and export valid files in either format. The first implementation phase establishes the memory model and format readers/writers; later phases add the virtualized editor, comparison, editing, merge, and checksum tools.

## User Stories

1. As a firmware engineer, I want to open Intel HEX and Motorola S-Record files, so that I can inspect firmware images in one application.
2. As a firmware engineer, I want file format detection to inspect file contents, so that a misleading extension does not select the wrong parser.
3. As a firmware engineer, I want invalid records reported with a line number and reason, so that I can locate and fix damaged input files.
4. As a firmware engineer, I want import to validate record syntax, byte counts, address widths, extended addresses, and checksums, so that malformed data is not accepted silently.
5. As a firmware engineer, I want original path, format, and format metadata tracked separately from the memory image, so that I can save or export with the right context.
6. As a firmware engineer, I want overlaps within an input file and overlaps with an existing image to be summarized, so that duplicate or conflicting data is visible before it is applied.
7. As a firmware engineer, I want to choose whether to keep existing data, overwrite it with incoming data, or cancel an overlapping import, so that no byte is silently replaced; this prompt must also appear when overlapping values are identical.
8. As a firmware engineer, I want non-overlapping bytes imported regardless of the overlap choice, so that valid new address ranges are not discarded unnecessarily.
9. As a firmware engineer, I want each byte associated with its absolute 32-bit address, so that data at `0xA0000000` remains at `0xA0000000` instead of being treated as offset zero.
10. As a firmware engineer, I want a memory image to distinguish an absent address from stored `00` and `FF` bytes, so that sparse data is preserved accurately.
11. As a firmware engineer, I want contiguous occupied addresses grouped into memory segments, so that actual blocks can be listed and navigated.
12. As a firmware engineer, I want address lookup, insertion, deletion, splitting, joining, and relocation to operate without allocating intervening gaps, so that distant address ranges remain practical to handle.
13. As a firmware engineer, I want overflow and supported-address-range checks, so that edits and relocations cannot wrap addresses or corrupt data.
14. As a firmware engineer, I want a single-file view with Address, HEX Data, and ASCII columns, so that I can inspect memory in familiar forms.
15. As a firmware engineer, I want to select 8, 16, or 32 bytes per row and show or hide ASCII, so that I can tune the view to the task and display size.
16. As a firmware engineer, I want absent bytes to appear as blank positions and non-printable stored bytes as a consistent placeholder, so that missing data is distinct from stored values.
17. As a firmware engineer, I want to select bytes or address ranges and navigate directly to an absolute address, so that I can reach regions without scrolling through gaps.
18. As a firmware engineer, I want long address gaps to collapse to a marker while short gaps remain aligned as blank positions, so that sparse images stay readable without materializing empty rows.
19. As a firmware engineer, I want a list of occupied memory segments with start address, end address, and byte count, so that I can inspect and navigate the image structure.
20. As a firmware engineer, I want to compare two images in synchronized side-by-side panels, so that corresponding absolute addresses stay aligned even when files have different sizes or start addresses.
21. As a firmware engineer, I want each compare panel to show Address, HEX Data, and ASCII, with a shared ASCII toggle and row-width setting, so that both images remain visually consistent.
22. As a firmware engineer, I want each byte classified as Equal, Different, OnlyA, or OnlyB and highlighted individually, so that changed and missing data are easy to identify.
23. As a firmware engineer, I want missing bytes to stay blank at their actual addresses and look different from unequal bytes, so that later bytes never shift to fill a gap.
24. As a firmware engineer, I want to jump to the next or previous difference, select an address range, and click a byte to locate the same address in the other panel, so that I can investigate changes efficiently.
25. As a firmware engineer, I want a Differences Only view to collapse identical regions and large address gaps, so that I can focus on meaningful changes in sparse images.
26. As a firmware engineer, I want comparison results to update after edits, so that I do not need to reopen either image.
27. As a firmware engineer, I want to edit a byte and cut, copy, paste, delete, or fill address ranges, so that I can make controlled firmware changes.
28. As a firmware engineer, I want copy and paste to preserve sparse offsets and holes, so that bytes retain their relative address positions.
29. As a firmware engineer, I want deletion to remove occupancy without shifting later addresses, so that memory operations preserve absolute locations.
30. As a firmware engineer, I want insert and replace operations to use explicit target addresses and overlap choices, so that data placement is deliberate.
31. As a firmware engineer, I want to relocate a segment while preserving relative offsets and checking address overflow and destination collisions, so that I can move firmware blocks safely.
32. As a firmware engineer, I want undo and redo for data-changing operations and a visible modified state, so that edits remain recoverable until saved.
33. As a firmware engineer, I want to preview a merge before applying it, so that I can review combined address ranges and overlaps.
34. As a firmware engineer, I want to merge non-overlapping bytes automatically and choose which image to keep for overlapping ranges, so that conflicts are resolved explicitly.
35. As a firmware engineer, I want merge choices to apply to a defined address range and support undo, so that I can resolve local conflicts without reopening the files.
36. As a firmware engineer, I want additive checksum, CRC-16/CCITT-FALSE, CRC-32/ISO-HDLC, and configurable CRC calculations over an absolute-address range, so that I can validate firmware contents.
37. As a firmware engineer, I want CRC parameters, width, byte order, and missing-address treatment to be explicit, so that a result is reproducible and unambiguous.
38. As a firmware engineer, I want calculations to require an explicit action and optionally write the result to a chosen address, so that checksum fields are never changed unexpectedly.
39. As a firmware engineer, I want displayed checksum results marked stale when input bytes change, so that I do not rely on an outdated value.
40. As a firmware engineer, I want Intel HEX and S-Record record checksums regenerated on export, so that edits do not require maintaining serialized checksums in memory.
41. As a firmware engineer, I want Save and Save As to preserve the image and format, use safe file replacement, and warn before overwriting or closing unsaved work, so that file operations do not silently discard data.
42. As a firmware engineer, I want to export either supported format with 8, 16, or 32 data bytes per record, so that record layout can match downstream tools without changing memory contents.
43. As a firmware engineer, I want export to preserve occupied addresses and optionally select blocks or ranges, so that gaps are not implicitly filled and only intended data is written.
44. As a firmware engineer, I want exported files re-imported and compared in automated round-trip tests, so that serialization preserves the address-to-byte mapping.
45. As a firmware engineer, I want the UI to show current address, selection length, image format, modified state, and comparison status, so that the current editing context is clear.

## Implementation Decisions

- Use a format-independent `MemoryImage` containing sorted, non-overlapping memory segments. Missing addresses remain absent; there is no dense allocation spanning gaps.
- Support absolute addresses from `0x00000000` through `0xFFFFFFFF`. Do not impose an artificial occupied-data size cap; typical input images are up to 10 MB, and performance should be improved and measured on representative images.
- Use separate Core, Formats, Compare, Tools, UI, and Tests projects. Keep format parsers and comparison logic independent of WPF controls.
- Use WPF with MVVM on Windows only. A shared custom virtualized HEX view should serve the single-image editor and both compare panels.
- Detect Intel HEX and Motorola S-Record from content; support common file extensions, Intel HEX extended addressing, and applicable S-Record widths.
- Validate syntax, byte counts, address ranges, and record checksums. Return actionable diagnostics with source line and reason.
- Surface all overlaps, including identical values. Summarize overlaps once per operation and let the user keep existing values, overwrite with incoming values, or cancel. Import non-overlapping bytes either way. Within one file, keeping retains the earlier record and overwriting selects the later record. Merge preview additionally supports a default choice and per-address-range overrides.
- Derive segment boundaries from occupied addresses, independently of source record boundaries. Check address arithmetic for overflow.
- Display absolute-address rows with 8, 16, or 32 bytes. Keep holes blank within a row; collapse gaps spanning more than one row to a marker. Use a shared ASCII toggle in compare mode and allocate hidden ASCII space to HEX data.
- Compare by absolute address and represent each byte as Equal, Different, OnlyA, or OnlyB. Highlight byte states individually and refresh results after edits.
- Preserve sparse offsets through internal copy/paste. Deleting removes occupancy and never shifts subsequent addresses.
- Keep edits in memory until saved. Use undo/redo, explicit overlap policy for insertion or replacement, and safe save/replace behavior.
- Offer additive checksums at 8, 16, and 32 bits, CRC-16/CCITT-FALSE, CRC-32/ISO-HDLC, and custom CRC parameters. Require explicit user action to calculate or write a value; specify fill byte for holes and byte order/width for writing. Mark affected results stale after data changes.
- Export Intel HEX and Motorola S-Record with configurable maximum payload of 8, 16, or 32 bytes. Preserve holes and occupied addresses, create required control/termination records, and reject unsupported ranges with clear errors.
- Deliver in four milestones: (1) sparse memory model and file formats; (2) virtualized viewer and compare; (3) editing and save; (4) merge, checksum/CRC, selected-range export, and performance improvements.

## Testing Decisions

- Test external behavior through public module interfaces rather than private implementation details.
- Use one high-level file-workflow seam for import and export: open an Intel HEX or S-Record stream into a memory image, inspect occupied addresses and diagnostics, export it, then re-import and compare the address-to-byte mapping. This is the agreed integration seam for phase 1.
- Test the memory model through public operations for sparse lookup, segment boundaries, insertion, deletion, splitting/joining, relocation, and overflow.
- Test format readers with valid records, malformed syntax, bad counts/checksums, extended addresses, supported S-Record widths, duplicates, and overlaps; assert diagnostics include line and reason.
- Test writers and round trips across supported address widths and 8/16/32-byte payload settings, including sparse gaps and high addresses.
- Later phases should test comparison states/alignment, edit and undo behavior, range merge choices, checksum known vectors and gap handling, and UI-visible navigation/virtualization behavior.
- There is no existing code or test suite in the repository, so there is no prior test pattern to reuse.

## Out of Scope

- Direct ECU flashing or live target-memory access.
- Automatic discovery of firmware-specific checksum locations or signature algorithms.
- Proprietary firmware containers beyond Intel HEX and Motorola S-Record.
- Disassembly or CPU-instruction-aware diffing.
- Linux or macOS support.
- User-defined memory-map naming and memory-map file import in the initial version.

## Further Notes

- The common expected file size is up to 10 MB. The design should not cap occupied bytes arbitrarily; performance acceptance thresholds and target hardware will be determined through later benchmarking.
- The initial delivery is phase 1. The full application scope remains the four milestones above.
- The application is Windows-only and uses .NET 10, WPF, and MVVM as recorded in ADR-0001.
