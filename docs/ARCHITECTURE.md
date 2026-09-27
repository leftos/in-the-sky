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

- `Ports/`: the interfaces other projects implement; `IClockSource` hands the Engine elapsed sim milliseconds.
- `Cabin/`: `CabinLayout` (rows of seat groups, aisles and fixtures in inches), `NavGraphBuilder` (the derived graph of aisle slots, seats, doors, lavs, lav queues and galleys, with edge costs in ticks) and `PathTable` (next hop and cost from every node to every node, Dijkstra per target, ties to the lower id). Nothing after the builder reads inches (ADR 0005). `Occupancy` is the capacity and legality table over the graph's nodes, not a mover: capacities per node kind come from `NodeCapacities`, a reservation and an arrival both count, a person may squeeze one over capacity into a full aisle slot with no cart, a cart takes only an empty aisle slot, galley or door and then admits nobody, and `Holders` lists who is on a node (R31). `LayoutAscii.Render` dumps a layout one line per row for debugging (R32).
- `Execution/`: `SequenceExecutor` (one `CharacterAction` per character, priority interrupts with cleanup on interrupt, starts made during a tick run from the next one) and `SyncPoint` (named waits that release every member together) (ADR 0006).
- `Needs/`: `SourceClass`, `RateModifier` and `RateMultiplier.Compose`, the rate rule of CONCEPT section 4 with its 0.2 floor and 2.5 cap; `Need` (the five), `NeedRates` (the flight's hourly rates as per-tick changes, the Unease pull factor precomputed) and `NeedSet` (one passenger's needs, 0 fine to 100 worst: `Tick` applies the base changes scaled by a composed multiplier per need, then pulses spread over their window, then Unease's pull toward the passenger's baseline, then the clamp; `Add` for a one-off push, `Set` for a starting value).
- `Randomness/`: `RngRoot` (the flight's seed) and `SimRandom`, a named stream (xoshiro256** seeded through SplitMix64 from the FNV-1a-64 hash of the seed and the name); every draw in the sim comes from one (ADR 0003, R5).
- `Time/`: `SimTime` (the 250 ms tick, ticks per sim minute and hour) and `TickAccumulator`, which turns elapsed milliseconds into whole ticks and carries the remainder.

`tests/Sky.Engine.Tests/Guards/ForbiddenApiTests.cs` reads the compiled Engine's metadata and fails on any wall-clock, randomness, threading, IO or string-hashing API (R3 in the M1 plan); a new Engine dependency on one of them is an allowlist edit there, argued in the change. A C# `event` or `lock` compiles to `System.Threading` calls, so either trips it.

### Content: `src/Sky.Content`

A class library referencing `Sky.Engine`. ADR 0001 governs its place. The schemas, loader and validator are not built yet.

### Scripting: `src/Sky.Scripting`

A class library referencing `Sky.Engine`. ADR 0001 and 0006 govern it: it hosts Lua and implements the Engine's ports. The runtime is chosen by the Lua runtime research (`docs/research/2026-09-26-lua-runtime.md`) and the boundary spike. Not built yet.

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
