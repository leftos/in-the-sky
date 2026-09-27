# Main Plan
<!-- plan-doc-hygiene: 2026-09-26 f38edb6 -->

Open work only, in working order: the next item is the first line from the top. One line an item: the action, the files, who asked and when. Designs and decisions live in `docs/design/` and `docs/decisions/` once they exist; until then, the kickoff decisions are [2026-09-26-rewrite-decisions.md](./2026-09-26-rewrite-decisions.md). A landed line moves, ticked with its landing note, to `archive/YYYY-MM-done.md`. A fresh session reads [2026-09-26-handoff.md](./2026-09-26-handoff.md) first.

## Now: M0, the concept pass and the repo skeleton

Grouped into waves by shared files. Waves run side by side where their files are disjoint; inside a wave, a line that consumes an earlier line's output waits for it.

### Wave M0-A: design docs (`docs/design/`, `docs/decisions/`; gate: owner ruling, `game-designer` review)

- [ ] Concept pass with the owner (owner, 2026-09-26): `docs/design/CONCEPT.md` with the player fantasy, pillars, core loop, the need set, what each scoring outcome measures, and M1's definition of done; settles the questions in section 4 of the decisions doc; `game-designer` drafts, the owner rules

### Wave M0-B: Lua runtime (`docs/research/`, `spike/lua-boundary` branch; gate: measured numbers, ADR)

- [ ] Research the Lua runtime (decisions doc, section 4): `librarian` checks MoonSharp, NLua/KeraLua and any maintained successor for maintenance and sandboxing; findings to `docs/research/`
- [ ] Lua boundary and tick spike on a `spike/lua-boundary` branch, after the research and the skeleton: 200 passengers, about 10 activities, 64x, the top two runtimes against C# scoring, 250 ms ticks; the result sets where Lua runs and the tick size (ADR)

### Wave M0-C: skeleton, gates and CI (repo root config, `src/`, `tests/`, `tools/`, `assets/`, `.github/`; gate: `tools/gate.ps1` once it exists, `code-review`)

- [ ] Repo skeleton: `InTheSky.slnx`, the `src/Sky.*` and `tests/Sky.*.Tests` projects in the decisions doc's table, `global.json`, `Directory.Build.props` (warnings as errors, `AnalysisLevel=latest-recommended`, EnforceCodeStyleInBuild, Deterministic), `Directory.Packages.props`, `.editorconfig`, CSharpier pinned in `.config/dotnet-tools.json`, `CodeMetricsConfig.txt`, `.gitattributes`, `.gitignore`, MIT `LICENSE`
- [ ] Gates, after the skeleton: `sky.ps1`, `tools/gate.ps1` with time ceilings, `tools/test-all.ps1`, prek (hygiene, CSharpier, a build with warnings as errors, doc drift, a secret scan, the provenance check) and a commit-msg docs check
- [ ] Provenance gate: `assets/PROVENANCE.toml` schema (`docs/README.md` already names the file), the checker (license allowlist, a ledger entry for every asset, no entry pointing at a missing file), `CREDITS.md` generated from the ledger, wired into prek and CI
- [ ] CI on GitHub Actions (Linux), after the gates and the provenance checker: actionlint, zizmor, build, test, csharpier, `dotnet format`, the provenance check; dependabot

### Wave M0-D: agent docs and studio (`CLAUDE.md`, `docs/*.md` outside plans, `.claude/`; gate: `writing-for-agents` read-through)

- [ ] Docs skeleton, following the siblings: `docs/README.md` start map and glossary, `ARCHITECTURE.md` with a task index, `DEVELOPMENT.md` (godot-mcp registered at local scope, scratch scenes), `TEST_ALMANAC.md`, `GODOT_CONVENTIONS.md` seeded through `godot-conventions-sync`; `CLAUDE.md` as a router; delete the handoff file once this lands
- [ ] Studio: `.claude/agents/` for game-designer, ux-reviewer, balance-analyst, narrative and event writer, art-director, playtester, godot-reviewer, `sky-explore`; skills `sky-nextup` (profile) and `sky-changelog-and-commit`; port and update what fits from `D:\openpax\.claude\`

### Milestone close

- [ ] Push M0 to `origin/main` once the waves above have landed (the public repo `leftos/in-the-sky` already exists, holding the kickoff commit)

## Next: M1, the headless cabin flight

Broken into steps, each with its proving command, in an M1 subplan written after the concept pass. It is scoped in section 1 of the decisions doc: one narrowbody, seeded manifest, crew auto-resolving event choices, a few new Lua events, score and report, replay and fuzz, and the 64x performance test.

## Later milestones

- [ ] M2: Godot client. A 2D cabin view drawing Session views, a dev inspector showing decision scores, scratch scenes, the art-direction decision
- [ ] M3: player HUD and interventions. Stage-manager surfaces, alerts, event choice UI, crew staffing as policy, twin-aisle widebody
- [ ] M4: voice. The ported TTS and subtitle stack, the bundled small model, announcements
- [ ] M5: MSFS. The SimConnect clock and feed, wired in and tested in the sim
- [ ] M6: layout editor and content authoring loop

## Backlog

- [ ] AirlineOps: crew auto-resolve for many flights at once, the booking market as the manifest source
- [ ] Saves and progression
