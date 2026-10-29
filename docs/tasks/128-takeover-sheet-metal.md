# 128: AWB Hole replaces Inventor's Hole on the Sheet Metal tab

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The command")
- **Status:** todo
- **Depends on:** 121
- **Needs:** windows build
- **Parallel-safe with:** none (same files as 120, 121, 129)

## Goal

Aidan, 2026-10-25: AWB Hole also replaces Inventor's Hole on the **Sheet Metal** tab, under the same "Use Inventor's Hole command" switch. The **Flat Pattern** tab keeps Inventor's. This replaces the Default that the Sheet Metal tab keeps Inventor's Hole.

## Scope

- `src/InventorAddin/UI/RibbonSetup.cs`, `HoleCommandTakeover.cs`: find the Sheet Metal tab's panel that holds Inventor's Hole (look it up at run time by the control's command, `PartHoleCmd` or the sheet metal one, rather than a guessed panel name; log what was found), put AWB Hole before it, large, and hide Inventor's button there, the same way task 091 does on the 3D Model tab. The switch shows and hides both. Add the new button to Core's `AddinUi.ForeignButtons` (task 121), or an unload leaves it behind.
- If the Sheet Metal tab uses a different command name for its Hole, H is still one binding: confirm in the log which command H reaches on a sheet metal part.
- AWB Hole itself already works on sheet metal parts (task 090 Default); confirm a hole made on a sheet metal face is correct.

## Acceptance criteria

- [ ] On a sheet metal part, the Sheet Metal tab shows AWB Hole where Inventor's Hole was; H opens AWB Hole.
- [ ] Flat Pattern still shows Inventor's Hole.
- [ ] The switch brings Inventor's Hole back on both tabs.
- [ ] The solution builds on Windows; `dotnet test tests/InventorAddin.Core.Tests` passes.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Open a sheet metal part. Sheet Metal tab: AWB Hole in Hole's place, Inventor's Hole hidden. Make a hole on a flange.
  2. Flat Pattern: Inventor's Hole is there.
  3. Switch on in Settings: Inventor's Hole is back on the Sheet Metal tab.

## Follow-ups
