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
| Sky.Content.Tests | 5 | 37 | 6 | 43 |
| Sky.Engine.Tests | 21 | 206 | 27 | 233 |
| Sky.Scripting.Tests | 4 | 31 | 25 | 56 |
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
| `CrewStrainTests` | 19 | `CrewStrain` with crew.md's first values: a pre-emption adds its step, a second inside the window adds the repeat step; breaks and idle lower strain; the on-task rate rises after an hour without a break and with fatigue; the backlog is capped; only ticks strictly over the redline count; only a full break restarts the no-break clock, short breaks never add up to one, and a zero minimum makes one break tick full; bad inputs are refused; a CsCheck property keeps strain in [0, 100]. |
| `SequenceExecutorTests` | 22 | `SequenceExecutor` runs one action per character: a higher priority interrupts and the old action's cleanup runs once before the new action's first tick; equal or lower priority does not interrupt; a finished action clears without cleanup; a start made during a tick first runs on the next tick whatever the character order, and survives its starter finishing; `Replace` ignores priority, runs cleanup once (inside a tick too, the successor running next tick) and refuses the action already running. A `SyncPoint` releases every member on the tick after the last arrival, a withdrawn member holds it, and bad arguments are refused. |
| `RngRootTests` | 18 | SplitMix64, xoshiro256** and FNV-1a-64 match their published reference vectors; a stream is seeded from the root seed and its name hashed together, so streams are independent (drawing from `crew/0` does not shift `passenger/0`) and a (seed, name) pair cannot stand in for another; `passenger/0`'s first draws under seed 1 are pinned; `NextInt`, `NextDouble` and `Chance` stay in range, consume one draw each, and refuse bad arguments. |
| `ExperienceTests` | 20 | `Experience.Compute`: a 4-minute spike does not set the held peak and a 5-minute one does, at 1 and 240 samples a minute; the end is the last 20 minutes; a short series takes its minimum as its peak; the hold window's bounds and bad inputs are refused; `ExperienceSpread` gives the nearest-rank P10 and median (20 at 10 and 160 at 90 give 10 and 90) without reordering the caller's data; CsCheck properties match the one-pass peak to a brute-force one, ties included. |
| `OutcomeTests` | 28 | Doors lateness (on time, early, the worse of the two); every verdict boundary of `balance.md` 3.4 on its documented side for experience, incidents, strain and doors; no incidents is Smooth; the nearest-rank median time to crew; missed ids in order; strain takes the worst crew member; bad thresholds, inputs and a null incident are refused. |
| `RateMultiplierTests` | 15 | `RateMultiplier.Compose` is CONCEPT's rule, `clamp(1 + Σ_class (Π m − 1), 0.2, 2.5)`: the worked example caps at 2.5, two classes at 0.4 give the 0.2 floor, classes add as deltas, a zero factor zeroes its class even after an overflow (no NaN), and CsCheck properties keep any modifier set, extremes included, in [0.2, 2.5]; every invalid factor or class is refused. |
| `NeedSetTests` | 25 | `NeedSet` over `NeedRates`: Refreshment at 25 an hour is full in 4 sim hours; a +15 pulse over 30 minutes is half landed at 15 and whole at 30; Unease closes half its gap to the baseline in one half-life; Boredom holds while asleep or on IFE; Rest rises awake and falls only asleep, the fall unscaled; the Unease multiplier scales every push (the context's hourly push, held at m × P ÷ k above baseline; `PushUnease`; positive pulse slices) and not relief or the pull, and a positive `Add` on Unease throws; `Add` clamps, `Set` out of range throws, the pending pulse count falls to 0; bad multipliers, settings, values and needs are refused; a CsCheck property keeps every need in [0, 100], NaN caught. |
| `SustainGateTests` | 13 | `SustainGate`: one tick short of the window raises nothing, the full window raises once on its last tick, a below-threshold or ineligible tick resets the run, a value exactly at the threshold counts; a need still failing never raises again; re-arming needs a value strictly below threshold minus margin (exactly at it stays disarmed) whatever its eligibility; NaN and out-of-range settings are refused; a CsCheck property over failing, near-miss and re-arming values keeps raises spaced by a re-arm and checks that some sequence raised twice. |
| `CascadeTests` | 12 | `Cascades` with D3's three rules: Refreshment strictly above 70 adds ×1.1 on Unease; the Bladder rule fires only with the lav unreachable; all three compose by multiplying in one class to 1.1 × 1.2 × 1.3; another target gets none; a buffer sized for what fires is accepted and a shorter one refused; a threshold of 100, a factor of 0, negative or NaN, a rule driving its own need and a null rule are refused. |
| `DistressTests` | 8 | `Distress` with D3's terms: Unease alone at 60 gives 30; needs at or below their thresholds give 0; everything at 100 clamps to 100; a missing, repeated or null term and an out-of-range threshold or weight are refused; a CsCheck property keeps distress in [0, 100] and never lowers it when a need rises. |
| `StageMachineTests` | 12 | `StageMachine`: a feed jump from taxi-out to cruise starts takeoff, climb and cruise in order on one tick; the first advance enters pre-boarding first; a missing or null handler fails at construction naming the stage; an earlier or equal stage starts nothing and the next later one resumes from the current stage; an undefined stage is refused before any handler runs; a handler re-entering `Advance` neither regresses nor double-starts; a throwing handler leaves its stage entered and is not retried; the eleven stages are pinned in order, contiguous from 0. |
| `ManifestGeneratorTests` | 20 | `ManifestGenerator` on the reference layout (built by its internal `ReferenceLayout()`) with passengers.md's shares: one seed gives one manifest and seed 1's is pinned (a change there breaks replays); no seat is booked twice; load factor and business count stay in range over seeds, and a full load seats 180 with 12 in business; everyone sits in their class; a group of three lands in one seat group; a split family leaves no child without an adult it could have had; forbidden trait pairs never meet, children carry `child` and only allowed extras, no child has a sleep kit or a profession; business trips carry no children, retirees or students; families have their adults; wake times fall in range; bad rules and a mismatched layout and graph are refused naming the field. |
| `IdTests` | 2 | `ActivityId` and `TraitId` refuse a negative value. |
| `ForbiddenApiTests` | 2 | `Sky.Engine`'s compiled metadata references no wall clock, unseeded or crypto randomness, threading, IO, network, console, process, environment (beyond `NewLine`), `Guid.NewGuid`/`CreateVersion7` or string hashing API (ADR 0001, R3); members of generic types are checked through their definition. A second test proves the scanner's reach on the test assembly itself: a type, a member and a generic type's member. |

## Sky.Content.Tests

| Class | Tests | Proves |
|---|---|---|
| `ReferenceTests` | 1 | `ReferencesOnlyItsAllowedSkyProjects`: `Sky.Content` references `Sky.Engine` and no other Sky project. |
| `ContentLoaderTests` | 27 | `ContentLoader` over fixture trees written by `ContentTree`: a minimal tree loads into Engine types with ids interned in declaration order; a malformed, unknown, missing or null field fails naming the file and JSON path; enums take only exact member names; omitted optional fields take their defaults; an unknown or duplicate id and a missing module fail naming them; an Engine refusal becomes a `ContentLoadException`; the hash changes with a byte or a rename, ignores listing order, root location and files other than `.json` and `.lua`. |
| `LayoutValidatorTests` | 20 | `LayoutValidator` over the minimal tree edited one rule at a time: an empty id, a zero or negative cabin, aisle, pitch or seat size, an aisle or seat group past a wall, a seat group over an aisle or another group, a duplicate fixture id, a missing door, lav or galley, and a seat with no walk to a fixture are refused at their JSON paths; spans that only touch are accepted, fractional widths included. |
| `ScenarioValidatorTests` | 13 | `ScenarioValidator`: an unknown layout, a gate delay outside 0 to 240 (both bounds accepted), a locked lav that is not a lav, a zone gap or overlap, a zone without crew, a crew id not in the roster, a crew count that does not match the zones, an unknown station and a cart span outside the layout are refused at their JSON paths. |
| `ThoughtCatalogueValidatorTests` | 4 | `ThoughtCatalogueValidator`: an unknown lever tag, neighbour lever tag or hook is refused; the whole minimal tree passes `ContentValidator.Validate`. |

## Sky.Scripting.Tests

| Class | Tests | Proves |
|---|---|---|
| `ReferenceTests` | 1 | `ReferencesOnlyItsAllowedSkyProjects`: `Sky.Scripting` references `Sky.Engine` and no other Sky project. |
| `LuaHostTests` | 38 | `LuaHost`: the sandbox hides `io`, `os`, `require`, `load`, `dofile`, `debug`, `_G`, the metatable and raw functions, `print`, `coroutine`, `string.dump` and `math.randomseed`, while string method syntax works; the budget stops a bare and a `pcall`-wrapped loop and disables only that module; the stack depth is restored after a caught error; an overflow disables its module, rebuilds the state and restarts module-level state; load-time errors, wrong returns and non-function calls disable with a reason taken from the error value; a host exception escapes; unknown ids throw; `string.rep` is capped in both forms; `math.random` matches the `SimRandom` draws, rejects non-integer bounds and is unavailable while a module loads; modules share no globals; `Run` re-entered throws. |
| `EventScriptsTests` | 73 | `LuaBehaviorScripts`' event half over inline modules: a trigger fires only in its phase; choices come back with their fields and need a crew-free `leave`; effects come back as the five consequence kinds at their tick offsets, half a tick rounding away from zero; `ctx` read after the trigger, a throwing `effects`, a malformed load, choice, effect or trigger result, an unknown need or trait name each disable the module with a reason naming the field, and another module still fires; a disabled event reports why; trigger and effects draw only from their own stream; a released handle, an unoffered choice, an unrenderable context and a bad event id throw. |
| `ActivityScoringTests` | 60 | `LuaBehaviorScripts`: a thirsty passenger scores `drink` above `sleep`; a module that throws scores 0 and is disabled while the others score, and is not called again; a negative, NaN, infinite, nil, string or numeric-string result scores 0 and disables with the value in the reason; a module without `utility` or failing to load scores 0; a `math.random` draw disables even with a stream set; every Lua member reaches Lua (one row each, every stage and turbulence id from the enums); a saved `facts` reads the passenger being scored; facts that cannot be rendered, mismatched spans, unknown candidates, blank, repeated or null ids and names are refused before any score is written. |

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
