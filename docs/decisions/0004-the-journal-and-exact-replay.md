# 0004. The journal and exact replay

- **Date:** 2026-09-26
- **Status:** Accepted
- **Source:** [rewrite decisions](../plans/2026-09-26-rewrite-decisions.md) §1 Engine (state and time, language model), §1 Scope (M1)

## Context

The world advances in fixed ticks from one `IClockSource` ([0002](./0002-fixed-tick-and-iclocksource.md)), and all simulation randomness comes from one seeded root ([0003](./0003-one-seeded-rng-root-and-a-display-rng.md)). What remains to reproduce a flight is its inputs.

## Decision

The journal records how many ticks each frame ran, plus every command and observation. A replay from seed and journal is exact.

## Consequences

- M1 includes replay from seed and journal, and invariant fuzzing over hundreds of seeds.
- M1 has no player commands; crew make event choices on their own. From M3, a player's choice is a journaled command that takes precedence.
- The bundled small model changes only words, so a replay may word things differently while the simulation replays exactly ([0008](./0008-a-bundled-on-device-small-model-text-only.md)).
