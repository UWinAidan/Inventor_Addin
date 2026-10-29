# 108: Spike: Simple and Taper Tapped holes, spotfaces, seats on every type, tolerance on a clearance hole

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Types and seats", "Simple holes", "Taper tapped holes", "Toleranced clearance and Simple holes")
- **Status:** done (2026-10-28; the preview fix, spike 107 round 4, confirmed by Aidan the same day)
- **Depends on:** 106
- **Needs:** **windows** (spike, with Aidan in Inventor)
- **Parallel-safe with:** 103, 107 (other spikes, separate branches)

## Goal

Aidan, 2026-10-24: AWB Hole gets two more types, Simple (a diameter only) and Taper Tapped (NPT, BSPT), and a third seat, Spotface; every seat works on every type; Clearance and Simple holes can be toleranced. Settle how each is made, changed and read back through the API before tasks 114 and 115 build them. Nothing from this spike is merged.

## Questions

1. **Spotface.** How a `HoleFeature` gets a spotface (`HoleFeatures` definitions for a spotface placement, or `HoleFeature.SetSpotFace`?, and what it takes: diameter, depth, and how depth is measured); how to read one back (`HoleFeature.HoleType` value for a spotface, the spotface diameter and depth parameters); and how `HoleFeatureWriter.Change` switches between None, Counterbore, Countersink and Spotface on an existing hole.
2. **Taper Tapped.** How a tapered tapped hole is made (`HoleFeatures.CreateTaperedThreadInfo`?, the thread type names Inventor's thread table uses for NPT and BSPT, and the size names), whether it can be through or only blind, whether the hidden `_CreateTapInfo` route from spike 081 has a tapered equivalent so the model has the workbook's tap drill, and what the thread looks like in the model (task 095's display).
3. **Seats on a taper tapped hole**, and a spotface on a tapped and on a taper tapped hole: does Inventor accept each, and what does it measure the thread and drill depths from (needed for question 12's "from the bottom of the seat", task 116)?
4. **Simple.** A plain drilled hole at a typed diameter is what dowels already use; confirm a diameter written as an expression (`d0*2`, `8 mm`) keeps the hole parametric, like the depth boxes (task 100).
5. **Tolerance on a clearance hole.** Task 088 sets Inventor's fit tolerance (ISO rows) or deviations (Limits rows) on a dowel's diameter. Confirm the same works on a seated clearance hole's through diameter without touching the seat, and that Custom limits (typed deviations) go on the same way. Confirm the read-back.
6. **Read-back of a hole made by Inventor's command** with a spotface or a tapered thread: what `HoleFeatureReader` would see, so Edit opens it as the right type (spec 09, question 5).
7. **Seat depth 0 and a centre-drill spot** (Aidan, 2026-10-25): does Inventor accept a counterbore or spotface depth of 0 (and what it models), and a small countersink (60 and 90 deg) on a tapped, a dowel and a Simple hole? Task 127 is written from this.

## How

- A spike branch `spike/108-hole-types`, off `claude/m4-hole-generator`, with a temporary developer button that makes and changes each case on a test part and logs every call and the read-back to `awbaddin.log`. Aidan runs it on a part of his own and compares each hole with one made by Inventor's own Hole command. Results go in this file.
- Check every member against the Inventor API reference before trying it (`CLAUDE.md`).

## Results

Round 1, 2026-10-28. Aidan ran all 21 cases (`mode=make`, `dir=pos`) on a 20 mm block of his own, made a spotface hole and a taper tapped hole with Inventor's Hole command, then ran `mode=read`. Every hole made came out `kUpToDateHealth`. One case was refused (case 8).

**1. Spotface.**
- `HoleFeatures.AddSpotFaceByThroughAllExtent` and `AddSpotFaceByDistanceExtent2` both work. They take the spotface diameter and depth. `HoleType` reads `kSpotFaceHole`, and `SpotFaceDiameter` and `SpotFaceDepth` are model parameters.
- Depth is measured differently for a spotface:
  - The spotface depth is measured down from the start face.
  - The hole's `Depth` is measured from the spotface's bottom, not from the start face:
    - Through All in the 20 mm block reads `Depth` 1.95 cm with a 0.5 mm spotface (case 1), and 1.8 cm with Aidan's own 9.4 x 2 spotface.
    - Blind 12 with a 0.5 mm spotface ends 12.5 mm below the top (case 2), and blind 15 with a 1 mm spotface ends 16 mm below the top (case 13).
  - A counterbore is the opposite: the hole's `Depth` is measured from the start face (case 12 ends 15 mm below the top, with an 11 x 1 counterbore; Through All reads 2 cm, case 15).
- Changing the seat in place works in every order (case 3: counterbore → `SetSpotFace` → `SetCSink` → `SetCBore` → `SetDrilled` → `SetSpotFace`). `HoleType` follows each call. Inventor reuses the same parameters (`d28` for the seat diameter, `d29` for its depth), so there is no new parameter on each switch.
- **`HoleFeatureReader.ReadFacts` does not recognise a spotface:** `Seat` comes back empty for cases 1, 2, 3, 10, 13 and 18, and for Aidan's own spotface hole, and the spotface diameter and depth are not read.

**2. Taper Tapped.**
- `HoleFeatures.CreateTaperedTapInfo(true, "NPT", "1/4")` and `("BSP Taper Internal", "Rc 1/4")` are accepted. The holes are made by the plain `AddDrilledBy...` calls (blind, cases 4 and 6), and also Through All (case 5).
- The hole has no `HoleDiameter` parameter; the size comes from the table.
- `TaperedThreadInfo.TapDrillDiameter` is in the thread's own units: 0.422 for NPT 1/4 (inches, `Metric False`), 10.5 for Rc 1/4 (mm, `Metric True`). It is not in cm.
- Setting the tap drill before Add is refused (E_FAIL). Setting it after Add, by assigning a new `TapInfo`, is accepted and reads back 1.11, but the model's size does not change (case 7 has the same range as case 4). **The model's tap drill cannot be set**, so the workbook's tap drill can only appear in notes and tables. No hidden tapered equivalent of `_CreateTapInfo` was tried.
- Aidan's own taper hole from Inventor's dialog used the thread type `NPT for PVC Pipe and Fitting`, so the table has more taper types than `NPT` and `BSP Taper Internal`.
- **`HoleFeatureReader.ReadFacts` reads a taper tapped hole as Tapped with no type, designation or diameter**, because it casts `TapInfo` to `HoleTapInfo` only.

**3. Seats on tapped holes.**
- NPT 1/4 blind with a countersink 20 at 90° (case 9) and with a spotface 22 x 1 (case 10) are made.
- **NPT 1/4 blind with a counterbore 20 x 3 is refused** (E_FAIL, case 8), through `AddCBoreByDistanceExtent2` with a `TaperedThreadInfo`.
- M6x1 blind (drill 15, thread 10) is made with no seat, with a counterbore 11 x 1 and with a spotface 11 x 1 (cases 11 to 13). The drill depth follows the rule in question 1: from the start face with a counterbore, from the seat bottom with a spotface. The log's thread-face range covers the whole drilled cylinder, so it does not show where the thread starts; that needs a look in the model.

**4. Simple with an expression.** The diameter `spk108_d * 2` (with `spk108_d` = 4 mm) gives 8 mm, and Inventor stores the expression as `spk108_d * 2 ul`. After `spk108_d` is set to 5 mm, the hole is 10 mm. It stays parametric.

**5. Tolerance on a clearance hole.** `HoleFitWriter.ApplyTolerance` on a counterbored 6.6 Through All:
- H7 gives `kLimitsFitsStackedTolerance` 'H7' +0.015 / 0 mm on the hole diameter (case 15).
- Custom gives `kDeviationTolerance` +0.02 / 0 mm (case 16).
- The counterbore diameter keeps `kDefaultTolerance` both times.
- `ReadFacts` reads both back: the ISO code and deviations for H7, and the deviations with no code for Custom.

**6. Read-back of Inventor's own holes.** See the bold notes in questions 1 and 2. A spotface reads as no seat, and a taper tapped hole reads as Tapped with no thread data. Without changes to `HoleFeatureReader`, Edit would open both as the wrong type.

**7. Depth 0 and centre-drill spots.**
- A counterbore 11 x 0 (case 17) and a spotface 11 x 0 (case 18) are accepted. Each models only the hole (one face), and `HoleType` stays `kCounterBoreHole` or `kSpotFaceHole`.
- `ReadFacts` reads case 17 as Counterbore with depth 0.
- A small countersink is accepted on all three holes tried:
  - M6x1 Through All with 6.5 at 90° (depth 0.79 mm, full thread kept)
  - a 6 mm dowel blind 12 with 7 at 60°
  - a Simple 4 mm blind 10 with 5 at 60°

**Round 2 (Aidan, 2026-10-28, in Inventor).**
- **Taper thread in the model:** the thread texture covers the upper part of each taper tapped hole (the table's effective length), with a plain bore below. `TaperedThreadInfo` has a writable `FullThreadDepth`, and `EffectiveLength` is typed `Object` and writable, but neither was tried, so it is unknown whether a taper thread depth can be set.
- **Counterbore on a taper tapped hole:** Inventor's own Hole command greys out Counterbore for Taper Tapped and offers Spotface and Countersink. Case 8's refusal is Inventor's rule. Aidan: AWB Hole does not offer Counterbore on a taper tapped hole.
- **Edit read an Inventor-made Ø10 blind hole as Clearance M8 Loose.** That is correct: the workbook's M8 row is Close 8.4, Normal 9, Loose 10, and `HoleReadBack` matches the diameter exactly.

**Preview (spike 107 rounds 2 to 4, run in this build).**
- Round 2: `Face.CalculateFacets` throws DISP_E_TYPEMISMATCH from C# on every face, so `source=faces` drew nothing.
- Round 3 (`461e122`) takes each face's extent from points sampled along its edges (`CurveEvaluator.GetParamExtents` and `GetPointAtParam`, `ref` arrays the caller allocates).
  - Aidan: a counterbore or countersink running off the edge is drawn whole (pass).
  - Aidan: looking down a through hole shows red, so the end caps work (pass).
  - **A drill point is still missing (fail).** The log shows a NullReferenceException on the cone face, so the whole cone was skipped.
- Round 4 (`f0a2f33`): an edge that cannot be sampled (the apex) falls back to its vertices, and a cone whose points all sit at one height still runs to its apex. Aidan, in Inventor: the drill point is red from the side, a counterbore off the edge is whole, and a through hole is red down the bore. All pass; the preview matches Inventor's in every view.

## Recommendations

**Spotface (task 114).**
- Make a spotface with `AddSpotFaceBy...Extent`, and change a seat in place with `SetSpotFace`, `SetCBore`, `SetCSink` and `SetDrilled`. No recreate is needed to change a seat.
- `HoleFeatureReader` must read `kSpotFaceHole` with its `SpotFaceDiameter` and `SpotFaceDepth` into a Spotface seat. Today it returns no seat.
- **Depth reference:** Inventor measures a spotface hole's `Depth` from the spotface bottom and a counterbore hole's from the start face.
  - Spec 09 measures the drill depth from the seat bottom (question 12). So the writer passes the window's depth unchanged for a spotface, and adds the seat depth for a counterbore.
  - The reader does the reverse.
  - Check this against task 116's rule before writing it.

**Taper Tapped (task 114).**
- Make with `CreateTaperedTapInfo(true, threadType, designation)` and the plain `AddDrilledBy...`, `AddCSinkBy...` or `AddSpotFaceBy...` calls. Blind and Through All both work.
- Thread types come from the Taper Tapped tab and must match Inventor's thread table: `NPT` (designations like `1/4`), `BSP Taper Internal` (`Rc 1/4`), and others such as `NPT for PVC Pipe and Fitting`.
- **No Counterbore seat** (Aidan): the window hides or disables Counterbore for Taper Tapped, as Inventor does.
- **The model's tap drill is Inventor's table value.**
  - `TapDrillDiameter` cannot be set before Add, and setting it afterwards does not change the model.
  - It is in the thread's own units (inches for NPT, mm for metric).
  - The workbook's Tap drill column can only feed notes and hole tables. This replaces spec 09's "modelled at that diameter" (Default, Inventor's limit; spec question).
- **Thread depth on a taper tapped hole is unproven.** Inventor threads the table's effective length. Until a short round sets `FullThreadDepth` and `EffectiveLength`, the window shows no Thread depth box for taper tapped holes, and the drill depth stays editable (Default; spec question).
- `HoleFeatureReader` must read a `TaperedThreadInfo` (type, designation, Through All or blind) into a Taper Tapped spec. Today it casts to `HoleTapInfo` only and loses the thread.

**Simple (task 114).** Write the diameter as an expression through the hole's diameter parameter, as task 100 does for depths. Inventor keeps it parametric and stores the expression with a trailing ` ul` for a unitless factor. The reader should show the expression without the ` ul`.

**Tolerance on clearance and Simple holes (task 115).** `HoleFitWriter.ApplyTolerance` works unchanged on a seated clearance hole's diameter:
- ISO gives `kLimitsFitsStackedTolerance`, and Custom gives `kDeviationTolerance`.
- The seat is not touched.
- `ReadFacts` reads both back.
- Nothing new is needed in the API layer.

**Depth 0 and centre-drill spots (task 127).**
- A counterbore or spotface depth of 0 is accepted: Inventor keeps the hole type and models only the hole. Allow 0.
- A countersink at 60 or 90° is accepted on tapped, dowel and Simple holes. The read-back works with existing code, except that a spotface needs task 114's reader change.

**Preview (task 110).** Build each face's solid from edge samples, as in round 4 (confirmed by Aidan, 2026-10-28).
