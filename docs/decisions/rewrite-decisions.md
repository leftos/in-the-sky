# In the Sky: rewrite decisions (2026-09-26)

In the Sky is a greenfield rewrite of OpenPax (`D:\openpax`), a passenger and crew cabin simulator in Godot and C#. This file records what the owner decided in the kickoff interview on 2026-09-26, what was taken from OpenPax and its two sibling repos (`D:\delve-the-dungeon`, `D:\opening-hand`), and the questions left for the concept pass. It is the source the M0 and M1 briefs are written from. Terms are defined in the glossary in [docs/README.md](../README.md).

## 1. Owner decisions

### Repo and workflow
- The repo is a new sibling, `D:\in-the-sky`, public on GitHub from the first push. OpenPax stays as a read-only reference.
- Everything is MIT-licensed: code, original art, Lua events and data.
- Stack: Godot 4.7.2 .NET and C# on net10.0, matching the siblings and the godot-mcp server. OpenPax is on 4.6.2 (`OpenPax.csproj`).
- Git: commit often inside the `/nextup` loop, push at milestones, and use feature branches for major, experimental or spike work.
- It starts with a short concept pass (a CONCEPT doc) before any code, as opening-hand did.
- Studio agents from the start: game-designer, ux-reviewer, balance-analyst, a narrative and event writer, art-director and playtester. Also godot-reviewer, a docs-first explore agent and the user-level implementer.
- Godot MCP: the owner's `leftos/godot-mcp` (`D:\godot-mcp`), registered at local scope, with its bridge injected through `override.cfg`. There is no addon in the repo, and friction goes to that repo's issues. `GODOT_CONVENTIONS.md` is seeded from `~/.claude/godot-conventions/` through the `godot-conventions-sync` skill.
- Platforms: Windows first, with a portable core. Engine, Sim and tests run anywhere, and CI runs on Linux. Only the SimConnect adapter and Windows TTS are Windows-only, and the client builds without them.

### Asset provenance
- Every art, audio, font, shader, data and model-weight file has an entry in one machine-readable ledger, `assets/PROVENANCE.toml`. Each entry records the path, origin (`original`, `library` or `generated`), SPDX license, author, and source URL with its retrieval date. A generated file also records the tool and version, the model id, the prompt (a file kept in the repo) and the creation date. Every file records any modifications.
- A prek hook and a CI job fail the build on any asset without an entry, any license outside the allowlist, or any entry pointing at a missing file.
- `CREDITS.md` is generated from the ledger and never edited by hand.
- License allowlist: CC0 and public domain, CC-BY 3.0 and 4.0, CC-BY-SA, OFL for fonts, MIT and BSD for shaders and data. The ledger flags CC-BY-SA files, because derivatives of them must stay CC-BY-SA.
- The rule was set because OpenPax's credits have gaps. `Audio\Cabin\CabinChime.ogg` comes from a YouTube video with no license. The LimeZu spritesheets are a paid itch.io pack whose redistribution terms are unchecked. `LICENSE.txt` still has its `[year] [fullname]` placeholders.

### Engine
- **Solution layout** (the Engine references nothing; each src project has a `tests/` twin):

  | Project | Holds |
  |---|---|
  | `Sky.Engine` | World, fixed tick, needs, utility scoring, task board, executor, journal; defines the ports `IClockSource`, `ISimFeed` and `IBehaviorScripts`. References nothing. |
  | `Sky.Content` | Schemas, loader, validator. |
  | `Sky.Scripting` | The Lua host, implementing the Engine's ports. |
  | `Sky.Session` | `ISkySession`: the client pulls views from it and pushes commands to it. |
  | `Sky.Sim` | Headless runner, seed sweeps, balance CSV output. |
  | `Sky.SimConnect` | The MSFS adapter (Windows-only). |
  | `Sky.Voice` | TTS, the bundled small model, subtitles. |
  | `Sky.Client` | Godot: draws views, holds no rules. |

  The repo root also carries `sky.ps1`, `tools/gate.ps1` and `tools/test-all.ps1`. Tests use xunit.v3 on Microsoft.Testing.Platform, with CsCheck for property tests.
- **State and time:**
  - The world is mutable C# objects advanced by fixed-size ticks. The tick size is set by the spike (250 ms is the working figure).
  - One `IClockSource` says how much sim time has passed. In MSFS mode, MSFS's clock drives it, so MSFS pause and time acceleration are inputs. In standalone mode the emulator and the player's time warp drive it.
  - The journal records how many ticks each frame ran, plus every command and observation, so a replay from seed and journal is exact.
- **Flights** run at their real length in sim time, with need rates per sim hour. The engine must sustain 64x at full cabin; with 250 ms ticks that is 256 ticks per second, pinned by a performance test.
- **Character AI:**
  - Passengers choose their next activity by utility scoring over needs, traits and context, with a bias toward keeping the current one.
  - Crew claim work from a prioritized task board.
  - Both run on one sequence executor with priority, interrupt and cleanup on interrupt, and sync points.
  - Each decision records its top candidate scores for a dev inspector.
- **Content:** Lua for everything behavioral. How often Lua may run (only at decision points, or also per tick) is not decided yet. A spike settles it (section 4).
- **Cabin geometry:**
  - Layouts are authored in physical units: seat pitch and width in inches, aisle position and width. Each row lays out its own seat groups, so a 2-2 business row can sit in front of a 3-3 economy row. This fixes the owner's main complaint with OpenPax, where seats had to be 1 or 2 cells wide and seat columns had to line up across every row.
  - The engine derives a navigation graph from the layout (an aisle slot at each row, seat nodes on lateral edges, galley, lav and door nodes) and runs all logic on it.
  - Capacity and reservations on the nodes give aisle blocking, and a squeeze rule handles passing.
  - Positions in inches are used only for drawing.
  - There is one coordinate space, replacing OpenPax's 0-based and 1-based pair.
- **Visibility:** the player's view is built from what crew have observed, which ages and goes stale. A setting switches it to the true state. The Session has two view projections from the start.
- **Language model:** a small model is bundled and runs on-device. It adds variety to announcements around a preset theme and rules, and brings up topics about the destination or landmarks being overflown. Its output changes only the words spoken or shown, never sim state or the sim RNG. A replay may word things differently, and authored templates are the fallback. No cloud LLM provider and no API-key setting ships; cloud LLMs are for offline authoring by the team only. The model weights go in the provenance ledger and must be redistributable.

### Scope
- **M1 is a headless cabin flight:** one narrowbody, a seeded manifest and crew, and boarding through cruise service to deboarding in `Sky.Sim`, with no Godot. M1 includes:
  - a few newly designed Lua events;
  - an end-of-flight score and text report;
  - replay from seed and journal, and invariant fuzzing over hundreds of seeds.

  M1 has **no player commands.** Event choices are made by the crew on their own, weighted by competence, traits and fatigue. That is the path AirlineOps needs for flights nobody is watching. From M3, a player's choice is a journaled command that takes precedence.
- Scoring measures outcomes, not bookkeeping: the spread of passenger experience (not an average), incidents handled or missed, crew strain, and on-time doors. Each outcome is traced to the moments that moved it.
- Crew staffing is a player lever at the policy level (crew complement and zone assignment from a roster), landing with M3. M1 uses a fixed crew.
- Scale: narrowbody (about 200 passengers, 6 crew) in M1, twin-aisle widebody by M3.
- Manifest: a simple seeded generator (trip purpose, groups, traits, profession, seat class). The booking market is an AirlineOps backlog item.
- The build is CabinSim first and AirlineOps-ready. The two hooks from day one: a flight runs without a view, and crew can resolve events with nobody watching.
- **Ported from OpenPax:** only the TTS and subtitle stack. That is the provider registry, PCM into one bus, the intercom DSP, the subtitle segmenter and duration calculator, and the TTS/subtitle text split. It is re-shaped for the bundled small model, with the cloud LLM providers dropped. Events, layouts, traits, professions and needs are all redesigned from scratch.
- **Roadmap:**
  - M0: concept pass, repo skeleton, gates, CI, provenance gate, docs skeleton, Lua spike.
  - M1: headless cabin flight.
  - M2: Godot client. A 2D cabin view drawing Session views, a dev inspector, scratch scenes, and the art-direction decision.
  - M3: player HUD and interventions. Stage-manager surfaces, alerts, event choice UI, staffing, widebody.
  - M4: voice. Ported TTS and subtitles, the bundled small model, announcements.
  - M5: MSFS. The SimConnect clock and feed, wired in and tested in the sim.
  - M6: layout editor and content authoring loop.
  - Later: AirlineOps auto-resolve at scale, saves, progression, the booking market.
- The owner left the art direction open until M2.

## 2. What to keep from OpenPax

These ideas are kept; their code is not.
- **Lua events** (`Code/Flights/Events/`: `LuaSandbox`, `LuaEventApi`, `LuaEventLoader`, `FlightEventManager`).
  - Each event has its own sandboxed `Script` with no io, os, require or load.
  - Scripts see read-only adapter types, never simulation objects.
  - A module is declarative: `trigger`, `describe`, `choices`, `effects`.
  - Effects return delayed consequences instead of mutating state.
  - A definition that throws is disabled for that flight only.
- **Behavior/Sequence composition with named sync points** and a `GetSyncGroup` hook (`Code/Characters/Sequences/Sequence.cs:38-74`).
- **A stage state machine** whose factory map is checked for completeness at startup, which still runs each handler's `Start` when several stages are crossed in one frame (`Code/Flights/Flight.cs:433-444`, `:540-570`).
- **The need rate-modifier rule:** modifiers from the same source multiply, different sources add, and the total is capped at 2.5x (`Code/Characters/Need.cs:23-57`). April 2026 showed that purely additive stacking freezes needs.
- **Failures only after a sustained window** (`Code/Flights/Alerts/NeedFailureGate.cs`), so a spike at spawn has no instant consequences.
- **Injectable flight dependencies** (`IManagerFactory`, `ISimStateProviderFactory`) and per-entity RNG streams (`Code/Characters/Person.cs:88`).
- **Layout validation as a pipeline** of `IConfigValidator`s (`Code/Aircrafts/Validation/IConfigValidator.cs`), layout object inheritance (`extends`/`overrides`), and an ASCII dump of a layout for debugging and the CLI (`Code/Aircrafts/Aircraft.cs:146-200`).
- **Heatmaps as an `IHeatmapProvider` overlay** (Mood, NeedPressure, CrewLoad, ServiceDemand).
- **Editor undo and redo** through command objects plus `CompoundCommand`, rebuilt without `GodotObject` so it can be tested.
- **TTS returns PCM** (`Code/Audio/ITTSProvider.cs:16-38`), and every clip goes through one bus with the intercom DSP. Announcements carry separate text for speech and for subtitles.
- **Recolourable liveries:** marker colours in the SVGs are swapped for palette colours before rasterizing, cached by path, palette and scale (`Code/Aircrafts/Liveries/PalettedSvgLoader.cs:21-60`).
- **Atomic config writes with versioned migrations** (`Code/App/ConfigManager.cs:882-901`, `:384-407`).
- **Mid-flight join:** stage detection and fast-forward (`MidFlightStageDetector`, `FlightStateFastForwarder`, 61 tests between them).
- **Design pillars** (from `D:\openpax\.claude\agents\game-designer.md` and the QuickSim docs):
  - The player is a stage manager: agency is in setting the conditions, not moving the actor.
  - Emergent stories come from interacting systems; prefer fewer systems with more interactions.
  - Information must serve an action, and dev surfaces stay separate from player surfaces.
  - Decisions are made at the level of policy or a moment, not per passenger.
  - A competent normal flight produces no notifications.
  - Features can be switched on or off.

## 3. What to avoid

- **The simulation sharing an assembly with Godot and the tests.** NUnit is referenced by `OpenPax.csproj`, `Global.X` is used 113 times across 63 files, `ConfigManager` has 178 call sites, and `GlobalVars.IsTestEnvironment` branches out of production code. The fix is the Engine project that references nothing, with state passed in explicitly.
- **Time that depends on the frame and the machine:**
  - There is a variable delta times the sim rate (`Flight.cs:418`).
  - About 54 accumulators reset to zero and drop their overshoot.
  - Thought decay uses the wall clock (`Thought.cs:23-33`).
  - Stage timestamps use `DateTime.UtcNow`.
  - How much work a frame does depends on a Stopwatch budget (`FlightStageHandler.cs:36-47`).
- **Unseeded or shared randomness:**
  - `FlightEventManager.cs:31`, `AircraftScene.cs:38` and `FlightEmulator.cs:15` create unseeded `Random`s.
  - The UI draws from the sim stream (`Character.cs:141`).
  - The fix is one seeded root that hands out named streams, and a separate display RNG.
- **Traits as a C# enum** with 277 `HasTrait` checks across 45 files, and items as C# lambdas (`Items/ItemDefinitions.cs`). Load errors are swallowed into empty dictionaries, and there are two JSON libraries.
- **God objects:** `Flight.cs` (63 commits, 929 lines) and `Person.cs` (55 commits, 952 lines).
- **Stringly-typed object and seat types** (`"Seat"`, `"Economy"`), and `+1`/`-1` arithmetic between coordinate spaces scattered through the code.
- **No preemption model:** OpenPax has the clear-and-re-enqueue, `OverrideCurrentBehavior`, `ClearBehaviors` and `Abort` paths instead.
- **O(N^2) neighbour scans** with no spatial index (`PersonalityTraitInteraction.cs:262-292`).
- **UI built in code:** `Tools/lint-ui.sh` reports 270 violations, and `OptionsDialog.cs` has 2,098 lines of wiring. There were repeated redesigns: 25 scenes extracted in April 2026, and HUD P1 to P15 in one day.
- **No enforcement:** no CI, no git hooks, no analyzers, and `.editorconfig` rules only at `suggestion`.
- **An integration built and never wired:** `DefaultSimStateProviderFactory` always returns `FlightEmulator` (`Code/Flights/DefaultFlightDependencies.cs:20-26`).
- **API keys in plain text** in `user://app_config.json`. With no cloud LLMs in the game, this goes away.
- **A "complete" phase that was one commit** and later failed an honest audit (Phases 0 to 6 landed between 2025-12-31 and 2026-01-01; the 2026-04-26 audit found gaps in 3 of 11).

## 4. Open questions

For the concept pass:
- The need set and its math (owner: "concept pass decides"). OpenPax had 9 needs. The alternative on the table is fewer needs, each tied to a lever.
- The contradiction between "policy, not per passenger" and OpenPax's "optimize seat assignments" loop.
- What exactly each scoring outcome measures, and how the report traces moments to it.
- The player fantasy and core loop in In the Sky's own words, and M1's definition of done.

For the M0 spikes:
- **Lua runtime.** A librarian checks the current maintenance status and sandboxing of MoonSharp (pure C#), NLua/KeraLua (native Lua 5.4) and any maintained successor.
- **Lua boundary.** The spike runs 200 passengers with about 10 activities at 64x, comparing the top two runtimes against plain C# scoring. It settles whether Lua may run only at decision points or also per tick. The cost is unmeasured today.
- **Tick size.** 250 ms is the working figure; the spike confirms it against the 64x budget.

Later:
- The art direction (at M2, with art-director).
- The choice of bundled small model: license, size, and CPU-only speed (at M4).
- MSFS clock edge cases: active pause, slew, and a sim rate change mid-tick (at M5).
