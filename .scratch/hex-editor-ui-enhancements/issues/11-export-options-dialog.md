# 11: Export chosen occupied data with per-export options

**What to build:** Each export opens a dialog for entire image, selected memory segments, or address range, plus target path, Intel HEX or S-Record format, and 8/16/32 maximum data bytes per record. Export writes a copy without changing the base document.

**Blocked by:** 04: Share a base image across Editor and Compare tabs.

**Status:** ready-for-human

- [x] Export Image, Export Selected Segments, and Export Address Range are available from the active document, including when Compare is active.
- [x] Each export shows scope, path, format, and selectable 8/16/32 data bytes per record; a new profile starts at 16 and the last choice survives restart.
- [x] An empty selection, invalid range, unsupported format/address combination, or canceled dialog leaves document bytes, path, and dirty state unchanged.
- [x] Successful export preserves occupied addresses and gaps and generates valid control, checksum, and termination records; reimport matches the selected data.
- [x] Export is asynchronous with progress and cancellation and does not alter Editor/Compare bytes-per-row settings.
- [x] Test all scopes, formats, record sizes, round trips, preference persistence, and unchanged document association through public workflows.
- [x] Inspect dialog behavior in WPF.

## Comments

- Export dialog and preference implementation is merged on `integration`. The integrated suite passed 123 tests after fixing compile errors and correcting a test setup to explicitly approve an occupied-byte overwrite. Manual WPF dialog inspection remains outstanding.
