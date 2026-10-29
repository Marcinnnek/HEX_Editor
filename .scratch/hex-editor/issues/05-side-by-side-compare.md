# 05: Side-by-side memory comparison

**What to build:** Compare two imported images in synchronized panels that align every byte by absolute address and show differences without shifting data across missing positions.

**Blocked by:**
- 02: S-Record import and overlap decisions.
- 04: Virtualized single-image editor view.

**Status:** resolved

- [x] Show File A and File B in side-by-side panels, each with Address, HEX Data, and ASCII columns.
- [x] Synchronize the address axis, vertical navigation, row width (8/16/32 bytes), and shared Show ASCII toggle.
- [x] Keep corresponding bytes aligned at the same absolute address when images have different starts, sizes, or sparse gaps.
- [x] Classify each address as Equal, Different, OnlyA, or OnlyB and highlight individual byte states; style missing values distinctly from unequal values.
- [x] Keep absent bytes blank in their address positions and do not shift subsequent bytes.
- [x] Navigate to an arbitrary address and the next or previous difference; clicking either panel identifies the same address in the other panel, including absent bytes.
- [x] Offer All Regions, Differences Only, and selected-address-range views; collapse identical regions and large gaps without generating empty rows.
- [x] Refresh comparison results after either image is edited.
- [x] Show a live summary of total differences, unequal bytes, and bytes present on only one side.

## Comments

Implemented synchronized compare panels with absolute-address byte states, sparse gap markers, navigation, row-width and ASCII settings, three view modes, and refreshable image inputs. Added comparison behavior tests. dotnet test tests\HexEditor.Tests\HexEditor.Tests.csproj --no-restore -m:1 -nodeReuse:false passed (21 tests); dotnet build HexEditor.sln --no-restore -m:1 -nodeReuse:false passed with no warnings or errors.

Review follow-up: the compare window displays a byte-level difference summary and updates it whenever either image refreshes.
