# VBF Import

Status: accepted

Planning provenance: This specification preceded implementation; its [implementation tickets](../hex-editor-2026-11-06/spec.md) were added retrospectively.

Import Ford/Volvo Versatile Binary Format data blocks at their absolute 32-bit addresses, including raw and supported unencrypted compressed data. Use Vector HexView documentation as the primary format reference, with corroborating implementation evidence where it omits binary details. Reject encrypted files, unknown codecs, malformed files, and invalid block or file checksums without changing the active image. Signed but unencrypted files are importable without cryptographic signature verification. Header erase ranges and other metadata do not create occupied bytes. Resolve overlapping data blocks through the import overlap decision, even when bytes are identical. Apply the 100,000,000 occupied-byte limit to decoded data.

VBF is import-only. Save from a VBF source opens Save As for Intel HEX or Motorola S-Record; Export remains limited to those two formats. Unknown VBF versions or data processing methods report their identifiers rather than guessing.

Supported compressed blocks use the unencrypted LZSS 10/4 (Ford method 26) or 12/6 (Vector method 21) bitstream. When the method is absent from the VBF header, accept a decoded block only if one of these codecs matches its CRC16. The file CRC32 covers the binary block stream.
