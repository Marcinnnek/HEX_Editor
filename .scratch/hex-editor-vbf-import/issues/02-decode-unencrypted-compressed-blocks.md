# 02: Decode unencrypted compressed VBF blocks

Status: resolved

Spec: [VBF Import](../spec.md)

## What to build

Decode supported LZSS blocks and reject encryption or unknown processing methods without guessing.

## Acceptance

- [x] Support Ford 10/4 and Vector 12/6 LZSS with decoded-byte CRC16 verification.
- [x] When the method is absent from the VBF header, accept only a codec whose decoded CRC16 matches.
- [x] Reject encrypted data-format identifiers, chained processing, and unsupported methods.
- [x] Accept a signed but unencrypted header without claiming signature verification.

## Verification

`VbfImportTests` cover both codecs, encrypted and unknown formats, decoded-size handling, and a signed header. The fixtures are constructed; real-file compatibility remains subject to sample validation.
