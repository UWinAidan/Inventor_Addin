# 112: Core: Simple and Taper Tapped types, Spotface, any seat on any type

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window", "Types and seats", "Simple holes", "Taper tapped holes")
- **Status:** done
- **Depends on:** 111
- **Needs:** cloud or windows (Core only)
- **Parallel-safe with:** 110, 119

## Goal

Core can describe, check, store and lay out every type with every seat (Aidan, 2026-10-24): Clearance, Tapped, Simple and Taper Tapped, each with None, Counterbore, Countersink or Spotface. The add-in side is task 114.

## Scope

- `HoleSpec.cs`: `HoleKind` gains Simple and TaperTapped; `HoleSeat` gains Spotface; `HoleStandards` gains the Simple and Taper Tapped types and the "AWB Taper Tapped" standard (Simple has none).
- `HoleRules.cs`, `HoleSeatCheck.cs`, `HoleGeometry.cs`: geometry and checks for the new cases (a spotface wider than the hole, a seat deeper than a blind hole, a taper tapped tap drill); a Simple hole's diameter from the Diameter box.
- Seat sizes, as spec 09 "Types and seats" and "Seat boxes" (Defaults, question 19): a spotface from the Spotface row of the same size, always editable; any seat on Simple and Taper Tapped sized in boxes, starting from the last values used with OK. Extend `HoleLastDowelSeats` or add a sibling for Simple (per units and seat) and Taper Tapped (per units, size and seat) seats, and a last Simple diameter per units, kept in `AddinSettings` across restarts.
- `HoleStoredData.cs`: the new kinds, the spotface and the Simple diameter (with any expression); bump the stored version and keep reading the old one. `HoleReadBack.cs`, `HoleEditRules.cs`, `HoleReapply.cs`: the new cases.
- `ViewModels/AwbHoleViewModel.cs`: Type lists four; Standard hidden for Simple and showing AWB Taper Tapped for Taper Tapped; Size hidden for Simple; a Diameter box (expressions, as Depth, task 100); Fastener type only for a clearance or tapped hole with a counterbore or countersink; the seat boxes for a spotface on every type and for any seat on dowel, Simple and Taper Tapped; thread and drill depths for blind Taper Tapped holes; the cascades when Type changes (seat kept where the new type can take it, as spec 09 "Editing reads the hole first").
- `HoleDiagram.cs`: a spotface, and the new types (a taper thread drawn as the tapped thread, or as the mockup's style).
- `HoleIcons.cs`: names for the new Type and Seat buttons; the drawings are task 119.
- Tests for all of it in `tests/InventorAddin.Core.Tests`.

Out of scope:

- Tolerancing (113); depths below the seat (116); writing to Inventor (114).

## Acceptance criteria

- [x] Every type with every seat can be described, checked, stored and read back in Core; old stored data still reads.
- [x] The view-model shows and hides fields as spec 09's table says, for each type.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes.

## Notes for the implementer

- Spike 108 may not be done when this runs. Where its answer changes Core (for example whether a taper tapped hole can be through), choose the simplest rule, note it in Verification, and task 114 adjusts it.

## Verification

Spike 108 was done when this ran, so Core follows its answers and the 2026-10-28 decisions, not the "simplest rule" fallback above: no Counterbore on Taper Tapped; a spotface depth of 0 is allowed; a taper tapped hole is checked and drawn at the Taper Tapped row's tap drill, while Inventor models its own thread-table tap drill (the add-in writes none); there is no Thread depth box on a taper tapped hole; and a taper tapped hole can be blind or Through All.

- **Ran (Windows):**
  - `dotnet build src/InventorAddin.Core`: succeeded, 0 warnings.
  - `dotnet test tests/InventorAddin.Core.Tests`: 4001 passed, 0 failed. Before the task: 3903 passed.
  - `dotnet build InventorAddin.slnx -p:DeployToInventor=false`, with Inventor not running: succeeded with 0 errors. The 4 warnings were already there (CS8600 in the Create Print, New Part, Save As Copy and Save As Replace commands).
  - One run showed a timeout failure in `RenamePlanTests.ListFilesToSearch_AFolderThatDoesNotAnswer_IsSkippedAfterTheProbeTimeout`. It does not touch hole code and passed on every later run.
- **What was built, file by file:**
  - `HoleSpec.cs`:
    - `HoleKind.Simple` and `HoleKind.TaperTapped`, and `HoleSeat.Spotface`. All three are appended, so stored enum names still read.
    - `HoleStandards` gains the Simple and Taper Tapped types, the AWB Taper Tapped standard, `Types`, `KindFor(type, standard)`, `Takes(kind, seat)` (everything except Counterbore on Taper Tapped) and `IsThreaded`.
    - `HoleSpec` gains `Diameter` and `DiameterExpression` (Simple), and `SpotfaceDiameter`, `SpotfaceDepth` and `SpotfaceDepthExpression`. `Normalised` was extended for all of them.
  - `HoleRules.cs`:
    - `BuildSimple`, `BuildTaperTapped`, a spotface on every kind, and the `HoleField` values `Diameter`, `SpotfaceDiameter` and `SpotfaceDepth`.
    - `SameSizeSpotfaces` and `SpotfaceRowFor`, which use the same-size rule tapped and dowel seats already use. `SpotfaceStart`.
    - Spotface checks: the spotface must be wider than the hole or thread; a depth of 0 or more is allowed; it must be shallower than a blind hole's depth.
    - `TaperCounterboreText`.
    - `SeatRowStart` and `DowelSeatStart` also return the spotface values from the row.
  - `HoleGeometry.cs`: `SpotfaceDiameterCm`, `SpotfaceDepthCm`, `SpotfaceDepthExpression`, `DiameterExpression` and `HoleThread.IsTapered`. All are init-only, so the add-in's positional constructors still compile.
  - `HoleSeatCheck.cs`: checks a spotface's diameter and depth.
  - `HoleReadBack.cs`:
    - `InventorHoleFacts` gains `TaperedThread`, `SpotfaceDiameterCm`, `SpotfaceDepthCm`, `SpotfaceDepthExpression` and `DiameterExpression`. Task 114 fills them from Inventor.
    - A taper thread reads as Taper Tapped, matched by thread type and size.
    - A spotface reads as a clearance hole, dowel or Simple hole with its own spotface.
    - A plain hole whose diameter no Clearance row has now reads as a Simple hole of that diameter (see Defaults).
  - `HoleStoredData.cs`: version 3. It stores the new kinds and seat, the Simple diameter and its expression, and the spotface sizes and their expression. A Simple hole stores no Size. Versions 1 and 2 still read (tested).
  - `HoleReapply.cs`: compares spotface sizes. A taper tapped hole's diameter is not compared, and its drill depth is.
  - `HoleLastDowelSeats.cs`:
    - `HoleDowelSeat` gains spotface values as init-only JSON properties, so old settings files still read. `HoleDowelSeat.Of(spec)`.
    - `HoleLastDowelSeats` gains `ForSimple(units)`, `ForTaperTapped(units, size)` and `ForKind`. Remember now also covers Simple, Taper Tapped, and spotfaces on dowels and tapped holes.
    - The class was not renamed.
  - `HoleLastDepths.cs`: remembers the last Simple diameter per units, as a number. A taper tapped hole leaves no blind depth to remember.
  - `AddinSettings.cs`: `LastTaperTappedSeatsMm/In`, `LastSimpleSeatMm/In` and `LastSimpleDiameterMm/In`, included in Equals and GetHashCode.
  - `HoleDiagram.cs`: a spotface is drawn as a shallow counterbore; a depth of 0 is drawn flush with the top face, with no depth dimension. A taper thread is drawn as the tapped thread, with no thread-depth dimension. The summary includes "spotface ⌀D by d" and, for a blind taper tapped hole, "drill X".
  - `HoleIcons.cs`: `Simple`, `TaperTapped` and `Spotface`, and `ForType` and `ForSeat` for them. `All` now has 14 names. Task 119 draws them.
  - `HoleFaceColours.cs`: `Classify` treats a Spotface seat as a counterbore, as task 122 decided for a spot face.
  - `ViewModels/AwbHoleViewModel.cs`:
    - Type lists four. Spotface is in the seat list. Counterbore is left out of the seat list on Taper Tapped.
    - `ShowsStandard` is false for Simple. `ShowsSize` is false for Simple. `SizeNote` names the missing Taper Tapped tab.
    - A `Diameter` box for Simple: typed values are recorded (`MarkTyped`, `SettleBox`), and it takes expressions. `ShowsDiameter`, `DiameterText`, `DiameterValueText` and `DiameterProblem`.
    - Spotface boxes on every type: `ShowsSpotfaceSizes`, `SpotfaceDiameter[Text]`, `SpotfaceDepth[Text]`, the matching ValueText and Problem properties.
    - Fastener type shows only for a counterbore or countersink on a clearance or tapped hole. The Fit list stays for a clearance hole with a spotface.
    - `ShowsDrillDepth` and `ShowsThreadDepth` are new. `ShowsThreadDepths` is unchanged, for the current window.
    - The drill depth starts from the Taper Tapped row. A Simple hole's first depth is the multiplier x its Diameter.
    - Seat-box start values follow spec 09 "Seat boxes". The cascade keeps a seat the new type can take (task 123).
- **Tests added:**
  - `tests/InventorAddin.Core.Tests/Holes/HoleTypesAndSpotfaceTests.cs`, 61 tests: spec, rules, seat check, stored data, read-back, re-apply, remembered values including a settings-file round trip, diagram and icons.
  - `tests/InventorAddin.Core.Tests/ViewModels/AwbHoleTypesViewModelTests.cs`, 29 tests.
  - One `HoleFaceColours` test, and 7 `Takes` theory cases.
  - Existing tests updated for the wider lists: four types, Spotface in the seat list, 14 icon names, stored version 3, the new settings, and the read-back change below.
- **Not compiled (changed under `src/InventorAddin`):** none. Nothing under `src/InventorAddin` changed. The whole solution was built on Windows against the changed Core.
- **Inventor API members not confirmed:** none are used. This task writes no Inventor code.
- **Until task 114 is done, do not use the new choices in Inventor.** The window binds `TypeOptions` and `SeatOptions`, so it now shows Simple, Taper Tapped and Spotface buttons, with no icons until task 119. The add-in does not write them yet:
  - A Simple hole is made as a drilled hole of its number; an expression is not written.
  - A spotface is made as a hole with no seat, and the seat check reports the difference.
  - A taper tapped hole passes its size to `HoleTapInfo`, which Inventor will probably refuse.
  - The new Diameter and Spotface boxes are not bound yet, so the window cannot show or edit them.
- **Manual checklist:** none needed for this task, since no add-in code changed. Task 114's checklist covers the window.

## Defaults

These choices were made to keep the work moving. Aidan can overturn any of them.

- **Simple has no standard.** `HoleStandards.StandardName(Simple)` is `""`. The window's kind comes from the Type for Simple, and from the Standard for every other type.
- **An Inventor-made plain hole that matches no Clearance row opens as a Simple hole of its own diameter**, with a note. This includes a hole with a spotface. Before this task it opened as the closest Clearance row. A hole whose diameter cannot be read opens as a Simple hole with an empty Diameter.
- **A spotface on a clearance size with no Spotface row starts empty and OK waits.** No last values are used, and none are remembered for a clearance hole. This matches Aidan's answer to question 26 (task 137).
- **A spotface on a tapped hole or dowel starts from the Spotface row of the same size**, then from the last spotface kept for that thread or dowel size. On a dowel, the row comes before the last values, unlike the dowel's counterbore and countersink, which put the last values first.
- **A seat on a taper tapped hole must be wider than the row's tap drill**, since the tab has no nominal. The tap drill is also the hole diameter used for the checks and the diagram.
- **A spotface's depth must be less than a blind hole's depth**, compared as a counterbore's is: the Depth, or the drill depth for a threaded hole. Task 116 may change this for seats measured from the seat bottom.
- **A counterbore chosen before switching to Taper Tapped falls back to None** with no message, under task 123's rule, and comes back on a type that takes it. Task 114's brief asks for a note; that is left to 114. `HoleRules` refuses a counterbore on a taper tapped spec with `TaperCounterboreText` under Seat.
- **Simple and Taper Tapped seats are remembered by kind.** A Simple hole's last seat is one `HoleDowelSeat` per units; each seat's values are kept apart, so this is "per units and seat". A taper tapped hole's last seat is one entry per units and size.
- **The last Simple diameter is remembered as a number, per units**, as the blind depth is (question 23). An untyped Diameter box follows a units switch to the other units' last diameter, else it empties.
- **A Simple hole's untyped first Depth follows its Diameter** (multiplier x D), as another hole's follows its size, unless a last depth exists.
- **An edited Simple or taper tapped hole's seat values always count as typed**, as a dowel's do, since they follow no fastener row.
- **On a units switch, a taper tapped size moves to the nearest size by tap drill.**
- **A spotface depth remembered as an expression is read again in the next part**, as a dowel's counterbore depth is (task 125). A depth of 0 is kept.
- **The diagram draws a spotface with no depth yet at 0.2 x D.** A taper thread is drawn as a straight thread, as the brief allowed.
- **`HoleDowelSeat` keeps its name.** It now holds spotface values too; renaming it was not cheap, so it was left.
- **The stored data version is 3.** Version 2 data reads as before.

## Follow-ups

- **Task 114, window bindings.** Bind the new view-model properties: `ShowsStandard`, `ShowsSize`, `SizeNote`, `ShowsDiameter`, `DiameterText`, `DiameterValueText`, `DiameterProblem`, `ShowsSpotfaceSizes`, `SpotfaceDiameterText`, `SpotfaceDepthText`, `SpotfaceDepthValueText`, `SpotfaceDiameterProblem`, `SpotfaceDepthProblem`, `ShowsThreadDepth` and `ShowsDrillDepth`. The window still binds both depth rows to `ShowsThreadDepths`, which is false for Taper Tapped, so a blind taper tapped hole's Drill depth box stays hidden until 114 binds it to `ShowsDrillDepth`.
- **Task 114, writer and reader.** The writer should use `HoleGeometry.SpotfaceDiameterCm`, `SpotfaceDepthCm`, `SpotfaceDepthExpression`, `DiameterExpression` and `Thread.IsTapered`. The reader should fill `InventorHoleFacts.TaperedThread`, `SpotfaceDiameterCm`, `SpotfaceDepthCm`, `SpotfaceDepthExpression` and `DiameterExpression`. `HoleFitWriter.SeatOf` can map `kSpotFaceHole` to `HoleSeat.Spotface` now that the seat exists.
- **Task 114, counterbore note.** The note shown when switching to Taper Tapped drops a counterbore (114's brief) is not built. The view-model falls back to None without a message.
- **Re-apply offer for a Simple hole's diameter.** A diameter driven by an expression is compared by its stored number (`HoleAppliedState`). A parameter change would therefore offer re-apply, where a depth expression is compared by what it evaluates to. Compare the diameter expression the same way later.
- **`HoleEditRules.cs` needed no change.** Nothing in it depends on the hole's kind or seat.
- **Task 127.** A counterbore depth of 0 is still refused by `HoleRules` and dropped by `HoleDowelSeat.Valid`. Only the spotface depth of 0 is allowed here, as the 2026-10-28 decision says.
- **Task 113.** Tolerance on Simple holes builds on this: `ShowsTolerance` and the Fit list are empty for Simple now.
