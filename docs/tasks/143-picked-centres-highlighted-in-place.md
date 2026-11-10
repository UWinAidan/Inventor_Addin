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

- **Ran:** on Windows with Inventor 2026 installed and not running: `dotnet build InventorAddin.slnx` (0 errors, 9 warnings, none in `HoleSession.cs`; deployed to `%APPDATA%\Autodesk\Inventor 2026\Addins`). `dotnet test tests/InventorAddin.Core.Tests`: 4461 passed, 0 failed. No Core change, so no new tests: the change is which document holds the highlight set and which object is added to it, both Inventor-side.
- **Approach:** `HoleSession.CreateHighlightSet()` makes the set on `ActiveDocument` when it is an `AssemblyDocument` other than the part, and keeps `assembly.ComponentDefinition.ActiveOccurrence`. `Highlight()` then adds each picked point's proxy, made with `occurrence.CreateGeometryProxy(point, out result)`; a proxy that cannot be made is logged once per session and skipped. With no assembly active, or no edited occurrence or set found (logged), the set is on the part as before. `Finish` deletes whichever set was made, as today, and forgets the occurrence.
- **Not compiled (changed under `src/InventorAddin`):** none; `src/InventorAddin/Holes/HoleSession.cs` was built on Windows. Not yet run in Inventor.
- **Inventor API members not confirmed:** none. Confirmed by reflection on the Inventor 2026 interop: `AssemblyDocument.CreateHighlightSet()`, `AssemblyComponentDefinition.ActiveOccurrence`, `ComponentOccurrence.CreateGeometryProxy(object Geometry, out object Result)` (`Result` is `out`), `HighlightSet.AddItem/Clear/Delete/SetColor`. Untested in Inventor: whether a set on the assembly with proxies draws in place (spike 103 had reported the part's set showing in place; Aidan's check 105-1 says it does not), and a part edited in place inside a sub-assembly, where `ActiveOccurrence` may be a proxy occurrence.
- **Manual checklist for Inventor:**
  1. Edit a part in place, press H: the default centres are blue.
  2. Pick another point and un-pick one: the highlight follows.
  3. OK and Cancel: no highlight is left behind.
  4. The same in the part's own window: as before.
  5. A part edited in place two levels down (top assembly, sub-assembly, part): H, the centres turn blue. If not, check `awbaddin.log` for the warning about the occurrence or proxy.

## Follow-ups

- Spike 103 question 5 recorded that a highlight set on the part shows in place; Aidan's check 105-1 contradicts it. Once this task is checked in Inventor, the spike's result 5 could carry a note pointing here.
- Not checked: a part edited in place inside a sub-assembly (the highlight's occurrence would be a proxy occurrence).
