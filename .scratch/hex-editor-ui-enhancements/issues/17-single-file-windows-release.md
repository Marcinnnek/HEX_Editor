# 17: Ship one self-contained Windows x64 executable

**What to build:** A Release publish produces one Windows x64 executable that launches the completed WPF UI application without application DLLs beside it or a separately installed .NET desktop runtime.

**Blocked by:** 12: Apply a consistent WPF UI theme; 15: Replace permanent advanced panels with compact commands; 16: Keep comparison and scrolling responsive.

**Status:** ready-for-agent

- [ ] Release publishing embeds the required managed application files and supported native components into one distributable executable; trimming is not enabled without compatibility proof.
- [ ] The executable starts when copied alone into an isolated directory, and it does not require application DLLs, resources, or configuration beside it.
- [ ] Theme and export preferences and diagnostics use user-data locations, with defaults available on first launch.
- [ ] On a clean compatible Windows x64 machine without a separately installed .NET desktop runtime, launch the executable and exercise open, compare, edit, save, export, and close workflows.
- [ ] Record the publish command, output location, executable size, environment used for validation, and any clean-machine limitation in the ticket comments.
