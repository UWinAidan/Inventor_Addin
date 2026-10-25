# 108: Spike: Simple and Taper Tapped holes, spotfaces, seats on every type, tolerance on a clearance hole

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Types and seats", "Simple holes", "Taper tapped holes", "Toleranced clearance and Simple holes")
- **Status:** todo
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

Filled in after the rounds with Aidan.

## Recommendations

Filled in after the rounds with Aidan; tasks 114, 115 and 127 are completed from them.
