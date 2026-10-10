# 032: Spike: templates, creating and saving, rename, base view (Windows)

- **Milestone:** M2
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` (Templates; Commands)
- **Status:** done
- **Depends on:** none
- **Needs:** windows (run by Aidan in a local Claude session on his PC, with Inventor 2026)
- **Parallel-safe with:** every cloud task (it changes no committed source)

## Goal

Settle, in Inventor, the API behaviours that the second M2 run builds on: finding the default templates, creating and saving a new document, updating references when a file is renamed, opening files invisibly to read and write properties, placing a base view, and replacing an occurrence. Record the answers in this file. Tasks 043 to 048 are not run until this file says what works.

## Scope

Throwaway code on a local scratch branch (`spike/032`), never merged. The only committed change is this task file, with the Results section filled in.

Out of scope:

- Any real command, dialog or Core logic. Use the smallest code that answers each question; a temporary button on the Developer panel, or code in the About command, is fine.

## What to find out

Use a scratch folder inside the active project's workspace, with test files made for the spike (no work files). Wrap COM getters in `ComSafe.Get`. Log what you find with `AddinServices.Log.Info` and copy the lines into the Results.

**1. Default templates.**

- Call `InventorHost.App.FileManager.GetTemplateFile(DocumentTypeEnum, SystemOfMeasureEnum, DraftingStandardEnum, object DocumentSubType)` for: part, sheet metal part (subtype `"{9C464203-9BAE-11D3-8BAD-0060B0CE6BB4}"`), assembly and drawing, each with `kMetricSystemOfMeasure` and `kEnglishSystemOfMeasure`, and `kDefault_DraftingStandard`.
- Record each path returned, whether the file exists, and any exception. Record the drawing template's extension (`.idw` or `.dwg`).
- If `GetTemplateFile` fails or returns the wrong units, record `DesignProjectManager.ActiveDesignProject.TemplatesPath` and the template files under it, so the add-in can find them by name instead.

**2. Create, write, save.**

- `Documents.Add(DocumentTypeEnum.kPartDocumentObject, templatePath, true)` with the metric part template. Do the same for a sheet metal part and an assembly.
- Write Part Number, Description and a custom `Part Type` with `PropertyWriter.Apply` (no transaction needed on a new document? record whether a transaction works and whether one is needed).
- `doc.SaveAs(fullFileName, false)` to `AWB-90001,spike_part.ipt` in the scratch folder. A comma in the name: does Inventor accept it? Record the file name Inventor shows afterwards and whether Part Number kept the written value.

**3. Workspace path.**

- Record `DesignProjectManager.ActiveDesignProject.WorkspacePath` (and `FullFileName` of the project) for Aidan's active project. Is it a full path, or relative to the `.ipj`?

**4. Rename and update references** (the main question).

Make a part `A.ipt`, an assembly `Asm.iam` that places it, and a drawing `A.idw` with a view of it. Close all three. Then try to rename `A.ipt` to `B.ipt` so both still open without a missing-reference prompt:

- a. With `A.ipt` open as the active document: `doc.SaveAs(newPath, false)` (the open document becomes `B.ipt`; `A.ipt` stays on disk). Then for each referencing file: open it invisibly (`Documents.Open(path, false)`), find the reference to `A.ipt` in `doc.File.ReferencedFileDescriptors` (or `doc.ReferencedFileDescriptors`), call `ReplaceReference(newPath)`, `Save`, `Close`. Then delete `A.ipt`. Record which members compile and work.
- b. If a fails: copy the file on disk first (`System.IO.File.Copy`), replace the references as above, then delete the old file.
- Set `InventorHost.App.SilentOperation = true` while opening invisibly, and restore it afterwards. Does any dialog still appear?
- If `Asm.iam` is **open** in Inventor during the rename: what happens? Does `ReplaceReference` work on an open document, or must it be closed first?
- Record how long opening, replacing and saving takes for one assembly and one drawing.

**5. Open invisibly to read and write properties** (Mass Update).

- Open 20 to 50 closed parts invisibly one at a time, read Part Number and Designer, close. Record the time per file.
- Open one invisibly, write Designer with `PropertyWriter.Apply`, `Save`, `Close`. Reopen: did the value stick? Does a file open in Inventor show in `Documents` so it can be detected and skipped (`Documents.ItemByName` or a loop comparing `FullFileName`)?

**6. Base view on a new drawing** (Create Print).

- With a saved part open: `Documents.Add(kDrawingDocumentObject, drawingTemplate, true)`, then `sheet.DrawingViews.AddBaseView((_Document)part, point, scale, ViewOrientationTypeEnum.kFrontViewOrientation, DrawingViewStyleEnum.kHiddenLineDrawingViewStyle)` with `point = TransientGeometry.CreatePoint2d(sheet.Width / 2, sheet.Height / 2)` and scale 1. Record the exact signature that compiles (optional arguments), and `Sheet.Width` and `Sheet.Height` (cm) for the default sheet.
- Record `ComponentDefinition.RangeBox` of the part next to the view's `Width` and `Height` at scale 1, so a fit rule can be checked.
- `SaveAs` the drawing beside the part with the part's file name and the template's extension. Write the drawing's Part Number. Record any prompt.

**7. Replace an occurrence** (Save As and Replace).

- In an assembly with two occurrences of one part: `occurrence.Replace(newPath, false)` on one. Does only that occurrence change? Does it keep its constraints? With `true`, do both change?
- Record how the selected occurrence is read: `doc.SelectSet[1]` as `ComponentOccurrence`, and what it is when the user selected in a sub-assembly (a `ComponentOccurrenceProxy`?).

**8. An unreachable register folder.**

- With Tailscale disconnected (or a mapped drive that is offline), time `System.IO.Directory.Exists(@"B:\aidan\AWB_Design_Data")` and `File.ReadAllText` on a file in it. How long until each returns or throws? This sets the 3 second timeout in task 036.

## Acceptance criteria

- [x] The Results section answers questions 1 to 8, with the exact members and the method that worked
- [x] Any member that did not compile is listed with the name that did
- [x] The scratch branch is not merged or pushed to `main`; only this file is committed (with the tracking files and spec 02, which the parent updates when it commits a task)

## Notes for the implementer

- This runs on Windows with Inventor. Close Inventor before each build (`dotnet build InventorAddin.slnx -tl:off`), then start it and test.
- The member names in the questions are from the Inventor API reference and have not been compiled in this project. The point of the spike is to find which ones work.
- Keep the code you tried, in short, in the Results so tasks 043 to 048 can copy it.

## Results

Run on 2026-10-10 in Inventor 2026.2 on Aidan's PC (Windows 11 Pro, build 26200), by a local Claude session, on the local branch `spike/032` (not pushed).

**How it was run.** Not as an add-in button. A throwaway .NET 8 console app (`spike/Spike032` on that branch) references the same `Autodesk.Inventor.Interop.dll` as the add-in. It starts its own invisible Inventor instance with `Activator.CreateInstance(Type.GetTypeFromProgID("Inventor.Application"))` and calls the API over COM. This means:

- Every member below **compiled** against the add-in's interop assembly, and **ran** in Inventor 2026.
- The calls ran **out of process**, so the timings are upper bounds. Inside the add-in, the same calls should be as fast or faster.
- Inventor was invisible, so dialogs could not be seen. A modal dialog would have blocked the call. **No call blocked**, so no dialog appeared in any step below.
- The spike made its own test files in a scratch folder under the project folder, and deleted them afterwards.

### 1. Default templates

`FileManager.GetTemplateFile(DocumentTypeEnum, SystemOfMeasureEnum, DraftingStandardEnum, object DocumentSubType)` compiles. Paths are under `TemplatesPath` = `C:\Users\Public\Documents\Autodesk\Inventor 2026\Templates\en-US\`.

| Kind | Metric | Imperial (`kEnglishSystemOfMeasure`) |
|---|---|---|
| part | `Metric\Standard (mm).ipt` | `English\Standard (in).ipt` |
| sheet metal (subtype `{9C464203-…}`) | `Metric\Sheet Metal (mm).ipt` | `English\Sheet Metal (in).ipt` |
| assembly | `Metric\Standard (mm).iam` | `English\Standard (in).iam` |
| drawing, `kDefault_DraftingStandard` | **`""` (empty string, no exception)** | **`""`** |

- Every path returned exists. A part made from each part template has the right units: `UnitsOfMeasure.LengthUnits` is `kMillimeterLengthUnits` and `kInchLengthUnits`. The sheet metal template gives `SubType` = `{9C464203-9BAE-11D3-8BAD-0060B0CE6BB4}`.
- **Drawings need a named drafting standard.** With `kDefault_DraftingStandard`, the drawing lookup returns an empty string in both units. With a named standard:
  - metric with `kANSI_DraftingStandard` gives `Metric\ANSI (mm).dwg`. BSI, DIN, GB, ISO, JIS and GOST also work in metric, each giving `Metric\<std>.dwg`.
  - imperial works **only** with `kANSI_DraftingStandard`, giving `English\ANSI (in).dwg`. Every other standard returns `""`.
  - `kOther_DraftingStandard` and the `kLegacy*` values throw `ArgumentException` (E_INVALIDARG).
- **Units-neutral calls.** `kDefaultSystemOfMeasure` with `kDefault_DraftingStandard` gives the root templates with no units in the name: `standard.dwg` for drawings and `Standard.ipt` for parts. `GetTemplateFileWithOptions(kDrawingDocumentObject, Type.Missing ×4)` also gives `standard.dwg`. With metric and the other three arguments missing, it gives `""`.
- **Extension: `.dwg`.** `DrawingOptions.DefaultDrawingFileType` is `kDWGDefaultDrawingFileType` on this install, and every drawing lookup returns a `.dwg`. `.idw` templates (`Metric\ANSI (mm).idw` and others) exist in the same folders but are not what the lookup returns.
- `FileOptions.TemplatesPath` equals `ActiveDesignProject.TemplatesPath`.

Code that works for the drawing template:

```csharp
string path = InventorHost.App.FileManager.GetTemplateFile(
    DocumentTypeEnum.kDrawingDocumentObject, units, DraftingStandardEnum.kANSI_DraftingStandard, Type.Missing);
// fallback: GetTemplateFileWithOptions(DocumentTypeEnum.kDrawingDocumentObject, Type.Missing, Type.Missing, Type.Missing, Type.Missing)
```

### 2. Create, write, save

- `Documents.Add(DocumentTypeEnum.kPartDocumentObject, templatePath, true)` works for a part and a sheet metal part, and `kAssemblyDocumentObject` works for an assembly. A new document has `FullFileName = ""`, `DisplayName` `Part3` or `Assembly1`, `Dirty = false`, and Part Number `""`. Before the first save, Part Number is blank, not the display name.
- **Writing properties:** `doc.PropertySets["Design Tracking Properties"]["Part Number"].Value = …` (likewise Description), and `PropertySets["Inventor User Defined Properties"].Add(value, "Part Type")`. These are the calls `PropertyWriter` makes.
  - It works with no transaction and also inside `TransactionManager.StartTransaction((_Document)doc, …)` / `End()`. **No transaction is needed** on a new document. Using one does no harm.
  - Reading a custom property that does not exist (`udp["Part Type"]`) throws `COMException` E_FAIL, not a "not found" error. `PropertyWriter` already catches this through `ComSafe.Get`.
- `doc.SaveAs(@"<folder>\AWB-90001,spike_part.ipt", false)`: **the comma is accepted.** `DisplayName` and the file name afterwards are `AWB-90001,spike_part.ipt`. Part Number keeps the written value `AWB-90001`, and Description and Part Type survive a close and reopen. The same held for `AWB-90002,spike_sheet_metal.ipt` and `AWB-90003,spike_assembly.iam`.
- Timings: `Documents.Add` took 7.2 s the first time (cold) and about 2.5 s after that, out of process. `SaveAs` took 0.2 to 0.5 s.

### 3. Workspace path

- The active project is **`Default`**: `FullFileName` = `C:\Users\Public\Documents\Autodesk\Inventor 2026\Default.ipj`, `ProjectType` = `kSingleUserMode`.
- **`WorkspacePath` is an empty string** for this project. Nothing comes back to resolve. `TemplatesPath` is a full path ending in `\`.
- If the add-in treated a blank workspace as the project file's folder, that would be `C:\Users\Public\Documents\Autodesk\Inventor 2026`. That folder holds Inventor's own `Templates` and `Design Data`, so it is a poor folder to scan or search. See the follow-up and spec 02 question 23.
- Not tested: a project with a workspace set. Whether such a path comes back relative to the `.ipj` is still unknown. Code should resolve it: `Path.IsPathRooted(ws) ? ws : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project.FullFileName), ws))`.

### 4. Rename and update references

**Approach a works, and so does approach b.** Test setup: `A.ipt`, then `Asm.iam` placing it, then `A.dwg` with a base view of it. All three were saved and closed.

- **a.**
  1. `Documents.Open(a, true)` then `doc.SaveAs(b, false)`. The open document becomes `B.ipt`, and `A.ipt` stays on disk.
  2. For each referencing file: open it invisibly, then use `foreach (FileDescriptor fd in doc.File.ReferencedFileDescriptors)`. Where `fd.FullFileName` matches the old path (compare ignoring case), call `fd.ReplaceReference(newPath)`, then `doc.Save()` and `doc.Close(true)`.
  3. Delete `A.ipt`.

  Result: both files reopen with `FullFileName` = `B.ipt` and `ReferenceMissing = false`.
  - Members that compile and work: `Document.File` → `File.ReferencedFileDescriptors` → `FileDescriptor.FullFileName`, `FileDescriptor.ReplaceReference(string)` and `FileDescriptor.ReferenceMissing`.
  - There is **no** `Document.ReferencedFileDescriptors`. The add-in must go through `doc.File`.
- **b.** `System.IO.File.Copy(old, new)`, then the same `ReplaceReference` loop, then delete the old file. This also works. It was tested on the drawing (`B.ipt` → `C.ipt`).
- **SilentOperation:** `InventorHost.App.SilentOperation = true` around `Documents.Open(path, false)`, restored afterwards. No call blocked. Opening the renamed assembly with SilentOperation **off** did not block either. The references were already good, so there was nothing to prompt about.
- **Assembly open during the rename:** `ReplaceReference` **works on an open document.**
  - With `Asm.iam` open and visible, which also loads `B.ipt`, `fd.ReplaceReference(c)` succeeded. The occurrence's `ReferencedDocumentDescriptor.FullDocumentName` became `C.ipt`.
  - `Save()` on the open assembly succeeded. `B.ipt` could then be deleted, and both files reopened clean.
  - The document does not need to be closed first.
- **The in-session way (4d):** with `Asm.iam` open, `SaveAs(d)` on the part it holds immediately repoints the open assembly to `D.ipt` and makes it `Dirty`. `Save()` on the assembly then stores the new reference.
  - Closed files are **not** updated this way: the closed drawing still pointed at `C.ipt` until the `ReplaceReference` loop ran on it.
  - So Rename always needs the loop over closed files, and only has to `Save` the open ones that `SaveAs` dirtied.
- **Times, out of process, one referencing file each:**

  | File | Open (invisible) | `ReplaceReference` | `Save` + `Close` |
  |---|---|---|---|
  | assembly | 0.2 s | 0.03 to 0.09 s | 0.3 s |
  | drawing | 0.25 to 0.5 s | 0.03 to 0.75 s | 0.5 to 0.8 s |

  So expect roughly 1 to 2 s per referencing file.
- **Drawing extension:** a drawing made from a `.dwg` template **cannot** be saved as `.idw`. `DrawingDocument.SaveAs("A.idw", false)` threw `COMException` E_FAIL. Save with the template's extension.

Code that worked:

```csharp
InventorHost.App.SilentOperation = true;
Document d;
try { d = InventorHost.App.Documents.Open(path, false); }
finally { InventorHost.App.SilentOperation = false; }
foreach (FileDescriptor fd in d.File.ReferencedFileDescriptors)
    if (string.Equals(fd.FullFileName, oldPath, StringComparison.OrdinalIgnoreCase))
        fd.ReplaceReference(newPath);
d.Save();
d.Close(true);
```

### 5. Open invisibly to read and write properties

- 30 closed parts were each opened with `Documents.Open(p, false)`, had Part Number and Designer read, and were closed with `Close(true)`. Time per file: **42 to 78 ms**, median 57 ms. 30 files took 1.7 s in total, out of process.
- Write Designer, then `Save()` and `Close(true)`: 141 ms. **The value stuck.** It read back after reopening.
- Detecting a file that is open in Inventor:
  - **A loop works:** `Documents.Cast<Document>().Any(d => string.Equals(d.FullFileName, p, StringComparison.OrdinalIgnoreCase))`. It finds documents opened visibly and invisibly. `Documents.Count` counts both, while `Documents.VisibleDocuments.Count` counts only visible ones.
  - `Documents.get_ItemByName(fullPath)` finds an open document, ignoring case. For a file that is **not** open, it **throws** `ArgumentException` (E_INVALIDARG). It does not return null.
  - Use the loop, or wrap `get_ItemByName` in `ComSafe.Get`.
  - `ItemByName` is an indexed property, so C# calls it as `get_ItemByName(string)`.

### 6. Base view on a new drawing

- `Documents.Add(DocumentTypeEnum.kDrawingDocumentObject, drawingTemplate, true)` with `Metric\ANSI (mm).dwg`. The first sheet is `dwg.ActiveSheet`: `Sheet:1`, `Size` = `kDDrawingSheetSize`, **`Width` = 86.36 cm, `Height` = 55.88 cm**. That is an ANSI D sheet, even in the metric template.
- **Signature that compiles:**

  ```csharp
  DrawingView v = sheet.DrawingViews.AddBaseView((_Document)part, point, 1.0,
      ViewOrientationTypeEnum.kFrontViewOrientation, DrawingViewStyleEnum.kHiddenLineDrawingViewStyle);
  ```

  - The three trailing optional arguments (`ModelViewName`, `ArbitraryCamera`, `AdditionalOptions`) can be left out.
  - The first parameter is `_Document`. Passing a `Document` gives **CS1503** ("cannot convert from 'Inventor.Document' to 'Inventor._Document'"), so cast it.
  - A `PartDocument` does not convert implicitly to `Document` either, so cast with `(Document)partDoc` where a `Document` is wanted.
  - `point = TransientGeometry.CreatePoint2d(sheet.Width / 2, sheet.Height / 2)` centres the view at (43.18, 27.94).
- **Fit rule check:** the part's `ComponentDefinition.RangeBox` was (0,0,0) to (12,5,3) cm.
  - The front view at scale 1 had **`Width` = 12, `Height` = 5** (cm), which is exactly the X and Y extents of the range box.
  - At scale 0.5 it was 6 × 2.5.
  - So for a front view, width = (Max.X − Min.X) × scale and height = (Max.Y − Min.Y) × scale.
- `dwg.PropertySets["Design Tracking Properties"]["Part Number"].Value = …`, then `dwg.SaveAs(@"…\AWB-90010,print_box.dwg", false)`: **no prompt**, and it took 0.85 s. The drawing kept the Part Number. Reopened, its reference to the part has `ReferenceMissing = false`.
  - The part had no Part Number set, so reading it gave the file name, `AWB-90010,print_box`, which is Inventor's fallback. That fallback value is what was copied.

### 7. Replace an occurrence

Test setup: `Rep.iam` with two occurrences of `X.ipt`, each with a flush constraint to the assembly's XY plane.

- **`occurrence.Replace(y, false)`** changes only that occurrence: `X:1` became `Y:1` and `X:2` was unchanged. **Constraints were kept:** 2 constraints, both `kUpToDateHealth`, none suppressed.
- **`Replace(z, true)`** on one occurrence replaced **every** occurrence of that occurrence's file. With both on `X.ipt`, `o1.Replace(y, true)` changed both to `Y.ipt`.
- **Reading the selection:** `doc.SelectSet[1]` (1-based) is a `ComponentOccurrence`, with `Type` = `kComponentOccurrenceObject`, for a top-level occurrence.
- **In a sub-assembly**, for example `Top.iam` → `Sub:1` → `X:1`, it is a **`ComponentOccurrenceProxy`**, with `Type` = `kComponentOccurrenceProxyObject`.
  - `is ComponentOccurrence` is also true, so a `ComponentOccurrence` cast works for both.
  - `proxy.NativeObject` is the `ComponentOccurrence` inside `Sub.iam`, and `proxy.ParentOccurrence.Name` is `Sub:1`.
  - **`proxy.Replace(y, false)` works but edits the sub-assembly:** `Sub.iam` became `Dirty` and `Top.iam` did not. Every placement of `Sub.iam` everywhere then changes. See spec 02 question 25.

### 8. An unreachable register folder

| Path | `Directory.Exists` | `File.ReadAllText` afterwards |
|---|---|---|
| `B:\aidan\AWB_Design_Data`, NAS reachable | true in 5 ms | 14 ms |
| `B:\aidan\AWB_Design_Data`, mapped drive showing **Disconnected** in `net use` (it dropped during the run) | false after **8.2 s**, and 2.8 s on a repeat a minute later | `IOException` in 2 to 31 ms |
| `\\100.100.100.100\share\…`, an IP that does not answer | false after **11.5 s** and **42 s** in two runs | `IOException` in under 0.2 s |
| `\\awb-no-such-host\share\…`, unknown host | false after 1.3 to 2.5 s | `IOException` at once |
| `Q:\not\mapped`, no such drive | false at once | `DirectoryNotFoundException` at once |

- Neither call throws for an unreachable folder. `Directory.Exists` returns false, **but only after up to 42 s**.
- Once `Exists` has returned false, `ReadAllText` fails fast.
- The 3 s timeout in task 036 is right, and the check must stay off the UI thread: a plain `Directory.Exists` on the UI thread would freeze Inventor for 8 to 42 s.
- Not tested: a Tailscale disconnect made on purpose. The drive dropping by itself during the run gave the 8.2 s figure above.

## Verification

- **Ran:**
  - Built `spike/Spike032` on Windows with `dotnet build`. It compiled against `C:\Program Files\Autodesk\Inventor 2026\Bin\Public Assemblies\Autodesk.Inventor.Interop.dll` after the two `_Document` casts noted under question 6.
  - Ran it four times against a separate, invisible Inventor 2026.2 instance: all questions, then 1, 4 and 6 again after fixing the drawing template, then 4 alone after fixing the drawing extension, and question 8 twice with no Inventor.
  - Each run quit its Inventor instance. The scratch folders were deleted afterwards.
- **Not compiled (changed under `src/InventorAddin`):** none. Nothing under `src/` changed.
- **Inventor API members not confirmed:** none of those used. Everything named in the Results compiled and ran. Not exercised: `AddBaseView`'s three optional arguments, `ComponentOccurrence.Replace2`, and a project with a non-blank `WorkspacePath`.
- **Manual checklist for Inventor:** no separate steps. Tasks 043 to 048 each carry their own checks. Two things were not seen by eye because Inventor was invisible: whether any dialog flashes up, and how the base view looks on the sheet. 044's and 047's checklists cover both.

## Follow-ups

Things noticed but not done.

- **Drawing template (044):** look it up with `kANSI_DraftingStandard`, not `kDefault_DraftingStandard`, which returns `""`. ANSI is the only standard that exists in both metric and imperial. Fall back to `GetTemplateFileWithOptions(kDrawingDocumentObject, Type.Missing ×4)` (units-neutral `standard.dwg`), and stop with a message if both are blank. Save with the template's extension: on this install it is `.dwg`, and saving a `.dwg`-template drawing as `.idw` fails. Logged as a Default.
- **Sheet size (044):** the default sheet in `ANSI (mm).dwg` is ANSI D, 86.36 × 55.88 cm. The 60% fit rule works against `Sheet.Width` and `Sheet.Height`, so no change is needed, but the sheet is large.
- **Blank workspace (043, 047, 048):** Aidan's active project is `Default.ipj`, whose `WorkspacePath` is `""`. Its folder is Inventor's own data folder. The tasks must not treat a blank workspace as that folder. Logged as a Default, and asked as spec 02 question 23.
- **Mass Update (048):** use the `FullFileName` loop to detect files already open, not a bare `get_ItemByName`, which throws when the file is not open.
- **Save As and Replace (046):** a selection inside a sub-assembly is a `ComponentOccurrenceProxy`, and `Replace` on it edits the sub-assembly file. Logged as a Default, and asked as spec 02 question 25.
- **Register store (036):** an unreachable path can block `Directory.Exists` for up to 42 s, so the existing follow-up about abandoned probe threads piling up is real. Each abandoned probe can live that long. The Settings Browse handler's `Directory.Exists` on the UI thread (task 037 follow-up) will freeze the window for 8 s or more when the NAS is down.
