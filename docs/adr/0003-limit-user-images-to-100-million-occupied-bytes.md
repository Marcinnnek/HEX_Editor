# Limit user-facing images to 100 million occupied bytes

Status: accepted

Every memory image loaded or produced by a user operation is limited to 100,000,000 unique occupied byte addresses. The limit applies independently to the Editor image and to each base or reference image in Compare; Fill, Insert, Paste, Merge, and other edits must leave their resulting image within it. Absolute addresses still span `0x00000000` through `0xFFFFFFFF`, and source text-file size or address span does not define the limit. Small segments may sit far apart anywhere in that address space; unoccupied gaps consume none of the byte allowance. A result exceeding the allowance is rejected without partially changing the image.

This product limit replaces ADR 0002's requirement to expose a full 4 GiB Fill. Retaining Core's wide logical-range model supports sparse high addresses and avoids reversing the existing storage migration. The limit makes typical 5 MB files fast to open and bounds the amount of user-visible data, while keeping separately loaded Compare images independent.
