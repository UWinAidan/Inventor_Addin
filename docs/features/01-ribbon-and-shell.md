# 01: Ribbon and shell

The add-in's frame: the ribbon tab, per-user settings, and the plumbing every command shares.

Reference screenshots (local only): `addin tab.png`, `print tab and features.png`.

## What exists today

- A "Workflow Tools" tab on the ZeroDoc, Part, Assembly and Drawing ribbons with one "Developer" panel (`UI/RibbonSetup.cs`).
- `AddinCommand` base class: registers a text-only button and shows a message box on an unhandled exception.
- Two developer commands: *Export Model Data* and *Export Libraries*.

## Target

The tab shows different panels depending on the environment. Layout seen in the reference tool, with our own names:

**Part and Assembly**

| Panel | Button | Size | Spec |
|---|---|---|---|
| CAD Automation | Change Material and Finish | large | 03 |
| CAD Automation | New Part | small | 02 |
| CAD Automation | Save As Copy | small | 02 |
| CAD Automation | Save As and Replace | small | 02 |
| CAD Automation | Open Drawing | small | 05 |
| CAD Automation | Settings | small | this spec |
| CAD Automation | About / Version | small | this spec |
| Drawing Tools | Create Drawing | large | 05 |
| Modelling | Mass Update | small | open question |

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

- **Settings.** Per-user JSON file, proposed location `%APPDATA%\InventorWorkflowTools\settings.json`. The model, defaults and load/save logic live in Core. The Settings button opens a window for it.
- **About / Version.** Shows the add-in version and build date.
- **Command base.** Editing commands run inside one transaction and use a command type that matches what they change.
- **Icons.** 16 px and 32 px per button. Text-only is acceptable until icons exist.
- **Logging.** A rolling log file next to the settings file, so a failure in Inventor can be diagnosed afterwards.

## Open questions

1. What should "Mass Update" do? The reference button's behaviour is not known.
2. Should the About button also check for a newer version somewhere, or only display the current one?
3. Which settings does the first version need? Candidates: designer name, library file locations, template folder, default sheet size. Until answered, M0 builds only `ShowDeveloperTools` (default off).
4. Should Settings and About also appear on the ZeroDoc ribbon (Inventor with no document open)? Until answered, ZeroDoc keeps only the Developer panel.
