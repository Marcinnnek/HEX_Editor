# HEX / S-Record Memory Editing

This context covers inspecting, comparing, editing, merging, and exporting firmware memory images represented by Intel HEX or Motorola S-Record files.

## Language

**Memory image**:
A set of explicitly stored byte values, each associated with an absolute address. An address that is not present has no stored value.

**Occupied byte**:
A byte explicitly stored at an address, including values `00` and `FF`.

**Memory segment**:
A maximal contiguous range of occupied addresses. Segment boundaries come from address occupancy, not from source record boundaries.

**Block**:
An alternate name for a memory segment: one maximal contiguous range of occupied addresses. Its boundaries follow occupied addresses, not source file records.

**Address gap**:
A range of unoccupied addresses between or around occupied bytes. A gap is not implicitly filled with any byte value.

**Overlap**:
Incoming data that supplies an address already occupied by another value or record. Overlaps are surfaced for a user decision even when the values are identical.

**Import**:
Reading a HEX or S-Record file into a memory image while retaining its absolute addresses and recording source format metadata.

**Source record**:
An encoded entry in an Intel HEX or Motorola S-Record file. Its boundary does not define a memory segment.

**Byte sequence**:
An ordered list of byte values used as a search pattern across consecutive occupied addresses in a memory image. It is independent of source-record boundaries and does not cross address gaps.

**Merge**:
Combining two memory images by absolute address, with explicit handling for addresses occupied by both images.

**Comparison difference**:
An absolute address where two memory images contain unequal byte values or where only one image contains a byte. An address absent from both images is not a difference.

**Comparison cursor**:
The base or reference image side and address used as the starting point for navigation within an open comparison. The address may come from a selected byte, a visible row, or an address entered by the user.

**Edited byte**:
An occupied byte whose current value differs from the value at the same address in the image's last saved state, including a byte newly added at that address. A removed byte has no visible cell in the Editor.

**Base image**:
The editable memory image whose occupied addresses and byte values are compared with a reference image.

**Reference image**:
The read-only memory image used to identify differences from a base image.
