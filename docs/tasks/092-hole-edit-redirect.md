# 092: Edit Feature and double-click open AWB Hole; re-apply offer

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The command"); spike results in `docs/tasks/081-spike-awb-hole.md`
- **Status:** todo
- **Depends on:** 090
- **Needs:** windows build
- **Parallel-safe with:** 091

## Goal

Every way of editing a hole in Inventor opens the AWB Hole window on it: Edit Feature from the browser menu and from the viewport's mini toolbar, and double-click. A hole changed outside AWB Hole is offered a re-apply of its fit, tolerance and colour.

## Scope

- `src/InventorAddin/Holes/` (an edit-routing class hooked at startup), the "Edit Feature" button definition with Inventor's icon
- Core: the re-apply decision (from 084's comparison) and its message, with tests

## Acceptance criteria

- [ ] Browser right-click (`OnLinearMarkingMenu`): when the selection is a hole or a face made by one (`Face.CreatedByFeature`), Inventor's `PartNGxHoleEditCtxCmd` is replaced by our "Edit Feature" in the same place.
- [ ] Viewport mini toolbar (`OnContextualMiniToolbar`): `PartNGxHoleEditCtxCmd` swapped for ours in the `NameValueMap`; our definition carries Inventor's Edit Feature icon (copied from `PartNGxHoleEditCtxCmd.StandardIcon`/`LargeIcon`), since the toolbar shows icons only.
- [ ] Double-click (`OnDoubleClick`, `kEventHandled`) on a hole or its face opens our window.
- [ ] Backstop: if `PartNGxHoleEditCtxCmd` starts anyway (`OnActivateCommand`), `StopActiveCommand()` and open our window.
- [ ] The window always opens after Inventor's event has returned; while it is open, a second request brings it to the front.
- [ ] Only while AWB Hole has taken over (switch off): with "Use Inventor's Hole command" on, Edit Feature and double-click stay Inventor's.
- [ ] Re-apply: when an AWB hole is opened and its stored data no longer matches the hole (diameter, tolerance, colour), the window says so and offers to re-apply; Inventor's own edit keeps the colour (spike 081).
- [ ] An Inventor hole opens with "OK makes this an AWB hole" (spec 09 question 5).
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx` builds

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Browser right-click, Edit Feature: our window, on that hole, with its settings; every setting kept (counterbore stays, dowel stays ticked).
  2. Left-click a hole face, mini toolbar Edit Feature (Inventor's icon): our window.
  3. Double-click a hole in the browser and on a face: our window.
  4. A hole made with Inventor's Hole (switch on), then switch off: Edit Feature opens ours, and OK makes it an AWB hole.
  5. Change an AWB dowel with Inventor's Hole (switch on), switch off, open it: the re-apply offer.

## Follow-ups
