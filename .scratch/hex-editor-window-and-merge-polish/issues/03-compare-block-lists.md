# 03: Compare Block lists

**What to build:** Show the same one-line Block list in both Compare columns, with each image's Blocks above its own data and full descriptions accessible in narrow panels.

**Blocked by:** 02: Editor Block list.

**Status:** resolved

- [x] Both Compare lists use the established Block sentence, hexadecimal and decimal length format, and ascending numbering.
- [x] Each list remains aligned with its own image and stays above that image's HEX/ASCII data.
- [x] Long entries remain on one line and are fully reachable by horizontal scrolling in either narrow Compare column.
- [x] Selecting a Block navigates to its start in the relevant image without changing the editable base/reference roles.
- [x] Ctrl/Shift can select multiple base-image Blocks for the existing selected-segment export operation.
- [x] Rendered WPF scenarios verify both columns, scrolling, navigation, and selection with separated memory segments.

## Answer

Replaced both Compare segment tables with one-line, horizontally scrollable Block lists using the Editor's shared description format. The base-image list supports extended selection and continues to expose selected segment indexes for export; the reference list remains single-select. Selecting a Block in either list navigates to its address in the aligned data panels while preserving base/reference roles.

Rendered WPF coverage verifies the two lists stay above their corresponding panels, long descriptions are horizontally reachable at narrow widths, labels use ascending numbering and the established format, base multiselection produces export indexes, and selecting either image's Block navigates both data panels. Focused tests passed: `dotnet test tests/HexEditor.Tests/HexEditor.Tests.csproj --filter 'FullyQualifiedName~CompareSegmentListLayoutTests|FullyQualifiedName~CompareViewModelTests' --no-restore -p:IsTestProject=true -v minimal` (27 tests).
