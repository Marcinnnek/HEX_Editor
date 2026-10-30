# 01: Make Edit Row compact and efficient to navigate

**What to build:** Edit Row presents one centered line of the active view's 8, 16, or 32 byte columns in a content-height dialog. Keyboard and pointer focus make replacing values quick while preserving sparse memory-image editing rules.

**Blocked by:** None (can start immediately).

**Status:** ready-for-agent

- [ ] Offset, original HEX/ASCII, and proposed HEX/ASCII values are centered horizontally and vertically in each byte column; row labels remain left-aligned.
- [ ] Tab visits enabled Proposed HEX inputs by increasing byte offset, then enabled Proposed ASCII inputs in the same order; Shift+Tab reverses this sequence.
- [ ] Unoccupied addresses remain noneditable and are skipped by Tab. After the last ASCII input, normal focus navigation reaches OK.
- [ ] Entering an input with Tab, Shift+Tab, or a first mouse click selects its whole value; a later click in the focused input can position the caret.
- [ ] The chosen byte receives initial focus and visual emphasis, while the visible Selected byte heading is removed.
- [ ] The dialog fits its controls vertically and has a fixed normal-use height at 8, 16, and 32 bytes per row. On a constrained display, scrolling keeps all inputs and actions reachable without unreadably shrinking text.
- [ ] Existing one- or two-digit HEX input, one-character printable ASCII input, validation, Cancel behavior, sparse-address protection, synchronized representations, and one reversible commit still work.
- [ ] Verify editing outcomes through public editing operations and the focus, alignment, and sizing behavior in rendered WPF scenarios.
