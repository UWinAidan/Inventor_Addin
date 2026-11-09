# 143: Picked hole centres are highlighted in a part edited in place

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md`
- **Status:** todo
- **Depends on:** 105
- **Needs:** windows build
- **Parallel-safe with:** 140, 141, 142 (not 145: same file)

## Goal

Aidan's check 105-1 (2026-11-09): in a part edited in place, H opens AWB Hole with the centres picked, but the points do not turn blue. They stay white or purple, the colour of their constraints. In the part's own window they turn blue. The highlight must show in place too.

## Scope

- `src/InventorAddin/Holes/HoleSession.cs`: the highlight set is made on the part (`_doc.CreateHighlightSet()`), and `Highlight()` adds the part's own sketch points (`NativeOf` turns proxies into native points). In place, the part's highlight set is probably not drawn in the assembly's view. Make the highlight show in place. The likely fix is a highlight set on the document Inventor is showing (`ActiveDocument`, the assembly), with each point added as its proxy through the occurrence being edited (`CreateGeometryProxy`). Keep the part's own highlight set when the part is open in its own window.
- Clear and release whichever set was made when the session ends, as today.

Out of scope:

- Which points are picked (task 117), and picking a sketch from the browser (task 145).

## Acceptance criteria

- [ ] In place, picked centres show in the highlight colour, and the highlight follows picks and un-picks.
- [ ] The part's own window behaves as before.
- [ ] Built on Windows, 0 errors; Core tests pass.

## Notes for the implementer

Spike 103 and task 105 describe how the session finds the edited occurrence and its proxies. Check `ComponentOccurrence.CreateGeometryProxy` and `AssemblyDocument.CreateHighlightSet` against existing usage or the API reference, and list any you could not confirm. If a proxy cannot be made, log it once and highlight what can be.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Edit a part in place, press H: the default centres are blue.
  2. Pick another point and un-pick one: the highlight follows.
  3. OK and Cancel: no highlight is left behind.
  4. The same in the part's own window: as before.

## Follow-ups
