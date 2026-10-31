# 08: Warn about a format mismatch on Open

**What to build:** After a successful parse, Open warns when a recognized filename extension implies a different format from the memory image's content and lets the user continue or keep the old Editor image.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] Intel HEX is implied by `.hex` and `.ihx`; Motorola S-Record by `.s19`, `.s28`, `.s37`, `.srec`, and `.mot`, ignoring extension case. Unknown extensions do not warn.
- [x] The warning shows the filename, extension-implied format, and content-detected format with Open anyway and Cancel. Valid content is not treated as a parse error merely because the filename differs.
- [x] The existing save-changes prompt remains before loading. The mismatch warning appears after successful parsing but before replacing the Editor image.
- [x] Cancel preserves the old Editor image and its dependent views; an earlier completed Save stays saved. Open anyway installs the detected image and retains its detected source format.
- [x] File-service and Open acceptance scenarios cover both mismatch directions, matching and unknown extensions, and Cancel/continue outcomes.
