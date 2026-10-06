# Inventor_Addin
this repository is for autodesk inventor. The perpose is to automate part number assignmnet, include specific ilogic in models, add an added material and finish library, automated print tools such as hole tables, and other custom scripts to improve workflow.

## Layout

| Path | What |
|---|---|
| `src/InventorAddin.Core` | Plain data models (part, assembly, drawing, library) + JSON. No Inventor dependency — builds anywhere. |
| `src/InventorAddin` | The add-in (.NET 8, Inventor 2025+). Entry point `StandardAddInServer.cs`. |
| `src/InventorAddin/Extraction` | Reads everything from Inventor documents into the Core models: iProperties, parameters, iLogic rules, material/appearance, mass, sheet metal, holes/threads, finishes, occurrence tree, BOM, drawing sheets/views/title blocks/hole tables/parts lists/revision tables, asset libraries. Start at `ModelExtractor.Extract(doc)`. |
| `src/InventorAddin/Commands` | Ribbon buttons. |
| `src/InventorAddin/UI` | Ribbon setup (and future WPF windows). |
| `docs/ui-mockups` | Pictures of the target UI. |

## Build & run

Requires Windows + Inventor 2026 (for `Autodesk.Inventor.Interop.dll`).

```
dotnet build InventorAddin.slnx
```

The build copies the add-in to `%APPDATA%\Autodesk\Inventor 2026\Addins` so it loads next time Inventor starts (close Inventor first). Pass `-p:DeployToInventor=false` to skip.

`InventorAddin.Core` alone builds without Inventor: `dotnet build src/InventorAddin.Core`.

In Inventor, the **Workflow Tools** tab has developer buttons — *Export Model Data* dumps everything extracted from the active document to JSON, *Export Libraries* lists material/appearance libraries.
