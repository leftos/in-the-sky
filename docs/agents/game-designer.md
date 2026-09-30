# game-designer overlay: In the Sky

Read by the user-level `game-designer` agent before anything else; it adds to what it does and wins where it is more specific.

## What you own and read

- You own `docs/design/CONCEPT.md` and every design doc under `docs/design/` no other agent owns; you write `.md` files under `docs/design/` only.
- The frame (the player is a stage manager) and the six pillars are `docs/design/CONCEPT.md` section 2: read them there and cite a pillar by number in a review. The vocabulary (needs, levers, moments, auto-resolve, the task board, the crew-observed view, the four outcomes) is the glossary in `docs/README.md`: use it as defined there.
- The rulings are CONCEPT section 8 and the kickoff decisions `docs/plans/2026-09-26-rewrite-decisions.md`.
- The neighbouring owners' docs: `docs/design/balance.md` (`balance-analyst`), `docs/design/events.md` (`event-writer`), `docs/design/art-direction.md` (`art-director`).

## The client and the sim

- The client is `src/Sky.Client`, from M2; a player-facing screen can be looked at only from then. The sim is `Sky.Sim`: a spec's success criteria are measurable there where possible.
- A lever holds a fixed default from the scenario file in M1 and is set by the player from M3; M1 has no player commands, so every event is auto-resolved.

## Pushing back, in this project's terms

> "I'd push back on this. A per-passenger seat picker is direct control, and it breaks pillar 2: on a full flight it is 180 rows with a control on each. What if the conflict surfaces as an event instead, and the report attributes what the swap caused?"

Genre tourism here is a mechanic that imports RimWorld's direct override of a colonist: the agency is in staging the conditions, not in moving the actor.
