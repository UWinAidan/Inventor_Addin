# 064: Ribbon panels: Create, Properties, Drawing, AWB

- **Milestone:** M2 (fix run 2)
- **Feature spec:** `docs/features/01-ribbon-and-shell.md` ("Target"); `docs/features/02-part-creation-and-numbering.md` ("Commands")
- **Status:** done
- **Depends on:** none
- **Needs:** windows build (local session); Core tested
- **Parallel-safe with:** none

## Goal

Aidan, 2026-10-12: re-group the ribbon. Panels, in order, on every ribbon where their buttons appear:

| Panel | Buttons |
|---|---|
| Create | New Part (large); Save As Copy and Save As and Replace (small, stacked) |
| Properties | Part Properties (large); Mass Update (small) |
| Drawing | Create Print (large) |
| Developer | only when developer tools are on: Export Model Data, Export Libraries (small) |
| AWB | Settings and About (small, stacked), always far right |

Aidan, 2026-10-12: Developer sits to the left of AWB, so AWB is always far right.

## Scope

- `src/InventorAddin.Core/Ribbon/RibbonLayout.cs`
- `src/InventorAddin.Core/Ribbon/RibbonModels.cs` (`RibbonIds`: the panel ids and names)
- `src/InventorAddin.Core/Branding.cs` if the AWB panel's name needs a constant there
- `src/InventorAddin/UI/RibbonSetup.cs` only if needed
- The matching tests under `tests/InventorAddin.Core.Tests/`

Out of scope:

- Which ribbons each button appears on: unchanged (for example Settings and About stay off ZeroDoc, Save As and Replace stays on Assembly only). Button sizes: unchanged (task 056).

## Acceptance criteria

- [x] `RibbonIds` has the Create, Properties, Drawing and AWB panels (ids `id_Panel_Awb_Create`, `…_Properties`, `…_Drawing`, `…_Awb`, built from the existing prefix) and keeps the Developer panel. The CAD Automation, Drawing Tools and Modelling panels are gone
- [x] The AWB panel's display name comes from `Branding` (a new constant, for example `Branding.ShortName = "AWB"`), since brand names live only there
- [x] `RibbonLayout.Definitions` lists the panels in the order Create, Properties, Drawing, Developer, AWB, with the buttons and sizes in the table above; each button keeps its current environments
- [x] Tests pin, for each environment with developer tools on and off, the panel order and each panel's buttons in order. For example Part with developer tools off: Create (New Part, Save As Copy), Properties (Part Properties, Mass Update), Drawing (Create Print), AWB (Settings, About); ZeroDoc: Create (New Part), Properties (Mass Update); Drawing ribbon: AWB (Settings, About), and with developer tools on Developer before AWB
- [x] Spec 08's ribbon icon list and any test or doc naming the old panels are updated
- [x] `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` succeeds and `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Inventor stacks consecutive small buttons in a panel into a column of up to three by itself, so "stacked" needs no API call: keep the two small buttons next to each other in the definition.
- The ribbon is rebuilt at every start from `RibbonLayout`; old panel ids from earlier builds are not stored, so nothing needs cleaning up.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false`: 0 errors (8 existing warnings, none in changed files). `dotnet test tests/InventorAddin.Core.Tests`: 2135 passed, 0 failed, 0 skipped.
- **Changed under `src/InventorAddin` (compiled locally, not tested in Inventor):** none. `RibbonSetup` already draws whatever `RibbonLayout.For` returns, so no add-in change was needed.
- **Changed elsewhere:** `Branding.ShortName = "AWB"`; `RibbonIds` now has Create, Properties, Drawing, Developer and AWB panels (CAD Automation, Drawing Tools and Modelling removed); `RibbonLayout.Definitions` regrouped. `RibbonLayoutTests` now pins the full layout (panel ids, names, buttons, sizes, order) for all four ribbons with developer tools off and on, plus AWB last with Developer right before it, and small buttons kept together in each panel. `BrandingTests` updated. Spec 08's icon list and spec 07's "Part Properties button" line now name the new panels.
- **Inventor API members not confirmed:** none used.
- **Manual checklist for Inventor:**
  1. Build and deploy (`dotnet build InventorAddin.slnx` with Inventor closed), start Inventor with no document open. The AWB Addin tab shows **Create** (large New Part) then **Properties** (small Mass Update). No AWB panel.
  2. Open a part. Panels, left to right: **Create** (large New Part, small Save As Copy), **Properties** (large Part Properties, small Mass Update), **Drawing** (large Create Print), **AWB** (Settings and About, small, stacked).
  3. Open an assembly. Same as the part, with Save As and Replace stacked under Save As Copy in Create.
  4. Open a drawing. Only the **AWB** panel (Settings, About).
  5. Turn developer tools on in Settings and restart Inventor. On every ribbon the **Developer** panel appears just left of **AWB** (on ZeroDoc, where there is no AWB panel, it is last with Export Libraries only).
  6. Check no old panel (CAD Automation, Drawing Tools, Modelling) is left behind on any ribbon.

## Follow-ups

Things noticed but not done.

- Older task files (004, 008, 009, 019, 043 to 048, 056, 057) still name the CAD Automation, Drawing Tools and Modelling panels. They are history and were left as written.
- Spec 01's "reference tool's layout" tables still use the old panel names; spec 01 was out of scope for this task.
