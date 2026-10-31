# 01: Expand memory image ranges

**What to build:** Prepare the memory image to represent a fully occupied 32-bit address range alongside its current small-image representation, without changing existing user workflows. This is the expand step of ADR 0002's wide model change.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] A public memory-image operation can represent and read a logical occupied range from `0x00000000` through `0xFFFFFFFF` without allocating one 4 GiB byte array.
- [x] Occupancy and byte counts use values wide enough for `2^32` bytes, including inclusive end-address arithmetic.
- [x] Existing small-image creation, byte reads, segment ordering, and address-gap semantics remain correct while the old form still exists.
- [x] The application and existing public small-image workflows remain usable after the expansion.
