# 244: Off-units FINISH and WEIGHT: fix the repoint, and refresh on open and save

- **Milestone:** M5 (from Aidan's chunk 3 check, 2026-11-26)
- **Feature spec:** `docs/features/05-drawing-tools.md` (question 33)
- **Status:** todo
- **Depends on:** 232
- **Needs:** windows build
- **Parallel-safe with:** none (245 builds on it)

## Goal

Step 12 of the chunk 3 check failed: an inch part on a metric print still printed lb and the inch finish. Aidan also overturned question 33 (2026-11-26): the off-units FINISH and WEIGHT refresh whenever the drawing is opened or saved, not only on Create Print, because Create Print no longer rebuilds an existing print and the values would go stale. Both directions (inch part on a metric print, metric part on an imperial print) must work.

## Cause (found by the parent, 2026-11-26)

Aidan's log: `the print is in Metric units, the model is not; 0 title block field(s) now read the drawing's Mass, Finish Spec.` The drawing's own `Mass` ("0.07 kg") and `Finish Spec` (the µm spec) were written correctly. The repoint changed nothing because `PrintUnitValues.PointAtDrawing` matches `PropertySet='Inventor User Defined Properties'`, but the real field text (read with a COM probe from a copy of that print) is:

```
<Property Document='model' PropertySet='User Defined Properties' Property='Mass' FormatID='{D5CDD505-2E9C-101B-9397-08002B2CF9AE}'>Mass</Property>
<Property Document='model' PropertySet='User Defined Properties' Property='Finish Spec' FormatID='{D5CDD505-2E9C-101B-9397-08002B2CF9AE}'>Finish Spec</Property>
```

Each sits inside a `<StyleOverride FontSize='0.2036'>`. Identify a custom property field by its FormatID `{D5CDD505-2E9C-101B-9397-08002B2CF9AE}` or either set name, and add this real text as a Core test fixture (it is our own template's text). Check the 238 fit (`TitleBlockFit.FieldOf`) and anything else that matches on the set name for the same mistake.

## Scope

- **Core:** the fix above, and a rule that, given the print's units, the model's units and the model's values, says for each managed field (FINISH, WEIGHT) whether it should read the model or the drawing and what the drawing's value is. When the units match, the field reads the model (repoint back if it was pointed at the drawing; the drawing's own copies may stay). Tests both ways.
- **Add-in:** one refresher used by Create Print, on opening a drawing (`ApplicationEvents.OnOpenDocument`, after), and before saving a drawing (`OnSaveDocument`, before; `Extraction/MassOnSave.cs` shows the pattern). It finds the model (the first view's referenced document, as Place Initial Views does), reads the print's units, writes the drawing's Finish Spec and Mass only when they differ from what is stored, and repoints the fields only when needed, so opening an up-to-date print changes nothing and does not mark it dirty.
- The print's units: the drawing's own length units (`UnitsOfMeasure.LengthUnits` of the drawing), which the AWB templates should set to mm and inch. Verify on copies of both templates; if a template's units are not set that way, say so under Verification and pick another reliable signal (Default), for example the title block's sheet format name (ISO or ANSI).
- Only drawings whose title block has these AWB fields are touched; any other drawing is left alone. The open and save writes are made outside a transaction (as `MassOnSave` does) and never block the open or the save; a failure is logged.

## Acceptance criteria

- [ ] An inch part on a metric print shows kg and the µm spec; a metric part on an imperial print shows lb and the inch spec. The log line reports 2 fields.
- [ ] Changing the part's finish or mass, then opening or saving the print, updates FINISH and WEIGHT on the print.
- [ ] Opening a print in the part's own units, or an up-to-date off-units print, writes nothing.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member is confirmed in a spike or existing code, or listed under Verification.

## Notes for the implementer

- A PowerShell 5.1 late-bound COM check on copies in `C:\AwbSpike\244\` is allowed and recommended for the repoint (write FormattedText inside `TitleBlockDefinition.Edit`, then `ExitEdit(true)`, then read the printed value). Use `GetActiveObject('Inventor.Application')`, or if Inventor is not running `New-Object -ComObject Inventor.Application` and `Quit()` it at the end of the same script; set `SilentOperation` while opening. Copy files there first; never open or save files in `C:\CAD_TEST` or the repo's `libraries\Templates`. `C:\AwbSpike\242\probe.idw` is a copy of the failed print (inch part on a metric print); its model is `C:\CAD_TEST\TEST_222_IMPERIAL,AWB-10059.ipt` (copy it beside the print and fix the reference, or make a new print in the spike folder).

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. …

## Follow-ups

Things noticed but not done.
