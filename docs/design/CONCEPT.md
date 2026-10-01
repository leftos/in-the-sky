# Game Concept

Status: ruled by the owner in the concept pass; drafted by `game-designer`. Amended by the owner's triage of the OpenPax goals ([the AVSIM goals research](../research/2026-09-26-openpax-avsim-goals.md)), whose rulings T1 to T13 are in section 8 and whose deferred work is in section 9. What the owner decided at kickoff is in [the decisions doc](../decisions/rewrite-decisions.md) and is not reopened here. Section 8 records each ruling; options not taken are kept as the record of what was weighed. Terms are in the glossary in [docs/README.md](../README.md).

## 0. Terms

The terms this doc coins (lever, moment, distress, experience, strain, cabin ready, rate multiplier, preset, thought, crewless flight, smoothness, resource budget) are defined in the glossary in [docs/README.md](../README.md).

## 1. Player fantasy

You run the cabin, not the people in it. Somewhere up front a pilot (maybe you, in MSFS) is flying the aircraft; back here 180 strangers, six crew, two lavatories and one galley are about to spend three hours in a tube together, and you decide the conditions they do it in: who works which aisle, when the carts roll and in which direction, when the lights go down, what the crew do when row 23 starts to argue. Then you watch. When it goes right, nobody notices you, which is the point: a quiet cabin, a clean door at the gate, and a report afterwards that tells you which of your calls made the flight and which one person you still let down.

In the Sky runs beside MSFS or on its own with no simulator. The sim feed port (`ISimFeed`) is public: MSFS is the one adapter this project builds, and community adapters for other simulators (X-Plane, Prepar3D and the rest) are welcome (T6).

## 2. Pillars

Ruled: accepted as drafted (sharpened from the kept pillars in section 2 of the decisions doc; the substance is unchanged).

1. **Stage the conditions, never move the actor.** The player sets staffing, schedules, rules and moment-level choices; crew and passengers act on their own. Consequence: the flight must run to completion with nobody watching (M1 proves it, AirlineOps depends on it), and a player command is only ever a journaled input that takes precedence over what the crew would have done.
2. **Decide at the level of a policy or a moment, never per passenger.** A decision either applies to a class of people or situations (a policy) or answers one surfaced event (a moment). Test for any new feature: if the natural UI is a list of 180 rows with a control on each, it fails this pillar.
3. **Stories come from systems touching each other.** Prefer fewer systems with more interactions. A new system earns its place by changing the behaviour of at least two existing ones; a system that only feeds the score is cut. The canonical story: the drinks round fills the aft lav queue forty minutes later, and the queue blocks the cart.
4. **A competent normal flight is quiet.** No alerts, no prompts, no klaxons when the crew have it in hand. Notifications are for exceptions, and a flight that nags the player on its baseline is a design bug, not a tuning note.
5. **Every number on a player surface serves a lever.** Information that cannot change a decision stays on dev surfaces (the inspector, the balance CSV). Dev surfaces and player surfaces are separate and have separate rules.
6. **Every system can be switched off and the flight still runs.** Needs, events, contagion, voice, the small model and the MSFS feed each have an off switch. This serves players who want a light companion to MSFS, and it serves testing: a system that cannot be isolated cannot be tuned. Thoughts (section 3) have a switch too.

   **A resource budget beside MSFS (T1).** In the Sky shares a machine with a flight simulator, so it has a memory and CPU budget and stays inside it. Working figures, to check with the client at M2 and to measure beside MSFS at M5: with voice off, the sim and client together stay under 1 GB of RAM and 512 MB of VRAM, the simulation at 1x uses under 5% of one CPU core, and the client can cap its frame rate. The bundled voice model's ceiling is set at M4, when the model is chosen, and measured in the sim at M5; switching voice off returns a player to the lean budget. A feature that cannot fit the budget ships behind its own switch, off by default.

## 3. Core loop

Ruled: accepted as drafted.

### A minute of play (M3 onward)

1. **Glance.** The player reads the cabin through the crew-observed view: what crew have seen, ageing and going stale, including the passenger thoughts crew have heard (below). Staleness is itself information ("nobody has walked the aft cabin in 25 minutes").
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
| Quiet cruise | Between and after the rounds: lights down in two windows (after the drinks, and after the meal until descent), sleepers, lav traffic, events | Lighting plan, check-in cadence, event choices | Incidents handled or missed |
| Descent, landing | Cabin secure, last lav rush, collection | When to call cabin secure, last service cutoff | Incidents, experience (the end of it) |
| Deboarding | Aisle stands up at once, bins empty | Deboarding order, assistance passengers | On-time doors (deboard time), experience end |
| Report | Score and moments | Read it; carry lessons into the next flight's staffing and policies | |

### Ways to play: presets of the one role (M3)

Ruled (T2): there is one role, the stage manager, and the ways to play OpenPax promised ("play as the Captain with everything automated, or as the Lead Flight Attendant, and every experience in between") are **presets** of it, not modes. A preset is a saved, shareable set of lever values: the service plan, zones and staffing, the lighting plan, announcement and check-in policy, and which kinds of moment reach the player and which the crew auto-resolve without asking. A Captain preset leaves every moment to crew and plays the flight hands-off; a Lead Flight Attendant preset surfaces the moments and leaves the levers to the player. A preset is a data file a player can share, and loading one is the flight's starting lever values in the journal; it adds no mechanic the levers do not already have. A preset covers moments as well as levers. Built at M3 with the levers.

### Passenger thoughts (engine M1, drawn M2, on the HUD M3)

Ruled (T12, overriding the recommendation to drop them): passengers have **thoughts**, for the RollerCoaster Tycoon and Planet Coaster feel of a cabin you can overhear. A thought is what one passenger is thinking for a while, raised by what just happened to them (a long lav queue, a drink that came late, being woken, turbulence, a call nobody answered, a call answered at once), and every kind of thought points at the lever or moment that would change it. Thoughts are **fog-of-war**: the observed view shows a passenger's thought only once a crew interaction has revealed it (every interaction does: a round's pass, an answered call, an incident reached, an event's crew task, a check-in walk or stop), and it ages like every other observation. The fog is thin during service, when the carts hear nearly everyone, and thick in the quiet stretches between rounds, where the check-in cadence decides whether the player hears the passengers who are suffering without calling. Thoughts are information only: nothing in the sim reads them, switching them off changes no outcome, and they never alert (pillar 4). The player reads them grouped by kind and zone, never as 180 rows (pillar 2). The design is [passengers.md section 10](./passengers.md).

### M1

M1 has **no player commands**. Every lever holds a fixed default policy from the scenario file, and every event is auto-resolved by crew, weighted by competence, traits and fatigue. The loop that exists in M1 is the developer's: run a seed, read the report, change a scenario, run a sweep, compare the CSVs. M1 must still prove that the levers matter, by changing them in scenario files (section 7).

## 4. The need set and its math

**Ruled: option A, five needs each tied to a lever** (section 8, question 1). Options B and C are kept below as the record of what was weighed; the floor under the rate rule is still open (question 2).

### What is shared by every option

- **Scale.** Each need is 0 to 100, where 0 is fine and 100 is at its worst, so there are no inverted "positive needs" (OpenPax mixed both directions, `Need.cs:124-137`). Rates are per sim hour.
- **Mood is an output, not a need.** Distress at a tick is a weighted combination of the needs above their comfort thresholds; nothing reads mood as an input except contagion (section 8, question 7).
- **The rate-modifier rule (kept, decided).** Every modifier has a source class. Modifiers inside one class multiply; classes add as deltas around 1.0; the total is capped at 2.5x:

  `m = clamp(1 + Σ_class (Π_i m_i − 1), floor, 2.5)`

  Source classes: Trait, Context (seat, cabin temperature, neighbour), Cascade (another need), Service (items, a meal in progress), Event, and Phase (flight phase, turbulence). Example, Unease for an anxious passenger who is afraid of flying, in turbulence, while tired: Trait 1.3 × 1.5 = 1.95 (+0.95), Phase 1.4 (+0.40), Cascade 1.2 (+0.20), total 2.55, capped to 2.5.
- **The floor is new and needs a ruling** (question 2). OpenPax clamps at 0 (`Need.cs:53`), so two different classes each at 0.4 give 1 − 0.6 − 0.6 = −0.2, clamped to 0: the need freezes, which is the April 2026 failure coming back through the cross-class sum.
- **Failure only after a sustained window (kept).** A need at its failure threshold becomes an incident only after it holds there for a window measured in sim time (OpenPax used 75 real seconds, `NeedFailureGate.cs:18`; the working figure here is 10 sim minutes, to tune).

### Option A (ruled): five needs, each tied to a lever

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

### What passengers bring aboard

Ruled in the triage of the OpenPax goals; the numbers and rules are in [passengers.md](./passengers.md), all first values for balance-analyst.

- **Starting needs from the gate (T11, from M1).** Two scenario fields describe what passengers went through before boarding: `gate_delay_minutes`, how long they waited past the scheduled boarding time, and `concessions_open`, whether the airside food and drink outlets were open while they waited. A long wait with the outlets closed boards a hungrier, more bored and more uneasy cabin, and a long delay makes Unease rise faster until the passenger is first served, which ties the gate to the service plan lever: a delayed flight with the outlets shut wants its drinks early. The fields are conditions, not levers, and do not move the timeline, so cabin ready is still measured against its plan (question 9). Where the fields come from in MSFS mode is M5's question (section 9).
- **Belongings are traits (T9).** What OpenPax called inventory becomes a small set of traits, drawn on their own roll beside the personality traits: a sleep kit (earplugs and mask), which lets its owner sleep through lights and neighbours, and an own device (a tablet), which keeps its owner entertained through an IFE outage. Each belonging blunts a lever or an event for the passenger who has it; a belonging that would touch nothing (sanitizing wipes, an amenity kit) is not carried. Relationships between passengers move to AirlineOps (section 9).
- **Helpful passengers (T10, eligible from M1 content).** A doctor or nurse on board assisting in a medical event is event content, read from the passenger's profession. Off-duty crew and pilots calm the passengers around them: an awake, calm off-duty crew passenger is a **negative source of Unease contagion**, an Event-class modifier below 1 on the neighbours in contagion reach, so the Unease-only contagion of question 7 now spreads calm as well as fear.

### Option B: four core needs plus needs that traits bring

The core four are Refreshment, Bladder, Rest and Unease, which every passenger has. A trait or profession, authored in Lua, can add one personal need from a content library: Boredom (children, teenagers), Connectivity (business travellers who must work: only WiFi moves it), Nicotine (rises with no relief short of gum or landing, so it pumps Unease), Attention (elite status: wants crew visits).

**Worst case in play:** the need set becomes content. Balance is scattered across trait files, the player never learns one small universal set, and each content pack widens what the dev inspector has to show. A trait-borne need with no lever (Nicotine) is a timer that turns into Unease, which is the passive-simulation problem in a new place.

### Option C: OpenPax's nine, redesigned

Hunger, Thirst, Bladder, Comfort, Entertainment, Health, Fatigue, Anxiety, Shopping, rebuilt on the shared scale and rule above.

**Worst case in play:** three meters with no mid-flight lever (Comfort, Health, Shopping) produce numbers without decisions, which is OpenPax's own core problem #1, "passive simulation" (`D:\openpax\Docs\Plans\CabinSim\README.md:35`). Nine needs times two thresholds times the cascade table is the tuning surface the balance-analyst inherits.

## 5. "Policy, not per passenger" against "optimize seat assignments"

**Ruled: resolution 2, seating is only ever a moment** (section 8, question 3). The other two resolutions are kept below as the record of what was weighed. OpenPax's CabinSim loop had "Optimize seat assignments" (`CabinSim\README.md:59`); its later Pillar v2 removed the seat-assignment panel and seat-compatibility calculator as per-individual surfaces (`AirlineOps\PHASE_9_TIME_SYSTEM.md:76-89`) without putting anything in their place. Seating is still the strongest pre-flight lever there is, because contagion, lav climbs and group splits all run through it.

1. **Recommended: seating is a set of rules, and the gate applies them.** Before boarding the player orders a short list of seating rules (keep groups together, families forward, assistance passengers near doors, nervous flyers on the aisle, lone business travellers away from children). The manifest generator seats everyone by those rules, in order, and a full flight forces the rules to conflict: that conflict is the decision. The report attributes moments to the rule that caused them ("keep groups together" split three groups because it ranked below "families forward"). Seat-swap requests during boarding are ordinary Lua events on top, which this composes with. **Worst case:** the player finds one dominant ordering and never touches it again, or the effect is diffuse enough that they cannot tell the rules mattered; the attribution in the report is what has to stop that.
2. **Seating is only ever a moment.** There is no pre-flight seating; seat conflicts surface as events ("a couple split across rows 12 and 19: ask 14C to swap?"). **Worst case:** purely reactive, and on a full flight swap prompts become a chore, which is per-passenger control wearing an event's clothes.
3. **Seats are not a lever.** Seats come from the booking side (the manifest generator now, the booking market later) and the player's levers are crew and service. The layout editor (M6) becomes the only seat-level lever. **Worst case:** the pre-flight phase has almost nothing to decide, and contagion and lav climbs become things that happen to the player rather than things they staged.

M1 seats with a fixed default rule order from the scenario file under any of these.

## 6. Scoring

Ruled: the measures below, as set by section 8, questions 4, 5, 6, 8 and 9. They score a flight with cabin crew. A **crewless flight** (T7), with passengers and no cabin crew, is scored on flight smoothness instead; question 9 is reopened for that mode only, and is due at M5.

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

Ruled: no single grade (section 8, question 6): the report shows the four outcomes side by side, each with a verdict word (Smooth, Rough, Bad), so the trade between them stays visible (pushing crew to serve everyone costs strain). The options are in question 6.

### The report

In M1 the report is text from `Sky.Sim`: flight header, the four outcomes with their measures, and under each the top moments that moved it (five at most), each with the tick it happened at so it can be found in the journal. A replay of the same seed and journal gives the same report.

## 7. M1 definition of done

Ruled: accepted as drafted; this is the acceptance list of the M1 subplan. Numbers are first values, set with the balance-analyst.

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

From the triage of the OpenPax goals (T11, T12)
- [ ] Thoughts are fog-of-war and information only: the observed view shows a passenger's thought only after a crew interaction or a check-in walk revealed it, and a flight with thoughts switched off gives the same end-state hash and the same outcome records as with them on.
- [ ] The gate conditions matter: across 200 seeds, a scenario that differs from the reference only in a 60-minute gate delay with the concessions closed raises the mean starting Refreshment and the `refreshment` calls in the first hour after the seatbelt sign goes off (thresholds set with the balance-analyst; first values in passengers.md's success criteria).

## 8. Open questions for the owner

Each option is ranked; the first was the recommendation. Every question of the concept pass is ruled; the rulings are in bold. Question 9 is reopened for crewless flights only (T7), below the triage rulings.

1. **Which need set does In the Sky use?** **Ruled: option 1, five needs, each tied to a lever.**
   1. Five needs, each tied to a lever: Refreshment, Bladder, Rest, Unease, Boredom; comfort becomes a seat-and-cabin modifier and health becomes an event-raised incident. Worst case: passengers are harder to tell apart, and on long flights Refreshment dominates everyone's distress.
   2. Four core needs plus needs that traits bring (Boredom, Connectivity, Nicotine, Attention), authored in Lua. Worst case: balance is scattered across trait content and some trait-borne needs have no lever.
   3. OpenPax's nine, redesigned on the new scale. Worst case: Comfort, Health and Shopping are meters nobody can move mid-flight, which is the passive simulation OpenPax set out to fix.

2. **Does the kept rate-modifier rule get a floor under it?** **Ruled: option 1, a floor of 0.2x.**
   1. A floor of 0.2x: no combination of modifiers can slow a need below a fifth of its base rate. Worst case: a passenger who should be "fully looked after" still drifts, so perfect service is impossible by a small margin.
   2. No floor, 0 as in OpenPax. Worst case: two helpful modifiers from different classes freeze a need, the April 2026 bug through the cross-class sum.
   3. A floor per need, set in content. Worst case: one more number per need to tune and explain, for little gain over a single floor.

3. **How does seating become a player lever?** **Ruled: option 2, seating only as moments.** There are no pre-flight seating rules; in M1, seat-conflict events are auto-resolved by crew like any other event.
   1. Seating rules the player orders before boarding, applied by the gate; a full flight makes the rules conflict. Worst case: one dominant ordering is found and never touched again.
   2. Seating only as moments: seat conflicts arrive as events with swap choices. Worst case: on a full flight the swap prompts become per-passenger chores.
   3. Seats are not a lever; they come from booking, and the layout editor is the only seat-level tool. Worst case: pre-flight has almost nothing to decide.

4. **How is one passenger's experience computed?** **Ruled: option 1, the peak-end blend.**
   1. Peak-end: 40% worst sustained distress, 30% the last 20 minutes, 30% the flight's mean. Worst case: a passenger who suffered a long moderate stretch scores better than one with a short sharp spike, which can read as unfair in the report.
   2. Time-weighted mean distress over the flight. Worst case: a ten-minute panic attack disappears into three hours of fine, so incidents barely touch experience.
   3. Mood at the moment of deboarding only. Worst case: everything before descent stops mattering, and a good landing hides a bad flight.

5. **What is the headline number for the spread of experience?** **Ruled: option 1, the 10th percentile.**
   1. The 10th percentile (the worst-served tenth), with the median and the count under 40 beside it. Worst case: sensitive to a handful of passengers the seed made hard, so it is noisier across seeds than a mean.
   2. The count of passengers under a threshold ("would complain"). Worst case: a cliff: a passenger at 41 counts the same as one at 95.
   3. The spread itself (interquartile range). Worst case: an evenly miserable flight scores as well as an evenly happy one.

6. **Does the report roll the four outcomes into one grade?** **Ruled: option 1, no grade; four verdicts side by side.**
   1. No grade: four outcomes side by side, each with a verdict word. Worst case: no single number to compare flights or feed AirlineOps reputation, so a composite has to be designed later anyway.
   2. A weighted composite grade over the four. Worst case: the weights become the game, and the trade between outcomes vanishes into one letter, as OpenPax's six weighted categories did.
   3. Gate then rank: any missed serious incident caps the flight's verdict, otherwise experience ranks it. Worst case: one unlucky incident on a seed flattens an otherwise excellent flight.

7. **Does mood spread between neighbours in M1?** **Ruled: option 1, only Unease spreads.**
   1. Yes, only Unease spreads, to adjacent seats and across the aisle, as an Event-class modifier. Worst case: a single panicking passenger can tip a row, which may look like an avalanche before it is tuned.
   2. No contagion in M1; add it with the player levers in M3. Worst case: seating has no effect in M1, so the seating lever cannot be tested until M3.
   3. Full mood contagion across all needs, as in OpenPax. Worst case: feedback loops across the cabin that dominate every other system and are hard to trace in the report.

8. **How is crew strain measured?** **Ruled: option 1, peak strain of the most-strained crew member plus minutes over the redline.**
   1. The peak strain of the most-strained crew member, plus minutes over the redline. Worst case: a flight with one bad stretch for one crew member scores as badly as one where the whole crew was run ragged.
   2. The team's mean strain. Worst case: one crew member run into the ground is hidden by five who were fine.
   3. Minutes of backlog on the task board (work waiting with no one to take it). Worst case: it measures under-staffing, not what it cost the crew, so a crew that skips breaks to clear the board looks great.

9. **What does "on-time doors" measure?** **Ruled: option 1, what the cabin controls.**
   1. What the cabin controls: cabin ready against plan at departure, deboarding duration against a target at arrival. Worst case: in MSFS mode a player can be "on time" by this measure while the aircraft pushes back late for reasons the cabin cannot see.
   2. Actual door close and door open times from the clock and feed. Worst case: the score blames the cabin for ATC and pilot delays.
   3. Departure end only (cabin ready). Worst case: deboarding, where bins and aisle blocking make the best stories, stops counting.

   **Reopened for crewless flights only (T7).** The ruling above stands for every flight with cabin crew; the crewless mode's question is at the end of this section.

10. **What is M1's reference flight?** **Ruled: option 1, about 2.5 hours, one drinks round and one meal, day departure.**
    1. About 2.5 hours, one drinks round and one meal, day departure. Worst case: long enough that a 500-seed sweep at 64x takes minutes, which slows the balance loop.
    2. About 1 hour, drinks only. Worst case: too short for Rest, Boredom or a lav wave to matter, so half the need set goes untested.
    3. About 5 hours, overnight, two services. Worst case: the sweep takes several times longer, and a night flight's sleep dominates everything else.

### Rulings from the triage of the OpenPax goals

The owner triaged the goals the [AVSIM goals research](../research/2026-09-26-openpax-avsim-goals.md) found CONCEPT missing, adopting, deferring or dropping each. Where a ruling differs from `game-designer`'s recommendation in that triage, it says so.

- **T1. A resource budget beside MSFS** is stated in pillar 6 now; the voice model's ceiling is set at M4 and measured in the sim at M5.
- **T2. Automation profiles and the Captain and Lead Flight Attendant ways to play** are presets of the one stage-manager role: a shareable saved lever set, built at M3 (section 3).
- **T3. SimBrief** is a scenario source at M5.
- **T4. GSX** waits until after M5, and then syncs only the door and boarding-start signals.
- **T5. Cargo and weight limits** (MZFW and MTOW on sales) move to AirlineOps, with the booking market.
- **T6. Other simulators:** the sim feed port is public, and community adapters are welcome (section 1). The project builds only the MSFS adapter.
- **T7. Crewless flights stay,** scored on flight smoothness instead (G-rates, turbulence met, prompt departure and arrival), which needs the sim feed (M5) and reopens question 9 for that mode only (below).
- **T8. Announcements:** the announcement templates become editable data at M4, and destination weather joins them as a feed variable with its own switch at M5.
- **T9. Belongings become traits now** (section 4); relationships move to AirlineOps.
- **T10. Helpful passengers:** doctors assisting and off-duty crew or pilots calming their neighbours are adopted as event content and a negative source of Unease contagion, eligible from M1 content (section 4).
- **T11. Starting needs** set by the gate delay and open concessions are scenario fields from M1 (section 4).
- **T12. Passenger thoughts are adopted,** for a RollerCoaster Tycoon or Planet Coaster feel, and are fog-of-war: the observed view shows a thought only once a crew check-in reveals it (section 3; passengers.md section 10). This overrides the recommendation to drop them as a player surface. The owner widened the reveal to every crew interaction in a follow-up ruling (below).
- **T13. Overrides of the derived layout work** (crew positions, service zones) wait for the M6 layout editor.

Follow-up rulings on the fold, answering `game-designer`'s questions:
- **T12, the reveal:** every crew interaction reveals a thought (passengers.md section 9's table and the check-in walk), not only check-ins. This overrides the recommendation of check-ins only; the owner accepted its worst case, a thin fog during service.
- **T12, the report:** the M1 text report does not quote thoughts; the balance CSV counts grumbles born by lever tag.
- **T2, presets:** a preset covers the moments that reach the player as well as the levers.
- **T11 and T12, M1 done list:** both are proven in M1; two items join section 7.
- **T1, the figures:** pillar 6's figures stay working figures, checked at M2 and measured at M5.

### Question 9, reopened for crewless flights (due at M5)

**How is a crewless flight scored?** With no cabin crew, nothing handles incidents and nobody accumulates strain, and the player is most likely the pilot, so the cabin-controlled measure of question 9 has nothing to measure. The owner ruled the direction (smoothness: G-rates, turbulence met, prompt departure and arrival); the shape is open, ranked, the first recommended:

1. **Experience and smoothness side by side.** Experience stays (passengers still have needs, and rough flying pushes Unease), smoothness is a new outcome with its own verdict word (G-rates past a comfort band, turbulence flown through, and departure and arrival promptness taken from the actual door times, question 9's option 2), and incidents and strain drop. Worst case: this mode scores the flying, which no crewed flight does, so its verdicts cannot be compared with a crewed flight's.
2. **Smoothness only.** One outcome, from the feed. Worst case: passengers' needs play no part in the score, and the cabin simulation under the flight is scenery.
3. **The crewed four, unchanged.** Worst case: every incident on a crewless flight is missed by construction, so it scores Bad on incidents every time.

## 9. Deferred work by milestone

What the triage placed after M1, so the milestone that takes it up finds it here. Each line names its ruling.

- **M2 (client):** thoughts drawn over seats in the cabin view from the observed view, with the true thought beside the revealed one in the dev inspector (T12; passengers.md section 10). The client confirms the resource budget's working figures (T1).
- **M3 (HUD and levers):** presets of the stage-manager role (T2); the grouped thoughts panel, each group opening the lever its thoughts point at (T12).
- **M4 (voice):** the voice model's resource ceiling (T1); announcement templates as editable data (T8).
- **M5 (MSFS):** the resource budget measured beside the sim (T1); SimBrief as a scenario source (T3); crewless flights and their scoring, question 9 reopened above (T7); destination weather as a feed variable with its own switch (T8); where the gate delay and concessions fields come from in MSFS mode (T11).
- **After M5:** GSX, syncing only the door and boarding-start signals (T4).
- **M6 (layout editor):** overrides of the derived crew positions and service zones (T13).
- **AirlineOps:** relationships between passengers (T9); cargo and weight limits on sales (T5).
