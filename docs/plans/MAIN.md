# Main Plan
<!-- plan-doc-hygiene: 2026-09-26 f38edb6 -->

Open work only, in working order: the next item is the first line from the top. One line an item: the action, the files, who asked and when. Designs and decisions live in `docs/design/` and `docs/decisions/` once they exist; until then, the kickoff decisions are [2026-09-26-rewrite-decisions.md](./2026-09-26-rewrite-decisions.md). A landed line moves, ticked with its landing note, to `archive/YYYY-MM-done.md`. A fresh session reads the root `CLAUDE.md` first.

## Now: M0, the concept pass and the repo skeleton

Every M0 wave has landed; only the push is left.

- [ ] Push M0 to `origin/main` (the public repo `leftos/in-the-sky` holds only the kickoff commit); the owner's go is asked first, since a push is public. The first CI run on that push is the first time the workflow runs on `ubuntu-24.04`, so a red run there is the next item.

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
