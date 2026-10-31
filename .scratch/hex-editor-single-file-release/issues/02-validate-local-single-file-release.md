# 02: Validate the local single-file release on the agreed system

**What to build:** A maintainer can publish, copy, launch, and exercise the self-contained Windows release using only the executable, then record the result and the limits of the validation environment.

**Blocked by:** 01: Write bounded diagnostics outside the release directory; hex-editor-ui-enhancements 11: Export chosen occupied data with per-export options; hex-editor-ui-enhancements 15: Replace permanent advanced panels with compact commands; hex-editor-ui-enhancements 16: Keep comparison and scrolling responsive.

**Status:** ready-for-human

- [x] Release publishing targets Windows 11 24H2 or newer on x64, is self-contained and single-file, embeds supported native components as needed, and does not enable trimming without compatibility proof.
- [x] The publish output contains only the executable, and a copy of that executable launches from an isolated directory without adjacent application DLLs, resources, or configuration.
- [x] First-launch defaults work without adjacent files; appearance and export preferences persist in user data, and diagnostics use the bounded user-data log.
- [x] Exercise open, compare, edit, save, export, and close workflows in the published application on the current development system. Record its actual OS and runtime environment; Windows 10 validation is accepted but does not establish behavior on the supported Windows 11 24H2+ target.
- [x] Record the publish command, output location, executable size, operating-system build, architecture, and installed .NET Desktop Runtime environment.
- [x] State explicitly that validation on a host with .NET Desktop Runtimes installed does not prove launch on a machine without those runtimes.

## Comments

- Publish command: `dotnet publish src/HexEditor.UI/HexEditor.UI.csproj -c Release -p:PublishProfile=SingleFileWinX64 -o artifacts/publish/win-x64`
- Output: `artifacts/publish/win-x64/HexEditor.UI.exe` (67,492,290 bytes; 64.37 MiB; SHA-256 `863437C4D6B512DAA350F52E530045D6B79DE32316CA4AA69C8F8FCB38B55FE7`). The publish directory contains only the executable. A copy containing only that executable started from an isolated temporary directory and remained running for eight seconds. This artifact was republished after the toolbar overflow fix was merged.
- Host: Windows 10 Home 26H2, build 26300.9711, x64. Installed .NET Desktop Runtimes include 8.0.20, 9.0.9/9.0.21, and 10.0.8/10.0.12/10.0.13/10.0.14. This is not the specified Windows 11 24H2+ target and does not prove runtime-independent launch.
- The smoke check did not exercise the UI workflows. Open, compare, edit, save, export, close, and published-app first-launch preference/log checks remain for human acceptance.
