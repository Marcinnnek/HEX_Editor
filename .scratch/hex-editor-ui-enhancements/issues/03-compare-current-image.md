# 03: Compare the current base image with one new file

**What to build:** Compare uses the active in-memory base image and asks for only a reference file. With no open image, it takes two selections and opens the first in Editor; canceling the second selection leaves that Editor image open.

**Blocked by:** 02: Give the Editor a stable image document.

**Status:** ready-for-agent

- [ ] Compare from Editor with an open image requests only one additional file and uses unsaved in-memory bytes as the left base.
- [ ] Compare with no image selects a base and a reference; the base opens in Editor.
- [ ] Canceling reference selection after loading a new base leaves the base in Editor and creates no Compare tab.
- [ ] Canceling with an existing base or encountering a reference parse error leaves that document and its dirty state intact.
- [ ] Verify these flows through workspace operations and a picker-driven UI acceptance scenario.
