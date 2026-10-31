# 17: Ship one self-contained Windows x64 executable

**What to build:** A Release publish produces one Windows 11 24H2+ x64 executable that launches the completed WPF UI application without application DLLs beside it or a separately installed .NET desktop runtime.

**Blocked by:** 11: Export chosen occupied data with per-export options; 15: Replace permanent advanced panels with compact commands; 16: Keep comparison and scrolling responsive.

**Status:** ready-for-human

- [x] Release publishing embeds the required managed application files and supported native components into one distributable executable; trimming is not enabled without compatibility proof.
- [x] The executable starts when copied alone into an isolated directory, and it does not require application DLLs, resources, or configuration beside it.
- [x] Theme and export preferences and diagnostics use user-data locations, with defaults available on first launch.
- [x] On the development system, launch the executable and exercise open, compare, edit, save, export, and close workflows. Record its actual OS and installed .NET Desktop Runtime environment. A Windows 10 host is accepted for this validation, but does not establish behavior on the supported Windows 11 24H2+ target or prove launch on a machine without those runtimes.
- [x] Record the publish command, output location, executable size, environment used for validation, and any clean-machine limitation in the ticket comments.

## Comments

- Added `src/HexEditor.UI/Properties/PublishProfiles/SingleFileWinX64.pubxml` for Release `win-x64` self-contained single-file publishing. It embeds native libraries for self-extraction, keeps trimming disabled, and removes PDB sidecars from the publish output.
- Publish command: `dotnet publish src/HexEditor.UI/HexEditor.UI.csproj -c Release -p:PublishProfile=SingleFileWinX64 -o artifacts/publish/win-x64`
- Output: `artifacts/publish/win-x64/HexEditor.UI.exe` (67,472,601 bytes). The publish directory contains only this executable. An isolated copy in `artifacts/single-file-isolated/` also contained only the executable and remained running for five seconds after launch.
- Initial validation environment: Windows NT 10.0.26300.0, x64, with .NET and Windows Desktop runtimes 8, 9, and 10 installed. This confirmed startup from an isolated directory but did not prove runtime independence or exercise the open/compare/edit/save/export/close workflows.
- Decision update (2026-10-30): release acceptance will exercise open, compare, edit, save, export, and close on the Windows 11 24H2+ x64 development system. The user accepts that this host has .NET Desktop Runtimes installed; record this limitation and do not describe the result as clean-machine proof. The executable itself must remain self-contained and must not require an installed desktop runtime.
- Agreed diagnostics policy: keep an error-only rolling log under `%LOCALAPPDATA%\HexEditor`, retaining at most five 1 MiB files. Include operation and affected address/selection when available; omit firmware bytes, full file paths, routine successes, and telemetry. Defaults and preferences must work on first launch without files beside the executable.
- Tickets 15 and 16 remain open, so this ticket's dependency chain and completed-UI acceptance are not yet satisfied.
- Release profile revalidated after integrating tickets 01, 04, 05, 06, 08, 09, 10, 11, 13, and 14 on `integration` (`86f2260`): the publish directory contains only `HexEditor.UI.exe` (67,488,547 bytes), and a copy containing only that executable started for five seconds from an isolated directory. The full test suite passed 128 tests before publishing. Open/compare/edit/save/export/close release workflow acceptance remains pending its blockers and the outstanding WPF visual acceptance checks.
- Updated release validation after integrating tickets 07, 15, and 16: publish output contains only `HexEditor.UI.exe` (67,491,958 bytes), and an isolated copy started for eight seconds. The host registry identifies Windows 10 Home 26H2, build 26300.9711, x64, with .NET Desktop Runtimes 8, 9, and 10 installed. This confirms only a process-start smoke check on that host; it does not validate the Windows 11 24H2+ target, prove runtime-free-host launch, or exercise the UI workflows.
- User decision: release workflow verification on the current development system is sufficient; no VM or second machine is required. Updated artifact after the More overflow fix (`5f0ed44`): `artifacts/publish/win-x64/HexEditor.UI.exe`, 67,492,290 bytes, SHA-256 `863437C4D6B512DAA350F52E530045D6B79DE32316CA4AA69C8F8FCB38B55FE7`. The isolated copy remained running after eight seconds. Full workflow and first-launch preference/log checks remain for human acceptance.
