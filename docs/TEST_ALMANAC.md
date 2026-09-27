# Test Almanac

Every test class in the repo, what it proves, and where a new test goes. The doc-drift pre-commit hook fails when a `tests/Sky.*.Tests` project or a `*Tests` class is not named in this file in backticks (a project may be named by its name or its `tests/` path); its `-Update` switch rewrites the counts table below from the tree and changes nothing else.

## How to run

- `pwsh ./sky.ps1 test` runs every test project.
- `pwsh ./sky.ps1 test -Project Engine` runs one project.
- `pwsh ./sky.ps1 test -Project Engine -Filter "*ReferenceTests"` runs one class.
- `pwsh tools/test-all.ps1` builds once and runs every check, ending on one verdict.

## Counts

| Project | Classes | `[Fact]` | `[Theory]` | Total |
|---|---|---|---|---|
| Sky.Client.Tests | 1 | 1 | 0 | 1 |
| Sky.Content.Tests | 1 | 1 | 0 | 1 |
| Sky.Engine.Tests | 13 | 114 | 18 | 132 |
| Sky.Scripting.Tests | 1 | 1 | 0 | 1 |
| Sky.Session.Tests | 1 | 1 | 0 | 1 |
| Sky.Sim.Tests | 1 | 1 | 0 | 1 |
| Sky.SimConnect.Tests | 1 | 1 | 0 | 1 |
| Sky.Voice.Tests | 1 | 1 | 0 | 1 |

Every class below reads the referenced assembly names from its project's compiled `AssemblyMarker` assembly and compares the `Sky.*` ones, sorted, with the edges `docs/ARCHITECTURE.md` allows.

## Sky.Engine.Tests

| Class | Tests | Proves |
|---|---|---|
| `ReferenceTests` | 1 | `ReferencesNoOtherProject`: `Sky.Engine` references no Sky project, and no assembly whose name starts with `Godot`, `MoonSharp`, `NLua` or `KeraLua`. |
| `TickAccumulatorTests` | 4 | `TickAccumulator` turns elapsed sim milliseconds into whole 250 ms ticks and carries the remainder across calls (a CsCheck property: ticks over any split of a duration equal the ticks of the whole); negative input is refused. |
| `NavGraphBuilderTests` | 11 | `NavGraphBuilder` turns a real-unit layout (a 2-2 row ahead of a 3-3 row) into the nav graph: every seat reaches the door, lav and galley; a window seat reaches the aisle only through the middle and aisle seats; an end seat links only to the nearest aisle with no group between; edge ticks round up from inches without float noise and never fall below 1; bad fixtures, empty seat groups and sub-1-tick links are refused. |
| `OccupancyTests` | 25 | `Occupancy` over the two-row test layout: a full seat refuses a reservation; a person squeezes into a full aisle slot and a third is refused, and the squeeze is free again after a release; a cart blocks a squeeze, cannot enter an occupied slot and is refused on seats, lavs and lav queues; `Holders` lists reservation order; arrive and release errors name the node and character; a CsCheck property over random reserve, arrive and release never exceeds capacity plus the squeeze and keeps a cart alone. |
| `LayoutAsciiTests` | 3 | `LayoutAscii.Render` pins the two-row layout's dump, renders a 2-3-2 row's two aisles in lateral order, and refuses a fixture on a missing row as the builder does. |
| `PathTableTests` | 4 | `PathTable` walks next hops from every node to every target at the tabled cost, equal-cost ties go to the lower node id, two builds are identical, and an unreachable target reports -1. |
| `TaskBoardTests` | 20 | `TaskBoard` through a real `SequenceExecutor`: claims go highest claim priority, oldest, lowest id, within `mayTake`; a call button does not break a cart or business hand service, a secure check breaks a cart (cleanup once, the cart back with its posted tick), a call button breaks a galley break, a due break claims ahead of a cart and yields to it; a second claim on one task finds nothing; `Withdraw`, wait ticks and idle-wait accrual; pre-empting a finished or swapped-out action, nested board calls and duplicate posts throw. |
| `CrewStrainTests` | 19 | `CrewStrain` with crew.md's first values: a pre-emption adds its step, a second inside the window adds the repeat step; breaks and idle lower strain; the on-task rate rises after an hour without a break and with fatigue; the backlog is capped; only ticks strictly over the redline count; bad inputs are refused; a CsCheck property keeps strain in [0, 100]. |
| `SequenceExecutorTests` | 22 | `SequenceExecutor` runs one action per character: a higher priority interrupts and the old action's cleanup runs once before the new action's first tick; equal or lower priority does not interrupt; a finished action clears without cleanup; a start made during a tick first runs on the next tick whatever the character order, and survives its starter finishing; `Replace` ignores priority, runs cleanup once (inside a tick too, the successor running next tick) and refuses the action already running. A `SyncPoint` releases every member on the tick after the last arrival, a withdrawn member holds it, and bad arguments are refused. |
| `RngRootTests` | 18 | SplitMix64, xoshiro256** and FNV-1a-64 match their published reference vectors; a stream is seeded from the root seed and its name hashed together, so streams are independent (drawing from `crew/0` does not shift `passenger/0`) and a (seed, name) pair cannot stand in for another; `passenger/0`'s first draws under seed 1 are pinned; `NextInt`, `NextDouble` and `Chance` stay in range, consume one draw each, and refuse bad arguments. |
| `RateMultiplierTests` | 15 | `RateMultiplier.Compose` is CONCEPT's rule, `clamp(1 + Σ_class (Π m − 1), 0.2, 2.5)`: the worked example caps at 2.5, two classes at 0.4 give the 0.2 floor, classes add as deltas, a zero factor zeroes its class even after an overflow (no NaN), and CsCheck properties keep any modifier set, extremes included, in [0.2, 2.5]; every invalid factor or class is refused. |
| `NeedSetTests` | 25 | `NeedSet` over `NeedRates`: Refreshment at 25 an hour is full in 4 sim hours; a +15 pulse over 30 minutes is half landed at 15 and whole at 30; Unease closes half its gap to the baseline in one half-life; Boredom holds while asleep or on IFE; Rest rises awake and falls only asleep; multipliers scale the base changes and not the Unease pull; `Add` clamps, `Set` out of range throws, the pending pulse count falls to 0; bad multipliers, settings, values and needs are refused; a CsCheck property keeps every need in [0, 100], NaN caught. |
| `ForbiddenApiTests` | 2 | `Sky.Engine`'s compiled metadata references no wall clock, unseeded or crypto randomness, threading, IO, network, console, process, environment (beyond `NewLine`), `Guid.NewGuid`/`CreateVersion7` or string hashing API (ADR 0001, R3); members of generic types are checked through their definition. A second test proves the scanner's reach on the test assembly itself: a type, a member and a generic type's member. |

## Sky.Content.Tests

| Class | Tests | Proves |
|---|---|---|
| `ReferenceTests` | 1 | `ReferencesOnlyItsAllowedSkyProjects`: `Sky.Content` references `Sky.Engine` and no other Sky project. |

## Sky.Scripting.Tests

| Class | Tests | Proves |
|---|---|---|
| `ReferenceTests` | 1 | `ReferencesOnlyItsAllowedSkyProjects`: `Sky.Scripting` references `Sky.Engine` and no other Sky project. |

## Sky.Session.Tests

| Class | Tests | Proves |
|---|---|---|
| `ReferenceTests` | 1 | `ReferencesOnlyItsAllowedSkyProjects`: `Sky.Session` references `Sky.Content`, `Sky.Engine` and `Sky.Scripting` and no other Sky project. |

## Sky.Sim.Tests

| Class | Tests | Proves |
|---|---|---|
| `ReferenceTests` | 1 | `ReferencesOnlyItsAllowedSkyProjects`: `Sky.Sim` references `Sky.Session` and no other Sky project. |

## Sky.SimConnect.Tests

| Class | Tests | Proves |
|---|---|---|
| `ReferenceTests` | 1 | `ReferencesOnlyItsAllowedSkyProjects`: `Sky.SimConnect` references `Sky.Engine` and no other Sky project. |

## Sky.Voice.Tests

| Class | Tests | Proves |
|---|---|---|
| `ReferenceTests` | 1 | `ReferencesOnlyItsAllowedSkyProjects`: `Sky.Voice` references `Sky.Engine` and no other Sky project. |

## Sky.Client.Tests

| Class | Tests | Proves |
|---|---|---|
| `ReferenceTests` | 1 | `ReferencesOnlyItsAllowedSkyProjects`: `Sky.Client` references `Sky.Session` and no other Sky project, and `GodotSharp` is not among the test process's loaded assemblies. |

## Planned

None of these exists yet; each is an item of M1's definition of done (`docs/design/CONCEPT.md` section 7).

- Planned: invariant fuzzing over 500 seeds, with no exception and no invariant failure (needs in 0 to 100, rate multipliers between the floor and 2.5, node capacity and reservations never exceeded, and the rest of section 7's list).
- Planned: replay equality: replaying a seed and its journal gives the same end-state hash and the same report text.
- Planned: a performance test pinning 64x at full cabin: 256 ticks per wall-clock second at 250 ms ticks, or at the tick size the spike sets.

## Conventions

- Tests use xunit.v3 on Microsoft.Testing.Platform (`UseMicrosoftTestingPlatformRunner`); package versions are in `Directory.Packages.props`.
- Property tests use CsCheck, which `Sky.Engine.Tests` alone references today.
- Test method names are PascalCase sentences with no underscores (CA1707 refuses them).
- Tests pin behaviour, not implementation: a refactor that keeps behaviour keeps every test green.

## Where a new test goes

| Project under test | Test project |
|---|---|
| `Sky.Engine` | `tests/Sky.Engine.Tests` |
| `Sky.Content` | `tests/Sky.Content.Tests` |
| `Sky.Scripting` | `tests/Sky.Scripting.Tests` |
| `Sky.Session` | `tests/Sky.Session.Tests` |
| `Sky.Sim` | `tests/Sky.Sim.Tests` |
| `Sky.SimConnect` | `tests/Sky.SimConnect.Tests` |
| `Sky.Voice` | `tests/Sky.Voice.Tests` |
| `Sky.Client` | `tests/Sky.Client.Tests` |
