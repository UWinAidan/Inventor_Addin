# How work gets planned and built

One **parent** session plans and coordinates. **Child** agents do the work: an `implementer` builds one task, a `reviewer` checks it. Both are defined in `.claude/agents/`.

```
Aidan ──► parent session ──► docs/PLAN.md, docs/tasks/NNN-*.md      (plan mode)
                         └─► implementer ─► reviewer ─► commit       (run mode, per task)
                         └─► pull request ──► Aidan builds on Windows and tests in Inventor
```

Parallel work happens across cloud sessions, one per milestone or independent task. Within a session, tasks run in order.

## One-time setup

1. **Push the repo.** A cloud session clones from GitHub, not from the local folder. Untracked and unpushed files do not exist for it.
2. **Connect GitHub** to Claude Code, with the Claude GitHub App or `/web-setup`.
3. **Add a setup script** to the cloud environment (environment settings, "Setup script" field). The .NET SDK is not pre-installed:

   ```bash
   #!/bin/bash
   apt-get update -qq || true
   DEBIAN_FRONTEND=noninteractive apt-get install -y -qq dotnet-sdk-10.0 dotnet-sdk-8.0
   ```

   SDK 10 reads the `.slnx` solution file. SDK 8 brings the .NET 8 runtime the tests run on. Leave network access at **Trusted** so NuGet restore works.

## Starting sessions

Plan a milestone:

```
claude --cloud "Plan milestone M1 following docs/WORKFLOW.md. Stop after writing the task briefs."
```

Run a planned milestone:

```
claude --cloud "Run milestone M1 following docs/WORKFLOW.md."
```

Run a single task:

```
claude --cloud "Run task docs/tasks/001-core-test-project.md following docs/WORKFLOW.md."
```

## Parent procedure: plan mode

1. Read `CLAUDE.md`, `docs/PLAN.md`, the feature specs for the milestone, and the code they touch.
2. Check the specs' "Open questions". A task must not depend on an unanswered question. If the milestone cannot be planned without answers, list the blocking questions under "Blocked on Aidan" in `docs/PLAN.md` and plan only what is unblocked.
3. Split the milestone into tasks using `docs/tasks/_TEMPLATE.md`. A good task:
   - is small enough for one agent to finish and one reviewer to check in one pass
   - puts as much as possible in Core, where it can be tested in the cloud
   - keeps any add-in change thin and says exactly what Aidan must check in Inventor
   - names its dependencies, so the run order is unambiguous
4. When an approach depends on how the Inventor API behaves and that cannot be settled from documentation, write a **spike task** flagged `needs: windows` for Aidan to run locally. Do not plan work on top of an unproven assumption.
5. Update the milestone's task list in `docs/PLAN.md`. Commit the plan. Do not write source code in plan mode.

## Parent procedure: run mode

For each task, in dependency order:

1. Confirm its dependencies are done and it is not flagged `needs: windows`. Skip tasks that are, and say so in the summary.
2. Delegate to the `implementer` subagent with the task file path. One task per implementer.
3. Delegate to the `reviewer` subagent with the same path.
4. If the reviewer returns CHANGES REQUIRED, send the must-fix list back to an implementer, then review again. After two failed rounds, stop work on that task, mark it blocked in the task file with the reason, and move on to tasks that do not depend on it.
5. On PASS, set the task's status to `done`, tick it in `docs/PLAN.md`, and commit the task's files as one commit: `task NNN: <title>`.

Two tasks may run at the same time only when the plan marks them as touching disjoint files. Implementers never commit; the parent commits each task separately after its review.

When the milestone's runnable tasks are finished, push the branch and open a pull request. The description lists:

- tasks completed, skipped and blocked
- every file under `src/InventorAddin` that was changed and **not compiled**
- Inventor API members the reviewer could not confirm
- the combined manual test checklist from the task files

## Aidan's part: build and test on Windows

The cloud cannot compile the add-in project or run Inventor, so every pull request that touches `src/InventorAddin` needs this before merging:

1. `git fetch` and check out the branch. Close Inventor.
2. `dotnet build InventorAddin.slnx`
3. If the build fails, run `claude` locally in the repo: "Build the solution and fix the compile errors on this branch without changing behaviour." Push the fixes.
4. Start Inventor and work through the manual checklist in the pull request.
5. Merge, or reply on the pull request with what failed.

Spike tasks flagged `needs: windows` are run the same way, in a local session.
