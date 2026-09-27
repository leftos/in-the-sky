# 0010. Lua-CSharp, Lua at decision points only, and 250 ms ticks

- **Date:** 2026-09-26
- **Status:** Accepted
- **Source:** [Lua runtime research](../research/2026-09-26-lua-runtime.md); [Lua boundary and tick spike](../research/2026-09-26-lua-boundary-spike/README.md); settles the open parts of [0001](./0001-the-engine-references-nothing.md), [0002](./0002-fixed-tick-and-iclocksource.md) and [0006](./0006-utility-scoring-and-a-task-board-on-one-executor.md)

## Context

The kickoff left three things to the M0 spike: which Lua runtime `Sky.Scripting` hosts, whether Lua may run only at decision points or also every tick, and the tick size. The research ranked Lua-CSharp and MoonSharp first and second. The spike ran 200 passengers with 10 activities over a 2.5-hour flight on this machine (Windows), with the numbers in its results page.

## Decision

- `Sky.Scripting` hosts **Lua-CSharp** (NuGet `LuaCSharp`, Lua 5.2 semantics, pure C#).
- **Lua runs only at decision points**: when a character chooses its next activity, and when an event's `trigger`, `describe`, `choices` or `effects` is evaluated. Per-tick work (need rates, the rate rule, movement) is C#.
- **The tick stays at 250 ms**, so 64x is 256 ticks per wall-clock second.
- Every Lua call runs under an instruction budget (Lua-CSharp's count hook). A definition that exceeds the budget or throws is disabled for that flight, per the kept OpenPax rule.
- After catching a script error, the host restores the Lua stack to its depth before the call. A state that overflowed its stack is discarded, and that definition is disabled for the flight.
- `math.random` is replaced by a host function drawing from the character's named stream (ADR 0003). Scripts never receive the global table.

## Measured basis

- Decision-only Lua-CSharp ran at 156,083 ticks/s (610x the 64x budget); with the budget on, 134,275 ticks/s (524x).
- Per-tick Lua-CSharp ran at 980 ticks/s (3.83x), too thin for a widebody cabin or heavier scripts. Per-tick MoonSharp ran at 272 ticks/s (1.06x), and at 38 ticks/s with a budget.
- One utility call costs 619 ns and 0 B on Lua-CSharp, against 859 ns and 2,112 B on MoonSharp. Lua-CSharp allocates 0 B per tick in both modes.
- Lua-CSharp shows 0 trim warnings and runs under NativeAOT; MoonSharp shows 23 and fails under NativeAOT.
- All nine variants gave identical end-state hashes across two runs and a fresh process, equal to the C# baseline, on Windows.
- Both runtimes load and return the expected value inside a headless Godot 4.7.2 run of `Sky.Client`.

## Open

- Byte-identical replay on Linux, and `pairs` order across Windows and Linux, are unmeasured. The M1 replay test runs in CI on Linux and settles them.
- Loading from an exported Windows build is unmeasured; M2 checks it with the first export.
- Seeded `System.Random` output across .NET major versions is not checked here; the journal format should not rely on it staying stable across a runtime upgrade.

## Consequences

- `Sky.Engine` still references nothing; the Lua package is referenced by `Sky.Scripting` only.
- Content has no integers or bitwise operators (Lua 5.2 doubles). The spike's double arithmetic matched C# bit for bit.
- KeraLua and MoonSharp are not used. The spike code stays on the `spike/lua-boundary` branch for reference.
