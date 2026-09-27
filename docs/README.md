# In the Sky docs

Start here. In the Sky is a passenger and crew cabin simulator in Godot 4.7.2 .NET and C#, a greenfield rewrite of OpenPax.

| Question | Owner |
|---|---|
| What was decided at kickoff, and why? | [plans/2026-09-26-rewrite-decisions.md](./plans/2026-09-26-rewrite-decisions.md) |
| Which engineering decisions stand? | [decisions/README.md](./decisions/README.md) (the ADRs) |
| What is the game, and what did the owner rule? | [design/CONCEPT.md](./design/CONCEPT.md) |
| Where does code live, and what may reference what? | [ARCHITECTURE.md](./ARCHITECTURE.md) (projects, dependency edges, task index) |
| How do I build, test, run the hooks and set up a clone? | [DEVELOPMENT.md](./DEVELOPMENT.md) (toolchain, `sky.ps1` commands and ceilings, hooks, provenance, the godot MCP server) |
| Which tests exist, and where does a new one go? | [TEST_ALMANAC.md](./TEST_ALMANAC.md) |
| What must Godot client code follow? | [GODOT_CONVENTIONS.md](./GODOT_CONVENTIONS.md) |
| What did research find? | [research/](./research/) (dated notes, such as the Lua runtime comparison) |
| What is next? | [plans/MAIN.md](./plans/MAIN.md) |
| What does a word mean? | The glossary below |

## Glossary

| Term | Meaning |
|---|---|
| 64x | The engine's speed target: 64 seconds of sim time per wall-clock second at full cabin (about 200 passengers and 6 crew), pinned by a performance test. |
| ADR | Architecture Decision Record: one numbered file in `docs/decisions/` stating an engineering decision, its context and its consequences. |
| Allowlist | The licenses an asset may carry: CC0 and public domain, CC-BY 3.0 and 4.0, CC-BY-SA, OFL for fonts, MIT and BSD for shaders and data. |
| AssemblyMarker | The one public class in each project; its `Dependencies` field names a type from every project it references, so the compiler keeps each reference and `ReferenceTests` can see it. |
| Auto-resolve | Crew make an event's choice on their own, weighted by competence, traits and fatigue. It is used when no player is making the choice. |
| Balance CSV | `Sky.Sim`'s output from a sweep: one row per seed with the four outcome measures, read by `balance-analyst`. |
| Bridge | The code the godot MCP server injects into a running client through an `override.cfg` beside `project.godot`, so an agent can drive the game; it is removed when the run stops and never tracked. |
| Cabin ready | The tick at which boarding is complete, bins are closed and every passenger is seated and belted: the part of an on-time door the cabin controls. |
| Cascade | One system's effect setting off another's, such as a drinks round filling the lav queue that then blocks the cart. |
| Ceiling | The longest a gate may run before `tools/gate.ps1` or `tools/test-all.ps1` kills it with its children (exit 124); a few times what it takes today, so reaching one means a hang. |
| Concept pass | The short design sitting before any code that produces `docs/design/CONCEPT.md`. |
| Contagion | A need spreading between neighbours; in M1 only Unease spreads, to adjacent seats and across the aisle. |
| Crew-observed view | The player's picture of the cabin, built from what crew have seen, which ages and goes stale. A setting switches it to the true state. |
| Delayed consequence | What a Lua event's effect returns instead of changing state at once: a change the engine applies later, at a tick it names. |
| Dependency edge | A project reference between two `Sky.*` projects. `docs/ARCHITECTURE.md` lists the allowed edges and each test twin's `ReferenceTests` pins them. |
| Dev inspector | A developer-only surface showing why a character chose what it did, from the top candidate scores each decision records. |
| Display RNG | The random source for purely visual draws, kept apart from the sim streams so drawing never changes the simulation. |
| Distress | How far a passenger is from fine at one tick: 0 is fine, 100 is as bad as the sim allows. It is computed from their needs. |
| Doc drift | A doc naming a file, project or test that has changed or gone since the doc was written; a prek hook checks `ARCHITECTURE.md` and `TEST_ALMANAC.md` against the tree. |
| Docs map | The table in the `sky-nextup` skill naming which doc owns which kind of change, walked before every commit. |
| Engine | `Sky.Engine`, the simulation library, which references no other project or package. |
| Event | A Lua module (`trigger`, `describe`, `choices`, `effects`) that surfaces a situation with choices; crew auto-resolve it when no player chooses. |
| Experience | A passenger's whole-flight result, 0 to 100, computed from their distress over the flight (`docs/design/CONCEPT.md` section 6). |
| Flight phase | A stage of a flight from boarding to deboarding (boarding, taxi, climb, cruise, descent, deboarding and so on); the stage machine enters each in order. |
| Gate | A check that must pass before work lands (a build, a test run, a format check, a hook), run under `tools/gate.ps1`; `tools/test-all.ps1` is the whole gate. |
| Hazard | A review finding that can break a build, a run or a player's session; after one is fixed, the reviewer does a last pass. |
| Headless | Run without a window or Godot: `Sky.Sim` flies a whole flight headless, and M1 is a headless flight. |
| Incident | A situation that needs crew action within a window (a medical case, a dispute); it is handled or missed, and the report counts both. |
| Invariant fuzzing | Running many seeded flights and checking after every tick that rules which must always hold still hold. |
| Journal | The record of a flight's inputs (tick counts per frame, commands, observations) that, with the seed, replays the flight exactly. |
| Last pass | A reviewer's final read of a diff after a hazard was fixed, checking only that the fix holds and broke nothing. |
| Lever | Something the player (from M3) or a policy (in M1) sets that changes the conditions the cabin plays out in: a service schedule, a crew zone, the lighting plan. A lever is never an order to one passenger. |
| Milestone | A numbered stage of the roadmap (M0 to M6) in `docs/plans/MAIN.md`; each has a definition of done. |
| Moment | Two senses. (1) A surfaced situation the player or crew answers with a choice, as opposed to a policy ("decide at the level of a moment"; seat conflicts arrive as moments). (2) In the report, a journal record that moved an outcome: its tick, what happened, who was involved, the cause chain behind it, and its effect on each scoring outcome. The report is built from moments. |
| Named stream | A random sequence handed out by the one seeded RNG root under a fixed name, so adding draws in one system never shifts another's. |
| Nav graph | The graph the engine derives from a layout: aisle slots, seat nodes, galley, lav and door nodes, with capacity and reservations. |
| Need | One of the five meters a passenger carries (Refreshment, Bladder, Rest, Unease, Boredom), 0 to 100; each rises at a rate and a lever moves it. |
| Orchestrator | The main Claude session: it owns plans, docs, config and commits, dispatches code edits to the `implementer` agent, and settles non-game technical decisions itself. |
| Pillar | One of the design principles in `docs/design/CONCEPT.md` section 2 that every feature is tested against. |
| Policy | A standing decision that applies to a class of people or situations (a service plan, a crew zone), as opposed to a moment. |
| Port | An interface the Engine defines and another project implements (`IClockSource`, `ISimFeed`, `IBehaviorScripts`). |
| Provenance gate | The prek hook and CI job that fail the build on an asset without a ledger entry, a license outside the allowlist, or an entry pointing at a missing file. |
| Provenance ledger | `assets/PROVENANCE.toml`, one entry per asset recording origin, license, author and source. The gate checks it, and `CREDITS.md` is generated from it. |
| Rate multiplier | The factor applied to a need's base rate, composed from all active modifiers by the rule in `docs/design/CONCEPT.md` section 4. |
| Reference flight | M1's baseline flight: a narrowbody day departure of about 2.5 hours with one drinks round and one meal. |
| Replay equality | Replaying a seed and its journal gives the same end-state hash and the same report text as the original run. |
| RNG root | The one seeded random source of a flight, which hands out named streams. |
| Scenario | A file naming a flight's setup (aircraft, manifest seed, crew, service plan, levers) that `Sky.Sim` runs. |
| Scratch scene | A Debug-only Godot scene under `src/Sky.Client/Scratch/` that shows one piece of the client in isolation; no export carries it. |
| Sequence executor | The one runner for passenger and crew actions: it orders them by priority, interrupts a lower one for a higher, runs cleanup on interrupt, and holds sync points. |
| Session | `Sky.Session`'s `ISkySession`: the client pulls views from it and pushes commands to it. |
| Source class | The origin a rate modifier is grouped by: modifiers from the same source multiply, different sources add. |
| Spike | A short, throwaway experiment on its own branch that measures something a decision depends on. |
| Squeeze rule | The nav graph rule that lets one character pass another in an aisle slot that is already occupied. |
| Stage manager | The player's role: they set the conditions the cabin plays out in, rather than moving people. |
| Strain | A crew member's accumulated load: time on task without a break, pre-emptions, and fatigue. |
| Studio agent | One of the project's own agents in `.claude/agents/` (game-designer, event-writer, balance-analyst, art-director, ux-reviewer, playtester, godot-reviewer, sky-explore), each owning a kind of work and, for most, a design doc. |
| Sustain window | How long a need must stay past its threshold before it counts as a failure, so a spike at spawn has no consequence. |
| Sweep | Running the same scenario over many seeds (a seed sweep) and collecting one balance CSV row per seed. |
| Sync point | A named moment where several characters' sequences wait for each other, such as two crew working one cart. |
| Task board | The prioritized list of work (services, call buttons, checks) that crew claim. A higher-priority task can pre-empt a lower one. |
| Test twin | The `tests/Sky.<X>.Tests` project paired with each `src/Sky.<X>` project. |
| Tick | One fixed step of simulation time: 250 ms (ADR 0010). |
| Time warp | Running the sim faster than real time (up to 64x) in standalone mode; in MSFS mode the sim's own rate drives it. |
| Utility scoring | How a passenger picks the next activity: each candidate scores itself from needs, traits and context, and the highest score wins. |
| Verdict | The word the report gives each of the four outcomes (Smooth, Rough, Bad); there is no overall grade. |
| View projection | One of the Session's two ways of building what the client sees: from crew observations, or from the true state. |
