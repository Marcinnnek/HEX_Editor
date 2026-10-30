# 05: Retire the single-array segment contract

**What to build:** Complete the expand–contract migration so user operations no longer depend on every memory segment fitting in one byte array.

**Blocked by:** 02: Migrate Editor range operations; 03: Migrate Compare range operations; 04: Migrate streamed file workflows.

**Status:** ready-for-agent

- [ ] Editor, Compare, import, save, export, and editing paths use the range-aware contract after the migration batches are complete.
- [ ] Legacy single-array assumptions that would reject or overflow a fully occupied 32-bit range are removed from the active contract.
- [ ] Ordinary files retain their existing editing, comparison, and round-trip behavior.
- [ ] The integrated application remains buildable and usable before the full-range Fill command is exposed.
