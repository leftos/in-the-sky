# M1 slice explorations, 2026-09-27

The `sky-explore` maps for the slice cut on 2026-09-27 (F4 with the retro OpenPax prior-art check, X2 with X3, X4), stored for the next `/nextup` to brief from without exploring again (owner, 2026-09-27: "Store the exploration results so they can get picked up by /nextup but don't start any implementation work"). All read at main `c7b8604`; re-check a cited line with `rg -n` before a brief quotes it. Each section ends with its open decisions: those marked `(orchestrator)` are settled, the rest go to the decision round. The steps themselves stay in [2026-09-26-m1-headless-cabin-flight.md](./2026-09-26-m1-headless-cabin-flight.md).

## X4: the M1 event modules

**Event Lua API.** A module (events.md section 1, `src/Sky.Scripting/LuaEventScripts.cs` `Load`) is a chunk returning `id`, `phases` (stage names), `trigger(ctx)`, `describe(facts)`, `choices(facts)`, `effects(facts, choice_id)`; a bad field disables the module naming it. The port is `IBehaviorScripts` (`src/Sky.Engine/Ports/IBehaviorScripts.cs`): `Trigger`, `Describe`, `Choices`, `Effects`, `Release`, `EventIds`, `IsEventDisabled`. Records are in `EventRecords.cs`; `LuaEventContext.cs` (internal) is readable only during `trigger`. `EventFacts.Handle` keys the Lua facts table held privately in `LuaEventScripts`.

**Consequences.** `ConsequenceReader.cs` reads each `effects` entry: exactly one of `need`, `incident`, `swap`, `to_seat`, `line` decides the kind; `after_minutes` rounds half away from zero to ticks; `incident` and `to_seat` take one passenger, never `neighbours`; a swap's two entries resolve to different passengers.

**Phase gate.** Two layers: `LuaEventScripts.Trigger` checks disabled and `phases` before calling Lua, and each module's own `trigger` checks `ctx.stage` itself, which `ShippedEventTests` proves by calling `trigger` directly.

**The M1 events** (events.md section 10), each with the crew-free `leave` that load enforces:

| Event | Phases | Choices (crew, minutes) | Effects |
|---|---|---|---|
| `split-group` | boarding | `swap` (y, 3), `pair` (y, 3), `leave` (n, 0) | seat swap or move, Unease steps; `leave` a line and Unease, no incident |
| `nervous-flyer` | taxi-out, climb | `sit` (y, 2), `companion` (n, 0), `leave` (n, 0) | Unease to subject, neighbours, companion; `leave` raises `panic` when `high` |
| `armrest-dispute` | cruise | `calm` (y, 3), `drink` (y, 2), `reseat` (y, 4), `leave` (n, 0) | seat move, Refreshment, Bladder and Unease pairs; `leave` raises `fight` when `fierce` |
| `restless-child` | cruise | `pack` (y, 1), `play` (y, 4), `leave` (n, 0) | Boredom and Unease to subject, parent, the seat in front; never an incident |

No spec's effects fall outside the parser. The only per-module care is Lua-side (a nil `facts.front` guarded before a consequence names it). A two-passenger step is one `NeedConsequence` per passenger, as the X4 line says.

**Gap: the test project cannot see the shipped events.** `ShippedContentTests` finds `Data/` under `AppContext.BaseDirectory` because `Sky.Content.csproj` copies `Data\**` to every referencing project's output (ARCHITECTURE.md). `tests/Sky.Scripting.Tests/Sky.Scripting.Tests.csproj` references only `Sky.Scripting`, so X4's first step adds a `Sky.Content` project reference to the test csproj alone. `tests/Sky.Scripting.Tests/ReferenceTests.cs` pins the references of the compiled `Sky.Scripting.dll`, not of the test assembly, so ADR 0001 and the architecture table are untouched.

**Brief split.**

1. Implementer, test first: `tests/Sky.Scripting.Tests/Sky.Scripting.Tests.csproj` (the reference) and `tests/Sky.Scripting.Tests/ShippedEventTests.cs`. Tests: every `.lua` under the shipped `Data/events/` (id = file name) loads with no disabled reason; each `trigger`, called directly on a raw `LuaHost` and `LuaEventContext` (internal, visible through `InternalsVisibleTo`), returns nil for every stage outside the module's `phases`; every offered choice's `effects` parses with no disablement; every `Passenger(id)` target a branch returns is among the numeric values of that trigger's facts. The suite must run green on an empty `Data/events/` too (the modules come in step 2); step 1 proves itself on fixture modules under the test project. Red proof: a fixture whose `trigger` skips its own stage check turns the phase test red; a fixture returning a target id absent from its facts turns the membership test red.
2. `event-writer`: `src/Sky.Content/Data/events/split-group.lua`, `nervous-flyer.lua`, `armrest-dispute.lua`, `restless-child.lua`, each checking `ctx.stage` in `trigger`, drawing only in `trigger` and `effects`, never reading `ctx` outside `trigger` (events.md section 6). Proves: `pwsh sky.ps1 test -Project Scripting -Filter "*ShippedEventTests"` green.

**Prior art (OpenPax).** OpenPax events (`D:\openpax\Library\Events\*.lua`) called imperative host functions (`choice`, `improve`, `worsen`) that built C# consequences (`Code\Flights\Events\Lua\LuaEventApi.cs`), not returned tables. Two fixed bugs bear on X4:

- `a3712204` removed an engine gate (`Stage is Cruise or Descent`) that silently dead-coded every trigger's own phase check outside those stages. Covered: the module owns its phase check (events.md section 1), and step 1's direct-call test is the regression test OpenPax lacked.
- `c6a5789f` fixed triggers with inverted conditions (firing on `Comfort > 60` for `< 60`) and branches with no reachable path. The `leave` rule and the parser catch shape bugs, not a wrong-direction threshold in a trigger's Lua. Not covered by any planned test: a new plan line for event-writer (below).

**Depends on.** Nothing open: X4 consumes S3's landed API. F6 and H1 consume X4's modules.

**Open decisions.**

- How `ShippedEventTests` reads a trigger's raw facts for the membership check: (orchestrator) a raw `LuaHost` harness in the test, calling the module's functions and handing the returned tables to `ConsequenceReader`, with no new accessor on `LuaEventScripts`: a production surface added only for a test is the larger change.
- Whether each shipped trigger's conditions are checked against events.md's stated hard and soft conditions (the OpenPax polarity bug): not asked by X4's proving line; booked as a plan line under X4 for event-writer to decide the check (a fixed-facts table per event: fires on the stated condition, stays silent on its opposite).

## F4 and the retro prior-art check

### Where each piece goes

- **Decision points and cadence.** Called from `FlightWorld.Step` and `TickNeeds` (`src/Sky.Engine/Flight/FlightWorld.cs`). Cadence: 2 sim minutes (480 ticks), staggered by passenger id mod 480 (`balance.md` section 3.2, line 462). "Activity ends" is the point the executor already exposes: `SequenceExecutor.TickCharacter` clears the character's current action to null on `Done` (`src/Sky.Engine/Execution/SequenceExecutor.cs`).
- **Scoring.** `IBehaviorScripts.ScoreActivities(in PassengerFacts, ReadOnlySpan<ActivityId>, Span<double>)`, facts built per call from `Passenger`. `FakeBehaviorScripts` (tests) and `LuaBehaviorScripts` (wave S) both implement it; F4 builds neither.
- **Keep-current bias.** A bonus on the current activity's score before comparing (glossary, `docs/README.md`); no magnitude anywhere (`balance.md` 2.5 to 2.8 and 3.2 checked).
- **Activities as executor actions.** Reuse `Movement`'s `Walk`, `Hold`, `Cross` and the generic `ActionChain` at `Movement.ActionPriority` (`Movement.cs`) for the lav visit (walk, queue, use, return). `sleep`, `screen`, `chat`, `stretch` are new small `CharacterAction`s (a drawn duration, need effects). `drink_served` and `meal_served` are offered by crew (F5); F4 needs only their accept or decline rule (passengers.md line 84).
- **Cascades and contagion.** `Cascades.AppendModifiers(NeedSet, Need, bool lavUnreachable, Span<RateModifier>)` (`src/Sky.Engine/Needs/Cascades.cs`) is called nowhere yet: `FlightWorld.ComposeMultipliers` composes only the Trait and Context classes. F4 adds the Cascade class call and a new `Contagion.cs` for the Event-class modifier from seat neighbours (passengers.md lines 153-159: same seat group w = 0.25, across the aisle w = 0.15, above Unease 40; the off-duty crew calming source w_calm 0.15 and 0.10; witness pulses +8 and +15). No neighbour lookup exists on `CabinLayout` or `NavGraph`; F4 builds one from `CabinLayout.Rows[].Groups[].Seats`.
- **`DecisionLog` and `Dump`.** New (R16: about 2,700 decisions a flight, all kept).

### The move-interrupt hazard against the executor

`SequenceExecutor.TryStart` swaps only for a strictly higher priority; `Replace` swaps unconditionally for a caller that has decided the swap (as R34 gives the task board). Every movement action runs at `Movement.ActionPriority = 10`, and each `Cleanup` leaves the character `Standing` at its last fully reserved node, never mid-hop.

- **Option 1: activities never interrupt a move.** Decision points skip a passenger whose `Movement.StateOf` is `Walking`, `Stowing`, `Retrieving`, `Queued` or `Absent`; activity-to-activity switches use `Replace`, arbitrated by the decision loop (the new score beat the current one plus its bias), so activities need no priority relative to 10. No resume path, no new machinery.
- **Option 2: a resume path.** Activities above 10 cut into a move, and the mover records the target to re-issue later. Buys nothing the docs ask for (passengers.md's Bladder-85 case is the `lav_visit` activity, started from standing or seated), and adds a failure mode: a resumed move interrupted again.

### Numbers

Present in D3: contagion weights and threshold, witness pulses, the cadence, activity durations and thresholds, movement ticks, lav pick cost and overflow, need-failure thresholds (`balance.md` 2.5 to 2.8, 2.12, 2.13, 3.2). Missing: the keep-current bias, and what makes a lav "unreachable" for `Cascades` (queue length, path cost or the overflow state; `Cascades.cs` takes a bool).

### Brief split

F4 is at least F3a (91 calls) and F3b (104) in scope. Split, as F3 was:

- **F4a, the decision core:** the cadence and activity-end points with the move gate (Option 1), scoring with the keep-current bias, `DecisionLog` and `Dump`. Files: `Passengers/Passenger.cs`, `Passengers/DecisionLog.cs`, `FlightWorld.cs` (the call site), `PassengerDecisionTests.cs`, `DecisionLogTests.cs`.
- **F4b, activities, cascades and contagion:** the activity actions, the Cascade class wired into `ComposeMultipliers`, `Contagion.cs` with the neighbour table. Files: `Passengers/Activities.cs`, `Passengers/Contagion.cs`, `FlightWorld.cs`, an activity test file and `ContagionTests.cs`. Needs F4a.

### Prior art (OpenPax), the retro check of F3a and F3b included

- **Movement against decisions: a constraint for F4a.** `D:\openpax\Code\Tests\Characters\PassengerRestroomDuringBoardingTests.cs` pins that the bladder release opens only once a passenger is seated, never while still walking to the seat: "a passenger still walking to their seat has nowhere to return to and no boarding-side queue logic to handle them." The same answer as Option 1. F4a carries it as a test: a passenger whose cadence tick falls mid-boarding walk starts no activity until seated, and a deboarding row is never frozen by a decision.
- **State leaking past deboarding: a constraint for F4a.** `5fbdab69` fixed `NeedCascadeManager` dictionaries growing because deboarded passengers were never cleared. Here per-passenger state is arrays indexed by manifest id, so the leak cannot recur; `DecisionLog` follows the same shape (a per-passenger list, never a dictionary keyed by tick).
- **Lav queues: a test for F4b.** `b1c38cec` rewrote OpenPax's restroom sequence (320 lines, a new `Restroom.cs`) after launch. Here the queue is `Occupancy`'s reservations (B3) and passengers.md line 94 gives the overflow escape (8 minutes waiting under Bladder 80, back to the seat). F4b carries a test that a full lav queue deadlocks no walker and that overflow sends the waiter back.
- **Rate stacking: covered.** `cb04d9d4` (unbounded multiplicative stacking) is `RateMultiplier.Compose` (C1, ruling R15).
- **Blocking scans: covered.** `6615f5cf` fixed an O(n) roster scan per mover; `Movement.IsBlocked` reads one node's holders through `Occupancy.HolderAt`.
- **Pathfinder loops: covered.** `2d5ff0ce` and `5d3bde63` capped OpenPax's free-grid A*; here `PathTable` is precomputed at load (B2), with no runtime search.

The retro check finds nothing in F3a or F3b as landed that needs a new line: each OpenPax movement lesson is covered or carried into F4a and F4b above.

### Depends on

F4 needs F3a, F3b and C3 (landed), and runs after the in-flight F3b follow-up fix (`Movement.HoldAction`), which edits `Movement.cs`. It does not need X3: its tests use `FakeBehaviorScripts`, and X3's module list follows F4's `ActivityId` set and D1. F5 and F6 consume F4.

### Open decisions

1. The move-interrupt option: (orchestrator) Option 1. An engine mechanism, the docs describe no activity cutting into a move, and OpenPax converged on the same gate.
2. The keep-current bias magnitude: balance-analyst sets it in `balance.md` section 3.2 before F4a's brief.
3. What makes a lav unreachable for `Cascades`: a design question for game-designer (passengers.md), the number then balance-analyst's; F4b's brief waits on it.
4. The neighbour lookup: (orchestrator) a per-seat neighbour table built once from `CabinLayout` at `FlightWorld` construction, in `Contagion.cs`.
5. `DecisionLog` retention: (orchestrator) every decision kept (R16), a per-passenger list of fixed-size records, no cap in M1.
6. The split: (orchestrator) F4a and F4b as above.

## X2 and X3: crew, scenarios and activity modules

### X2

**Schema.** `src/Sky.Content/Schema/CrewSchema.cs`: `CrewFile(Traits, Roster, Fatigue, Service)`, `CrewMemberSpec(Id, Competence, Empathy, StartingFatigue, Trait?)`, `CrewTraitSpec` (id and seven factors defaulting to 1.0: `FocusFatigueWeightFactor`, `FocusFactorOverRedline`, `PreemptionStrainFactor`, `OnTaskStrainFactor`, `ServiceTimeFactor`, `UneaseReliefFactor`, `DwellFactor`), `FatigueSpec`, `ServiceTimingSpec`. `ScenarioSchema.cs`: `ScenarioFile(Id, Layout, GateDelayMinutes, ConcessionsOpen, Systems, ServicePlan, LockedLavs, LightingPlan, Crew, Timeline)`, `CrewAssignment(Count, Zones, Carts)`, `CrewZone(Id, FirstRow, LastRow, Stations, Crew)`, `CartSpan(FirstRow, LastRow, Crew)`. `ScenarioValidator.cs` already checks every K2b rule (roster cross-reference, zone ids unique, no duplicate in a zone, carts inside a zone, two different crew on a cart, one member covering any number of zones).

**Gap: the `floater` field.** crew.md line 82 says the Floater trait's content field is the number of zones it carries free (1); `CrewTraitSpec` has no such field and nothing in `src/` names it. X2 adds it to `CrewSchema.cs`, a file the X2 line does not list.

**Numbers.** All in crew.md and mirrored in `balance.md` 2.16 to 2.22: the roster (lines 167-174), task priorities (109-127), fatigue and service timings (152, 262-276), the reference timeline (308-326), the six- and four-crew zone tables (29-51). Nothing missing.

**Variants.** The X2 line names `service-back-to-front`, `one-lav-locked`, `lights-up`, `four-crew` and a covering variant; plan line 9's T-rulings also give X2 `gate-delay-closed.json` (60 minutes, outlets closed) and `reference.json` at 0 minutes with outlets open.

### X3

**Schema.** `PassengerSchema.cs`: `ActivitiesFile(Activities, CallReasons, Movement)`, `ActivitySpec(Id, Site, NeedsCrew, Duration?, ChildDuration?, Effects[], Pauses[], After?)`, `ActivityEffect(Need, Change, Timing, OverMinutes?, Lighting)`, `CallReasonSpec(Id, Granted[], Refused[])`, `MovementSpec` (seven required fields, passengers.md section 3). Scoring thresholds (Rest 45 or more, Boredom 20 or more, ...) are not schema fields: they live in each module (passengers.md lines 77-84).

**Lua host.** `LuaBehaviorScripts.ScoreActivities(in PassengerFacts, ReadOnlySpan<ActivityId>, Span<double>)`; each module is `utility(facts)`. `PassengerFacts` (`src/Sky.Engine/Ports/PassengerFacts.cs`) matches passengers.md line 63's list one for one. Belongings are read as traits: `facts:has_trait("sleep_kit")`, `has_trait("own_device")`. Budget `LuaHost.StandardInstructionBudget = 1_000_000`; sandbox base, string, table and math; `math.random` inside `utility` throws and disables the module (`ActivityScoringTests.MathRandomInUtilityDisablesTheModule`). House style: events.md section 6's foot (S1b).

**Missing numbers.** The `frequent_flyer` sleep bonus, `sociable` chat bonus and `restless` stretch bonus (passengers.md lines 78, 80, 82; `balance.md` 2.6 and 2.9 paraphrase them) have no magnitude anywhere. `demanding` and `patient` (±15) are the only hooks with one.

**Gap: the test project cannot see the shipped modules.** As for X4: `tests/Sky.Scripting.Tests/Sky.Scripting.Tests.csproj` references only `Sky.Scripting`, and `Data/**` reaches only projects referencing `Sky.Content`.

### Fixtures: where X2 and X3 meet

Both drop fixtures from the same dictionary, `ContentTree.MinimalFiles` in `tests/Sky.Content.Tests/ContentTree.cs` (lines 203-215): X2 drops `Crew` and `Scenario` (`layouts/tiny.json` stays for other tests), X3 drops `Activities`, `IdleModule`, `SleepModule`. `ShippedContentTests.cs` needs no edit for the fill to drop: `HybridTree.FillUnshippedKinds` fills by file presence. X2 still extends `ShippedContentTests` with the per-variant check its line names.

### Prior art (OpenPax)

OpenPax had no data-driven crew roster, scenarios or activities: crew were C# (`Code/Characters/CrewMember.cs`), configuration `Code/Flights/FlightConfiguration.cs` and `FlightPlan.cs`, activities hard-coded `Behavior` classes (`Code/Characters/Behaviors/*.cs`); only events were Lua. Its fixed bugs here are covered: `57516fba` (the sandbox and poison-tracking of one bad module) is `LuaSandbox.cs` and `LuaHost.IsDisabled`; `cb04d9d4` (unbounded multiplicative stacking across traits, cascades and outages) is `RateMultiplier.Compose`'s per-class multiply, cross-class add and 0.2 to 2.5 cap (C1). Lua activity scoring is new in this rewrite, with no history to draw on.

### X1's review observations, by owner

- `woken_up`'s lever tag: a passengers.md section 10 question, game-designer.
- `late_and_fed_up`'s null `lasts_minutes`: F8's to read (the content is legal).
- `manifest.json`'s `business_row_count`: a content and code fix (derive from the layout), orchestrator; not X2 or X3.
- Trait behaviour hooks with no `TraitSpec` field: X3's modules carry them, no schema change.
- `contagion.witness_rows` unsourced: balance-analyst.
- `ManifestGeneratorTests.ReferenceLayout()` sharing the shipped layout's id: a test fix, not X2 or X3.

### Depends on

F4 consumes X3's activity ids and the `call_crew` shape (decision 4). F5 reads the Floater field X2 adds. H1 maps both into the Engine. X3 and X4 both add the `Sky.Content` reference to the Scripting test csproj: whichever lands second drops that edit.

### Brief split

Two briefs, both editing `ContentTree.cs` in different entries, so they run one after the other or the second is cut from the first's landing:

- **X2:** `src/Sky.Content/Schema/CrewSchema.cs` (the Floater field), `Data/crew.json`, `Data/scenarios/` (`reference`, `service-back-to-front`, `one-lav-locked`, `lights-up`, `four-crew`, `covering`, `gate-delay-closed`), `tests/Sky.Content.Tests/ContentTree.cs`, `tests/Sky.Content.Tests/ShippedContentTests.cs`, and a validator test for the new field.
- **X3:** `Data/activities.json`, `Data/activities/*.lua` (one per D1 activity), `tests/Sky.Content.Tests/ContentTree.cs`, `tests/Sky.Scripting.Tests/ShippedActivityTests.cs`, `tests/Sky.Scripting.Tests/Sky.Scripting.Tests.csproj`. Dispatch only after decisions 4 and 5 are settled: they change the module count and numbers.

### Open decisions

1. `gate-delay-closed.json` in X2: (orchestrator) yes. Plan line 9 records the owner's triage giving it to X2, and the X2 line's omission is the plan's own inconsistency; the X2 line is amended to list it.
2. The Floater field: (orchestrator) `CrewTraitSpec.FreeZones`, an int defaulting to 0, validated 0 or more; `floater` sets 1 (crew.md line 82).
3. How `ShippedActivityTests` reaches the modules: (orchestrator) a `Sky.Content` project reference in the Scripting test csproj alone, loading through `ContentLoader`, the same route as X4; `ReferenceTests` pins the compiled `Sky.Scripting.dll`, so ADR 0001 is untouched.
4. How `call_crew` names its reason: one module scoring `call_crew` with the reason chosen by the Engine (F4), or one activity id per reason (`call_refreshment`, `call_reassurance`, `call_lav_permission`) against passengers.md's single `call_crew` row. A design question for game-designer first, then the owner's round if the designer does not settle it from the doc.
5. The `frequent_flyer`, `sociable` and `restless` bonus magnitudes: balance-analyst sets them in `balance.md` before X3's brief.
