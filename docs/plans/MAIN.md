# Main Plan
<!-- plan-doc-hygiene: 2026-09-27 bbe992c -->

Open work only, in working order: the next item is the first line from the top. One line an item: the action, the files, who asked and when. Designs and decisions live in `docs/design/` and `docs/decisions/` once they exist; until then, the kickoff decisions are [2026-09-26-rewrite-decisions.md](./2026-09-26-rewrite-decisions.md). A landed line moves, ticked with its landing note, to `archive/YYYY-MM-done.md`. A fresh session reads the root `CLAUDE.md` first.

## Now: M1, the headless cabin flight

The plan is [2026-09-26-m1-headless-cabin-flight.md](./2026-09-26-m1-headless-cabin-flight.md): each step with its files, its proving command and its landing note, the acceptance map against `docs/design/CONCEPT.md` section 7, the rulings and the owner's decisions. One line below per wave still open, in working order; a wave's steps inside the subplan are ticked as they land, and the count here moves in the same commit.

**Progress: 36 of 62 steps landed.** Finished waves: M1-D design docs (4), M1-A engine foundations (3), M1-B cabin geometry (4), M1-C needs (5), M1-E executor and crew primitives (4), M1-S scripting (4), M1-K content loader (4). Acceptance (wave M1-Z): none proved yet.

- [ ] **M1-F the flight**, 5 of 10 landed. Open: F3b's content mapping of the movement numbers (H1's), F4 (passenger decisions, split into F4a and F4b; explored in [the slice explorations](./2026-09-27-m1-slice-explorations.md), waiting on balance-analyst's keep-current bias and game-designer's unreachable-lav rule), F5 (crew and observations), F6 (events, auto-resolve, incidents), F7 (invariant checker), F8 (passenger thoughts). Shared: `src/Sky.Engine/{Flight,Journal,Manifest,Passengers,Observation,Events,Invariants}/`, `tests/Sky.Engine.Tests/`. Gate: `code-review` and `oracle`; proved by `pwsh sky.ps1 test -Project Engine`.
- [ ] **M1-G scoring**, 2 of 3 landed. Open: G3 (moments and the flight's score; beside F). Shared: `src/Sky.Engine/Scoring/`. Gate: `code-review` and `oracle`; proved by `pwsh sky.ps1 test -Project Engine`.
- [ ] **M1-X content data**, 1 of 4 landed. Open: X2 (crew and scenarios, a covering variant; explored, ready to brief), X3 (`activities.json` and the activity modules; explored, waiting on the `call_crew` shape and three trait bonuses), X4 (event modules, event-writer; explored, ready to brief, test first), X4's trigger-direction check (event-writer), and X1's review observations (a line in the subplan). The maps are in [the slice explorations](./2026-09-27-m1-slice-explorations.md). Shared: `src/Sky.Content/Data/`. Gate: the owning design doc's agent; proved by `ShippedContentTests`, `ShippedActivityTests`, `ShippedEventTests`.
- [ ] **M1-H the session**, 0 of 5 landed. Open: H1 (composing and running a flight), H2 (journal, replay, state hash), H3 (the report), H4 (true and observed views), H5 (system switches). Shared: `src/Sky.Session/`, `src/Sky.Engine/State/`, `tests/Sky.Session.Tests/`. Gate: `code-review`, and `oracle` on H2's Engine hash; proved by `pwsh sky.ps1 test -Project Session`.
- [ ] **M1-I the headless runner**, 0 of 3 landed. Open: I1 (the command line), I2 (sweeps, fuzz, balance CSV), I3 (`sky.ps1 sim`). Shared: `src/Sky.Sim/`, `sky.ps1`, `tests/Sky.Sim.Tests/`. Gate: `code-review`; proved by `pwsh sky.ps1 test -Project Sim`, then `pwsh sky.ps1 sim run --scenario reference --seed 1`.
- [ ] **M1-Z acceptance**, 0 of 9 landed. Open: Z1 to Z5 (fuzz, replay and golden flight, 64x performance, report citations, events), Z6 and Z6b (balance-analyst's sweeps), Z7 (tuning), Z8 (close). Shared: `tests/Sky.Session.Tests/Acceptance/`, `src/Sky.Content/Data/`, `docs/design/balance.md`. Gate: balance-analyst's runs logged in `balance.md`; proved by `pwsh tools/test-all.ps1`, and by the owner reading a printed report (`pwsh sky.ps1 sim run --scenario reference --seed 1`).

## Later milestones

- [ ] M2: Godot client. A 2D cabin view drawing Session views, a dev inspector showing decision scores, scratch scenes, the art-direction decision
- [ ] M3: player HUD and interventions. Stage-manager surfaces, alerts, event choice UI, crew staffing as policy, twin-aisle widebody
- [ ] M4: voice. The ported TTS and subtitle stack, the bundled small model, announcements
- [ ] M5: MSFS. The SimConnect clock and feed, wired in and tested in the sim
- [ ] M6: layout editor and content authoring loop

## Backlog

- [ ] Review the heavy/light gate slot picks. `tools/gate.ps1` now requires `-Slot heavy|light`, and every call site here (`sky.ps1`, `tools/test-all.ps1`, the docs that quote the usage) was given a kind from outside this repo's agents, as a preliminary pick so the gates kept running. Check each kind against what the command really does (does it fan out across cores, or keep one or two threads busy for its whole run?), measure where unsure, and correct any that are wrong. The pool sizes (`%LOCALAPPDATA%\gate\slot-counts.json`, set from the gate dashboard) belong to the machine-wide gate in `~/.claude/tools/gate/`, not to this repo.
- [ ] Review and land Dependabot PR #1 (uv-build `>=0.12.17,<0.13.0` in `tools/provenance/pyproject.toml`): checks pass (lint, build-test, analysis, GitGuardian); GitHub reports mergeable as unknown, so re-check before merging.
- [ ] AirlineOps: crew auto-resolve for many flights at once, the booking market as the manifest source
- [ ] Saves and progression
- [ ] `docs/ARCHITECTURE.md` lacks two sections of the user-level architecture entry point (`~/.claude/docs/templates/ARCHITECTURE.md`): Integration Footguns (the couplings the ADRs and CLAUDE.md's Non-negotiables state) and Test locations (a pointer to `docs/TEST_ALMANAC.md`).
