# 136: Full depth checkbox on a Through All tapped hole

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window", "Tapped holes")
- **Status:** planned
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
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes, and the solution builds on Windows.

## Notes for the implementer

- `HoleTapInfo.FullThreadDepth` (bool, settable) and `HoleTapInfo.ThreadDepth` (`Parameter`) exist in the 2026 interop (checked by reflection, 2026-10-28), as does `HoleFeatures.CreateTapInfo(RightHanded, ThreadType, ThreadDesignation, Class, FullTapDepth, ThreadDepth)`, already used.

## Verification

Filled in by the implementer.

## Follow-ups
