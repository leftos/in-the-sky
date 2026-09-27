# Architecture

The projects as built, what each may reference, and where a change goes. Read this before the source. The decisions the code cannot show are under `docs/decisions/`; terms are in the glossary in `docs/README.md`. The doc-drift pre-commit hook (`tools/hooks/Test-DocDrift.ps1`) fails when a `src/Sky.*` project is not named in this file in backticks.

## Projects and dependency edges

Eight projects under `src/`, the set ADR 0001 lays out. Each has a test twin under `tests/`, whose `ReferenceTests` pins the edges below from the compiled assembly's own metadata.

| Project | Holds | References |
|---|---|---|
| `Sky.Engine` | World, fixed tick, needs, utility scoring, task board, executor, journal; defines the ports `IClockSource`, `ISimFeed` and `IBehaviorScripts`. | nothing (no Sky project, no Godot, no Lua runtime) |
| `Sky.Content` | Schemas, loader, validator. | `Sky.Engine` |
| `Sky.Scripting` | The Lua host, implementing the Engine's ports. | `Sky.Engine` |
| `Sky.Session` | `ISkySession`: the client pulls views from it and pushes commands to it. | `Sky.Engine`, `Sky.Content`, `Sky.Scripting` |
| `Sky.Sim` | Headless runner, seed sweeps, balance CSV output. | `Sky.Session` |
| `Sky.SimConnect` | The MSFS adapter (Windows-only). | `Sky.Engine` |
| `Sky.Voice` | TTS, the bundled small model, subtitles. | `Sky.Engine` |
| `Sky.Client` | Godot: draws views, holds no rules. | `Sky.Session` |

A new edge needs two things in the referencing project: a `ProjectReference` in its csproj, and a `typeof(<Referenced>.AssemblyMarker)` entry in its `AssemblyMarker.Dependencies`. Without the `typeof` the compiler drops a reference nothing uses from the assembly's metadata, and `ReferenceTests` cannot see the edge. The test's `expected` array then changes with it, and so does this table.

## Task index: I need to change X, which files?

| Change | Files, in order | Section |
|---|---|---|
| Add a project reference | the referencing project's `src/Sky.<Name>/Sky.<Name>.csproj` (`ProjectReference`), its `AssemblyMarker.cs` (`Dependencies`), `tests/Sky.<Name>.Tests/ReferenceTests.cs` (`expected`), this file's table | Projects and dependency edges |
| Add a src project and its twin | `src/Sky.<Name>/Sky.<Name>.csproj` (`InternalsVisibleTo` its twin), `src/Sky.<Name>/AssemblyMarker.cs`, `tests/Sky.<Name>.Tests/Sky.<Name>.Tests.csproj`, `tests/Sky.<Name>.Tests/ReferenceTests.cs`, `InTheSky.slnx`, this file's table and a `###` under Layers, `docs/TEST_ALMANAC.md` (a section, a row in "Where a new test goes", then the hook's `-Update` for the counts); the project set is ADR 0001's, so a project outside it is a new ADR first | Layers |

## Layers

Every project has an `AssemblyMarker` class that names the assembly and holds one `typeof` per referenced project. Only the Engine holds more so far (M1 in progress).

### Engine: `src/Sky.Engine`

A plain `net10.0` class library with no project or package references; its `AssemblyMarker` is empty. ADR 0001 governs its isolation; 0002 (fixed tick and `IClockSource`), 0003 (the RNG root and named streams), 0004 (the journal), 0005 (the nav graph) and 0006 (utility scoring, task board, sequence executor) govern what it will hold. Built so far, one folder each:

- `Ports/`: the interfaces other projects implement; `IClockSource` hands the Engine elapsed sim milliseconds, and `ISimFeed` reports a `FeedObservation` (stage, seatbelt sign, turbulence) at a tick; `IBehaviorScripts.ScoreActivities` scores a passenger's candidate activities from a `PassengerFacts` (needs, traits, stage, sign, turbulence, current activity and the seat-level context `passengers.md` section 3 reads), 0 meaning unavailable.
- `Passengers/`: `ActivityId` and `TraitId`, content ids interned to integers at load (R9).
- `Flight/`: `FlightStage` (the eleven stages in flight order), `Turbulence` (none, light, moderate) and `StageMachine`, which takes one `IStageHandler` per stage (checked complete at construction), enters pre-boarding on the first `Advance`, then every stage up to the one the feed reports, calling each handler's `Start` once with the tick; it refuses an undefined stage, ignores an earlier one, and derives each step from `Current`, so a handler that throws leaves its stage entered and a handler that re-enters `Advance` cannot move it backwards (R17).
- `Cabin/`: `CabinLayout` (rows of seat groups, aisles and fixtures in inches), `NavGraphBuilder` (the derived graph of aisle slots, seats, doors, lavs, lav queues and galleys, with edge costs in ticks) and `PathTable` (next hop and cost from every node to every node, Dijkstra per target, ties to the lower id). Nothing after the builder reads inches (ADR 0005). `Occupancy` is the capacity and legality table over the graph's nodes, not a mover: capacities per node kind come from `NodeCapacities`, a reservation and an arrival both count, a person may squeeze one over capacity into a full aisle slot with no cart, a cart takes only an empty aisle slot, galley or door and then admits nobody, and `Holders` lists who is on a node (R31). `LayoutAscii.Render` dumps a layout one line per row for debugging (R32).
- `Execution/`: `SequenceExecutor` (one `CharacterAction` per character, priority interrupts with cleanup on interrupt, starts made during a tick run from the next one; `Replace` swaps an action with no priority check, for the task board) and `SyncPoint` (named waits that release every member together) (ADR 0006).
- `Crew/`: `CrewTask` (a kind, a claim and a hold priority, a zone, a posted tick) and `TaskBoard`, which arbitrates crew work: an idle crew member claims the highest claim priority, then the oldest, then the lowest id, among the tasks the caller's `mayTake` allows; a task pre-empts a held one only when its claim is strictly above the held task's hold, through `SequenceExecutor.Replace`, and the bumped task returns with its posted tick; `Withdraw` takes an open task off; `LongestWaitWhileIdle` feeds the task-wait invariant (R34). `CrewStrain` accumulates one crew member's strain from `StrainSettings` (time on task scaled by fatigue and by an hour without a break, pre-emptions, backlog, severe incidents, idle and break decay) and counts ticks over the redline; only a full break, one that runs its minimum, restarts the no-break clock.
- `Scoring/`: the outcome calculators, pure functions with no flight wiring yet (R36). `Experience.Compute` scores one passenger from a distress series sampled at a stated rate (the held peak, the end and the mean, CONCEPT section 6), and `ExperienceSpread.From` gives the cabin's nearest-rank 10th percentile, median and count under 40; `IncidentOutcome`, `StrainOutcome` and `DoorsOutcome` are built by `From` over records; each outcome's `VerdictFor` takes its own thresholds record from content and gives Smooth, Rough or Bad.
- `Needs/`: `SourceClass`, `RateModifier` and `RateMultiplier.Compose`, the rate rule of CONCEPT section 4 with its 0.2 floor and 2.5 cap; `Need` (the five), `NeedRates` (the flight's hourly rates as per-tick changes, the Unease pull factor precomputed) and `NeedSet` (one passenger's needs, 0 fine to 100 worst: `Tick` applies the base changes scaled by a composed multiplier per need, then pulses spread over their window, then Unease's pull toward the passenger's baseline, then the clamp; `Add` for a one-off change, `Set` for a starting value). Unease is pushed up by the hourly sources the caller sums into `NeedContext`, by `PushUnease` and by positive pulses, all scaled by the Unease multiplier, while relief and the pull are not; Rest's multiplier scales only its rise (R33 as amended). `Cascades` holds the flight's `CascadeRule`s and writes the Cascade-class modifiers that fire for one target need into a caller's span (a rule fires strictly above its threshold, and a `LavUnreachable` rule only when the caller says so); `Distress` is the weighted sum of each need's excess over its comfort threshold, clamped to [0, 100]; `SustainGate` raises a need failure once per crossing and re-arms below the threshold minus its margin. All three take their numbers at construction and allocate nothing per tick.
- `Randomness/`: `RngRoot` (the flight's seed) and `SimRandom`, a named stream (xoshiro256** seeded through SplitMix64 from the FNV-1a-64 hash of the seed and the name); every draw in the sim comes from one (ADR 0003, R5).
- `Time/`: `SimTime` (the 250 ms tick, ticks per sim minute and hour) and `TickAccumulator`, which turns elapsed milliseconds into whole ticks and carries the remainder.

`tests/Sky.Engine.Tests/Guards/ForbiddenApiTests.cs` reads the compiled Engine's metadata and fails on any wall-clock, randomness, threading, IO or string-hashing API (R3 in the M1 plan); a new Engine dependency on one of them is an allowlist edit there, argued in the change. A C# `event` or `lock` compiles to `System.Threading` calls, so either trips it.

### Content: `src/Sky.Content`

A class library referencing `Sky.Engine`. ADR 0001 governs its place. Built so far:

- `ContentLoader.Load(directory)` reads a content root (`layouts/<id>.json`, `needs.json`, `traits.json`, `activities.json` with `activities/<id>.lua`, `crew.json`, `scenarios/<id>.json`, `thoughts.json`) into a `ContentSet`. Layouts, need rates, cascades, distress terms, node capacities and sustain gates map to their Engine types and pass the Engine's own checks; traits, activities, crew, scenarios and thoughts stay schema records until a later step gives them an Engine type. Trait and activity ids are interned to `TraitId` and `ActivityId` by their index in their file's list, and activity modules come back as text in that order, as `LuaBehaviorScripts` requires (R9).
- `Schema/`: the records per file and `ContentJsonContext`, a source-generated `System.Text.Json` context: snake_case fields, an unknown or repeated field fails, a missing required field fails, enums only as their exact C# member names (`StrictEnumConverter`), and an omitted optional field takes its documented default.
- `ContentLoadException`: every load failure, naming the file relative to the root, the JSON path, and what was expected (R10). A null list entry fails at its index.
- `ContentSet.Hash`: the content hash, SHA-256 over every `.json` and `.lua` file under the root in ordinal order of its `/`-separated relative path (the path, a zero byte, the length, the bytes), so the same files hash alike on any machine and in any listing order.
- `Data/**` is copied to the output of `Sky.Content` and every project that references it. No content ships yet.

The loader checks shape, ids and the Engine's rules; references across files (a scenario's layout, a locked lav's fixture) and value ranges are the validator's, not built yet.

### Scripting: `src/Sky.Scripting`

A class library referencing `Sky.Engine`. ADR 0001 and 0006 govern it: it hosts Lua and implements the Engine's ports. The runtime is Lua-CSharp (`LuaCSharp` 0.5.7 with its source generator, ADR 0010, R7), chosen by the Lua runtime research (`docs/research/2026-09-26-lua-runtime.md`) and the boundary spike.

- `LuaHost` owns one Lua state per flight. Each module is Lua source under a string id that returns a table, run in its own whitelist environment that `LuaSandbox` builds (the state opens only the basic, string, table and math libraries; `string.rep` is capped and `string.dump` removed). Every load and call runs under an instruction budget re-armed per call. A module that errors, runs out of budget, overflows the stack or returns the wrong shape is disabled for the flight with a reason; a .NET exception from a host function escapes instead, as a host bug. A stack overflow rebuilds the state and re-runs the enabled modules. `math.random` draws from the `SimRandom` set with `SetRandom`, rejects non-integer bounds, and is unavailable while a module loads. The Lua-typed members (`LoadModule`, `HasFunction`, `TryCall`, `DisableModule`, `ClearRandom`) are internal, for the port adapters in this assembly.
- `LuaBehaviorScripts` implements `IBehaviorScripts` over the host: each `ActivityModule` (an id and its source) is the module of the `ActivityId` at its index, loaded at construction; `ScoreActivities` clears the host's random stream (activity modules draw nothing, R6), refuses facts it cannot render, then calls each candidate's `utility(facts)` through one reused `[LuaObject]` adapter, `LuaPassengerFacts`, with no allocation per call. A module that throws, is disabled, or returns anything but a finite number of 0 or more scores 0, and a bad return disables it. The Lua names a module reads are listed in `docs/design/passengers.md` section 3.

### Session: `src/Sky.Session`

A class library referencing `Sky.Engine`, `Sky.Content` and `Sky.Scripting`. ADR 0001 governs `ISkySession`, and ADR 0007 its two view projections. Not built yet.

### Sim: `src/Sky.Sim`

An executable referencing `Sky.Session`; its entry point runs nothing. ADR 0001 governs it, and M1's headless flight runs here. Not built yet.

### SimConnect: `src/Sky.SimConnect`

A class library referencing `Sky.Engine`. ADR 0001 (Windows-only; the client builds without it) and 0002 (MSFS's clock drives `IClockSource`) govern it. Not built yet.

### Voice: `src/Sky.Voice`

A class library referencing `Sky.Engine`. ADR 0008 governs it: the bundled small model changes only words. Not built yet.

### Client: `src/Sky.Client`

A Godot 4.7.2 .NET project (`Godot.NET.Sdk`) referencing `Sky.Session`, with its marker at `Scripts/AssemblyMarker.cs`, one scene (`Scenes/Main.tscn`), and `Scratch/` compiled into the Debug assembly alone. ADR 0001 governs it: it draws views and holds no rules. Not built yet.

## Where decisions live

- `docs/decisions/`: the ADRs, one engineering decision each, indexed in its `README.md`.
- `docs/design/CONCEPT.md`: what the game is, the owner's rulings, and M1's definition of done.
- `docs/research/`: dated research notes, such as the Lua runtime comparison.
