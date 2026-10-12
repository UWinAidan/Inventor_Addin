# 062: Spike: occurrence names in a closed assembly; the startup tab (Windows)

- **Milestone:** M2 (fix run 2, from Aidan's checks of the first fix run)
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` ("Commands: Rename"); `docs/features/01-ribbon-and-shell.md`
- **Status:** done
- **Depends on:** none
- **Needs:** windows (local session with Aidan)
- **Parallel-safe with:** none

## Goal

Find, in Inventor 2026, why Rename leaves a closed assembly's occurrences under the old name, and whether the add-in can make its own tab the active one when Inventor starts with no document open. Tasks 065 and 066 build on the answers.

## Questions

1. A closed assembly is opened invisibly and `ReplaceReference` points it at the renamed part. What do `ComponentOccurrence.Name` and the browser show, and how can the occurrence name be changed so the browser shows it?
2. Does `ApplicationEvents.OnReady` fire after the add-in is activated at startup, and does setting `RibbonTab.Active = true` on the ZeroDoc ribbon select the add-in's tab and keep it selected?

## Results

Run on Aidan's PC, 2026-10-12, Inventor 2026.2. Question 1 used a throwaway COM console app (scratchpad `Spike062`) driving its own invisible Inventor. Question 2 used two throwaway test builds of the add-in that Aidan started Inventor with, logging every step.

### 1. Occurrence names in a closed assembly (bug 3 of these checks)

Aidan's log for the rename with the assembly closed reads `'C:\CAD_TEST\ASSY_GEARBOX,AWB-10018.iam' updated, 0 occurrences renamed.`

Reproduced: part `BRACKET,AWB-90001.ipt`, assembly with two occurrences, both closed. The part was opened, `SaveAs` to `BRACKET_V2,AWB-90001.ipt`, then the assembly opened invisibly with `SilentOperation`:

- Before `ReplaceReference`: occurrences `BRACKET,AWB-90001:1` and `:2`, descriptor on the old path.
- **After `ReplaceReference`: `ComponentOccurrence.Name` already reads `BRACKET_V2,AWB-90001:1` and `:2`**, and `ReferencedDocumentDescriptor.FullDocumentName` reads the new path. So `RenamePlan.RenamedOccurrence`, which looks for the old file name, finds nothing to rename. That is the bug.
- Saved without setting the names, reopened: `Name` still reads `BRACKET_V2,…:1`, but opened visibly the **browser shows `BRACKET,AWB-90001:1` and `:2`** (`BrowserPane.TopNode.BrowserNodes[…].BrowserNodeDefinition.Label`). The stored name is the old one; `Name` reports a name computed from the new file.
- Setting `Name` to the value it already reads (`BRACKET_V2,AWB-90001:1`) changes nothing: the browser still shows the old name after save and reopen.
- **Setting `Name` to a temporary name first (`TMPNAME:1`), then to `BRACKET_V2,AWB-90001:1`, then `Dirty = true` and `Save`, works**: reopened visibly, the browser shows `BRACKET_V2,AWB-90001:1` and `:2`.
- Aidan's own assembly, read back after his rename: `Name` reads `BRACKET_V3,AWB-10017:1` and `:2` (the current part), matching the reproduction.

The open-assembly case was confirmed by Aidan's checks (task 058): there `Name` still reads the old name after the part's `SaveAs`, and one set renames it.

### 2. The startup tab

Test build 1 (set the tab active in `OnReady`):

- At `Activate`, `Application.Ready` is `False` and no document is visible; the ZeroDoc ribbon's active tab reads `id_TabTools`.
- `OnReady` fires about 1.2 s later, once, with `EventTimingEnum.kAfter`, no document visible.
- `RibbonTab.Active = true` on the add-in's ZeroDoc tab: the tab then reads `Active = True` and is the active ZeroDoc tab. **But Aidan saw Inventor stay on its own start tab.**

Test build 2 (log every ribbon every 0.5 s for 10 s after `OnReady`, and set the tab active again at 2 s and 6 s):

- At `OnReady` the active environment is `FWxMainFrameEnvironment` and the active ribbon is ZeroDoc. Setting the tab works at first.
- **Between 0.5 s and 1.5 s after `OnReady`, Inventor switches the ZeroDoc ribbon back to `id_TabTools` by itself.**
- Set again at 2 s: it stuck, and was still the active tab at 6 s and after. Aidan saw Inventor end on the AWB Addin tab.

Members confirmed (compile and run): `Application.Ready`, `Application.ApplicationEvents`, `ApplicationEvents.OnReady` (delegate `ApplicationEventsSink_OnReadyEventHandler(EventTimingEnum, NameValueMap, out HandlingCodeEnum)`), `Documents.VisibleDocuments.Count`, `UserInterfaceManager.Ribbons[name]`, `Ribbon.RibbonTabs`, `Ribbon.Active` (get), `RibbonTab.Active` (get and set), `RibbonTab.InternalName`, `UserInterfaceManager.ActiveEnvironment.InternalName`, `ComponentOccurrence.Name` (set to a temporary name, then the final one), `BrowserPane.TopNode.BrowserNodes`, `BrowserNodeDefinition.Label`.

## Recommendations

- **Task 065:** in every assembly Rename updates, open or closed, an occurrence of the renamed file whose name reads as the old file name **or as the new file name**, a colon and a number is set to a temporary name and then to the new name, so the stored name changes even when `Name` already reports the new one. The rename count counts these.
- **Task 066:** at startup, when Inventor is not yet ready, wait for `OnReady`. Then, if no document is visible, make the tab active, and watch the ZeroDoc ribbon for a few seconds: the first time another tab becomes active while no document is visible, make the tab active once more, then stop. If a document opens, stop. Never set it again after that, so a tab Aidan clicks later stays. When the add-in is loaded after Inventor is ready (Add-in Manager), do nothing.
