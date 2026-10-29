# HEX / S-Record Memory Editing

This context covers inspecting, comparing, editing, merging, and exporting firmware memory images represented by Intel HEX or Motorola S-Record files.

## Language

**Memory image**:
A set of explicitly stored byte values, each associated with an absolute address. An address that is not present has no stored value.

**Occupied byte**:
A byte explicitly stored at an address, including values `00` and `FF`.

**Memory segment**:
A contiguous range of occupied addresses. Segment boundaries come from address occupancy, not from source record boundaries.

**Address gap**:
A range of unoccupied addresses between or around occupied bytes. A gap is not implicitly filled with any byte value.

**Overlap**:
Incoming data that supplies an address already occupied by another value or record. Overlaps are surfaced for a user decision even when the values are identical.

**Import**:
Reading a HEX or S-Record file into a memory image while retaining its absolute addresses and recording source format metadata.

**Merge**:
Combining two memory images by absolute address, with explicit handling for addresses occupied by both images.
