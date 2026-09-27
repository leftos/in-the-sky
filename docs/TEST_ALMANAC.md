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
| Sky.Engine.Tests | 4 | 18 | 1 | 19 |
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
| `RateMultiplierTests` | 15 | `RateMultiplier.Compose` is CONCEPT's rule, `clamp(1 + Σ_class (Π m − 1), 0.2, 2.5)`: the worked example caps at 2.5, two classes at 0.4 give the 0.2 floor, classes add as deltas, a zero factor zeroes its class even after an overflow (no NaN), and CsCheck properties keep any modifier set, extremes included, in [0.2, 2.5]; every invalid factor or class is refused. |
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
