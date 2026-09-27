# 0002. A fixed tick driven by one IClockSource

- **Date:** 2026-09-26
- **Status:** Accepted
- **Source:** [rewrite decisions](../plans/2026-09-26-rewrite-decisions.md) §1 Engine (state and time, flights), §3 What to avoid (time that depends on the frame and the machine), §4 Open questions (tick size, MSFS clock edge cases)

## Context

OpenPax's time depends on the frame and the machine. It multiplies a variable delta by the sim rate (`Flight.cs:418`). About 54 accumulators reset to zero and drop their overshoot. Thought decay uses the wall clock (`Thought.cs:23-33`), stage timestamps use `DateTime.UtcNow`, and how much work a frame does depends on a Stopwatch budget (`FlightStageHandler.cs:36-47`).

## Decision

- The world is mutable C# objects advanced by fixed-size ticks.
- One `IClockSource` says how much sim time has passed. In MSFS mode, MSFS's clock drives it, so MSFS pause and time acceleration are inputs. In standalone mode the emulator and the player's time warp drive it.
- Flights run at their real length in sim time, with need rates per sim hour.
- The engine must sustain 64x at full cabin. With 250 ms ticks that is 256 ticks per second, pinned by a performance test.

## Open

The tick size is not settled. 250 ms is the working figure; the M0 Lua boundary and tick spike confirms it against the 64x budget and sets it.

## Consequences

- Simulation time never reads the frame delta, the wall clock or a Stopwatch budget.
- The per-second tick count in the performance test follows from whatever tick size the spike sets.
- MSFS clock edge cases (active pause, slew, a sim rate change mid-tick) are open until M5.
