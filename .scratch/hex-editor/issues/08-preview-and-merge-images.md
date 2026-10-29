# 08: Preview and merge memory images

**What to build:** Preview the result of merging one image into another, resolve overlaps by address range, and apply the chosen result as a reversible edit.

**Blocked by:**
- 05: Side-by-side memory comparison.
- 06: Edit memory images and save changes.

**Status:** resolved

- [x] Merge File A into File B or File B into File A by absolute address.
- [x] Combine non-overlapping addresses without changing their values or locations.
- [x] Summarize every overlap, including identical values, and show a preview before applying the merge.
- [x] Provide a default keep-source or keep-destination choice and allow overrides for a defined address range.
- [x] Never apply conflicting bytes silently; allow cancelling the merge.
- [x] Apply the resolved merge and support undo and redo.
- [x] Refresh the memory segment list and any open comparison after the merge.

## Comments

Added a sparse address-based merge preview with identical and differing overlap counts, keep-source/keep-destination defaults, inclusive address-range overrides, and cancellation. The WPF merge dialog supports either direction, shows overlap ranges, and opens a side-by-side preview of the resolved image before applying. Applying creates one undoable edit; segment rows and comparisons tracking the destination refresh on apply, undo, and redo.

Verification after merging integration `3b9850e`: `dotnet test tests/HexEditor.Tests/HexEditor.Tests.csproj --artifacts-path .ticket08-release-artifacts -c Release -m:1 -nodeReuse:false -p:UseSharedCompilation=false` passed all 46 tests; `dotnet build HexEditor.sln --no-restore --artifacts-path .ticket08-release-artifacts -c Release -m:1 -nodeReuse:false -p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
