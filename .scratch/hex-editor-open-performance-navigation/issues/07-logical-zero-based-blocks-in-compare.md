# 07: Logical zero-based Blocks in Compare

**What to build:** Each Compare side uses the same maximal-contiguous Block definition as Editor, with its own zero-based numbering and working navigation.

**Blocked by:** 06: Logical zero-based Blocks in Editor.

**Status:** resolved

- [x] A continuous occupied segment backed by multiple technical ranges appears once in each side's Block list.
- [x] Each side independently sorts its Blocks by start address and numbers them `0, 1, 2...`; labels refresh after changes to the editable base image.
- [x] Clicking a Block navigates the corresponding image to its first occupied address without disturbing the other side's document identity.
- [x] Existing single-line text and horizontal scrolling preserve the complete description in narrow Compare panels.
- [x] Public comparison and rendered Compare tests cover both sides, large continuous segments, address gaps, numbering, and navigation.
