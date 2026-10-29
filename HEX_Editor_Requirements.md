# HEX / S-Record Compare & Editor — Requirements Specification

**Document status:** Initial requirements baseline  
**Target platform:** Windows desktop  
**Proposed technology:** C# / .NET 10 / WPF / MVVM  
**Supported input/output formats:** Intel HEX and Motorola S-Record

## 1. Purpose and Scope

Build a Windows desktop application for inspecting, comparing, merging, editing, and exporting address-based firmware memory images stored in Intel HEX or Motorola S-Record files.

The application shall treat files as **sparse address spaces**, not as continuous byte streams or sequences of text records. All comparisons and editing operations shall use the actual memory addresses encoded in the files.

## 2. Core Design Principles

- **Address-based model:** Every byte is associated with its absolute memory address. For example, data beginning at `0xA0000000` is displayed and compared at `0xA0000000`, not offset `0x00000000`.
- **Sparse memory:** Missing addresses are not automatically filled with `0x00` or `0xFF` and shall remain distinguishable from explicitly stored bytes with these values.
- **Format-independent representation:** Importers convert Intel HEX and S-Record data into a common in-memory representation; exporters serialize that representation into the selected format.
- **Non-destructive workflow:** Edits remain in memory until saved; support undo/redo and clear warnings for unsaved changes.
- **Scalable rendering:** The UI shall virtualize rows and avoid allocating memory for gaps between distant address ranges.

## 3. Functional Requirements

### FR-001 — Open and Import Files

1. The application shall open Intel HEX files and Motorola S-Record files, including common extensions such as `.hex`, `.ihx`, `.s19`, `.s28`, `.s37`, `.srec`, and `.mot`.
2. Format detection shall be based on file contents, not solely on the extension.
3. The importer shall validate record syntax, byte count, addresses, and record checksums.
4. The importer shall support Intel HEX extended address records and applicable S-Record address widths.
5. Invalid or unsupported records shall produce actionable diagnostics with the source line number and reason.
6. Duplicate or overlapping address ranges shall be detected. Import behavior shall be explicit (reject, or use a documented conflict policy); conflicting bytes must not be silently overwritten.
7. The original file path, input format, and relevant format metadata shall be tracked separately from the memory contents.

### FR-002 — Memory Representation and Blocks

1. The application shall represent data as sorted, non-overlapping memory segments containing a start address and bytes.
2. Segments may be separated by arbitrarily large address gaps without allocating memory for those gaps.
3. The application shall support address lookup, segment insertion, deletion, splitting, joining, and relocation.
4. Segment boundaries displayed in the UI shall be derived from actual occupied address ranges; source record boundaries shall not dictate UI block boundaries.
5. Address computations shall perform overflow and supported-address-range checks.

### FR-003 — Single-File HEX Editor

1. The single-file editor shall display **Address | HEX Data | ASCII** columns.
2. The number of displayed bytes per row shall be configurable: **8, 16, or 32**.
3. The ASCII column shall be independently showable/hideable through a display option.
4. Printable ASCII characters shall be shown as characters; non-printable bytes shall be rendered using a consistent placeholder (for example `.`).
5. Users shall be able to select individual bytes or address ranges and navigate to a specific absolute address.
6. Rendering and scrolling shall remain responsive when the memory image contains widely separated segments.
7. Missing bytes within a displayed address range shall appear as empty positions, visually distinct from stored `00` or `FF` bytes.

### FR-004 — Side-by-Side Compare View

1. Compare mode shall display two panels side by side: **File A (left)** and **File B (right)**.
2. **Each panel shall contain three columns in this order: Address | HEX Data | ASCII.**
3. Both panels shall share a synchronized absolute-address axis and vertical scrolling.
4. Corresponding rows shall align by absolute address, regardless of source record ordering, file size, or starting address.
5. If one file lacks bytes at addresses present in the other file, its corresponding HEX and ASCII byte positions shall remain blank; subsequent bytes shall **not** shift into those positions.
6. The comparison engine shall identify the following byte states:
   - `Equal`: both files contain the same byte at the address.
   - `Different`: both files contain a byte, but the values differ.
   - `OnlyA`: only File A contains a byte at the address.
   - `OnlyB`: only File B contains a byte at the address.
7. Differences shall be highlighted **at the individual-byte level**. Missing-data states shall use visually distinct styling from unequal values.
8. A shared **Show ASCII** toggle shall show/hide ASCII columns in **both** panels simultaneously. Hiding ASCII shall allocate the freed space to HEX data.
9. The bytes-per-row setting shall apply consistently to both panels, with supported values of **8, 16, and 32**.
10. Users shall be able to jump to the next/previous difference and to an arbitrary absolute address.
11. The application shall provide a **Differences Only** mode, showing differing regions while collapsing identical regions and large address gaps without materializing empty rows.
12. The application should support a full-address-range view and an optional selected-range comparison.
13. Clicking a byte in either panel should identify the matching address in the opposite panel, including cases where the opposite file has no byte at that address.
14. Comparison results shall update after edits without requiring files to be reopened.

**Example:**

| Address | File A HEX | File A ASCII | File B HEX | File B ASCII | Result |
|---|---|---|---|---|---|
| `0xA0000000` | `41` | `A` | `41` | `A` | Equal |
| `0xA0000001` | `42` | `B` | `43` | `C` | Different |
| `0xA0000002` | `FF` | `.` | *(empty)* | *(empty)* | OnlyA |
| `0xA0000003` | *(empty)* | *(empty)* | `00` | `.` | OnlyB |

### FR-005 — Memory Block List and Navigation

1. The application shall show a list/tree of occupied memory blocks for each loaded image.
2. Each block entry shall display at least its **start address, end address, and byte count**.
3. Clicking a block shall scroll the appropriate editor/compare panel to the start of that block.
4. The block list shall refresh after insertions, deletions, merges, and relocations.
5. Optional future enhancement: display user-defined names from a memory map (e.g., PF0, PF1, DFlash).

### FR-006 — Editing Operations

The application shall support:

1. Editing individual byte values in hexadecimal form.
2. Selecting address ranges and performing **Cut, Copy, Paste, and Delete/Remove**.
3. Filling a selected address range with a specified byte or repeating byte pattern.
4. Inserting or replacing data at explicit target addresses, with an explicit overlap policy.
5. Relocating a block from one base address to another (e.g., `0x80000000` to `0xA0000000`) while preserving its contents and relative offsets.
6. Validating relocation against address overflow and occupied destination ranges.
7. Undo and redo for data-changing operations.
8. Clearly indicating unsaved modifications.

**Deletion semantics:** Deleting data shall remove byte occupancy from the specified addresses, potentially splitting a segment. It shall not automatically shift all later memory addresses, as would happen in an ordinary sequential file editor.

### FR-007 — Merge

1. Users shall be able to merge File A into File B or File B into File A.
2. Non-overlapping occupied address ranges shall be combined without modifications to their absolute addresses.
3. Overlapping addresses with identical values shall be merged without conflict.
4. Overlapping addresses with different values shall be flagged as conflicts.
5. The user shall be able to resolve conflicts by keeping A, keeping B, or selecting a policy for a defined address range.
6. The application shall preview merge results before applying a merge and support undo after applying it.

### FR-008 — Checksum and CRC Tools

1. Intel HEX and S-Record **record checksums** shall be validated during import and generated automatically during export.
2. Editing the internal memory image shall not require editing or recalculating serialized record checksums immediately; they shall be regenerated at export.
3. The application shall provide user-invoked checksum/CRC calculation over a selected absolute-address range.
4. At minimum, checksum tools should support additive checksums and common CRC variants (e.g., CRC-16 and CRC-32), with clearly defined parameters.
5. CRC configuration shall include algorithm/polynomial, initialization, reflection, final XOR, and treatment of missing addresses within the selected range.
6. The tool shall show the calculated result and optionally write the checksum value to a chosen memory address, with explicit byte order and width.
7. Displayed checksum/CRC results shall be marked stale or recomputed when their input data changes. This behavior shall be independent of Intel HEX/S-Record record checksum generation.

### FR-009 — Save and Save As

1. **Save** shall write the current image to its associated path when a valid output format/path is known.
2. **Save As** shall allow selecting a new path and format.
3. Existing files shall not be silently overwritten during Save As.
4. Save should use a temporary file and safe replacement strategy to reduce the risk of corruption.
5. Unsaved modifications shall trigger a confirmation before closing a file or the application.

### FR-010 — Export

1. The application shall export to **Intel HEX** and **Motorola S-Record**.
2. Users shall be able to configure the maximum data payload per exported record: **8, 16, or 32 bytes**.
3. Record size shall be an export formatting parameter and shall not change internal segment boundaries or data contents.
4. Intel HEX export shall generate the required extended addressing and end-of-file records.
5. S-Record export shall use address-width-compatible data record types and an appropriate termination record; unsupported address ranges shall be rejected with a clear error.
6. Export shall preserve occupied addresses and bytes without implicitly filling gaps.
7. Users should be able to export only selected address blocks or ranges.
8. Exported files should be verified through a read-back/round-trip test in automated tests.

## 4. User Interface Requirements

### UI-001 — Main Layout

- Main menu/toolbar: **File, Edit, View, Compare, Merge, Tools, Export**.
- Workspace tabs for single-file editing and side-by-side comparison.
- A memory block list that may be shown/hidden.
- Status bar displaying current address, selection length, image format, modified state, and comparison status where relevant.

### UI-002 — Compare Layout

```text
┌──────────────────────────────────────────────────────────────────────────────┐
│ Compare: [File A] vs [File B]    Bytes/row [8|16|32]  [✓] Show ASCII        │
├──────────────────────────────────────┬───────────────────────────────────────┤
│ File A                               │ File B                                │
│ Address    HEX Data       ASCII      │ Address    HEX Data       ASCII       │
│ A0000000   41 42 43 44    ABCD       │ A0000000   41 42 43 44    ABCD        │
│ A0000004   FF 11 -- 22    .. .       │ A0000004   FF 99 33 22    ..3.        │
└──────────────────────────────────────┴───────────────────────────────────────┘
```

`--` is illustrative only: byte positions without stored data shall render as blank/empty cells in the actual application.

- Resizable split between panels and resizable columns.
- Synchronized address navigation and vertical scrolling.
- Optional horizontal scrolling where necessary for 32 bytes per row.
- Color/visual legend for unequal and missing bytes.
- Toggling **Show ASCII** shall hide both ASCII columns and expand HEX data columns accordingly.
- Optional filtering to **All Regions**, **Differences Only**, and **Selected Address Range**.

## 5. Non-Functional Requirements

### NFR-001 — Performance and Memory

- The application shall not allocate a dense buffer spanning from zero (or the lowest occupied address) to the highest occupied address.
- UI rendering shall be virtualized; only visible rows and a small buffer shall be laid out or drawn.
- Comparison shall operate on occupied segments and address intervals rather than iterating over empty multi-gigabyte gaps.
- Large operations shall provide progress indication and a cancel option where practical.
- **Open performance target:** Smooth handling of images with hundreds of MB of stored data is a design goal; exact acceptance thresholds and test hardware are to be defined during performance testing.

### NFR-002 — Reliability

- Invalid files shall not crash the application.
- Imports, exports, edits, and merges shall preserve address/data integrity.
- Save failures shall preserve the in-memory image and report the failure without losing the prior file.
- Error messages shall identify the failing file, record, address range, or operation when possible.

### NFR-003 — Maintainability

- Separate **Core**, **Formats**, **Compare**, **Tools**, **UI**, and **Tests** projects.
- Prefer an MVVM architecture for UI state and commands.
- Keep parsers and comparison algorithms independent of WPF controls.
- Provide automated tests for parsing, sparse addressing, byte edits, merge conflicts, and round-trip serialization.

## 6. Proposed Project Structure

```text
HexEditor.sln
├── HexEditor.Core
│   ├── MemoryImage.cs
│   ├── MemorySegment.cs
│   └── MemoryEditor.cs
├── HexEditor.Formats
│   ├── IntelHexReader.cs
│   ├── IntelHexWriter.cs
│   ├── SRecordReader.cs
│   └── SRecordWriter.cs
├── HexEditor.Compare
│   ├── MemoryComparer.cs
│   ├── DifferenceRange.cs
│   └── MergeEngine.cs
├── HexEditor.Tools
│   ├── ChecksumCalculator.cs
│   └── RelocateBlock.cs
├── HexEditor.UI
│   ├── Views
│   ├── ViewModels
│   └── Controls
│       ├── HexView.cs
│       ├── CompareView.cs
│       └── MemoryBlockList.cs
└── HexEditor.Tests
```

**Implementation preference:** A shared custom/virtualized `HexView` component should power both the single-file editor and each side of the comparison UI. Avoid building a separate per-byte editable `DataGrid` for large images.

## 7. Suggested Implementation Milestones

### Phase 1 — Core and File Formats

- Sparse memory model, block management, Intel HEX/S-Record read/write, parsing diagnostics, and unit tests.

### Phase 2 — Viewer and Compare

- Virtualized HEX/ASCII viewer, side-by-side panels, synchronized absolute addresses, missing-byte alignment, difference highlighting, ASCII toggle, bytes-per-row setting, and block navigation.

### Phase 3 — Editing

- Single-byte editing, selections, cut/copy/paste, delete, fill, relocation, undo/redo, Save and Save As.

### Phase 4 — Merge and Advanced Tools

- Merge conflict handling, checksum/CRC tools, selected-region export, comparison filtering, and performance improvements.

## 8. Acceptance Criteria / Test Scenarios

1. **Different start addresses:** Compare a file containing bytes at `0x80000000` with one containing bytes at `0xA0000000`. The application shows occupied regions at their absolute addresses without moving one file's bytes to match the other's file offset.
2. **Missing byte alignment:** At `0xA0000002`, File A has `FF` and File B has no data. File B's HEX and ASCII cells remain empty at that address, and its next existing byte stays aligned to its actual address.
3. **Changed byte:** The same address contains `22` in File A and `99` in File B. Only the corresponding byte positions are highlighted as different.
4. **ASCII toggle:** Hiding ASCII removes ASCII columns from both compare panels; showing it restores both columns without changing scroll position or address alignment.
5. **Row width:** Switching between 8, 16, and 32 bytes per row preserves absolute address alignment and comparison correctness.
6. **Sparse gap:** Files containing small blocks separated by a very large address gap load without allocating the gap as a byte array and allow navigation directly between blocks.
7. **Relocation:** Moving data starting at `0x80000000` to `0xA0000000` preserves byte order and relative offsets and handles collisions explicitly.
8. **Delete:** Removing an interior address range creates a gap (or splits a segment) and does not shift addresses of subsequent data.
9. **Merge conflict:** When A and B contain different bytes at the same address, the application reports a conflict and does not silently overwrite data.
10. **Export record length:** Exporting a file with 8-, 16-, and 32-byte maximum payloads produces valid Intel HEX/S-Record files that reimport to an identical address-to-byte mapping.
11. **Checksum validation:** A file with an incorrect record checksum is reported with the offending line; corrected exports have valid record checksums.
12. **Save integrity:** Closing a modified document prompts the user; a failed save does not silently discard modifications.

## 9. Open Design Decisions

These decisions should be finalized before implementation or recorded as configuration defaults:

- Maximum supported occupied data size, address width, and realistic performance acceptance targets.
- Whether ranges with holes are shown as blank rows or collapsed with an explicit gap marker in the default compare view.
- Default behavior when importing overlapping records containing identical bytes versus conflicting bytes.
- How pasted bytes are mapped when a selection spans sparse regions.
- Whether calculated firmware CRC values are updated automatically in specified destination addresses or only on explicit user action.
- Exact supported CRC presets and memory-map import formats.
- Whether the application must eventually support Linux/macOS (which could favor a cross-platform UI toolkit).

## 10. Out of Scope for the Initial Version

- Direct ECU flashing or live target-memory access.
- Automatic discovery of firmware-specific CRC locations or signature algorithms.
- Parsing proprietary firmware containers beyond Intel HEX and S-Record.
- A disassembler or CPU-instruction-aware diff engine.
