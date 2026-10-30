# 02: Validate the local single-file release on the agreed system

**What to build:** A maintainer can publish, copy, launch, and exercise the self-contained Windows release using only the executable, then record the result and the limits of the validation environment.

**Blocked by:** 01: Write bounded diagnostics outside the release directory; hex-editor-ui-enhancements 11: Export chosen occupied data with per-export options; hex-editor-ui-enhancements 15: Replace permanent advanced panels with compact commands; hex-editor-ui-enhancements 16: Keep comparison and scrolling responsive.

**Status:** ready-for-agent

- [ ] Release publishing targets Windows 11 24H2 or newer on x64, is self-contained and single-file, embeds supported native components as needed, and does not enable trimming without compatibility proof.
- [ ] The publish output contains only the executable, and a copy of that executable launches from an isolated directory without adjacent application DLLs, resources, or configuration.
- [ ] First-launch defaults work without adjacent files; appearance and export preferences persist in user data, and diagnostics use the bounded user-data log.
- [ ] Exercise open, compare, edit, save, export, and close workflows in the published application on the agreed Windows 11 24H2+ x64 development system.
- [ ] Record the publish command, output location, executable size, operating-system build, architecture, and installed .NET Desktop Runtime environment.
- [ ] State explicitly that validation on a host with .NET Desktop Runtimes installed does not prove launch on a machine without those runtimes.
