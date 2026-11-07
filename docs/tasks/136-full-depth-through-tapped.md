# 136: Full depth checkbox on a Through All tapped hole

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window", "Tapped holes")
- **Status:** done, built on Windows, not yet run in Inventor
- **Depends on:** 116 (thread depth measured from the bottom of the seat)
- **Needs:** windows build
- **Parallel-safe with:** 135

## Goal

Aidan, 2026-10-28 (during spike 107): a tapped Through All hole gets a "Full depth" checkbox like Inventor's, ticked by default. Unticked, a Thread depth box shows (default 1.5 x D, takes expressions), so the hole is not tapped all the way through. The diagram shows the thread stopping at that depth.

Today Core sets `HoleThread.FullDepth` to `!spec.IsBlind` (`HoleRules`), and `HoleFeatureWriter` already writes `CreateTapInfo(..., false, ThreadDepthCm)` when it is false, so the add-in half is small.

## Scope

- Core (`HoleSpec`, `HoleRules`, `AwbHoleViewModel`):
  - A `FullDepth` choice on the spec, ticked by default for each new hole, shown only for a Tapped hole whose termination is Through All. Blind tapped holes keep their Thread depth and Drill depth boxes as now; taper tapped holes are unchanged (Default).
  - Unticked: a Thread depth box, starting at the row's default thread depth (1.5 x D from Settings, as a blind hole's), measured from the bottom of the seat (task 116), taking Inventor parameters and expressions like Depth (task 100), and kept as typed under task 124's rule.
  - `HoleRules` gives `HoleThread.FullDepth = false` and the thread depth (with its expression) when unticked; validation as for a blind hole's thread depth (above zero; not checked against the part thickness, Inventor reports that).
  - Reading back an edited hole: the box follows Inventor's `HoleTapInfo.FullThreadDepth` and `ThreadDepth`.
  - Last values: none; each new hole starts ticked (Default).
- `HoleDiagram`: a Through All tapped hole with Full depth unticked draws the thread stopping at the thread depth, below the seat, with the depth dimension as on a blind tapped hole.
- `src/InventorAddin/UI/AwbHoleWindow.xaml`: the checkbox and the box, on the row the blind thread depth uses.
- `src/InventorAddin/Holes/HoleFeatureWriter.cs`: check that the existing `FullDepth == false` path writes the thread depth's expression too (`WriteExpressions`), and the seat offset of task 116.
- Tests for every Core behaviour above, the diagram included.

## Acceptance criteria

- [ ] A new Through All tapped hole shows Full depth ticked and threads the whole hole, as now.
- [ ] Unticked, the Thread depth box shows with 1.5 x D; the model's thread stops there (measured from the bottom of the seat) and the diagram shows it.
- [ ] An expression typed in the box stays parametric in Inventor.
- [ ] Editing such a hole reads the checkbox and the depth back.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes, and the solution builds on Windows.

The first four are met in Core (the checkbox, the box, the geometry written, the stored data, the read-back; tested) and wait on Aidan's run in Inventor (checklist below).

## Notes for the implementer

- `HoleTapInfo.FullThreadDepth` (bool, settable) and `HoleTapInfo.ThreadDepth` (`Parameter`) exist in the 2026 interop (checked by reflection, 2026-10-28), as does `HoleFeatures.CreateTapInfo(RightHanded, ThreadType, ThreadDesignation, Class, FullTapDepth, ThreadDepth)`, already used.

## Verification

- **Ran (Windows, 2026-11-07, Inventor not running):**
  - `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: succeeded, 0 errors. The warnings are the 4 CS8600 ones from before (Create Print, New Part, Save As Copy, Save As Replace; MSBuild prints each twice) and the existing xUnit2012 in `HoleDiagramTests`.
  - `dotnet build src/InventorAddin.Core`: succeeded.
  - `dotnet test tests/InventorAddin.Core.Tests`: 4343 of 4347 passed in the full parallel run. The 4 failures are the `Numbering` timing and real-folder tests task 116 recorded (`RegisterStoreTests.RealEnvironment_...`, two `RenamePlanTests.ListFilesToSearch_...`, `NewPartCreatorTests.FailureAfterAssigning_...`). Split as task 116 did: everything but `Numbering` passed 3485 of 3485; `Numbering` alone passed 862 of 862 on two of three runs (the other run had 1 timing failure). This task changes no Numbering code. Before this task: 4296.
  - New tests: 51 (`HoleFullDepthTests`, `AwbHoleFullDepthViewModelTests`, and 2 new fit cases in `HoleDiagramTests`).
- **Built, file by file:**
  - `HoleSpec.cs`: `FullDepth` (init, default true), `HasThreadDepth` (blind tapped, or Through All tapped and unticked), `TakesFullDepth` (tapped and Through All). `Normalised` puts `FullDepth` back to true on every other hole.
  - `HoleRules.cs`: `BuildTapped` gives `HoleThread.FullDepth = !spec.HasThreadDepth` and the thread depth's expression when the thread stops. "Greater than zero" is checked whenever the thread stops; "thread deeper than drill" only on a blind hole. Task 116's seat offset already applied to any thread that is not full depth (`HoleDepthReference.ToInventorCm` and `ToInventorExpression`), so `(d0) + (6 mm)` is written under a 6 mm counterbore.
  - `HoleGeometry.cs`: doc comment only.
  - `HoleStoredData.cs`: a Through All tapped hole stores `FullDepth` (1 or 0). Missing reads as ticked; any value other than 0 or 1 cannot be read. Still version 5 (see Defaults).
  - `HoleReadBack.cs`: an Inventor-made Through All tapped hole that reports a thread depth opens unticked with that depth (below the seat, the expression read back as the user's). One with a full thread opens ticked with the row's depth. An AWB hole opens on its stored setting.
  - `HoleReapply.cs`: on a Through All tapped hole, a Full depth setting changed in Inventor is offered either way, and an unticked hole's thread depth is compared as a blind hole's is.
  - `HoleDiagram.cs`: `HoleDiagramInput.FullDepth` (default true) and `ThreadStops`. A Through All tapped hole that is not full depth draws the thread stopping at the thread depth below the seat, closed across as on a blind hole, with the part drawn deep enough to show plain hole below. The thread depth is dimensioned from the seat bottom. The summary reads "M6x1, through all, thread 9".
  - `AwbHoleViewModel.cs`: `ShowsFullDepth`, and `FullDepth`, which is ignored while not shown and goes through `Change`, so it previews. `ShowsThreadDepth` is now also true for a Through All tapped hole that is unticked. The Thread depth box is the blind hole's own box and property, so the typed record (`MarkTyped` in the `ThreadDepth` setter, `SettleBox` in `Refresh`), live typing and expressions apply unchanged. `ShowsThreadDepths` (task 114's unbound property) is removed; its two tests now check `ShowsThreadDepth`. An edited hole's thread depth counts as typed only when the hole uses it.
  - `HoleFeatureWriter.cs`: `WriteExpressions` wrote the thread depth's expression only for a blind hole. It now writes it for any straight thread that is not full depth, before the Distance-only depth expression. `CreateTapInfo(..., false, ThreadDepthCm)` and `SetThreadDepth` were already right, and the depth carries the seat offset from Core. Changing Full depth recreates the hole (`NeedsRecreate` already compares `FullTapDepth`).
  - `HoleFeatureReader.cs`: the thread depth's expression is read for any thread that is not full depth, not only a blind hole's.
  - `AwbHoleWindow.xaml`: a "Full depth" checkbox on its own row after Termination, shown by `ShowsFullDepth`, above the existing Thread depth row (which already binds with `UpdateSourceTrigger=PropertyChanged`). It has no access key because every letter of "Full depth" (F, U, L, D, E, P, T, H) is taken, as with Upper.
- **Not compiled:** none. The whole solution was built on Windows. Nothing here has been run in Inventor.
- **Inventor API members:** no new members. Used, all already compiling in this code: `HoleFeatures.CreateTapInfo` (both overloads), `HoleTapInfo.FullTapDepth`, `HoleTapInfo.ThreadDepth` (`Parameter`), `Parameter.Expression`, `Parameter.set_Units`. The task notes name `HoleTapInfo.FullThreadDepth`, but the existing reader and writer use `FullTapDepth`, which compiles, so that is the one used. `FullThreadDepth` was not used or checked.
- **Unconfirmed behaviour (Inventor):**
  - That Inventor takes a thread depth on a Through All hole created with `CreateTapInfo(..., false, depth)`, and keeps it when the hole is later changed in place.
  - That a Through All thread under a counterbore or countersink is measured from the start face, as task 116 took for a blind one, so 9 below a 6 mm counterbore is written as 15.
  - What Inventor does with a thread depth longer than the part. The window does not check it; the task says Inventor reports it.

**Manual checklist for Aidan (Inventor 2026, a block of your own at least 30 mm thick, with a few sketch points):**

1. AWB Hole. Type Tapped, M6, Termination Through All. The Full depth checkbox shows, ticked; there is no Thread depth box; the diagram threads the whole hole. OK: the thread runs all the way through, as before. (Criterion 1.)
2. AWB Hole again, M6 Through All, untick Full depth. The Thread depth box shows the workbook's default (1.5 x D, 9 for M6). The diagram shows the thread stopping at 9, with a dimension and plain hole below. Type 12 and wait: the preview follows without leaving the box. OK. In the model the thread texture runs 12 mm; Inventor's Hole dialog shows Full Depth off and thread depth 12. (Criterion 2.)
3. Tick and untick Full depth with the window open: the preview follows each time (the hole is remade). Cancel leaves the part as it was.
4. Same hole with a 6 mm counterbore and Thread depth 9. Inventor's thread depth reads 15, and the thread texture runs 9 below the counterbore floor. The diagram dimensions 9 from the counterbore floor.
5. Type `d0` (any length parameter of yours) in Thread depth, with the 6 mm counterbore. The box shows `= <value> mm`. OK. Inventor's thread depth reads `(d0) + (6 mm)`. Change `d0` in Parameters: the thread follows. (Criterion 3.)
6. Edit each hole from steps 2, 4 and 5 with AWB Hole: Full depth is unticked and Thread depth shows 12, 9 and `d0`, with no re-apply offer. Change the size to M8: the typed depth stays. (Criterion 4.)
7. Edit an M6 Through All hole that AWB Hole made full depth: it opens ticked. In Inventor's own Hole dialog, untick Full Depth on it and set a depth, then Edit in AWB Hole: the re-apply offer says "The thread stops at ...; AWB Hole threaded the full depth."
8. **A hole made with Inventor's own Hole command:** M6 tapped Through All, Full Depth off, thread depth 10. Edit with AWB Hole: it opens with Full depth unticked and Thread depth 10. With a 6 mm counterbore and thread depth 16 in Inventor, it opens with 10.
9. A Through All tapped AWB hole made before this build: Edit opens it ticked, with no re-apply offer.
10. A blind tapped hole and a Taper Tapped Through All hole show no Full depth checkbox. The blind one keeps its Thread depth and Drill depth boxes.
11. Untick Full depth and type 0 in Thread depth: the box says the thread depth must be greater than zero, and OK waits.
12. Access keys: Alt with each underlined letter still goes to its own field. Full depth has none.

## Defaults

- **No stored-data version bump.** `FullDepth` is a new optional attribute on version 5. Missing reads as ticked, which is how every older Through All tapped hole was made. Task 113 bumped the version so an older add-in would not misread new data. Here an older add-in would read an unticked hole as ticked, and its OK would thread the hole fully. Accepted, since only one add-in build is in use at a time. A bump to 6 is a one-line change if Aidan wants it.
- **Full depth is kept while the window is open**, across type and termination changes: a hole switched to Distance and back comes back unticked. Each new window starts ticked.
- **An AWB hole opens on its stored Full depth.** A setting changed in Inventor's dialog is offered for re-apply, as a blind hole's thread is, rather than the window following Inventor directly. A hole made by Inventor's Hole, or one whose data cannot be read, follows Inventor's `FullTapDepth` and thread depth.
- **The checkbox has no access key**: every letter of "Full depth" is in use.
- **The checkbox has a row of its own above Thread depth**, after Termination, like Toleranced.
- **A Through All thread depth is checked only for "greater than zero"**, not against the row's drill depth or the part.
- **An edited Through All hole that is ticked** opens with its unused thread depth not counted as typed, so it follows the size until the user unticks and types.
- **To a face stays full depth**, with no checkbox; the task names Through All only.
- **The diagram's summary** says "through all, thread 9" for an unticked hole.

## Follow-ups

- Confirm in Inventor that a Through All thread depth under a counterbore or countersink is measured from the start face, as task 116 took for a blind hole (checklist step 4).
- The task notes name `HoleTapInfo.FullThreadDepth`; the code uses `FullTapDepth`, which compiles. If both exist there is nothing to do, but the spike notes could say which one the add-in uses.
- A thread depth longer than the part is left to Inventor. Check what Inventor says, and whether the window should warn (it does not know the part's thickness).
- A hole ending To a face could take the same checkbox if Aidan wants it.
