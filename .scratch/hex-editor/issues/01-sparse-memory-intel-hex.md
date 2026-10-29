# 01: Sparse memory and Intel HEX import

**What to build:** Open an Intel HEX image in the Windows application and show its occupied memory segments with their absolute address ranges and byte counts. The image must retain sparse addressing, so widely separated data blocks do not allocate or display the intervening gap as stored bytes.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] Create the .NET 10 WPF/MVVM solution structure with separate Core, Formats, Compare, Tools, UI, and Tests projects.
- [x] Represent a memory image with sorted, non-overlapping segments of explicitly occupied bytes and support address lookup and segment summaries.
- [x] Open valid Intel HEX files, including extended addressing, and display each occupied segment's start, end, and byte count.
- [x] Preserve the original path, input format, and format metadata separately from memory contents.
- [x] Report malformed Intel HEX records with the source line and actionable reason, including invalid syntax, byte count, checksum, or address overflow.
- [x] Reject unsupported addresses without wrapping arithmetic, including data that would cross the 32-bit address limit.
- [x] Verify through the public import workflow that high absolute addresses remain unchanged and large gaps do not become dense allocations.

## Comments

- Implemented the sparse Core model, Intel HEX import, metadata result, and initial WPF segment list.
- Verification: 5 import tests pass; the full solution builds with 0 warnings and 0 errors.
