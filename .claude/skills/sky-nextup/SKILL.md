---
name: sky-nextup
description: Profile for the user-level `nextup` skill in the In the Sky repo, loaded by `nextup` at its step 0 for this project's plan convention, agents, reviewers, gates, docs map and landing path. Not a loop of its own; invoke `/nextup`.
---

# In the Sky profile for `nextup`

The generic loop is the user-level `nextup` skill; this file supplies only what is In the Sky's. Hotspots: none named yet. Terms (wave, slice, landing note, fix round, red proof) are in the glossary in `docs/README.md`.

siblings: none
linear: in-the-sky

## Pre-loop hook

**Trade Godot and .NET lessons with the other projects.** Run the user-level `conventions-sync` skill once with `--stack godot,dotnet`, before the queue is read. When it changed anything, `docs/GODOT_CONVENTIONS.md`, `docs/DOTNET_CONVENTIONS.md` and their `docs/.conventions-sync-<stack>.json` ledgers land on `main` as their own `docs:` commit (`git commit -F .tmp/commit-msg.txt -- <those four paths>`) before the first worktree is cut, since an implementer reads the conventions in its worktree. The skill commits its side of `~/.claude` itself.

**Check the driving docs against godot-mcp.** Run the user-level `godot-mcp-docs-sync` skill once, after the conventions sync and before the queue is read. Its driving docs: `docs/DEVELOPMENT.md` ("The Godot client", with "Scratch scenes" and "The godot MCP server"), `CLAUDE.md` (its godot MCP lines), the body of every agent file under `.claude/agents/` that names a godot tool, and `docs/GODOT_CONVENTIONS.md` (the `no-mcp-bridge-at-commit` rule). When it changed anything, the docs it edited and `docs/.godot-mcp-sync.json` land on `main` as their own `docs:` commit (`git commit -F .tmp/commit-msg.txt -- <those paths>`) before the first worktree is cut, since an implementer drives the client by them.

## Decision round

The owner directs; the orchestrator settles every non-game technical decision itself (build config, test layout, a data shape, a name, an API surface, a tool choice, where a step's scope ends), records it in its owning doc, an ADR, or the issue's description marked `(orchestrator)` with its reason, and names it in the next status note. The round asks the owner only a design, player-facing or public choice (a push, a GitHub post), with previews on the options and the recommended one first; when the owner asks to discuss two options, lay out their pros and cons in prose before asking again. A design question goes to its domain expert first (Agents), and the expert's recommendation rides on the option.

A feature-branch verdict (an explorer's `BRANCH: feat/<name>`, or a hygiene pass's branch proposal) is always the owner's, asked with its evidence in the option, never settled by the orchestrator as a technical call (user-level `nextup` §3).

## Concurrency

- Ceiling: **three** implementers, each in its own worktree: `git worktree add ../in-the-sky.wt/<slug> -b <slug> <base>` from the main checkout, then `branch.<slug>.base` and `branch.<slug>.landOn` recorded as the user-level `nextup` §3 **Base and target** says (`main` and `main` by default).
- Stacking (the user-level `nextup` §3 **Base and target**) goes through a throwaway commit, since an item lands as a patch and otherwise leaves nothing on its branch. Once a dependency's tree passes review, commit it on its branch as `wip: <slug> stack base (never lands)` and cut the dependent from that WIP sha, its `landOn` unchanged. A review fix to the dependency after the cut: commit a new WIP on the dependency, then commit the dependent's tree as its own WIP when it holds uncommitted work, `git -C <dependent wt> rebase --onto <new WIP sha> <old WIP sha>`, `git config branch.<slug>.base <new WIP sha>`, and re-run the dependent's proving commands. The dependent lands after the dependency. Each patch is taken against the item's own recorded base (Landing), so the dependency's carries its WIP and the dependent's leaves it out.
- The main checkout hosts none: every item lands there, and a landing commit must never meet an implementer's half-done tree (Traps, prek).
- Depends on, where the file lists hide it: an Engine port, type or journal record one item adds and another item's Session, Sim, Scripting or client work consumes; a Lua event API one item changes and an event module another item writes; two items that change a state shape the journal or replay reads.

## Plan

The plan lives in Linear (`linear: in-the-sky` above), per `~/.claude/docs/plan-operations.md`; `docs/plans/MAIN.md` is its generated snapshot. This repo's rules on top:

- Project order is working order: the milestone in flight's waves (`in-the-sky: M1-F` … `M1-Z`, each grouped by shared files and naming its gate in its project description), then `in-the-sky: Later milestones`, then `in-the-sky: Backlog`. A wave earlier in that order outranks a later one; inside a wave, an issue waits only for the issues it is blocked by.
- An M1 issue is titled by its step key (F5, H1, Z6), which the design docs cite; R and OD numbers are in `docs/decisions/m1-rulings.md`. Designs and maps live in `docs/design/`, `docs/decisions/` or a dated design file in `docs/plans/` linked from the issue.
- An unplanned GitHub issue goes to the wave whose files it shares, else `in-the-sky: Backlog`. An unplanned PR gets an **add**, "Review and land PR #N": a person's PR in the wave whose files it shares, a Dependabot bump in `in-the-sky: Backlog`, naming the files, whether the checks pass and whether it merges cleanly.
- The landing note `linear land --note` carries: what landed, test counts, the red proof, reviewer findings. A reviewer or implementer observation the item does not fix gets an **add** in the wave whose files it shares.

## Agents and the brief

- Domain experts (`.claude/agents/`, or user-level with this repo's overlay in `docs/agents/<agent>.md`), each consulted before the brief when the item's kind is theirs; their open questions go into the interview:
  - `game-designer`: a mechanic, system or pillar question `docs/design/` leaves open, a design item's draft, or a design-lens review of a spec or built feature.
  - `balance-analyst`: any number a decision rests on (a need rate, threshold, lever default, event chance, crew count) is its `Sky.Sim` sweep, never a guess; also the M1 acceptance numbers.
  - `event-writer`: a cabin event, its Lua module, or any player-facing words (event titles, choice labels, announcements), which the brief then carries verbatim.
  - `art-director`: an asset's brief or review and its provenance entry, or how a new aircraft, cabin or screen should look.
  - `ux-reviewer`: a proposed screen, HUD element, alert or event-choice UI, on the doc before it is built.
- Prior art: every exploration prompt asks for the `PRIOR ART` section (`docs/agents/explore.md`) (how `D:\openpax` built the subsystem and the bugs its history shows fixed there), and the brief carries each lesson that applies as a constraint with a proving test, or names it ruled out. A reproduce-and-measure or design consult on a subsystem OpenPax had reads it too.
- Explore: the user-level `Explore` (docs-first; this repo's overlay is `docs/agents/explore.md`). Orient from `docs/ARCHITECTURE.md` (its task index row, then the section for each layer touched); an item spanning layers, or whose files it does not name, goes to `Explore` with "what does this change touch, and where do the docs and the code disagree".
- Brief: the worktree root as an absolute path, the files, the change with no decision open, a proving command per step (a new behaviour gets a named new test, red first), and every string that lands in code verbatim; a wording change lists every existing string it must sweep. A client brief names the `docs/GODOT_CONVENTIONS.md` headings it touches under "Conventions touched" and asks for a `CONVENTIONS:` line in the report. Test names are PascalCase, without underscores.
- Gates, in wrapped form, each the worktree's own copy (`sky.ps1` and `test-all.ps1` move to their own root, so logs land in the worktree's `.tmp/`; a bare `tools/gate.ps1` call logs under the current directory and starts from the worktree root): `pwsh <worktree>/sky.ps1 build`; the proving command, `pwsh <worktree>/sky.ps1 test -Project P -Filter "*X"`; `pwsh <worktree>/sky.ps1 format -Check`; `pwsh <worktree>/sky.ps1 analysis -Check` when `tools/` Python changed; `pwsh <worktree>/sky.ps1 provenance -Check` when `assets/` changed; `pwsh <worktree>/tools/test-all.ps1` last, the whole gate. `sky.ps1` carries each ceiling; a command it lacks goes under `pwsh tools/gate.ps1 -Log .tmp/<name>.log -TimeoutSeconds <n> -Slot heavy|light -- …` (heavy when it builds or keeps many threads busy, light for one or two), and a `dotnet test` carries no runner `--timeout`. A gate killed with exit 124 is read by its kill line (`CLAUDE.md`): `STALLED` and `TIMED OUT` are read in the log, and only `BACKSTOP` with a low "machine free" figure is re-run once alone.
- Red proof: a new test is proven red by a named temporary break, written into the brief with the test it must turn red. The undo copies the file to `.tmp/` before the break and restores it after, then touches it so MSBuild recompiles; never `git checkout -- <file>`, which discards the implementer's other edits. A break that leaves its test green is a finding about the test's reach, reported, never worked round.

## Review

By change type; a diff of several types gets every reviewer its types name:

| Change | Reviewer |
|---|---|
| Any C# | `code-review` at `low` over the worktree's diff (if it returns nothing, `oracle` in its place) |
| Anything under `src/Sky.Engine` | also `oracle` over the worktree's unstaged diff, with the issue's decisions and a hazard list, closing `FINDINGS: <n hazard> <n defect> <n nit>` (owner's ask) |
| Anything under `src/Sky.Client` | `godot-reviewer`, with the changed files and the worktree's `.tmp/build.log` path so it builds nothing |
| A player-facing client change (M2 on) | after landing, `playtester`, and `ux-reviewer` on a new or changed screen; each finding gets an **add** |
| `CLAUDE.md`, `.claude/`, a skill | a `writing-for-agents` read-through by the orchestrator |
| `docs/design/` | the agent that owns the doc (Agents; `game-designer` for the rest) |

Stage the first round in the worktree (`git -C <worktree> add <files>`) before sending findings back, so the unstaged diff afterwards is the fix round alone. **Two fix rounds at most**, both to the same implementer by `SendMessage`; a last pass follows only a hazard. Anything a last pass raises beyond a hazard gets an **add**.

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
| A working rule for agents | `CLAUDE.md`, `.claude/agents/`, `.claude/skills/`, the user-level agents' overlays under `docs/agents/` |
| A user-visible change | nothing before the first release (`## Changelog`) |

## Landing

The item lands as one commit on its `landOn` branch (`main` below stands for it) in the checkout that has it out, the main checkout for `main`, never as a commit in its worktree: `git -C <worktree> add <the files the gate listed>`, `git -C <worktree> diff --cached --binary <branch.<slug>.base sha> -- > .tmp/<slug>.patch` (against the recorded base, not `HEAD`, so a `wip:` commit on the branch is in the patch; the `--` keeps git from reading the sha as a path), `git apply --index .tmp/<slug>.patch` on main, then the docs sweep written on main over it; the item is **land**ed after the commit. The doc-drift hook checks the committing tree's docs, which only main's sweep has. When `main` moved since the worktree was cut, `pwsh tools/test-all.ps1` runs on main over the patch first. Stage by name, write the message to `.tmp/commit-msg.txt` (the user-level `changelog-and-commit` with `## Changelog` below), commit without asking, then `git worktree remove` and `git branch -D <slug>` (a patch landing leaves the branch unmerged in git's sense, so `-d` refuses it; landed is judged by content, and a `wip:` commit never lands and never counts as unlanded work). Commit often; **push only at a milestone**, and ask first, since a push is public. Major or experimental work goes under a feature marker (user-level `nextup` §3, "Feature branches"): its items land by this same patch path in the feature worktree (`../in-the-sky.wt/feat-<name>`), `feat/<name>` is pushed at the same milestones with the same ask, the item is **land**ed with `--note "on feat/<name>, ships with #N"`, and the feature PR into main merges only through `/ship` on the feature branch. A throwaway spike lives on `spike/<topic>` and is not landed by this loop. A session running from a worktree offers `/ship` at each push point instead of pushing (user-level `nextup`, "A worktree session offers a ship instead of a push"); that offer is the ask.

## Changelog

Read by the user-level `changelog-and-commit`; each rule names the step it adds to or overrides.

- Pre-release (Step 3): no `CHANGELOG.md` until the first release cut creates it, with that release's section and an empty `## Unreleased` above it; never create it here. Until then every commit is a no-bullet commit, with the docs sweep and the plan reconciliation as usual.
- Plan (Step 2b): **land** each item after its commit.
- Docs map (Step 2c): `## Docs map` above.
- Audience (Step 4): players, in the game's own words (a lever, a verdict, an event by its title, a button's text); no project names, decision numbers or attribution.
- Commit message (Step 7): the `doc-drift-message` commit-msg hook refuses a commit staging anything under `src/Sky.Content/` and no `docs/design/` file unless the body carries `Docs: unchanged, <why>` at the start of a line.
- Hooks and tests (Step 8): never skipped; prek runs the whitespace fixers, `dotnet format style`, CSharpier, PSScriptAnalyzer, line length, gitleaks, the provenance check and a warnings-as-errors build, so no commit while an `implementer` has work in the tree (Traps). Implicated tests run as `pwsh ./sky.ps1 test -Project <P> -Filter "*<Class>"`, which gates itself.

## Traps

- prek stashes unstaged tracked edits but not untracked files, so a commit in a tree with half-done work builds a mixed tree; that is why no implementer works in the main checkout.
- MSBuild switches in dash form (`-warnaserror`, `-p:Name=Value`); Git Bash rewrites the slash form into a path.
- A test filter needs a leading wildcard or the full name: `-Filter "*ReferenceTests"` runs the class, a bare name runs zero tests. Two classes are two commands.
- Never a bare `dotnet format`: its whitespace pass undoes CSharpier. `sky.ps1 format` runs the three in order.
- A red-proof break changes a predicate or a value, never a body's shape (IDE rules fail the build first); the restored copy keeps its old timestamp, so the touch after it is a brief step, never a reminder.
- `git worktree remove` fails while a shell sits inside the worktree or an MSBuild node holds a file: stay in the main checkout, run a worktree's gate as `pwsh <worktree>/sky.ps1 …`, and after the last implementer finishes run `dotnet build-server shutdown`, then remove the folder and `git worktree prune`.
