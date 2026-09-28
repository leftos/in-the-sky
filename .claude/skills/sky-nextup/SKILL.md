---
name: sky-nextup
description: Profile for the user-level `nextup` skill in the In the Sky repo, loaded by `nextup` at its step 0 for this project's plan convention, agents, reviewers, gates, docs map and landing path. Not a loop of its own; invoke `/nextup`.
---

# In the Sky profile for `nextup`

The generic loop is the user-level `nextup` skill; this file supplies only what is In the Sky's. Siblings: none, one repository. Hotspots: none named yet. Terms (wave, slice, landing note, fix round, red proof) are in the glossary in `docs/README.md`.

## Pre-loop hook

**Trade Godot lessons with the other projects.** Run the user-level `godot-conventions-sync` skill once, before the index is read. When it changed anything, `docs/GODOT_CONVENTIONS.md` and `docs/.godot-conventions-sync.json` land on `main` as their own `docs:` commit (`git commit -F .tmp/commit-msg.txt -- <the two paths>`) before the first worktree is cut, since an implementer reads the conventions in its worktree. The skill commits its side of `~/.claude` itself.

## Decision round

The owner directs; the orchestrator settles every non-game technical decision itself (build config, test layout, a data shape, a name, an API surface, a tool choice, where a step's scope ends), records it in its owning doc, an ADR, or the subplan's Decisions list marked `(orchestrator)` with its reason, and names it in the next status note. The round asks the owner only a design, player-facing or public choice (a push, a GitHub post), with previews on the options and the recommended one first; when the owner asks to discuss two options, lay out their pros and cons in prose before asking again. A design question goes to its domain expert first (Agents), and the expert's recommendation rides on the option.

## Concurrency

- Ceiling: **three** implementers, each in its own worktree: `git worktree add ../in-the-sky.wt/<slug> -b <slug> main` from the main checkout. The main checkout hosts none: every item lands there, and a landing commit must never meet an implementer's half-done tree (Traps, prek).
- Depends on, where the file lists hide it: an Engine port, type or journal record one item adds and another item's Session, Sim, Scripting or client work consumes; a Lua event API one item changes and an event module another item writes; two items that change a state shape the journal or replay reads.

## Plan

- Index: `docs/plans/MAIN.md`, open work only, in working order: "Now" (the milestone in flight, grouped into waves by shared files, each wave naming its gate), "Next", "Later milestones", "Backlog". The next item is the first unchecked line from the top. A line is one item: the action, the files, who asked and when; designs and maps live in `docs/design/`, `docs/decisions/` or a linked dated subplan (`docs/plans/<date>-<slug>.md`).
- Issues: `gh issue list -R leftos/in-the-sky`. An unplanned issue gets a line in the wave whose files it shares, else under "Backlog".
- Finished-item convention: **a landed line leaves the index in its landing commit.** It moves, ticked, to the foot of `docs/plans/archive/YYYY-MM-done.md` (the month's file; a new month opens a new one) with its landing note, `Landed YYYY-MM-DD: <what landed, test counts, the red proof, reviewer findings>`. An item worked from a subplan is ticked there with the same note, and the index line goes with the subplan's last item; a finished subplan moves to `docs/plans/archive/`, never deleted. A reviewer or implementer observation the item does not fix becomes a new unchecked line beside the items that share its files.

## Agents and the brief

- Domain experts (`.claude/agents/`), each consulted before the brief when the item's kind is theirs; their open questions go into the interview:
  - `game-designer`: a mechanic, system or pillar question `docs/design/` leaves open, a design item's draft, or a design-lens review of a spec or built feature.
  - `balance-analyst`: any number a decision rests on (a need rate, threshold, lever default, event chance, crew count) is its `Sky.Sim` sweep, never a guess; also the M1 acceptance numbers.
  - `event-writer`: a cabin event, its Lua module, or any player-facing words (event titles, choice labels, announcements), which the brief then carries verbatim.
  - `art-director`: an asset's brief or review and its provenance entry, or how a new aircraft, cabin or screen should look.
  - `ux-reviewer`: a proposed screen, HUD element, alert or event-choice UI, on the doc before it is built.
- Prior art: every exploration prompt asks for `sky-explore`'s `PRIOR ART` section (how `D:\openpax` built the subsystem and the bugs its history shows fixed there), and the brief carries each lesson that applies as a constraint with a proving test, or names it ruled out. A reproduce-and-measure or design consult on a subsystem OpenPax had reads it too.
- Explore: `sky-explore`. Orient from `docs/ARCHITECTURE.md` (its task index row, then the section for each layer touched); an item spanning layers, or whose files it does not name, goes to `sky-explore` with "what does this change touch, and where do the docs and the code disagree".
- Brief: the worktree root as an absolute path, the files, the change with no decision open, a proving command per step (a new behaviour gets a named new test, red first), and every string that lands in code verbatim; a wording change lists every existing string it must sweep. A client brief names the `docs/GODOT_CONVENTIONS.md` headings it touches under "Conventions touched" and asks for a `CONVENTIONS:` line in the report. Test names are PascalCase, without underscores.
- Gates, in wrapped form, each the worktree's own copy (`sky.ps1` and `test-all.ps1` move to their own root, so logs land in the worktree's `.tmp/`; a bare `tools/gate.ps1` call logs under the current directory and starts from the worktree root): `pwsh <worktree>/sky.ps1 build`; the proving command, `pwsh <worktree>/sky.ps1 test -Project P -Filter "*X"`; `pwsh <worktree>/sky.ps1 format -Check`; `pwsh <worktree>/sky.ps1 analysis -Check` when `tools/` Python changed; `pwsh <worktree>/sky.ps1 provenance -Check` when `assets/` changed; `pwsh <worktree>/tools/test-all.ps1` last, the whole gate. `sky.ps1` carries each ceiling; a command it lacks goes under `pwsh tools/gate.ps1 -Log .tmp/<name>.log -TimeoutSeconds <n> -Slot heavy|light -- …` (heavy when it builds or keeps many threads busy, light for one or two), and a `dotnet test` carries no runner `--timeout`. A gate killed with exit 124 is read by its kill line (`CLAUDE.md`): `STALLED` and `TIMED OUT` are read in the log, and only `BACKSTOP` with a low "machine free" figure is re-run once alone.
- Red proof: a new test is proven red by a named temporary break, written into the brief with the test it must turn red. The undo copies the file to `.tmp/` before the break and restores it after, then touches it so MSBuild recompiles; never `git checkout -- <file>`, which discards the implementer's other edits. A break that leaves its test green is a finding about the test's reach, reported, never worked round.

## Review

By change type; a diff of several types gets every reviewer its types name:

| Change | Reviewer |
|---|---|
| Any C# | `code-review` at `low` over the worktree's diff (if it returns nothing, `oracle` in its place) |
| Anything under `src/Sky.Engine` | also `oracle` over the worktree's unstaged diff, with the subplan's decisions and a hazard list, closing `FINDINGS: <n hazard> <n defect> <n nit>` (owner's ask) |
| Anything under `src/Sky.Client` | `godot-reviewer`, with the changed files and the worktree's `.tmp/build.log` path so it builds nothing |
| A player-facing client change (M2 on) | after landing, `playtester`, and `ux-reviewer` on a new or changed screen; findings become plan lines |
| `CLAUDE.md`, `.claude/`, a skill | a `writing-for-agents` read-through by the orchestrator |
| `docs/design/` | the agent that owns the doc (Agents; `game-designer` for the rest) |

Stage the first round in the worktree (`git -C <worktree> add <files>`) before sending findings back, so the unstaged diff afterwards is the fix round alone. **Two fix rounds at most**, both to the same implementer by `SendMessage`; a last pass follows only a hazard. Anything a last pass raises beyond a hazard becomes a plan line.

## Docs map

Match the diff's paths and the implementer's `SURFACES` line against this table and make every owning doc true before the commit. A change with no surface says so on a line of the body starting `Docs: unchanged, <why>`.

| What changed | Owning doc |
|---|---|
| A `src/Sky.*` project added or removed, or a reference edge | `docs/ARCHITECTURE.md` |
| A test class or test project | `docs/TEST_ALMANAC.md`; its Counts table via `pwsh tools/hooks/Test-DocDrift.ps1 -Update` |
| `sky.ps1`, `tools/`, `prek.toml`, CI | `docs/DEVELOPMENT.md` (and `sky.ps1 help` for a subcommand) |
| An engineering decision the code cannot show | a new ADR in `docs/decisions/`, listed in its `README.md` |
| `src/Sky.Content/` | the owning `docs/design/` doc, or `Docs: unchanged, <why>` (the `doc-drift-message` commit-msg hook checks it) |
| An asset | `assets/PROVENANCE.toml`, then `pwsh ./sky.ps1 provenance` regenerates `CREDITS.md`; all three in one commit |
| A Godot client rule | `docs/GODOT_CONVENTIONS.md` |
| A new term | the glossary in `docs/README.md`, in the commit that first uses it |
| A new or moved doc | `docs/README.md`, and this table when the doc owns a surface |
| A working rule for agents | `CLAUDE.md`, `.claude/agents/`, `.claude/skills/` |
| A user-visible change | nothing before the first release (`sky-changelog-and-commit`) |

## Landing

The item lands as one commit on `main` in the main checkout (`D:\in-the-sky`), never as a commit in its worktree: `git -C <worktree> add <the files the gate listed>`, `git -C <worktree> diff --cached --binary > .tmp/<slug>.patch`, `git apply --index .tmp/<slug>.patch` on main, then the docs sweep and the plan edit written on main over it. The doc-drift hook checks the committing tree's docs, which only main's sweep has. When `main` moved since the worktree was cut, `pwsh tools/test-all.ps1` runs on main over the patch first. Stage by name, write the message to `.tmp/commit-msg.txt` (the `sky-changelog-and-commit` rules), commit without asking, then `git worktree remove` and `git branch -D <slug>` (a patch landing leaves the branch unmerged in git's sense, so `-d` refuses it). Commit often; **push only at a milestone**, and ask first, since a push is public. Major, experimental or spike work lives on a feature branch (`spike/<topic>`) and is not landed by this loop.

## Traps

- prek stashes unstaged tracked edits but not untracked files, so a commit in a tree with half-done work builds a mixed tree; that is why no implementer works in the main checkout.
- MSBuild switches in dash form (`-warnaserror`, `-p:Name=Value`); Git Bash rewrites the slash form into a path.
- A test filter needs a leading wildcard or the full name: `-Filter "*ReferenceTests"` runs the class, a bare name runs zero tests. Two classes are two commands.
- Never a bare `dotnet format`: its whitespace pass undoes CSharpier. `sky.ps1 format` runs the three in order.
- A red-proof break changes a predicate or a value, never a body's shape (IDE rules fail the build first); the restored copy keeps its old timestamp, so the touch after it is a brief step, never a reminder.
- `git worktree remove` fails while a shell sits inside the worktree or an MSBuild node holds a file: stay in the main checkout, run a worktree's gate as `pwsh <worktree>/sky.ps1 …`, and after the last implementer finishes run `dotnet build-server shutdown`, then remove the folder and `git worktree prune`.
