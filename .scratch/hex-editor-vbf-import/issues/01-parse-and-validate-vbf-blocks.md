# 01: Parse and validate VBF blocks

Status: resolved

Spec: [VBF Import](../spec.md)

## What to build

Detect Ford/Volvo VBF input, read header and binary data blocks, and construct sparse occupied data at the block addresses.

## Acceptance

- [x] Import raw blocks at their 32-bit addresses; header erase or omit ranges create no occupied bytes.
- [x] Validate each block's CRC16 and the binary stream's file CRC32 before publishing an image.
- [x] Reject truncation, invalid lengths, address overflow, missing data, and unknown versions atomically.
- [x] Limit decoded occupied bytes to 100,000,000.
- [x] Expose internal block overlaps through the existing import overlap choice, including identical bytes.

## Verification

`VbfImportTests` cover raw addresses, checksums, overlap decisions, and the import limit.
