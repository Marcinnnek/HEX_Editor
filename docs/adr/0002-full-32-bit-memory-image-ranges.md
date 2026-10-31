# Support a fully occupied 32-bit address space without one byte array

Status: superseded by ADR-0003

Fill Range must be able to make every address from `0x00000000` through `0xFFFFFFFF` occupied, then save, export, and undo the result. A single `byte[]` and `int` byte count cannot represent this 4 GiB range. The memory model will use bounded or logical storage with 64-bit range lengths and streaming operations; disk-backed working storage is allowed. This adds complexity to editing and serialization so the application can keep memory use bounded and preserve atomic cancellation for full-range operations.

ADR 0003 replaces the user-facing 4 GiB Fill requirement while retaining the Core range model described here.
