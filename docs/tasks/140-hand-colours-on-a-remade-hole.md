# 140: Hand colours on a remade hole: the feature's colour kept, new faces take the bore's

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md`
- **Status:** done
- **Depends on:** 122
- **Needs:** windows build
- **Parallel-safe with:** 141, 142

## Goal

Two self-test failures from Aidan's check of the round (2026-11-07), both in task 122's colour carry:

- **122-6.** A colour set on the whole hole through Feature Properties is lost when the hole is remade (for example as tapped). Afterwards the new feature reports `kBodyAppearance` (Default). The feature's colour must be kept.
- **122-8.** When a remade hole gains new faces, for example a counterbore or spotface added, those faces must take the bore's hand colour (Aidan, 2026-11-07, `DECISIONS.md`). Faces that existed keep their own colours. Today the new seat faces come out Default.

## Scope

- `src/InventorAddin.Core/Holes/HoleFaceColours.cs` and its tests: `Carry` gives a new face, whose role and centre had no old face, the colour of the bore at the same centre, if that bore had a hand colour. The all-over rule and the per-role carry are unchanged otherwise.
- `src/InventorAddin/Holes/HoleFitWriter.cs` (`ReadHandColours`, `ApplyHandColours`): the feature's appearance is kept whatever source Inventor reports for a colour set by hand on the feature. Today only `kFeatureAppearance` is read. Find out which source a Feature Properties colour gives (likely `kOverrideAppearance`) and log it at Info on the read. Body, part and material sources are still not hand colours.
- `docs/features/09-awb-hole.md`: one line on new faces taking the bore's colour.

Out of scope:

- Task 096's dowel reset.
- Remembering colours between steps while the window is open (task 122 follow-up).

## Acceptance criteria

- [x] Core tests: a new seat face with no old face of its role takes the bore's hand colour; with no bore colour it gets none; an existing seat face with its own colour keeps it.
- [ ] A Feature Properties colour on the hole survives a remade hole; the log names the source Inventor reported.
- [x] `dotnet build InventorAddin.slnx` on Windows, 0 errors; `dotnet test tests/InventorAddin.Core.Tests` passes. (Numbering timing tests can fail under load; see Verification.) Item 2 waits on Aidan in Inventor.

## Notes for the implementer

Read task 122's "How it works" and Defaults first. The local spike branch `spike/139-selftest` (never merged) has `src/InventorAddin/SelfTest/CasesColour.cs`. It sets the feature's colour through the API (`HoleFeature.Appearance`) and shows how cases 122-6 and 122-8 measured. Read it for reference only; copy nothing from it.

## Verification

Filled in by the implementer.

- **How it works:**
  - Core, `HoleFaceColours.Carry`: a new face whose key (role and centre) no old face had takes the hand colour of the old bore at the same centre, if it had one. A face whose key the old hole had, coloured or not, keeps the old behaviour. The all-over rule runs first, unchanged. `HoleFaceColours.IsFeatureHandSource(string?)` takes the name of Inventor's `AppearanceSourceTypeEnum` value: every source but `kBodyAppearance`, `kPartAppearance`, `kMaterialAppearance`, `kDefaultAppearance` (or unread) counts as a colour set on the feature.
  - Add-in, `HoleFitWriter.ReadHandColours` now takes the log (passed from `HoleFeatureWriter.Recreate`) and logs at Info, on every remake, the source and appearance the old feature reports, how many of its faces are overridden, and whether the colour is kept. The feature's colour is kept only when `HoleFaceColours.KeepFeatureColour` agrees: a hand source (`IsFeatureHandSource`) and at least one face reporting a readable source other than `kOverrideAppearance` (reviewer's guard), so a face override that moves the feature's source cannot spread one face's colour over the whole new hole. `ApplyHandColours` logs at Info the colour put back on the new feature and the source the new feature then reports.
  - A consequence of the new rule (Aidan's 2026-11-07 decision read literally): a through hole made blind gains a drill point or flat bottom, which is a new face, so it also takes the bore's colour. The existing test `Carry_SeatRemoved_SeatColourDroppedBoreKept` was updated for that (counterbored through hole made tapped blind: the drill point now takes the bore's blue). A blind hole that stays blind keeps an uncoloured drill point uncoloured, as before.
- **Ran:** On Windows with Inventor running, after the reviewer's changes: `dotnet build InventorAddin.slnx -p:DeployToInventor=false -tl:off`: build succeeded, 0 errors, warnings only the existing CS8600 in `CreatePrintCommand`, `NewPartCommand`, `SaveAsCopyCommand`, `SaveAsReplaceCommand` (files this task did not touch). Not deployed. `dotnet test tests/InventorAddin.Core.Tests --filter HoleFaceColours`: 40 passed, 0 failed (18 new cases). Full suite: 4392 passed, 0 failed. An earlier full run had 4 `Numbering` timing tests (3 s timeouts) failing, which also failed on the clean tree and pass alone, so they are load-dependent and not from this task.
- **Not compiled (changed under `src/InventorAddin`):** none; `Holes/HoleFitWriter.cs` and `Holes/HoleFeatureWriter.cs` compiled against the Inventor 2026 interop. Not run in Inventor.
- **Inventor API members not confirmed:** no new members. `AppearanceSourceTypeEnum` was checked by reflection on the 2026 interop: kOverrideAppearance, kFeatureAppearance, kBodyAppearance, kPartAppearance, kWeldsAppearance, kMaterialAppearance, kComponentOccurrenceAppearance, kDefaultAppearance, kMeshFeatureAppearance. Behaviour not seen in Inventor:
  - which source a Feature Properties colour gives the feature (likely `kOverrideAppearance`); the log line in step 1 answers it;
  - whether a face override alone changes the feature's own source to an override. The guard `HoleFaceColours.KeepFeatureColour` keeps the feature's colour only when at least one face reports a readable source other than `kOverrideAppearance`, so a hole whose every face is overridden never has one face's colour spread over the whole new hole. It does not cover a hole with some faces overridden and some not (step 2's blind hole has an un-overridden drill point): if a face override did move the feature's source there, the Info line would say "kept as the hole's own colour" and the drill point would come out coloured. The Info line logs the source, how many faces are overridden and the decision, so step 2 shows which.
- **Manual checklist for Inventor:** (deploy first: close Inventor and run `dotnet build InventorAddin.slnx`)
  1. Colour a clearance hole through Feature Properties, edit it to Tapped, OK: the hole keeps the colour, and the log names the source read (`... reports appearance source kXxx, appearance Name; kept as the hole's own colour.`) and the source the new hole reports after the colour is put back.
  2. A **blind** clearance hole with no seat, only its bore coloured by hand (select the bore face, Appearance override): edit, add a counterbore, OK. The counterbore wall and floor take the bore's colour, the drill point stays uncoloured, and the log says "not a colour set on the hole".
  3. A counterbored hole with its wall red and bore blue, its floor left uncoloured: change it to a spotface. The wall stays red, the bore blue, and the floor stays uncoloured; the log says "not a colour set on the hole".
  4. One Undo brings each back as it was.

## Follow-ups

- When the log from checklist step 1 shows the source Feature Properties gives, record it in `CLAUDE.md`'s "Confirmed in Inventor 2026" list or task 122's Defaults, and narrow `IsFeatureHandSource` if Aidan prefers a positive list.
- A through hole made blind now colours its new drill point or bottom with the bore's colour (the new rule applied to every new face, not only seats). If Aidan wants new bottoms left alone, `Carry` would limit the bore colour to seat roles.
- An old face whose shape could not be read has no key, so it counts as absent: a new face of that role at that centre takes the bore's colour, even if the old face had been left uncoloured on purpose.
- Core test suite: four or five `Numbering` timing tests (3 s timeouts) fail when the whole suite runs in parallel on this PC and pass alone. Worth a task to make them independent of load.
