# 04: Migrate streamed file workflows

**What to build:** Read, save, and export range-aware memory images as Intel HEX and Motorola S-Record through bounded streaming, while preserving existing file behavior. This is the file-workflow migration batch of the expand–contract change.

**Blocked by:** 01: Expand memory image ranges.

**Status:** resolved

- [ ] Import and export preserve absolute addresses, occupied bytes, and gaps when using the expanded image representation.
- [ ] A logical occupied range may span the full 32-bit address space without requiring a single giant array or a 32-bit byte count.
- [ ] Save and export report progress and honor cancellation; an existing destination remains unchanged and a new destination is not left partial after cancellation or failure.
- [ ] Insufficient working space produces a clear failure without altering the image or existing output.
- [ ] Existing small Intel HEX and S-Record round trips retain their observable format and data behavior.
