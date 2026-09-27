# Main Plan
<!-- plan-doc-hygiene: 2026-09-26 f38edb6 -->

Open work only, in working order: the next item is the first line from the top. One line an item: the action, the files, who asked and when. Designs and decisions live in `docs/design/` and `docs/decisions/` once they exist; until then, the kickoff decisions are [2026-09-26-rewrite-decisions.md](./2026-09-26-rewrite-decisions.md). A landed line moves, ticked with its landing note, to `archive/YYYY-MM-done.md`. A fresh session reads the root `CLAUDE.md` first.

## Now: M1, the headless cabin flight

- [ ] M1 — see [2026-09-26-m1-headless-cabin-flight.md](./2026-09-26-m1-headless-cabin-flight.md): 52 steps in 13 waves, its acceptance list `docs/design/CONCEPT.md` section 7, the owner's six decisions ruled. Waves M1-D (design docs: game-designer, balance-analyst, event-writer), M1-A (engine foundations), M1-B (cabin geometry), M1-C (needs), M1-E (executor and crew) and M1-S (scripting) share no files and start together.

## Later milestones

- [ ] M2: Godot client. A 2D cabin view drawing Session views, a dev inspector showing decision scores, scratch scenes, the art-direction decision
- [ ] M3: player HUD and interventions. Stage-manager surfaces, alerts, event choice UI, crew staffing as policy, twin-aisle widebody
- [ ] M4: voice. The ported TTS and subtitle stack, the bundled small model, announcements
- [ ] M5: MSFS. The SimConnect clock and feed, wired in and tested in the sim
- [ ] M6: layout editor and content authoring loop

## Backlog

- [ ] Tooling tidy (review findings, 2026-09-26): `tools/test-all.ps1` comments above the build step still say "both checks" and name only the format check and the tests; the ceiling comment near the top of `sky.ps1` omits the analysis and provenance runs; `prek.toml`'s builtin `check-merge-conflict` and `detect-private-key` also run at the commit-msg stage (give the builtins `stages = ["pre-commit"]`); `test-all.ps1` and `sky.ps1 provenance` call uv without the `--locked` CI uses

- [ ] AirlineOps: crew auto-resolve for many flights at once, the booking market as the manifest source
- [ ] Saves and progression
