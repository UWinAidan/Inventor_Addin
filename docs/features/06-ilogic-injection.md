# 06: iLogic injection

Add a standard set of iLogic rules to a model, so every part and assembly carries the same automation without copying rules by hand.

Reference screenshots: none specific. The reference environment shows iLogic global forms docked in the browser, which is a separate mechanism from rules inside a document.

## What exists today

`ILogicReader` reads the rules inside a document (name, active flag, text) through the iLogic add-in's automation interface. Nothing writes rules yet.

## Target

- A folder of rule files kept with the add-in or pointed to in settings.
- New documents created by the add-in (spec 02) get the rules that apply to their document type and part type.
- A command to add or update the standard rules in an existing document.
- A rule that already exists with the same name is updated only if it differs from the library version.

## Core logic (testable without Inventor)

- The rule library: discovering rule files, their names, and which document and part types each applies to.
- Deciding what to do for a document: which rules to add, update or leave, given the rules already in it.

## Add-in side

- Add, replace and remove rules in a document through the iLogic automation interface.
- Set event triggers for the injected rules, if they need any.

## Open questions

1. **Which rules?** What should the standard rules do?
2. **Internal or external?** Rules copied into each document travel with the file. External rules stay in one place and are easier to update, but every user needs the folder.
3. **Should any rule run on an event**, such as before save or when an iProperty changes?
4. Are iLogic forms in scope, or only rules?
5. Should some of this be plain add-in commands instead of iLogic? Anything the add-in can do directly does not need a rule in every file.
