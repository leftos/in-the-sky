# Main Plan

Open work only, in working order: the next item is the first line from the top. One line an item: the action, the files, who asked and when. Designs and decisions live in `docs/design/` and `docs/decisions/` once they exist; until then, the kickoff decisions are [2026-09-26-rewrite-decisions.md](./2026-09-26-rewrite-decisions.md). A landed line moves, ticked with its landing note, to `archive/YYYY-MM-done.md`. A fresh session reads [2026-09-26-handoff.md](./2026-09-26-handoff.md) first.

## Now: M0, the concept pass and the repo skeleton

- [ ] Concept pass with the owner (owner, 2026-09-26): `docs/design/CONCEPT.md` with the player fantasy, pillars, core loop, the need set, what each scoring outcome measures, and M1's definition of done; settles the questions in section 4 of the decisions doc; `game-designer` drafts, the owner rules
- [ ] Record the kickoff decisions as ADRs in `docs/decisions/`, starting at 0001: engine references nothing, fixed tick and `IClockSource`, one seeded RNG root with named streams, the journal, real-unit layouts with a nav graph, the utility scorer and crew task board, crew-observed views, the text-only small model, the provenance ledger
- [ ] Research the Lua runtime (decisions doc, section 4): `librarian` checks MoonSharp, NLua/KeraLua and any maintained successor for maintenance and sandboxing; findings to `docs/research/`
- [ ] Lua boundary and tick spike on a `spike/lua-boundary` branch: 200 passengers, about 10 activities, 64x, the top two runtimes against C# scoring, 250 ms ticks; the result sets where Lua runs and the tick size (ADR)
- [ ] Repo skeleton: `InTheSky.slnx`, the `src/Sky.*` and `tests/Sky.*.Tests` projects in the decisions doc's table, `global.json`, `Directory.Build.props` (warnings as errors, `AnalysisLevel=latest-recommended`, EnforceCodeStyleInBuild, Deterministic), `Directory.Packages.props`, `.editorconfig`, CSharpier pinned in `.config/dotnet-tools.json`, `CodeMetricsConfig.txt`, `.gitattributes`, `.gitignore`, MIT `LICENSE`
- [ ] Gates: `sky.ps1`, `tools/gate.ps1` with time ceilings, `tools/test-all.ps1`, prek (hygiene, CSharpier, a build with warnings as errors, doc drift, a secret scan, the provenance check) and a commit-msg docs check
- [ ] Provenance gate: `assets/PROVENANCE.toml` schema, the checker (license allowlist, a ledger entry for every asset, no entry pointing at a missing file), `CREDITS.md` generated from the ledger, wired into prek and CI
- [ ] CI on GitHub Actions (Linux): actionlint, zizmor, build, test, csharpier, `dotnet format`, the provenance check; dependabot
- [ ] Docs skeleton, following the siblings: `docs/README.md` start map and glossary, `ARCHITECTURE.md` with a task index, `DEVELOPMENT.md` (godot-mcp registered at local scope, scratch scenes), `TEST_ALMANAC.md`, `GODOT_CONVENTIONS.md` seeded through `godot-conventions-sync`; `CLAUDE.md` as a router
- [ ] Studio: `.claude/agents/` for game-designer, ux-reviewer, balance-analyst, narrative and event writer, art-director, playtester, godot-reviewer, `sky-explore`; skills `sky-nextup` (profile) and `sky-changelog-and-commit`; port and update what fits from `D:\openpax\.claude\`
- [ ] Create the public GitHub repo and push M0 (owner's go-ahead needed: public, outward-facing)

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
