# Main Plan
<!-- plan-doc-hygiene: 2026-09-26 67fedc5 -->

Open work only, in working order: the next item is the first line from the top. One line an item: the action, the files, who asked and when. Designs and decisions live in `docs/design/` and `docs/decisions/` once they exist; until then, the kickoff decisions are [2026-09-26-rewrite-decisions.md](./2026-09-26-rewrite-decisions.md). A landed line moves, ticked with its landing note, to `archive/YYYY-MM-done.md`. A fresh session reads the root `CLAUDE.md` first.

## Now: M1, the headless cabin flight

- [ ] M1 — see [2026-09-26-m1-headless-cabin-flight.md](./2026-09-26-m1-headless-cabin-flight.md): 52 steps in 13 waves, its acceptance list `docs/design/CONCEPT.md` section 7, the owner's six decisions ruled. 10 of 52 landed (A1 to A3, B1, B2, C1, E1, D1, D2, D4). Open and startable now: B3, B4, C2, D3, E2, E3, S1; waves M1-B, M1-C, M1-E and M1-S share no files.

- [ ] Owner triage of the goals `docs/research/2026-09-26-openpax-avsim-goals.md` finds CONCEPT missing (a memory and CPU budget beside MSFS, automation profiles and the Captain or Lead Flight Attendant ways to play, SimBrief, GSX, cargo and weight limits, other sims, crewless flights, player-written announcements with live weather, belongings and relationships, helpful passengers, airport-set starting needs, overrides of the automatic layout work): adopt into CONCEPT, defer to a milestone, or drop (owner, 2026-09-26). Ruled so far (owner, 2026-09-26), for game-designer to fold into CONCEPT when the triage ends: automation profiles and Captain or Lead Flight Attendant play are presets of the one stage-manager role, a shareable saved lever set, built at M3; crewless flights stay, scored on flight smoothness instead (G-rates, turbulence met, prompt departure and arrival), which needs the sim feed (M5) and reopens CONCEPT question 9 for that mode only; doctors assisting and off-duty crew or pilots calming their neighbours are adopted as event content and a negative Unease contagion source, eligible from M1 content; belongings become traits now, and relationships move to AirlineOps; a memory and CPU budget beside MSFS is stated in pillar 6 now, with the voice model's ceiling set at M4 and measured in the sim at M5; SimBrief is a scenario source at M5, and cargo and weight limits move to AirlineOps; GSX waits past M5 and syncs only the door and boarding-start signals; CONCEPT gets one line saying the sim feed port is public and community adapters for other sims are welcome

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
