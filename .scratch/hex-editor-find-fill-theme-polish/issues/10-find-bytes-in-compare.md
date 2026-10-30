# 10: Find bytes in Compare

**What to build:** Extend Find bytes to both images of the active Compare tab and navigate their results in one predictable sequence.

**Blocked by:** 09: Find bytes in Editor.

**Status:** resolved

- [ ] Find searches both the editable base image and the read-only reference image of the active comparison.
- [ ] Results are ordered by address, then panel A before B; a match at the same address in both panels is two results.
- [ ] Next and Previous wrap through this global order and identify the matching panel and address in Find.
- [ ] Navigation selects the complete current match on the correct side and keeps the Compare views synchronized.
- [ ] Switching between Editor and Compare while Find stays open clears the old indicator and makes later searches use the newly active tab.
- [ ] A missing or cancelled search leaves the current selection intact and does not edit the reference image.
