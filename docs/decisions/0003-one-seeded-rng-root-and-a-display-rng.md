# 0003. One seeded RNG root with named streams, and a separate display RNG

- **Date:** 2026-09-26
- **Status:** Accepted
- **Source:** [rewrite decisions](./rewrite-decisions.md) §3 What to avoid (unseeded or shared randomness), §2 What to keep (per-entity RNG streams), §1 Engine (language model)

## Context

OpenPax's randomness is unseeded or shared. `FlightEventManager.cs:31`, `AircraftScene.cs:38` and `FlightEmulator.cs:15` create unseeded `Random`s, and the UI draws from the sim stream (`Character.cs:141`). The idea of per-entity RNG streams (`Code/Characters/Person.cs:88`) is kept.

## Decision

- One seeded root hands out named streams for all simulation randomness.
- Anything drawn only for display uses a separate display RNG, never the sim streams.
- The bundled small model's output never touches the sim RNG ([0008](./0008-a-bundled-on-device-small-model-text-only.md)).

## Consequences

- A flight is reproducible from its seed and journal ([0004](./0004-the-journal-and-exact-replay.md)).
- No simulation code creates an unseeded `Random`.
- The client's drawing cannot shift the simulation's outcomes.
