# Main Plan
<!-- plan-doc-hygiene: 2026-09-26 f38edb6 -->

Open work only, in working order: the next item is the first line from the top. One line an item: the action, the files, who asked and when. Designs and decisions live in `docs/design/` and `docs/decisions/` once they exist; until then, the kickoff decisions are [2026-09-26-rewrite-decisions.md](./2026-09-26-rewrite-decisions.md). A landed line moves, ticked with its landing note, to `archive/YYYY-MM-done.md`. A fresh session reads [2026-09-26-handoff.md](./2026-09-26-handoff.md) first.

## Now: M0, the concept pass and the repo skeleton

Grouped into waves by shared files. Waves run side by side where their files are disjoint; inside a wave, a line that consumes an earlier line's output waits for it.

### Wave M0-C: skeleton, gates and CI (repo root config, `src/`, `tests/`, `tools/`, `assets/`, `.github/`; gate: `tools/gate.ps1` once it exists, `code-review`)

- [ ] Doc-drift hooks: port opening-hand's `tools/hooks/Test-DocDrift.ps1` for `docs/ARCHITECTURE.md` (every `src/Sky.*`) and `docs/TEST_ALMANAC.md` (every test twin and `*Tests` class, the Counts table via `-Update`), a pre-commit hook, and a commit-msg check that a commit staging `src/Sky.Content/` also stages `docs/design/` or carries `Docs: unchanged, <why>`
- [ ] Wire the provenance check, after the gates: a prek hook running `uv run --project tools/provenance python -m provenance check`, a `sky.ps1 provenance [-Check]` subcommand, and a row in `tools/test-all.ps1`
- [ ] CI on GitHub Actions (Linux), after the gates and the provenance checker: actionlint, zizmor, build, test, csharpier, `dotnet format`, the provenance check; dependabot

### Wave M0-D: agent docs and studio (`CLAUDE.md`, `docs/*.md` outside plans, `.claude/`; gate: `writing-for-agents` read-through)

- [ ] Docs skeleton, the rest: `docs/DEVELOPMENT.md` (toolchain, the `sky.ps1` commands and ceilings, prek and gitleaks, the provenance commands, godot-mcp registered at local scope, scratch scenes), then `CLAUDE.md` as a router; delete the handoff file once both land (README start map and glossary, `ARCHITECTURE.md`, `TEST_ALMANAC.md` and `GODOT_CONVENTIONS.md` have landed)
- [ ] Studio skills, after `DEVELOPMENT.md`: `sky-nextup` (the profile: agents, reviewers by change type with `oracle` for engine changes, gates, ceiling 3, Docs map, landing, the owner's rule that the orchestrator settles technical decisions) and `sky-changelog-and-commit` (no bullets before the first release); the eight agents in `.claude/agents/` have landed

### Milestone close

- [ ] Push M0 to `origin/main` once the waves above have landed (the public repo `leftos/in-the-sky` already exists, holding the kickoff commit)

## Next: M1, the headless cabin flight

- [ ] Write the M1 subplan: steps, each with its proving command, whose acceptance list is `docs/design/CONCEPT.md` section 7 (the concept pass and ADR 0010 have landed, so nothing blocks it).

It is scoped in section 1 of the decisions doc: one narrowbody, seeded manifest, crew auto-resolving event choices, a few new Lua events, score and report, replay and fuzz, and the 64x performance test.

## Later milestones

- [ ] M2: Godot client. A 2D cabin view drawing Session views, a dev inspector showing decision scores, scratch scenes, the art-direction decision
- [ ] M3: player HUD and interventions. Stage-manager surfaces, alerts, event choice UI, crew staffing as policy, twin-aisle widebody
- [ ] M4: voice. The ported TTS and subtitle stack, the bundled small model, announcements
- [ ] M5: MSFS. The SimConnect clock and feed, wired in and tested in the sim
- [ ] M6: layout editor and content authoring loop

## Backlog

- [ ] AirlineOps: crew auto-resolve for many flights at once, the booking market as the manifest source
- [ ] Saves and progression
