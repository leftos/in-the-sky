# In the Sky docs

Start here. In the Sky is a passenger and crew cabin simulator in Godot 4.7.2 .NET and C#, a greenfield rewrite of OpenPax.

| Question | Owner |
|---|---|
| What was decided at kickoff, and why? | [plans/2026-09-26-rewrite-decisions.md](./plans/2026-09-26-rewrite-decisions.md) |
| Which engineering decisions stand? | [decisions/README.md](./decisions/README.md) (the ADRs) |
| What is the game, and what did the owner rule? | [design/CONCEPT.md](./design/CONCEPT.md) |
| How do passengers, crew and cabin events work? | [design/passengers.md](./design/passengers.md) (needs, activities, traits, manifest, incidents, observation), [design/crew.md](./design/crew.md) (tasks, zones, service, strain, auto-resolve), [design/events.md](./design/events.md) (the event module shape and the M1 events) |
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
| Age band | A passenger's manifest band, `adult` or `child` (2 to 11); every child also carries the `child` trait (`docs/design/passengers.md`). |
| Allowlist | The licenses an asset may carry: CC0 and public domain, CC-BY 3.0 and 4.0, CC-BY-SA, OFL for fonts, MIT and BSD for shaders and data. |
| AssemblyMarker | The one public class in each project; its `Dependencies` field names a type from every project it references, so the compiler keeps each reference and `ReferenceTests` can see it. |
| Auto-resolve | Crew make an event's choice on their own, weighted by competence, traits and fatigue. It is used when no player is making the choice. |
| Back to your seat | The crew task posted when a passenger leaves their seat with the seatbelt sign on in cruise (only at Bladder 85 or more); `docs/design/crew.md`. |
| Balance CSV | `Sky.Sim`'s output from a sweep: one row per seed with the four outcome measures, read by `balance-analyst`. |
| Bin help | A crew task standing at a row where a passenger has been stowing or retrieving a bag too long, cutting their remaining bin time (`docs/design/crew.md`). |
| Body clock | The rule that sets a passenger's Rest from their wake time and the origin-local time of day, including the post-lunch dip (`docs/design/passengers.md` section 6). |
| Bridge | The code the godot MCP server injects into a running client through an `override.cfg` beside `project.godot`, so an agent can drive the game; it is removed when the run stops and never tracked. |
| Brief | The written instructions for one implementer run: the plan steps it carries, the files each touches and the command that proves each. |
| Cabin ready | The tick at which boarding is complete, bins are closed and every passenger is seated and belted: the part of an on-time door the cabin controls. |
| Call reason | Why a passenger pressed the call button (`refreshment`, `reassurance` or `lav_permission`): it decides what answering does and which need the answer reveals. |
| Cart span | The rows one service cart serves in a round, set with the crew zones as one lever (`docs/design/crew.md`). |
| Cascade | One system's effect setting off another's, such as a drinks round filling the lav queue that then blocks the cart. |
| Catch-up drink | A crew task posted when a crew member reads a passenger's Refreshment as `urgent`, bringing them the drink an answered call would. |
| Catch-up service | A crew task serving, after a round ends, the passengers the round skipped because they were asleep or away from their seat. |
| Ceiling | The longest a gate may run before `tools/gate.ps1` or `tools/test-all.ps1` kills it with its children (exit 124); a few times what it takes today, so reaching one means a hang. |
| Check cadence | How often the event scheduler asks every enabled event trigger whether to fire (`docs/design/events.md` section 3). |
| Check-in follow-up | A crew task posted some minutes after a crew member reads a passenger's Unease as `urgent`: a return visit that calms them and reads Unease again. |
| Check-in walk | A crew task walking a zone's rows at a set cadence in cruise, observing every seat and stopping at passengers who look uneasy or distressed. |
| Claim priority | A task's place on the task board, which decides who claims it first and whether it can pre-empt a running task (compare hold priority). |
| Competence | A crew member's experience and skill, 0 to 1: it sharpens event choices (focus) and, with empathy, need reads. |
| Concept pass | The short design sitting before any code that produces `docs/design/CONCEPT.md`. |
| Consequence kind | One of the four shapes an event's delayed consequence takes: a need change, an incident, a seat move, or a line (`docs/design/events.md` section 1). |
| Contagion | A need spreading between neighbours; in M1 only Unease spreads, to adjacent seats and across the aisle. |
| Content hash | A SHA-256 over every content file's path and bytes, written in a journal's header so a replay against changed content is refused. |
| Crew station | A nav graph node beside the forward door, off the passenger path, where the purser stands for boarding and deboarding. |
| Crew-observed view | The player's picture of the cabin, built from what crew have seen, which ages and goes stale. A setting switches it to the true state. |
| Decision log | The flight's record of every passenger decision with its top three candidate scores, which the dev inspector and `Sky.Sim decisions` read. |
| Decision point | A moment when Lua runs: a character choosing its next activity, or an event's `trigger`, `describe`, `choices` or `effects` being evaluated (ADR 0010). |
| Decision round | The step of the `/nextup` loop where every choice a brief needs is settled before dispatch: technical ones by the orchestrator, design, player-facing and public ones by the owner. |
| Delayed consequence | What a Lua event's effect returns instead of changing state at once: a change the engine applies later, at a tick it names. |
| Dependency edge | A project reference between two `Sky.*` projects. `docs/ARCHITECTURE.md` lists the allowed edges and each test twin's `ReferenceTests` pins them. |
| Dev inspector | A developer-only surface showing why a character chose what it did, from the top candidate scores each decision records. |
| Display RNG | The random source for purely visual draws, kept apart from the sim streams so drawing never changes the simulation. |
| Distress | How far a passenger is from fine at one tick: 0 is fine, 100 is as bad as the sim allows. It is computed from their needs. |
| Distress band | Distress read in three bands (`calm`, `uneasy`, `distressed`), which crew see by sight, exactly. |
| Doc drift | A doc naming a file, project or test that has changed or gone since the doc was written; a prek hook (`tools/hooks/Test-DocDrift.ps1`) checks `ARCHITECTURE.md` and `TEST_ALMANAC.md` against the tree, and a commit-msg hook refuses a `src/Sky.Content/` commit that stages no `docs/design/` file and gives no `Docs: unchanged, <why>` line. |
| Docs map | The table in the `sky-nextup` skill naming which doc owns which kind of change, walked before every commit. |
| Empathy | How closely a crew member attends to the person in front of them, 0 to 1; with competence it sets read accuracy, and nothing else in M1. |
| Engine | `Sky.Engine`, the simulation library, which references no other project or package. |
| Escalation deadline | The tick by which crew must reach an incident; an incident still unanswered then is missed. |
| Event | A Lua module (`trigger`, `describe`, `choices`, `effects`) that surfaces a situation with choices; crew auto-resolve it when no player chooses. |
| Experience | A passenger's whole-flight result, 0 to 100, computed from their distress over the flight (`docs/design/CONCEPT.md` section 6). |
| Facts | The read-only table an event's trigger returns, recording what it saw (who is involved, how busy crew are); `describe`, `choices` and `effects` read it. |
| Failure threshold | The level a need must reach and hold for its sustain window before it raises that need's incident kind. |
| Fatigue | A crew member's tiredness, 0 to 100, rising on duty: it flattens focus and speeds strain, and never affects need reads. |
| First value | A number a design doc sets so a system has a shape, marked FV or [D3], unmeasured until `balance-analyst` computes and tunes it in `docs/design/balance.md`. |
| Fix round | One return trip of review findings to the implementer that wrote the change; an item gets two at most. |
| Flight emulator | The standalone-mode `ISimFeed` in `Sky.Session`: it plays a scenario's phase timeline, seatbelt sign and turbulence, and its output is journaled so a replay never runs it. |
| Flight phase | A stage of a flight from boarding to deboarding (boarding, taxi, climb, cruise, descent, deboarding and so on); the stage machine enters each in order. |
| Focus | The number, from competence, fatigue and crew traits, that decides how sharply a crew member picks an event's best choice in auto-resolve. |
| Galley break | A crew task at the galley that lowers strain; it is posted when strain or time since the last break runs high, and call buttons can interrupt it. |
| Gate | A check that must pass before work lands (a build, a test run, a format check, a hook), run under `tools/gate.ps1`; `tools/test-all.ps1` is the whole gate. |
| Golden flight | A seed whose end-state hash is pinned in a test, recorded on Windows, so a run on another OS shows whether replay is byte-identical across platforms. |
| Hard condition | The one condition an event's trigger requires before it rolls its chance (compare soft condition). |
| Hazard | A review finding that can break a build, a run or a player's session; after one is fixed, the reviewer does a last pass. |
| Headless | Run without a window or Godot: `Sky.Sim` flies a whole flight headless, and M1 is a headless flight. |
| Hold priority | The priority a running task defends: a new task pre-empts it only when the new task's claim priority is higher (a cart claims low but holds high). |
| Incident | A situation that needs crew action within a window (a medical case, a dispute); it is handled or missed, and the report counts both. |
| Incident kind | Which incident it is: one per need from a sustained failure (`accident`, `food_demand`, `noise_complaint`, `panic`, `disruptive_passenger`), plus `fight`, raised only by events. |
| Input record | A journal record a replay consumes: a frame's tick count or a feed observation (from M3, a player command). |
| Instruction budget | The cap on how many Lua instructions one call may run, enforced by Lua-CSharp's count hook; a definition that exceeds it is disabled for the flight. |
| Invariant fuzzing | Running many seeded flights and checking after every tick that rules which must always hold still hold. |
| Journal | The record of a flight's inputs (tick counts per frame, commands, observations) that, with the seed, replays the flight exactly. |
| Jumpseat | A nav graph node where crew sit for taxi, takeoff and landing: two at the forward door, four at the galley. |
| Keep-current bias | The bonus utility scoring gives a passenger's current activity, so they do not flip between activities with nearly equal scores. |
| Landing note | The `Landed YYYY-MM-DD: …` text a finished plan line carries into `docs/plans/archive/`: what landed, test counts, the red proof and review findings. |
| Last pass | A reviewer's final read of a diff after a hazard was fixed, checking only that the fix holds and broke nothing. |
| Lav condition | A lav's uses since its last check: past about 20 it is untidy, so visits take longer and push Unease, until a lav check resets it. |
| Lav wave | The rise in lav visits some 30 to 60 minutes after a drinks round or meal, from the drink's Bladder pulse. |
| Leave it for now | The crew-free choice every event offers, with the id `leave`; its effects also apply when a crew task for the event times out unstarted. |
| Lever | Something the player (from M3) or a policy (in M1) sets that changes the conditions the cabin plays out in: a service schedule, a crew zone, the lighting plan. A lever is never an order to one passenger. |
| Lever variant | A scenario file that differs from the reference scenario in exactly one lever, swept against it to show the lever matters. |
| Line consequence | An event consequence that journals a sentence as a moment tied to the event and a passenger, changing no state, so the report has words for it. |
| Milestone | A numbered stage of the roadmap (M0 to M6) in `docs/plans/MAIN.md`; each has a definition of done. |
| Misread | A need read recorded one band below the true one (toward fine), possible only when the true value is just above a band edge. |
| Moment | Two senses. (1) A surfaced situation the player or crew answers with a choice, as opposed to a policy ("decide at the level of a moment"; seat conflicts arrive as moments). (2) In the report, a journal record that moved an outcome: its tick, what happened, who was involved, the cause chain behind it, and its effect on each scoring outcome. The report is built from moments. |
| Named stream | A random sequence handed out by the one seeded RNG root under a fixed name, so adding draws in one system never shifts another's. |
| Nav graph | The graph the engine derives from a layout: aisle slots, seat nodes, galley, lav and door nodes, with capacity and reservations. |
| Need | One of the five meters a passenger carries (Refreshment, Bladder, Rest, Unease, Boredom), 0 to 100; each rises at a rate and a lever moves it. |
| Need band | A need as crew learn it by interacting with a passenger: `fine`, `wants` or `urgent`, never the number. |
| Need read | A need band recorded by a crew interaction (a call answered, a service pass), stamped with the tick and the reader; crew act on what they read (`docs/design/passengers.md` section 9). |
| Next-hop table | The nav graph's precomputed routing: for every pair of nodes, the next node on a shortest path, with ties broken by the lower node id. |
| Notice cap | The longest an event waits for a crew member to notice it before the zone lead resolves it anyway. |
| Orchestrator | The main Claude session: it owns plans, docs, config and commits, dispatches code edits to the `implementer` agent, and settles non-game technical decisions itself. |
| Outcome record | A journal record of a moment, written as it happens; a replay regenerates outcome records and compares them rather than reading them as input. |
| Pillar | One of the design principles in `docs/design/CONCEPT.md` section 2 that every feature is tested against. |
| Policy | A standing decision that applies to a class of people or situations (a service plan, a crew zone), as opposed to a moment. |
| Port | An interface the Engine defines and another project implements (`IClockSource`, `ISimFeed`, `IBehaviorScripts`). |
| Profile | A project's `<project>-nextup` skill (here `sky-nextup`), which supplies the user-level `/nextup` loop with this repo's plan convention, agents, reviewers, gates, docs map and landing path. |
| Provenance gate | The prek hook and CI job that fail the build on an asset without a ledger entry, a license outside the allowlist, or an entry pointing at a missing file. |
| Provenance ledger | `assets/PROVENANCE.toml`, one entry per asset recording origin, license, author and source. The gate checks it, and `CREDITS.md` is generated from it. |
| Push source | Something that pushes a passenger's Unease up at a rate (takeoff, turbulence, a delay, an unanswered call button); the rate multiplier scales the pushes' sum, and the pull toward baseline brings Unease back down. |
| Quality | The number in [0, 1] an event's author gives each choice for the facts the trigger saw; auto-resolve picks the higher-quality choices more often, the sharper the crew member. |
| Rate multiplier | The factor applied to a need's base rate, composed from all active modifiers by the rule in `docs/design/CONCEPT.md` section 4. |
| Read accuracy | The chance a crew member reads a need band right where a misread is possible, from competence and empathy only. |
| Reading light | A passenger's seat light, on while they are awake and off the screen in a dimmed cabin; it keeps the seat visible to crew and can keep a light sleeper beside it awake. |
| Red proof | Showing a new test can fail: a named temporary break of the code turns it red, and restoring the code turns it green. |
| Redline | The strain level (70 as a first value) above which a crew member slows and tires faster; minutes over it are part of the strain outcome. |
| Reference flight | M1's baseline flight: a narrowbody day departure of about 2.5 hours with one drinks round and one meal. |
| Replay equality | Replaying a seed and its journal gives the same end-state hash and the same report text as the original run. |
| RNG root | The one seeded random source of a flight, which hands out named streams. |
| Round | One pass of service through the cabin, a drinks round or a meal, run by carts in economy and by hand in business, as the service plan sets. |
| Scenario | A file naming a flight's setup (aircraft, manifest seed, crew, service plan, levers) that `Sky.Sim` runs. |
| Scratch scene | A Debug-only Godot scene under `src/Sky.Client/Scratch/` that shows one piece of the client in isolation; no export carries it. |
| Secure check | A crew task walking a zone until every passenger in it is seated; the last zone's check at boarding is cabin ready, and the landing check stows any cart still out. |
| Sequence executor | The one runner for passenger and crew actions: it orders them by priority, interrupts a lower one for a higher, runs cleanup on interrupt, and holds sync points. |
| Service plan | The lever listing a flight's rounds in order, each with its start time and direction (front to back or back to front). |
| Session | `Sky.Session`'s `ISkySession`: the client pulls views from it and pushes commands to it. |
| Slice | The plan items one `/nextup` session explores and builds: the first cluster of lines plus the next ones with disjoint files, up to three implementers. |
| Soft condition | A condition that multiplies an event trigger's chance (a trait, a busy board) without being required (compare hard condition). |
| Source class | The origin a rate modifier is grouped by: modifiers from the same source multiply, different sources add. |
| Spike | A short, throwaway experiment on its own branch that measures something a decision depends on. |
| Spill time | How long a task waits before crew outside its zone may claim it. |
| Split group | A booking group seated apart; a child with no adult of their group beside them, and those adults, take extra Unease until an event's seat move ends it. |
| Squeeze rule | The nav graph rule that lets one character pass another in an aisle slot that is already occupied. |
| Stage manager | The player's role: they set the conditions the cabin plays out in, rather than moving people. |
| Stow | A cart's return to the galley as the cleanup of a pre-empted round, such as when the landing secure check ends a meal still in the aisle. |
| Strain | A crew member's accumulated load: time on task without a break, pre-emptions, and fatigue. |
| Studio agent | One of the project's own agents in `.claude/agents/` (game-designer, event-writer, balance-analyst, art-director, ux-reviewer, playtester, godot-reviewer, sky-explore), each owning a kind of work and, for most, a design doc. |
| Sustain window | How long a need must stay past its threshold before it counts as a failure, so a spike at spawn has no consequence. |
| Sweep | Running the same scenario over many seeds (a seed sweep) and collecting one balance CSV row per seed. |
| Sync point | A named moment where several characters' sequences wait for each other, such as two crew working one cart. |
| System switch | A scenario setting that turns one system (needs, events, contagion) off while the flight still runs, per pillar 6. |
| Target selector | How an event consequence names who it hits, resolved by the host: `"subject"`, `"neighbours"` (the contagion reach), or a passenger id from the facts. |
| Task board | The prioritized list of work (services, call buttons, checks) that crew claim. A higher-priority task can pre-empt a lower one. |
| Test twin | The `tests/Sky.<X>.Tests` project paired with each `src/Sky.<X>` project. |
| Tick | One fixed step of simulation time: 250 ms (ADR 0010). |
| Time warp | Running the sim faster than real time (up to 64x) in standalone mode; in MSFS mode the sim's own rate drives it. |
| Trip purpose | Why a passenger's booking is flying (`business`, `leisure`, `visiting`); it shapes group size, children, traits and wake time. |
| Utility scoring | How a passenger picks the next activity: each candidate scores itself from needs, traits and context, and the highest score wins. |
| Verdict | The word the report gives each of the four outcomes (Smooth, Rough, Bad); there is no overall grade. |
| View projection | One of the Session's two ways of building what the client sees: from crew observations, or from the true state. |
| Waiting limit | How long an event's crew task may sit unstarted before it times out into the event's `leave` effects, with a moment that nobody came. |
| Wave | A group of plan lines or subplan steps that share files and a gate; waves with disjoint files run side by side, and inside a wave a step waits only for the steps it consumes. |
| Witness pulse | The one-off Unease rise awake passengers within 2 rows take when an incident is raised or missed nearby; it is under the contagion switch. |
| Zone | A set of rows and stations (door, lav, galley) whose crew answer its call buttons, walk its check-ins and resolve its events first. |
| Zone lead | The first crew member listed for a zone, who resolves an event nobody noticed by the notice cap. |
