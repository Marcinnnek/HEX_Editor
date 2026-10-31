# 13: Fast five-megabyte Open

**What to build:** Typical 5 MB firmware images open to an interactive Editor within the agreed Release target, while larger loads remain responsive and cancellable.

**Blocked by:** 01: Open replaces the Editor document; 03: Limit loaded images to 100 million occupied bytes; 09: Live Editor scrolling and a usable thumb.

**Status:** resolved

- [x] A Release build opens a local-SSD Intel HEX fixture with 5,000,000 occupied bytes in one contiguous segment and ordinary 16–32-byte source records to an interactive Editor in at most two seconds.
- [x] The same Release target is met by an equivalent S-Record fixture.
- [x] A fragmented fixture loads correctly and remains responsive, without a fixed two-second total target.
- [x] During larger loads, the window handles user input and Cancel in about 100 ms, shows progress, and never partially replaces the prior document after cancellation or failure.
- [x] The benchmark measures selection-to-interactive-Editor time, runs in Release against local SSD fixtures, and records the environment and result; optimization does not weaken the 100,000,000-byte image limit.
