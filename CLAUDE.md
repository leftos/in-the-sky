# In the Sky

A passenger and crew cabin simulator: the player is the stage manager of an airliner cabin, setting the conditions the flight plays out in. Simulation in C# (`src/Sky.Engine`, references nothing), events in Lua (`Sky.Scripting`), Godot 4.7.2 .NET client from M2. A greenfield rewrite of OpenPax (`D:\openpax`, read-only). This file is a router: every bullet names the document that owns a question. Rules carry who set them and when, so a later reader can tell a standing rule from a superseded one.

## Start here

- `docs/README.md`: the map and the glossary. Read it before searching the tree; a word used in a project sense is defined there.
- `docs/plans/MAIN.md`: open work only, in working order; the next item is the first line from the top.
- `docs/ARCHITECTURE.md`: the projects, the dependency edges they may have, and where a change goes.
- `docs/DEVELOPMENT.md`: toolchain, first-clone setup, `sky.ps1` commands and ceilings, hooks, provenance, the godot MCP server.
- `docs/TEST_ALMANAC.md`: every test class, what it proves, where a new one goes.
- `docs/GODOT_CONVENTIONS.md`: the client's rules; read before any edit under `src/Sky.Client`.
- `docs/decisions/`: the ADRs, numbered records of engineering decisions the code cannot show.
- `docs/design/`: what the game is (`CONCEPT.md`, with the owner's rulings) and the design docs each studio agent owns.

## Commands

Everything runs from the repo root through `sky.ps1` (`pwsh ./sky.ps1 help`). Every build, test and format gate runs through `tools/gate.ps1` with one log per gate under `.tmp/` and the ceilings `docs/DEVELOPMENT.md` lists. `pwsh tools/test-all.ps1` is the whole gate: the build alone, then the tests, the format check, the Python checks, the provenance check and the 150-character line check side by side, each command under the gate, one table, one exit code. The gate is a copy of `~/.claude/tools/gate/gate.ps1`, which `sync-gate.ps1` keeps in step: change it there, never here.

## Non-negotiables

- `Sky.Engine` references nothing: no other Sky project, no Godot, no Lua runtime; state is passed in explicitly and other projects implement its ports. A new project reference is an architecture change raised in the plan and recorded in `docs/decisions/`, never added to make a build pass (ADR 0001).
- The repo is public and MIT-licensed: no secrets in the tree, and every asset has an entry in `assets/PROVENANCE.toml` with a license on the allowlist; `CREDITS.md` is generated from the ledger, never edited by hand (ADR 0009; owner, 2026-09-26).
- Every build, test, format or headless run goes through `sky.ps1` or `tools/gate.ps1` with its ceiling, never bare. The gate's ceiling counts load-adjusted time, so a run slowed by other agents is not killed for it (user, 2026-09-27). Its kill line (exit 124) says why: `STALLED` (no output and no CPU for 120 s) has hung, so read the log; `TIMED OUT` kept working past the ceiling even allowing for load, a busy loop or a ceiling set too tight, so read the log before touching the ceiling; `BACKSTOP` (5 times the ceiling in wall time) with a low "machine free" figure means the machine was busy, so re-run it once alone. `dotnet test` carries no runner `--timeout` locally: the gate catches a hung test. Never re-run a command to read its output differently.
- MSBuild switches in dash form: `-warnaserror`, `-p:Name=Value`. Git Bash rewrites the slash form into a path. Test filters need the full class name or a wildcard (`-Filter "*ReferenceTests"`); a bare name runs zero tests.
- Every csproj sets its own `TargetFramework` (`net10.0`); it never goes in `Directory.Build.props`, which Godot rewrites (godot#103545).
- Warnings are errors, and CA1502 complexity is capped at 8 by `CodeMetricsConfig.txt`.

## Workflow

- Every task is a checkbox line in `docs/plans/MAIN.md` (or a subplan it links); a landed line moves, ticked, to `docs/plans/archive/`. A steer that arrives mid-task becomes a line there before anything else.
- The orchestrator settles non-game technical decisions itself (build config, test layout, tool choices) and records each in the owning doc or an ADR; only design, player-facing or public choices (a push, a GitHub post) go to the owner, through `AskUserQuestion` (owner, 2026-09-26). The owner answers design questions well with previews on the options; when they ask to discuss the tradeoffs of two options, lay out the pros and cons in prose before asking again (owner, 2026-09-26).
- Source and test edits go to the user-level `implementer` agent with a brief naming the worktree root, the files, the change and a proving command per step; the main session owns docs, plans, config, ADRs and commits.
- `/nextup` runs the plan through the project profile `.claude/skills/sky-nextup/SKILL.md` (agents, reviewers, gates, the Docs map, landing); every commit follows `.claude/skills/sky-changelog-and-commit/SKILL.md`.
- Git: commit often inside the `/nextup` loop, push at milestones, and use feature branches for major, experimental or spike work (owner, 2026-09-26).
- Studio agents in `.claude/agents/`, the owner directing:
  - `game-designer`: design decisions, mechanics, design reviews and specs; owns `docs/design/CONCEPT.md` and every design doc no other agent owns.
  - `event-writer`: cabin events, their Lua modules and all player-facing words; owns `docs/design/events.md`.
  - `balance-analyst`: runs `Sky.Sim` seed sweeps and reads the balance CSV; owns `docs/design/balance.md`.
  - `art-director`: the look, asset briefs and asset review against the provenance ledger; owns `docs/design/art-direction.md`.
  - `ux-reviewer`: critique of a proposed or built screen, HUD element or event-choice UI.
  - `playtester`: plays the client through the godot MCP server and reports what a player would feel.
  - `godot-reviewer`: read-only review of every change under `src/Sky.Client`.
  - `sky-explore`: read-only "where is X, how does Y work" questions, starting from the docs; use it instead of a generic explorer.
- An agent driving the client loads the `godot-mcp` skill first; setup and the bridge rules are in `docs/DEVELOPMENT.md` "The godot MCP server". Friction with the server is filed at `leftos/godot-mcp`.
