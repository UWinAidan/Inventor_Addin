# 114: Simple and Taper Tapped holes and spotfaces in Inventor and the window

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Types and seats", "Simple holes", "Taper tapped holes")
- **Status:** done, built on Windows, not yet run in Inventor (completed from spike 108's Recommendations, 2026-10-28)
- **Depends on:** 108, 112, 119
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

The AWB Hole window makes, changes and reads back Simple and Taper Tapped holes and spotfaces, and any seat on any type, as real Inventor hole features (Aidan, 2026-10-24).

## Scope

Spike 108's Results and Recommendations (`docs/tasks/108-spike-hole-types-and-spotface.md`) are the API reference for this task.

- `src/InventorAddin/Holes/HoleFeatureWriter.cs`:
  - **Spotface:** `HoleFeatures.AddSpotFaceByThroughAllExtent` / `AddSpotFaceByDistanceExtent2` on a new hole; `SetSpotFace(diameter, depth)` on an existing one. Every seat change (None, Counterbore, Countersink, Spotface) is made in place with `SetDrilled`, `SetCBore`, `SetCSink`, `SetSpotFace`; no recreate.
  - **Depth reference:** Inventor measures a spotface hole's `Depth` from the spotface bottom and a counterbore hole's from the start face. The window measures drill and thread depths from the bottom of the seat (spec 09 question 12), so the writer adds the seat depth for a counterbore and passes the depth unchanged for a spotface. Confirm against task 116 before writing it, and cover the conversion with Core tests (the arithmetic belongs in Core).
  - **Taper Tapped:** `HoleFeatures.CreateTaperedTapInfo(true, threadType, designation)` with the plain `AddDrilledBy...`, `AddCSinkBy...`, `AddSpotFaceBy...` calls; blind and Through All. The thread type and designation are the Taper Tapped tab's, which must match Inventor's thread table (`NPT` / `1/4`, `BSP Taper Internal` / `Rc 1/4`). Do not set `TapDrillDiameter`: Inventor refuses it before Add and ignores it after (spike 108 case 7).
  - **Simple:** the diameter written to the hole's diameter parameter as an expression, as task 100 writes depths.
- `src/InventorAddin/Holes/HoleFeatureReader.cs` (and `HoleReadBack` in Core):
  - `kSpotFaceHole` with `SpotFaceDiameter` and `SpotFaceDepth` reads as a Spotface seat (today: no seat).
  - A `TaperedThreadInfo` reads as Taper Tapped with its type and designation (today the cast to `HoleTapInfo` loses it).
  - A Simple diameter expression is shown without Inventor's trailing ` ul`.
  - Depths are converted back to "from the seat bottom" by the reverse of the writer's rule.
- `src/InventorAddin/Holes/HoleStoredDataStore.cs`: stores the new type, seat and diameter expression as for the existing ones.
- Core view-model (task 112's): **no Counterbore seat on Taper Tapped** (Aidan, 2026-10-28; Inventor's rule): the Counterbore button is disabled for that type, and switching to Taper Tapped from a counterbored hole moves the seat to None with a note. No Thread depth box on a taper tapped hole (Default until a spike round proves a taper thread depth can be set; spec 09 question); the drill depth stays editable.
- `src/InventorAddin/UI/AwbHoleWindow.xaml(.cs)`: the four Type buttons, the Spotface seat button, the Diameter box, the seat boxes for spotfaces and for seats on Simple and Taper Tapped holes, bound to task 112's view-model; the icons from task 119.
- Edit (task 092): a hole made by Inventor's command with a spotface or a tapered thread opens as the right type.

## Acceptance criteria

- [ ] Each type with each allowed seat (Taper Tapped: None, Countersink, Spotface) can be made, changed in the window, OK'd, undone in one step, cancelled, and edited again.
- [ ] Changing the seat on an existing hole keeps the feature (same name and browser place).
- [ ] A spotface hole and a counterbore hole with the same window depth end at the same depth below the seat bottom.
- [ ] Inventor-made spotface and taper tapped holes open in Edit as Spotface and as Taper Tapped with the right thread.
- [ ] A Simple hole's diameter typed as a parameter expression stays linked to that parameter.
- [x] Counterbore is unavailable on Taper Tapped (view-model; tested).
- [x] Core tests for the depth conversion, the read-back of the new types and the Taper Tapped seat rule.
- [x] The solution builds on Windows; `dotnet test tests/InventorAddin.Core.Tests` passes.

## Verification

**Depth reference: what was built differs from this brief, and needs the parent's or Aidan's look.** The brief says the writer should add the seat depth for a counterbore and pass the depth unchanged for a spotface, because the window measures from the bottom of the seat. That is task 116's rule, and task 116 is not done: the window, the diagram, the checks and the stored data still measure every depth from the top face. `DECISIONS.md` (2026-10-24, Default) also keeps a blind clearance, dowel or Simple hole's Depth measured from the top face (spec 09 question 16). Writing the brief's rule now would make a counterbored tapped hole deeper than its diagram shows, and a spotface clearance hole deeper than its Depth box, with nothing in the stored data marking the change. What was built instead keeps the window's current meaning:
- spotface: the add-in writes the window depth less the spotface depth, and reads back Inventor's depth plus the spotface depth;
- every other seat: written and read unchanged;
- this applies to the drill or hole depth and to a thread depth (`HoleDepthReference` in Core).

The result is that a spotface hole and a counterbore hole with the same window depth end at the same depth below the top face. They end at the same depth below the seat bottom only when the seats are equally deep, so acceptance criterion 3 is met only in that sense. Task 116 changes this rule for threaded holes in that one class. If the brief's rule is wanted now, it is a change to `HoleDepthReference.ToInventorCm` and `ToWindowCm` plus their tests.

- **Ran (Windows, Inventor not running):**
  - `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: succeeded, 0 errors. The 4 warnings (CS8600 in the Create Print, New Part, Save As Copy and Save As Replace commands, each reported twice) were there before this task.
  - `dotnet test tests/InventorAddin.Core.Tests`: 4219 passed, 0 failed. Before this task: 4185.
- **Built, file by file:**
  - Core `HoleDepthReference.cs` (new): the spotface conversion in both directions, for values and for expressions. A depth typed as an expression is written as `(window) - (spotface)`, so it stays parametric. Reading back undoes that form; any other expression gets `+ (spotface)` added.
  - `HoleRules`: the geometry carries Inventor's depths (`DepthCm`, `DepthExpression` and the thread depth) for a spotface. `HoleReadBack.Read` and `HoleReapplyRules.Check` convert the facts back first.
  - `HoleExpressions.FromInventor`: drops a trailing ` ul` after a number, and nothing else.
  - `HoleFeatureMessages.SpotFaceNotOffered` is retired and replaced by `SeatNotOffered`, which is used only for a hole type Inventor reports that is not drilled, counterbore, countersink or spotface.
  - View-model:
    - `HoleOption.IsEnabled` is new.
    - On Taper Tapped, Counterbore is listed but disabled, with a tooltip saying why, and the `Seat` setter refuses it.
    - `SeatNote` appears under Seat when a counterbore was dropped by switching to Taper Tapped. It goes when another seat is picked, or when the type changes back and the counterbore returns.
  - `HoleFeatureReader`:
    - `kSpotFaceHole` reads as Spotface, with `SpotFaceDiameter`, `SpotFaceDepth` and its expression.
    - A `TaperedThreadInfo` sets `TaperedThread`, the thread type and the designation.
    - The `HoleDiameter` expression of an untapped hole is read without its trailing ` ul`.
  - `HoleFeatureWriter`:
    - New holes: `AddSpotFaceByThroughAllExtent`, `AddSpotFaceByDistanceExtent2` and `AddSpotFaceByToFaceExtent2`.
    - Seat changes: `SetSpotFace`, or the spotface parameters written directly on a hole that is already a spotface (a depth that was an expression is forced back to a number, as for a counterbore).
    - Taper Tapped holes: made with `CreateTaperedTapInfo(true, type, designation)`. No tap drill is set.
    - Simple holes: the diameter expression is written to `HoleDiameter`, and a diameter that is no longer an expression is forced back to a number.
    - Spotface depth expressions are written.
  - `HoleFitWriter.SeatOf`: `kSpotFaceHole` now maps to Spotface (task 122's face roles already treat a spotface as a counterbore).
  - `AwbHoleWindow.xaml`:
    - Standard and Size are hidden for Simple, and `SizeNote` shows under Size.
    - New boxes: a Diameter box (access key M) and the Spotface dia (C) and Spotface depth boxes. All use `UpdateSourceTrigger=PropertyChanged` (task 130) and show the expression's value and any problem under the box.
    - Thread depth binds to `ShowsThreadDepth` and Drill depth to `ShowsDrillDepth`, so a blind taper tapped hole shows Drill depth only.
    - `SeatNote` shows under Seat.
    - The Type and Seat buttons were already bound to the option lists. Their icons are task 119's.
  - `Styles.xaml`: `IconChoiceItem` binds `IsEnabled` to the option and shows its tooltip when it is disabled.
  - `HoleStoredDataStore`: no change, since it copies Core's attributes generically. Stored data stays at version 4, because its shape did not change.
- **Task 111 check:** every starter Taper Tapped size is in Column A of Inventor 2026's `thread.xlsx`, on the sheet the row names: NPT `1/16`, `1/8`, `1/4`, `3/8`, `1/2`, `3/4`, and BSP Taper Internal `Rc 1/8`, `Rc 1/4`, `Rc 3/8`, `Rc 1/2`, `Rc 3/4`. Task 112's rule for matching a thread to its Spotface Size (`SpotfaceRowFor`, by the same nominal) is already there.
- **Not compiled:** none. The whole solution was built on Windows. Nothing here has been run in Inventor.
- **Inventor API members:** each one is checked against `Autodesk.Inventor.Interop.xml` (Inventor 2026) and spike 108.
  - Used in spike 108: `AddSpotFaceByThroughAllExtent`, `AddSpotFaceByDistanceExtent2`, `SetSpotFace`, `SpotFaceDiameter`, `SpotFaceDepth`, `kSpotFaceHole`, `CreateTaperedTapInfo`, and `TaperedThreadInfo.ThreadType` and `ThreadDesignation`.
  - In the API reference but **not yet run**: `AddSpotFaceByToFaceExtent2`, which is reached only for an edited hole that ends at a face.
  - **Unconfirmed behaviour:**
    - Where Inventor measures a thread depth from on a spotface hole. It is taken to be the spotface bottom, as for the drill depth.
    - Whether `TaperedThreadInfo.ThreadDesignation` reads back as `1/4` or as the custom designation `1/4 - 18 NPT`. Only the first matches the Taper Tapped row.
    - Whether `SetCSink` and `SetDrilled` work on a tapered hole. They are not used: the API reference says `SetSpotFace` fails on a tapered thread, so any seat change on a taper tapped hole remakes the feature in place (same name, same browser place).

**Manual checklist for Aidan (Inventor 2026, a 20 mm block of your own with a few sketch points):**

1. AWB Hole. Type Clearance, size T-whatever you use, Seat Spotface. The Spotface dia and depth boxes show (from the Spotface row, or empty). Type 11 and 0.5, blind Depth 12. OK. In Inventor's Hole dialog (Use Inventor's Hole for once), the hole reads spotface 11 x 0.5, and the bottom of the drill (not the point) is 12 mm below the top. One Undo removes it.
2. Edit it (double-click). It opens as Spotface 11 x 0.5, Depth 12. Change the seat to Counterbore, then Countersink, then None, then Spotface again, pressing OK between some of them. The browser name and position stay the same each time.
3. Make a counterbored hole of the same size, with a 2 mm deep counterbore and Depth 12, and a spotfaced one with a 2 mm deep spotface and Depth 12. Both drills end 12 mm below the top (see the Depth reference note above).
4. Type Simple. Standard and Size disappear and a Diameter box shows. Type `d0*2` (or any parameter of yours times 2). The value appears beside the box. OK. Change the parameter in Parameters: the hole follows. Edit the hole: the box shows your expression without ` ul`.
5. Type Taper Tapped, Imperial, NPT `1/4`. The Counterbore button is greyed out, with a tooltip saying why. Make blind holes with no seat, with a countersink and with a spotface, then Through All. Each has the thread texture in the model. A blind one shows Drill depth and no Thread depth.
6. Start with Clearance and a counterbore, then switch the Type to Taper Tapped. Seat becomes None and a note under Seat says why. Switch back to Clearance: the counterbore comes back and the note goes.
7. On an existing taper tapped hole, change the seat from None to Spotface and press OK. The hole keeps its name and browser place (it is made again in place). Undo once.
8. With Inventor's own Hole command, make a spotface hole and an NPT 1/4 taper tapped hole. Edit each with AWB Hole: they open as Spotface (with the right sizes, and Depth plus the spotface depth) and as Taper Tapped NPT 1/4. If the taper hole opens with "not in the Taper Tapped tab", note the designation in the message (see the unconfirmed members above).
9. Edit an Inventor hole of an odd diameter, for example 6.25 mm. It opens as Simple with Diameter 6.25 and a note.
10. Task 137: on a seated clearance hole, change the Size back and forth between a size with seat rows and one without. Fit appears and disappears, but the window does not jump or resize in a way that moves the buttons under the mouse.
11. Blind tapped M-size with a 1 mm spotface and thread depth 7.5, drill 10: check in the model where the thread texture starts and ends (the thread depth on a spotface is unconfirmed).

## Defaults

- **Spotface depths are converted to keep the window's current meaning (from the top face)**, not the brief's "from the seat bottom" (see Verification). Task 116 changes this for threaded holes.
- **A thread depth on a spotface hole is converted like the drill depth**, assuming Inventor starts the thread at the spotface's bottom.
- **Any seat change on a taper tapped hole remakes the feature** (same name and browser place). The API reference says `SetSpotFace` fails on a tapered thread, and the other in-place calls were not tried on one.
- **Counterbore on Taper Tapped is shown disabled with a tooltip**, rather than left out of the list. The note when it is dropped sits under Seat and stays until another seat or type is chosen.
- **Only a trailing ` ul` after a number is dropped** from a Simple diameter expression read from Inventor. A ` ul` elsewhere is kept.
- **A depth expression read from an Inventor-made spotface hole** that is not in the add-in's `(a) - (b)` form opens as `(expression) + (spotface depth)`, so it keeps its parameters.

## Follow-ups

- ~~Decide the depth reference~~: the parent kept it as built (2026-11-05); task 116 switches it to the seat bottom.
- If Inventor reads a taper thread's designation back as the custom designation (`1/4 - 18 NPT`), `HoleReadBack`'s Taper Tapped matcher needs to accept it (checklist step 8).
- A short spike round: do `SetCSink` and `SetDrilled` work in place on a taper tapped hole? If they do, the writer could stop remaking the feature for those seat changes.
- `AwbHoleViewModel.ShowsThreadDepths` is no longer bound by the window. Remove it, or keep it for task 136.
- The Diameter and spotface boxes have no tests at the window level (WPF). Bindings were checked by the build only.
