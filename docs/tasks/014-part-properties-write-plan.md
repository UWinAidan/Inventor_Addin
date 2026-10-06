# 014: Part properties model, storage map and write plan (Core)

- **Milestone:** M1
- **Feature spec:** `docs/features/07-part-properties.md` (Where the data lives; Rules: Part name, Only changed fields are written; Open questions 3 and 4)
- **Status:** done
- **Depends on:** 013
- **Needs:** cloud
- **Parallel-safe with:** 011, 012, 015, 017

## Goal

Core can describe what a file holds for the Part Properties window, where each field is stored, and, given edited values, the exact list of iProperty writes to make. This is the heart of "only changed fields are written".

## Scope

- `src/InventorAddin.Core/PartProperties/PropertySetNames.cs` (new: Inventor's internal property set names)
- `src/InventorAddin.Core/PartProperties/PartPropertiesSnapshot.cs` (new)
- `src/InventorAddin.Core/PartProperties/PartPropertyStorage.cs` (new: field → location and value type)
- `src/InventorAddin.Core/PartProperties/PropertyWrite.cs` (new)
- `src/InventorAddin.Core/PartProperties/PartName.cs` (new)
- `src/InventorAddin.Core/PartProperties/PartPropertiesWritePlan.cs` (new)
- `tests/InventorAddin.Core.Tests/PartProperties/` (new test files for each of the above that has behaviour)

Out of scope:

- Pre-fill rules, Detailer following, dirty tracking (016). The plan takes final edited values; it does not know how they were produced.
- Reading or writing Inventor (018).

## Acceptance criteria

- [x] `PropertySetNames` holds the internal set names exactly as `PropertyReader` spells them today: `"Inventor Summary Information"`, `"Inventor Document Summary Information"`, `"Design Tracking Properties"`, `"Inventor User Defined Properties"`
- [x] `PropertyLocation(string SetName, string PropertyName, PropertyValueKind Kind)` record, with `PropertyValueKind` = `Text` or `Currency`, and `IsCustom` true for the user-defined set
- [x] `PartPropertyStorage` gives the location of each stored field, matching the spec's table: Part Number, Description, Designer and Cost (Currency) in Design Tracking Properties under the names `Part Number`, `Description`, `Designer`, `Cost`; custom `Part Type`, `Detailer`, `Finish`
- [x] `PartPropertiesSnapshot` (what the add-in read from the file): `FullFileName` (empty for a never-saved file), `DocumentKind`, `PartNumber`, `Description`, `PartType`, `Designer`, `Detailer`, `Cost` (`decimal?`), `Material`, `Finish`, `WeightDisplay` (`string?`, null when mass could not be read), and an `EditBlock` value (`None`, `ReadOnlyFile`, `NotModifiable`) with `PartPropertiesSnapshot.EditBlockMessage` giving the line the window shows for each (null for `None`)
- [x] `PartName.FromFileName(string? fullFileName)` returns the file name without folder or extension, or `""` for null or blank. It treats both `\` and `/` as folder separators, so `C:\Work\Bracket.v2.ipt` gives `Bracket.v2` on Linux as well as Windows
- [x] `PartEditedValues` record: `PartType`, `Designer`, `Detailer`, `Cost` (`decimal?`). The only fields the user can edit
- [x] `PartPropertiesWritePlan.Build(PartPropertiesSnapshot original, PartEditedValues edited)` returns `IReadOnlyList<PropertyWrite>` where each write is either Set(location, value) or Remove(location):
  - text is compared after `PropertyValues.NormaliseText`; null and blank are the same value
  - a changed standard text property is Set, including to `""` when cleared (standard properties cannot be removed)
  - a changed custom property is Set when the new value is not blank, and Removed when it is blank and the file held a non-blank value. Blank to blank produces nothing
  - Cost is compared as stored values (`PropertyValues.CostToStore`), so a file holding 0 and an edited blank produce nothing, and clearing a non-zero cost Sets 0
  - Description is Set to `PartName.FromFileName(original.FullFileName)` when that name is not blank and differs from the normalised Description. A never-saved file (blank name) never touches Description (open question 3)
  - Part Number, Material, Finish and weight never produce a write
  - writes come out in a fixed order (the spec's field order) so tests and logs are stable
- [x] `PartPropertiesWritePlan.Build` throws `ArgumentException` when `original.EditBlock` is not `None`; the view-model must never ask
- [x] Tests: no change gives an empty list; each field changed alone; clear a standard field; clear a custom field that existed and one that did not; cost 0 vs blank; description sync on and off, and for a never-saved file; whitespace-only edits give nothing; the order of writes
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Records and plain classes; no Inventor types. Write values as `object` only at the edge: `PropertyWrite` can carry `string` for Text and `decimal` for Currency, but expose them typed (`TextValue`, `CurrencyValue`) so the add-in writer does not guess.
- Part type codes are compared as normalised text here. The view-model (016) is what restricts them to the list.
- `ModelData.IsReadOnly` already exists for the exporter. The snapshot is a separate, smaller type on purpose; do not reuse `ModelData`.
- Task 018 will point `PropertyReader`'s set name constants at `PropertySetNames`. Do not edit the add-in here.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build src/InventorAddin.Core`: succeeded, 0 warnings, 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 455 passed, 0 failed, 0 skipped.
- **Not compiled (changed under `src/InventorAddin`):** none
- **Inventor API members not confirmed:** none
- **Manual checklist for Inventor:** none (covered by 019)

## Follow-ups

Things noticed but not done.

- Decisions made here that 016 and 018 should know about: text, Part Type codes and the Description sync compare ordinally (case-sensitive), so `m` vs `M` or `bracket` vs a file named `Bracket.ipt` produces a write. The part name is trimmed before it is compared and written. This is what the brief decides (compare after `NormaliseText`, which only trims). 016 must handle a file code that `PartTypes.Find` matches but that differs in case (`m` vs `M`): a WPF dropdown matching on `Code` is case-sensitive, so it must not show blank and write blank back.
- `PropertyWrite.ToString()` includes the value (`Set <set>/<name> = ...`). Do not log it: property values can hold personal data (names), so 018/019 should log the action and `Location` only.
- Task 018 should point `PropertyReader`'s set name constants at `PropertySetNames` (as already planned), and the writer can switch on `PropertyWrite.Action` and `Location.Kind`.
