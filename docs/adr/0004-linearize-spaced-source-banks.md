# Linearize spaced source banks during address remapping

Status: accepted

Firmware images may store bank data in source windows separated by address gaps, while the destination needs consecutive banks. For each occupied source address `A` in the selected range starting at `S`, map it to `L + floor((A - S) / O) * B + ((A - S) % O)`, where `L` is the linear base, `O` is the source bank offset, and `B` is the bank size; an occupied address whose remainder is at least `B` invalidates the operation. This uses the chosen range rather than memory-segment boundaries to define banks, leaves addresses outside the range unchanged, and evaluates destination collisions after removing the bytes being remapped so their vacated source addresses are not mistaken for conflicts.
