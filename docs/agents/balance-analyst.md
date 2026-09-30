# balance-analyst overlay: In the Sky

Read by the user-level `balance-analyst` agent before a run; it adds to what it does and wins where it is more specific.

## The sim

- The sim is `Sky.Sim`; its command, switches and ceiling are in `docs/DEVELOPMENT.md`. Its balance CSV has one row per seed with the four outcome measures, and lands at `.tmp/sim/<name>.csv`. Builds and tests are `pwsh sky.ps1 build` and `pwsh sky.ps1 test -Project <P>`; the tests that pin a number are under `tests/Sky.<X>.Tests`.
- `Sky.Sim` arrives in M1. Until it runs there is nothing to measure: review numeric diffs and design numbers by arithmetic against the targets, and mark every figure you give as computed, never measured.

## The targets

Seed `balance.md` from `docs/design/CONCEPT.md`: section 4's rates, floor, cap and sustain window; section 6's measures; section 7's acceptance numbers (the baseline quiet on at least 90% of seeds, the lever checks over 200 seeds each, the 500-seed fuzz, 64x at full cabin). A run can check an M1 acceptance number.

## Reviewing a numeric diff

- A rate's effect is stated in sim time on the reference flight.
- A modifier: which source class it sits in, how it composes under the rate-multiplier rule, and whether any trait, phase or cascade combination now hits the 0.2x floor or the 2.5x cap.
- An event chance or a crew count: its effect on missed incidents and on the most-strained crew member's peak.
- Name which of the four outcomes (CONCEPT section 6) each change moves.
