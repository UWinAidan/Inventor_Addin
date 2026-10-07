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

1. Read `CLAUDE.md`, `STATUS.md`, `TODO.md`, `DECISIONS.md`, `docs/PLAN.md`, the feature specs for the milestone, and the code they touch.
2. Check the specs' "Open questions". A task must not depend on an unanswered question. If the milestone cannot be planned without answers, list the blocking questions under "Waiting on Aidan: decisions" in `TODO.md` and plan only what is unblocked.
3. Split the milestone into tasks using `docs/tasks/_TEMPLATE.md`. A good task:
   - is small enough for one agent to finish and one reviewer to check in one pass
   - puts as much as possible in Core, where it can be tested in the cloud
   - keeps any add-in change thin and says exactly what Aidan must check in Inventor
   - names its dependencies, so the run order is unambiguous
4. Look through "Follow-ups from finished tasks" in `TODO.md` for items that touch the same code as this milestone. Fold the ones worth doing into tasks and delete them from `TODO.md`.
5. When an approach depends on how the Inventor API behaves and that cannot be settled from documentation, write a **spike task** flagged `needs: windows` for Aidan to run locally. Do not plan work on top of an unproven assumption.
6. Update the milestone's task list in `docs/PLAN.md`, set the item to Planned in `STATUS.md`, and log any default you chose in `DECISIONS.md`. Commit the plan. Do not write source code in plan mode.

## Parent procedure: run mode

For each task, in dependency order:

1. Confirm its dependencies are done and it is not flagged `needs: windows`. Skip tasks that are, and say so in the summary.
2. Delegate to the `implementer` subagent with the task file path. One task per implementer.
3. Delegate to the `reviewer` subagent with the same path.
4. If the reviewer returns CHANGES REQUIRED, send the must-fix list back to an implementer, then review again. After two failed rounds, stop work on that task, mark it blocked in the task file with the reason, and move on to tasks that do not depend on it.
5. On PASS, set the task's status to `done` and tick it in `docs/PLAN.md`. Copy any follow-up from the task file that is still open into `TODO.md`, and log any default the task chose in `DECISIONS.md`. Commit all of it with the task's files as one commit: `task NNN: <title>`.

Two tasks may run at the same time only when the plan marks them as touching disjoint files. Implementers never commit; the parent commits each task separately after its review.

When the milestone's runnable tasks are finished, update `STATUS.md` (the item becomes Built, since nothing has been tested in Inventor yet) and the "Next up" and "checks in Inventor" sections of `TODO.md`, then push the branch and open a pull request. The description lists:

- tasks completed, skipped and blocked
- every file under `src/InventorAddin` that was changed and **not compiled**
- Inventor API members the reviewer could not confirm
- the combined manual test checklist from the task files

End the run with one plain line: either "All tasks in this run are finished" or which tasks are still to come. Aidan merges on that line, so do not send progress updates that could be mistaken for it.

## Aidan's part: build and test on Windows

The cloud cannot compile the add-in project or run Inventor, so every pull request that touches `src/InventorAddin` needs this before merging:

1. Wait for the session's line saying all tasks in the run are finished. A pull request created earlier only carries the tasks done up to that moment.
2. `git fetch` and check out the branch. Close Inventor.
3. `dotnet build InventorAddin.slnx`, and check the output says the add-in was deployed. If Inventor is open the copy is skipped and Inventor keeps running the old build.
4. If the build fails, paste the errors to Claude, or run `claude` locally in the repo: "Build the solution and fix the compile errors on this branch without changing behaviour." Push the fixes.
5. Start Inventor. The Built time in the About window should match the build just run.
6. Work through the manual checklist in the pull request.
7. Merge, or reply with what failed. Tell the next session what the checks showed, so it can update `STATUS.md` and `TODO.md`.

Spike tasks flagged `needs: windows` are run the same way, in a local session.
