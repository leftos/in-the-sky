# Main Plan
<!-- plan-doc-hygiene: 2026-09-26 67fedc5 -->

Open work only, in working order: the next item is the first line from the top. One line an item: the action, the files, who asked and when. Designs and decisions live in `docs/design/` and `docs/decisions/` once they exist; until then, the kickoff decisions are [2026-09-26-rewrite-decisions.md](./2026-09-26-rewrite-decisions.md). A landed line moves, ticked with its landing note, to `archive/YYYY-MM-done.md`. A fresh session reads the root `CLAUDE.md` first.

## Now: M1, the headless cabin flight

- [ ] M1 — see [2026-09-26-m1-headless-cabin-flight.md](./2026-09-26-m1-headless-cabin-flight.md): 52 steps in 13 waves, its acceptance list `docs/design/CONCEPT.md` section 7, the owner's six decisions ruled. 8 of 52 landed (A1 to A3, B1, B2, C1, E1, D4); D1 and D2 are drafted and wait on the owner's read-through. Open and startable now: B3, B4, C2, E2, E3, S1; waves M1-B, M1-C, M1-E and M1-S share no files.

- [ ] Owner triage of the goals `docs/research/2026-09-26-openpax-avsim-goals.md` finds CONCEPT missing (a memory and CPU budget beside MSFS, automation profiles and the Captain or Lead Flight Attendant ways to play, SimBrief, GSX, cargo and weight limits, other sims, crewless flights, player-written announcements with live weather, belongings and relationships, helpful passengers, airport-set starting needs, overrides of the automatic layout work): adopt into CONCEPT, defer to a milestone, or drop (owner, 2026-09-26)

## Later milestones

- [ ] M2: Godot client. A 2D cabin view drawing Session views, a dev inspector showing decision scores, scratch scenes, the art-direction decision
- [ ] M3: player HUD and interventions. Stage-manager surfaces, alerts, event choice UI, crew staffing as policy, twin-aisle widebody
- [ ] M4: voice. The ported TTS and subtitle stack, the bundled small model, announcements
- [ ] M5: MSFS. The SimConnect clock and feed, wired in and tested in the sim
- [ ] M6: layout editor and content authoring loop

## Backlog

- [ ] Tooling tidy (review findings, 2026-09-26): `tools/test-all.ps1` comments above the build step still say "both checks" and name only the format check and the tests; the ceiling comment near the top of `sky.ps1` omits the analysis and provenance runs; `prek.toml`'s builtin `check-merge-conflict` and `detect-private-key` also run at the commit-msg stage (give the builtins `stages = ["pre-commit"]`); `test-all.ps1` and `sky.ps1 provenance` call uv without the `--locked` CI uses

- [ ] `tools/test-all.ps1` does not run prek's `line-length` (150) check, so a green whole gate still fails at commit (found landing A1+A3, 2026-09-26): add the line-length check to the whole gate, or have implementer briefs run `prek run --files <changed>`
- [ ] AirlineOps: crew auto-resolve for many flights at once, the booking market as the manifest source
- [ ] Saves and progression
