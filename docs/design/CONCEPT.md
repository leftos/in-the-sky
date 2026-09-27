# Game Concept

Status: drafted 2026-09-26 by `game-designer` for the concept pass. What the owner decided at kickoff is in [the decisions doc](../plans/2026-09-26-rewrite-decisions.md) and is not reopened here. Every section or table marked **Proposed — owner to rule** is a recommendation awaiting the owner; the open questions in section 8 are the rulings needed. Terms are in the glossary in [docs/README.md](../README.md); terms this doc coins are defined in section 0.

## 0. Terms this doc coins

These need glossary entries once the owner rules on the sections that use them.

| Term | Meaning |
|---|---|
| Lever | Something the player (from M3) or a policy (in M1) sets that changes the conditions the cabin plays out in: a service schedule, a crew zone, the lighting plan. A lever is never an order to one passenger. |
| Moment | A journal record that moved an outcome: its tick, what happened, who was involved, the cause chain behind it, and its effect on each scoring outcome. The report is built from moments. |
| Distress | How far a passenger is from fine at one tick: 0 is fine, 100 is as bad as the sim allows. It is computed from their needs. |
| Experience | A passenger's whole-flight result, 0 to 100, computed from their distress over the flight (section 6). |
| Strain | A crew member's accumulated load: time on task without a break, pre-emptions, and fatigue. |
| Cabin ready | The tick at which boarding is complete, bins are closed and every passenger is seated and belted: the part of an on-time door the cabin controls. |
| Rate multiplier | The factor applied to a need's base rate, composed from all active modifiers by the rule in section 4. |

## 1. Player fantasy

You run the cabin, not the people in it. Somewhere up front a pilot (maybe you, in MSFS) is flying the aircraft; back here 180 strangers, six crew, two lavatories and one galley are about to spend three hours in a tube together, and you decide the conditions they do it in: who works which aisle, when the carts roll and in which direction, when the lights go down, what the crew do when row 23 starts to argue. Then you watch. When it goes right, nobody notices you, which is the point: a quiet cabin, a clean door at the gate, and a report afterwards that tells you which of your calls made the flight and which one person you still let down.

## 2. Pillars

Proposed — owner to rule (sharpened from the kept pillars in section 2 of the decisions doc; the substance is unchanged).

1. **Stage the conditions, never move the actor.** The player sets staffing, schedules, rules and moment-level choices; crew and passengers act on their own. Consequence: the flight must run to completion with nobody watching (M1 proves it, AirlineOps depends on it), and a player command is only ever a journaled input that takes precedence over what the crew would have done.
2. **Decide at the level of a policy or a moment, never per passenger.** A decision either applies to a class of people or situations (a policy) or answers one surfaced event (a moment). Test for any new feature: if the natural UI is a list of 180 rows with a control on each, it fails this pillar.
3. **Stories come from systems touching each other.** Prefer fewer systems with more interactions. A new system earns its place by changing the behaviour of at least two existing ones; a system that only feeds the score is cut. The canonical story: the drinks round fills the aft lav queue forty minutes later, and the queue blocks the cart.
4. **A competent normal flight is quiet.** No alerts, no prompts, no klaxons when the crew have it in hand. Notifications are for exceptions, and a flight that nags the player on its baseline is a design bug, not a tuning note.
5. **Every number on a player surface serves a lever.** Information that cannot change a decision stays on dev surfaces (the inspector, the balance CSV). Dev surfaces and player surfaces are separate and have separate rules.
6. **Every system can be switched off and the flight still runs.** Needs, events, contagion, voice, the small model and the MSFS feed each have an off switch. This serves players who want a light companion to MSFS, and it serves testing: a system that cannot be isolated cannot be tuned.

## 3. Core loop

Proposed — owner to rule.

### A minute of play (M3 onward)

1. **Glance.** The player reads the cabin through the crew-observed view: what crew have seen, ageing and going stale. Staleness is itself information ("nobody has walked the aft cabin in 25 minutes").
2. **Notice.** Something surfaces: an event with choices, an alert that passed its sustain window, or a pattern the player spots on their own (a lav queue building, one crew member always busy).
3. **Decide.** At the level of a policy (move the aft crew member forward, pause the service, hold the lights down) or a moment (pick an event choice, or let the crew handle it).
4. **Watch it land.** Crew claim the resulting work from the task board, walk the aisle, and the cabin responds over the next minutes of sim time. At 1x, most minutes are steps 1 and 4; that is intended, and time warp exists for the stretches where nothing needs the player.

### A flight

| Phase | What the cabin is doing | Where the player's decisions sit (M3 onward) | Outcome most at stake |
|---|---|---|---|
| Before boarding | Crew brief, galley loaded | Staffing and zone assignment from the roster, service plan | All of them, set in advance |
| Boarding | Passengers find seats, bins fill, aisle blocks | Boarding order policy, when to call bins full and gate-check | On-time doors (cabin ready) |
| Taxi, takeoff, climb | Belted, seatbelt sign on, Unease peaks for nervous flyers | Announcement policy, which crew check on whom after the sign goes off | Experience (nervous flyers' peak) |
| Cruise service | Carts in the aisle, call buttons, first lav wave | Service order and direction, when to pause for turbulence, call-button priority | Experience spread, crew strain |
| Quiet cruise | Lights down, sleepers, lav traffic, events | Lighting plan, check-in cadence, event choices | Incidents handled or missed |
| Descent, landing | Cabin secure, last lav rush, collection | When to call cabin secure, last service cutoff | Incidents, experience (the end of it) |
| Deboarding | Aisle stands up at once, bins empty | Deboarding order, assistance passengers | On-time doors (deboard time), experience end |
| Report | Score and moments | Read it; carry lessons into the next flight's staffing and policies | |

### M1

M1 has **no player commands**. Every lever holds a fixed default policy from the scenario file, and every event is auto-resolved by crew, weighted by competence, traits and fatigue. The loop that exists in M1 is the developer's: run a seed, read the report, change a scenario, run a sweep, compare the CSVs. M1 must still prove that the levers matter, by changing them in scenario files (section 7).

## 4. The need set and its math

**Ruled (owner, 2026-09-26): option A, five needs each tied to a lever** (section 8, question 1). Options B and C are kept below as the record of what was weighed; the floor under the rate rule is still open (question 2).

### What is shared by every option

- **Scale.** Each need is 0 to 100, where 0 is fine and 100 is at its worst, so there are no inverted "positive needs" (OpenPax mixed both directions, `Need.cs:124-137`). Rates are per sim hour.
- **Mood is an output, not a need.** Distress at a tick is a weighted combination of the needs above their comfort thresholds; nothing reads mood as an input except contagion (section 8, question 7).
- **The rate-modifier rule (kept, decided).** Every modifier has a source class. Modifiers inside one class multiply; classes add as deltas around 1.0; the total is capped at 2.5x:

  `m = clamp(1 + Σ_class (Π_i m_i − 1), floor, 2.5)`

  Source classes: Trait, Context (seat, cabin temperature, neighbour), Cascade (another need), Service (items, a meal in progress), Event, and Phase (flight phase, turbulence). Example, Unease for an anxious passenger who is afraid of flying, in turbulence, while tired: Trait 1.3 × 1.5 = 1.95 (+0.95), Phase 1.4 (+0.40), Cascade 1.2 (+0.20), total 2.55, capped to 2.5.
- **The floor is new and needs a ruling** (question 2). OpenPax clamps at 0 (`Need.cs:53`), so two different classes each at 0.4 give 1 − 0.6 − 0.6 = −0.2, clamped to 0: the need freezes, which is the April 2026 failure coming back through the cross-class sum.
- **Failure only after a sustained window (kept).** A need at its failure threshold becomes an incident only after it holds there for a window measured in sim time (OpenPax used 75 real seconds, `NeedFailureGate.cs:18`; the working figure here is 10 sim minutes, to tune).

### Option A (recommended): five needs, each tied to a lever

| Need | Base behaviour | Moved by (lever or crew action) | The story it makes |
|---|---|---|---|
| Refreshment (hunger and thirst as one) | Rises, full scale in 4 sim hours | Drinks round (−40), meal (−70), a drink on a call button (−30). Levers: service plan (order, direction, start time), catering load, staffing | Row 30 gets served 50 minutes after row 1 because the cart went front to back |
| Bladder | Rises, full scale in 6 hours; each drink adds +15 spread over the next 30 minutes | A lav visit. Levers: service timing (drinks now means a lav wave later), lav count and state (layout), seatbelt sign (MSFS feed or emulator), cart position (it blocks the aisle) | The lav queue after the drinks round, and the window passenger who climbs over a sleeper |
| Rest | Rises by the passenger's body clock at origin and trip purpose; falls only while asleep | Sleep, which needs dim lights, quiet and no one climbing past. Levers: lighting plan, announcement policy, a "do not wake for service" rule, seat-swap choices when a conflict surfaces as an event | The aisle passenger woken three times by 23A's bladder |
| Unease | Not a clock: pulled toward a trait baseline with a half-life of about 15 sim minutes, pushed up by turbulence, delay, incidents seen nearby and unanswered call buttons | A crew check-in, a captain's announcement, a call button answered. Levers: check-in cadence, announcement policy, crew zone assignment | The nervous flyer whose unanswered call button turns into a panic two rows away |
| Boredom | Rises, full scale in 3 hours; paused while asleep or watching IFE | IFE and WiFi (layout and outage events), a service pass as a distraction. Children at 2x | The toddler in 14C once the IFE fails |

Comfort is **not** a need in option A. It is a Context-class modifier on Rest and Unease, computed from the seat's pitch and width in inches (the physical-unit layouts the owner asked for), cabin temperature, and the neighbour. That turns layout into a lever without adding a meter nobody can move mid-flight.

Health is **not** a need either. A medical situation is an incident raised by a Lua event whose trigger reads needs, traits and conditions (Unease sustained high plus a heart-condition trait, for example).

Cascades, all in the Cascade class: Refreshment above 70 gives Unease 1.1x; Rest above 80 gives Unease 1.2x; Bladder above 70 while the passenger cannot reach a lav (seatbelt sign, queue, blocked aisle) gives Unease 1.3x. Unease is where the other needs converge, which is what makes it the need incidents come from.

**Worst case in play:** too few meters to tell passengers apart. On a long flight Refreshment dominates distress for everyone, traits have fewer places to bite, and the flight's only story is "the meal was late". Merging hunger and thirst also loses the "had a drink but still hungry" state.

### Option B: four core needs plus needs that traits bring

The core four are Refreshment, Bladder, Rest and Unease, which every passenger has. A trait or profession, authored in Lua, can add one personal need from a content library: Boredom (children, teenagers), Connectivity (business travellers who must work: only WiFi moves it), Nicotine (rises with no relief short of gum or landing, so it pumps Unease), Attention (elite status: wants crew visits).

**Worst case in play:** the need set becomes content. Balance is scattered across trait files, the player never learns one small universal set, and each content pack widens what the dev inspector has to show. A trait-borne need with no lever (Nicotine) is a timer that turns into Unease, which is the passive-simulation problem in a new place.

### Option C: OpenPax's nine, redesigned

Hunger, Thirst, Bladder, Comfort, Entertainment, Health, Fatigue, Anxiety, Shopping, rebuilt on the shared scale and rule above.

**Worst case in play:** three meters with no mid-flight lever (Comfort, Health, Shopping) produce numbers without decisions, which is OpenPax's own core problem #1, "passive simulation" (`D:\openpax\Docs\Plans\CabinSim\README.md:35`). Nine needs times two thresholds times the cascade table is the tuning surface the balance-analyst inherits.

## 5. "Policy, not per passenger" against "optimize seat assignments"

**Ruled (owner, 2026-09-26): resolution 2, seating is only ever a moment** (section 8, question 3). The other two resolutions are kept below as the record of what was weighed. OpenPax's CabinSim loop had "Optimize seat assignments" (`CabinSim\README.md:59`); its later Pillar v2 removed the seat-assignment panel and seat-compatibility calculator as per-individual surfaces (`AirlineOps\PHASE_9_TIME_SYSTEM.md:76-89`) without putting anything in their place. Seating is still the strongest pre-flight lever there is, because contagion, lav climbs and group splits all run through it.

1. **Recommended: seating is a set of rules, and the gate applies them.** Before boarding the player orders a short list of seating rules (keep groups together, families forward, assistance passengers near doors, nervous flyers on the aisle, lone business travellers away from children). The manifest generator seats everyone by those rules, in order, and a full flight forces the rules to conflict: that conflict is the decision. The report attributes moments to the rule that caused them ("keep groups together" split three groups because it ranked below "families forward"). Seat-swap requests during boarding are ordinary Lua events on top, which this composes with. **Worst case:** the player finds one dominant ordering and never touches it again, or the effect is diffuse enough that they cannot tell the rules mattered; the attribution in the report is what has to stop that.
2. **Seating is only ever a moment.** There is no pre-flight seating; seat conflicts surface as events ("a couple split across rows 12 and 19: ask 14C to swap?"). **Worst case:** purely reactive, and on a full flight swap prompts become a chore, which is per-passenger control wearing an event's clothes.
3. **Seats are not a lever.** Seats come from the booking side (the manifest generator now, the booking market later) and the player's levers are crew and service. The layout editor (M6) becomes the only seat-level lever. **Worst case:** the pre-flight phase has almost nothing to decide, and contagion and lav climbs become things that happen to the player rather than things they staged.

M1 seats with a fixed default rule order from the scenario file under any of these.

## 6. Scoring

Proposed — owner to rule. The four outcomes are decided; what each measures is proposed here.

### Passenger experience (spread, not average)

Each passenger's experience uses the peak-end rule: people remember a flight by its worst stretch and by how it ended, not by its average.

`experience = 100 − (0.4 × peak + 0.3 × end + 0.3 × mean)`, where peak is the passenger's highest distress held for at least 5 sim minutes, end is their mean distress over the last 20 minutes before they leave the aircraft, and mean is their mean distress over the flight.

The flight's measure is the **10th percentile** of experience (how the worst-served tenth fared), shown beside the median and the count of passengers under 40 ("would complain"). A flight where 20 people had a terrible time and 160 were fine must read worse than its average suggests.

Traced by: each passenger's peak is a moment, recorded with its dominant need, the rate-modifier classes that drove it, and the task that was waiting on the board at the time. The report clusters the worst tenth's peaks by cause, zone and time window: "11 of the worst-served 18 peaked in the aft lav queue between 01:40 and 02:05, after the drinks round; the cart was in the aisle for 9 of those minutes."

### Incidents handled or missed

An incident is a failure that passed its sustain window (an accident, a panic, a fight) or an event that fired. It is **handled** when crew resolve it before its consequence fires or its escalation deadline passes, and **missed** otherwise. The measure is handled over total, with the median time from incident to crew arrival; every missed incident is listed by name.

Traced by: the incident and its resolution are each moments; a missed one carries why ("both aft crew were on the cart; the task waited 14 minutes behind a lower-priority service task").

### Crew strain

Each crew member accumulates strain from time on task without a break, pre-emptions, and fatigue. The measure is **the peak strain of the most-strained crew member** and the minutes any crew member spent over the redline, not a team average, because one crew member run into the ground is the story and the risk.

Traced by: the strain curve per crew member, with the moments that crossed the redline (a double pre-emption, a service run with no galley break).

### On-time doors

Measured as what the cabin controls, not what the pilot or ATC controls: **cabin ready** against its planned time at the departure end, and deboarding duration (door open to last passenger off) against a target set from the layout and load at the arrival end. Both are minutes late, 0 when on time.

Traced by: the causes of lateness as moments (the last passenger to sit and why: a late passenger, bins full at row 28, a lav queue during boarding).

### Rolling them up

The recommendation is no single grade: the report shows the four outcomes side by side, each with a verdict word (Smooth, Rough, Bad), so the trade between them stays visible (pushing crew to serve everyone costs strain). The options are in question 6.

### The report

In M1 the report is text from `Sky.Sim`: flight header, the four outcomes with their measures, and under each the top moments that moved it (five at most), each with the tick it happened at so it can be found in the journal. A replay of the same seed and journal gives the same report.

## 7. M1 definition of done

Proposed — owner to rule. Numbers are first values, set with the balance-analyst.

The flight
- [ ] `Sky.Sim` runs one narrowbody flight from a seed with no Godot: about 180 passengers from the seeded manifest generator, 6 crew, boarding through deboarding, and exits 0 with a text report.
- [ ] Every stage is entered in order, and a stage's `Start` runs even when several stages are crossed in one tick.
- [ ] Every passenger who boards is seated by cabin ready and off the aircraft at the end; none ends on a node outside the nav graph.
- [ ] Node capacity and reservations are never exceeded on any tick, and passing in the aisle only happens through the squeeze rule.

Needs, decisions and crew
- [ ] Every need stays in 0 to 100, and every rate multiplier stays between the floor and 2.5, on every tick of every fuzzed seed.
- [ ] A passenger decision records its top candidate scores, and a dump of them is readable for any passenger and tick.
- [ ] Crew claim work from the task board; a higher-priority task pre-empts a lower one and the pre-empted task's cleanup runs.
- [ ] No task waits on the board longer than a set limit while any crew member able to do it is idle.

Events
- [ ] At least three new Lua events fire across a 500-seed sweep, and every one is auto-resolved by crew.
- [ ] Across the sweep, crew with low competence or high fatigue pick an event's worse choice measurably more often than competent, rested crew.
- [ ] An event that throws is disabled for that flight only, and the flight finishes.

Replay, fuzz and speed
- [ ] Replaying a seed and its journal gives the same end-state hash and the same report text.
- [ ] Invariant fuzzing over 500 seeds passes with no exceptions and no invariant failure.
- [ ] A performance test pins 64x at full cabin: 256 ticks per wall-clock second at 250 ms ticks (or the spike's tick size).

The report and the levers
- [ ] The report shows all four outcomes with their measures, and every moment it cites exists in the journal at the tick it names (a test checks this).
- [ ] The baseline scenario is quiet: on at least 90% of seeds, no incident is missed.
- [ ] The levers matter: across 200 seeds each, changing one lever in the scenario moves its need the expected way (service plan against Refreshment, one lav locked against Bladder and the experience 10th percentile, lighting plan against Rest, 4 crew instead of 6 against strain and missed incidents).
- [ ] The balance CSV has one row per seed with the four outcome measures, ready for the balance-analyst.
- [ ] The Session produces both the crew-observed and the true view, and a test shows the observed view going stale where no crew member has looked.

## 8. Open questions for the owner

Each option is ranked; the first is the recommendation.

1. **Which need set does In the Sky use?** **Ruled (owner, 2026-09-26): option 1, five needs, each tied to a lever.**
   1. Five needs, each tied to a lever: Refreshment, Bladder, Rest, Unease, Boredom; comfort becomes a seat-and-cabin modifier and health becomes an event-raised incident. Worst case: passengers are harder to tell apart, and on long flights Refreshment dominates everyone's distress.
   2. Four core needs plus needs that traits bring (Boredom, Connectivity, Nicotine, Attention), authored in Lua. Worst case: balance is scattered across trait content and some trait-borne needs have no lever.
   3. OpenPax's nine, redesigned on the new scale. Worst case: Comfort, Health and Shopping are meters nobody can move mid-flight, which is the passive simulation OpenPax set out to fix.

2. **Does the kept rate-modifier rule get a floor under it?**
   1. A floor of 0.2x: no combination of modifiers can slow a need below a fifth of its base rate. Worst case: a passenger who should be "fully looked after" still drifts, so perfect service is impossible by a small margin.
   2. No floor, 0 as in OpenPax. Worst case: two helpful modifiers from different classes freeze a need, the April 2026 bug through the cross-class sum.
   3. A floor per need, set in content. Worst case: one more number per need to tune and explain, for little gain over a single floor.

3. **How does seating become a player lever?** **Ruled (owner, 2026-09-26): option 2, seating only as moments.** There are no pre-flight seating rules; in M1, seat-conflict events are auto-resolved by crew like any other event.
   1. Seating rules the player orders before boarding, applied by the gate; a full flight makes the rules conflict. Worst case: one dominant ordering is found and never touched again.
   2. Seating only as moments: seat conflicts arrive as events with swap choices. Worst case: on a full flight the swap prompts become per-passenger chores.
   3. Seats are not a lever; they come from booking, and the layout editor is the only seat-level tool. Worst case: pre-flight has almost nothing to decide.

4. **How is one passenger's experience computed?**
   1. Peak-end: 40% worst sustained distress, 30% the last 20 minutes, 30% the flight's mean. Worst case: a passenger who suffered a long moderate stretch scores better than one with a short sharp spike, which can read as unfair in the report.
   2. Time-weighted mean distress over the flight. Worst case: a ten-minute panic attack disappears into three hours of fine, so incidents barely touch experience.
   3. Mood at the moment of deboarding only. Worst case: everything before descent stops mattering, and a good landing hides a bad flight.

5. **What is the headline number for the spread of experience?**
   1. The 10th percentile (the worst-served tenth), with the median and the count under 40 beside it. Worst case: sensitive to a handful of passengers the seed made hard, so it is noisier across seeds than a mean.
   2. The count of passengers under a threshold ("would complain"). Worst case: a cliff: a passenger at 41 counts the same as one at 95.
   3. The spread itself (interquartile range). Worst case: an evenly miserable flight scores as well as an evenly happy one.

6. **Does the report roll the four outcomes into one grade?**
   1. No grade: four outcomes side by side, each with a verdict word. Worst case: no single number to compare flights or feed AirlineOps reputation, so a composite has to be designed later anyway.
   2. A weighted composite grade over the four. Worst case: the weights become the game, and the trade between outcomes vanishes into one letter, as OpenPax's six weighted categories did.
   3. Gate then rank: any missed serious incident caps the flight's verdict, otherwise experience ranks it. Worst case: one unlucky incident on a seed flattens an otherwise excellent flight.

7. **Does mood spread between neighbours in M1?** **Ruled (owner, 2026-09-26): option 1, only Unease spreads.**
   1. Yes, only Unease spreads, to adjacent seats and across the aisle, as an Event-class modifier. Worst case: a single panicking passenger can tip a row, which may look like an avalanche before it is tuned.
   2. No contagion in M1; add it with the player levers in M3. Worst case: seating has no effect in M1, so the seating lever cannot be tested until M3.
   3. Full mood contagion across all needs, as in OpenPax. Worst case: feedback loops across the cabin that dominate every other system and are hard to trace in the report.

8. **How is crew strain measured?**
   1. The peak strain of the most-strained crew member, plus minutes over the redline. Worst case: a flight with one bad stretch for one crew member scores as badly as one where the whole crew was run ragged.
   2. The team's mean strain. Worst case: one crew member run into the ground is hidden by five who were fine.
   3. Minutes of backlog on the task board (work waiting with no one to take it). Worst case: it measures under-staffing, not what it cost the crew, so a crew that skips breaks to clear the board looks great.

9. **What does "on-time doors" measure?**
   1. What the cabin controls: cabin ready against plan at departure, deboarding duration against a target at arrival. Worst case: in MSFS mode a player can be "on time" by this measure while the aircraft pushes back late for reasons the cabin cannot see.
   2. Actual door close and door open times from the clock and feed. Worst case: the score blames the cabin for ATC and pilot delays.
   3. Departure end only (cabin ready). Worst case: deboarding, where bins and aisle blocking make the best stories, stops counting.

10. **What is M1's reference flight?** **Ruled (owner, 2026-09-26): option 1, about 2.5 hours, one drinks round and one meal, day departure.**
    1. About 2.5 hours, one drinks round and one meal, day departure. Worst case: long enough that a 500-seed sweep at 64x takes minutes, which slows the balance loop.
    2. About 1 hour, drinks only. Worst case: too short for Rest, Boredom or a lav wave to matter, so half the need set goes untested.
    3. About 5 hours, overnight, two services. Worst case: the sweep takes several times longer, and a night flight's sleep dominates everything else.
