# Main Plan
<!-- plan-doc-hygiene: 2026-09-26 67fedc5 -->

Open work only, in working order: the next item is the first line from the top. One line an item: the action, the files, who asked and when. Designs and decisions live in `docs/design/` and `docs/decisions/` once they exist; until then, the kickoff decisions are [2026-09-26-rewrite-decisions.md](./2026-09-26-rewrite-decisions.md). A landed line moves, ticked with its landing note, to `archive/YYYY-MM-done.md`. A fresh session reads the root `CLAUDE.md` first.

## Now: M1, the headless cabin flight

- [ ] Reorganize this index so it gives a sense of detailed progress, within the `plan-doc-hygiene` rules (owner, 2026-09-27)

- [ ] M1 — see [2026-09-26-m1-headless-cabin-flight.md](./2026-09-26-m1-headless-cabin-flight.md): its acceptance list `docs/design/CONCEPT.md` section 7, the owner's six decisions ruled, and the triage's additions (new step F8, ruling R35). Landed: A1 to A3, B1 to B4, C1, C2, C2b, C3, C3a, D1 to D4, E1 to E4, F1, G1, G2, K1, S1, S1b, S2. Startable next: F2, K2, S3.

## Later milestones

- [ ] M2: Godot client. A 2D cabin view drawing Session views, a dev inspector showing decision scores, scratch scenes, the art-direction decision
- [ ] M3: player HUD and interventions. Stage-manager surfaces, alerts, event choice UI, crew staffing as policy, twin-aisle widebody
- [ ] M4: voice. The ported TTS and subtitle stack, the bundled small model, announcements
- [ ] M5: MSFS. The SimConnect clock and feed, wired in and tested in the sim
- [ ] M6: layout editor and content authoring loop

## Backlog

- [ ] AirlineOps: crew auto-resolve for many flights at once, the booking market as the manifest source
- [ ] Saves and progression
