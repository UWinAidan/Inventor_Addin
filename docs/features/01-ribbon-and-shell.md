# 01: Ribbon and shell

The add-in's frame: the ribbon tab, per-user settings, and the plumbing every command shares.

Reference screenshots (local only): `addin tab.png`, `print tab and features.png`.

## What exists today

- An "AWB Addin" tab on the ZeroDoc, Part, Assembly and Drawing ribbons with one "Developer" panel (`UI/RibbonSetup.cs`).
- The brand (tab name, product name, id prefix `Awb`, data folder `AwbAddin`, log file `awbaddin.log`) lives in `Branding` in Core.
- `AddinCommand` base class: registers a text-only button and shows a message box on an unhandled exception.
- Two developer commands: *Export Model Data* and *Export Libraries*.

## Target

The tab shows different panels depending on the environment.

**Panels today** (Aidan, 2026-10-12). In this order on every ribbon where their buttons appear: Create, Properties, Drawing, Developer (only when developer tools are on), AWB, so AWB is always far right. Small buttons next to each other are stacked.

| Panel | Buttons | Ribbons |
|---|---|---|
| Create | New Part (large); Save As Copy, Save As and Replace (small) | New Part: ZeroDoc, Part, Assembly. Save As Copy: Part, Assembly. Save As and Replace: Assembly |
| Properties | Part Properties (large); Mass Update (small) | Part Properties: Part, Assembly. Mass Update: ZeroDoc, Part, Assembly |
| Drawing | Create Print (large) | Part, Assembly |
| Developer | Export Model Data, Export Libraries (small) | as before; only when developer tools are on |
| AWB | Settings, About (small) | Part, Assembly, Drawing |

**At startup** (Aidan, 2026-10-12): when Inventor starts with no document open, the AWB Addin tab is made the active tab. Opening files later does not change the tab. Inventor switches back to its own tab about a second after it is ready, so the add-in watches for 5 s and makes the tab active once more the first time that happens, then stops (Default, spike 062).

The rest of this section is the reference tool's layout, kept for the buttons still to come; new buttons go into the panels above.

**Part and Assembly**

| Panel | Button | Size | Spec |
|---|---|---|---|
| CAD Automation | Change Material and Finish | large | 03 |
| CAD Automation | New Part | small | 02 (also on ZeroDoc, Default) |
| CAD Automation | Save As Copy | small | 02 |
| CAD Automation | Save As and Replace | small | 02 |
| CAD Automation | Open Drawing | small | 05 |
| CAD Automation | Settings | small | this spec |
| CAD Automation | About / Version | small | this spec |
| Drawing Tools | Create Print | large | 02 (Aidan named it Create Print on 2026-10-10; the reference called it Create Drawing) |
| Modelling | Mass Update | small | 02 (also on ZeroDoc, Default) |

**Drawing**

| Panel | Button | Size | Spec |
|---|---|---|---|
| CAD Automation | Hole Table Wizard | large | 04 |
| CAD Automation | Change Title Block | small | 05 |
| CAD Automation | Check Out of Bounds | small | 05 |
| CAD Automation | Check Balloon Usage | small | 05 |
| CAD Automation | Reference Axis Symbol | small | 05 |
| CAD Automation | Reference Part Note | small | 05 |
| CAD Automation | Add Parts List Column | small | 05 |
| CAD Automation | Place Mirror Note | large | 05 |
| CAD Automation | Place Initial Views | large | 05 |
| CAD Automation | Settings, About / Version | small | this spec |

The Developer panel stays, shown only when a setting enables it.

A button appears only once its command exists. Do not add placeholder buttons.

## Shared plumbing

- **Settings.** Per-user JSON file, location `%APPDATA%\AwbAddin\settings.json`. The model, defaults and load/save logic live in Core. The Settings button opens a window for it.
- **About / Version.** Shows the add-in version and build date.
- **Command base.** Editing commands run inside one transaction and use a command type that matches what they change.
- **Icons.** 16 px and 32 px per button. Text-only is acceptable until icons exist.
- **Logging.** A rolling log file next to the settings file, so a failure in Inventor can be diagnosed afterwards.

## Open questions

1. What should "Mass Update" do? The reference button's behaviour is not known. Partly answered 2026-10-10: Aidan wants a folder scan or Mass Update to replace the Windows login name in Designer and Detailer in bulk. Default: one command, Mass Update, scans a folder and also registers and assigns part numbers (spec 02, "Commands").
2. Should the About button also check for a newer version somewhere, or only display the current one?
3. Which settings does the first version need? Candidates: designer name, library file locations, template folder, default sheet size. Until answered, M0 builds only `ShowDeveloperTools` (default off). M1 added `DefaultDesigner`; M2 adds `RegisterFolder`, `DefaultUnits`, `WorkFolder`, `OtherCadFolders` and `OtherNamesForMe` (spec 02). A templates folder comes later (Aidan, 2026-10-10).
4. Should Settings and About also appear on the ZeroDoc ribbon (Inventor with no document open)? Until answered, ZeroDoc keeps only the Developer panel.
