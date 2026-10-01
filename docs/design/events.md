# Events

Status: written by `event-writer` for M1 step D4. The owner picked the four M1 events from the brainstorm and ruled the module additions; the specs are in section 10, and their trait, age-band and incident ids are the ones `docs/design/passengers.md` (D1) names. Sections 1, 6 and 7 match the event port and its Lua adapter (step S3). Owner of this doc: `event-writer`. It follows ruling R27 (the module shape) and owner decisions OD1 (auto-resolve by quality) and OD5 (one seat-conflict event) in `docs/decisions/m1-rulings.md`, with the additions in section 1. Terms are in the glossary in [docs/README.md](../README.md); the needs, phases and pillars are in [CONCEPT.md](./CONCEPT.md). Every number here marked "first value, D3" is a starting value that `docs/design/balance.md` owns and tunes.

## 1. What an event is

An event is a surfaced cabin situation with two to four choices whose consequences land later. It is one Lua module, declarative, and it never touches the simulation: it reads read-only facts and returns tables the host turns into work and delayed consequences. A module that throws, runs out of its instruction budget, or returns something malformed from any of its four functions (the checks below) is disabled for that flight only, with the offending field named in the reason; so is a module whose table is malformed when it loads (step S3).

A module is a Lua chunk that returns one table:

| Field | What it is |
|---|---|
| `id` | The event's content id, lowercase with hyphens (`split-group`). It must equal the id the module is loaded under, or the module is disabled at load. Never shown to a player. |
| `phases` | A non-empty list of the flight phases (section 2) the event may fire in, by the ids of section 2. A name that is not a stage disables the module at load. The host asks the trigger only in these phases. |
| `trigger(ctx)` | Reads the cabin through `ctx` (section 7) and returns `nil`, or a facts table recording what it saw: who is involved, how busy the crew are, the phase. It checks the phase first and rolls its own chance (section 3). `facts.subject`, when present, must be the id of a passenger aboard. |
| `describe(facts)` | The scene, a string: one or two sentences saying what a crew member would notice (section 6). |
| `choices(facts)` | Two to four choices, each `{ id, label, needs_crew, minutes, quality }`. `needs_crew` says whether the choice becomes a task on the task board; `minutes` is how long that crew work takes (0 for a choice with no crew); `quality` is how good the choice is for these facts, a number in [0, 1] (section 4). A choice the facts rule out (no free seat, no companion) is left out, as long as two to four remain. |
| `effects(facts, choice_id)` | The chosen branch's delayed consequences: a list of the four kinds below. `choice_id` is always one of the ids `choices` last offered for these facts. |

The facts table is the trigger's own Lua table: the host keeps it and hands the same table to `describe`, `choices` and `effects`, so it records plain values (ids, seat labels, flags, numbers), never `ctx` itself (section 7).

The consequence kinds (R27, plus two more):

| Kind | Shape | What the host does |
|---|---|---|
| Need | `{ after_minutes, target, need, delta }` | Moves one of the five needs of each target by `delta`, clamped to 0 to 100. |
| Incident | `{ after_minutes, target, incident }` | Raises an incident of that kind on the target, with its escalation deadline. |
| Seat move | `{ after_minutes, swap = { a, b } }` or `{ after_minutes, target, to_seat }` | Swaps the seats of passengers `a` and `b`, or moves one passenger to a free seat the facts recorded. The host checks it first (both aboard, the seat still free and in the same cabin class, nobody mid-walk to the lavatory); a move that fails the check is dropped and journaled as a moment with the reason, and the rest of the branch still lands. During boarding it changes the seat assignment, and the passenger walks there. |
| Line | `{ after_minutes, target, line }` | Journals the line as a moment tied to the event, the choice and the target, so the report can cite it. Changes no state. |

Targets are selectors the host resolves: `"subject"` (the passenger the facts name as `subject`), `"neighbours"` (the subject's adjacent seats and across the aisle, the reach of Unease contagion, leaving out any passenger the facts name, so no one named in the event takes a neighbours' delta on top of their own), or a passenger id the trigger recorded in the facts. A passenger id is checked only as a whole number when `effects` returns; a consequence whose passenger is no longer aboard when it lands is dropped and journaled.

**What the host checks** (step S3). Each rule below, broken, disables the module for the flight, and the reason names the field (`'choices[2].minutes' is 3, not 0 for a choice with no crew`).

- `choices` returns a list of 2 to 4 tables with unique `id`s, each with a string `id` and `label`, a boolean `needs_crew`, a number `minutes` and a number `quality` in [0, 1]. A choice with `needs_crew = false` has `minutes = 0`; one with crew has `minutes` above 0.
- The list includes a crew-free choice with the id `leave`, which a timed-out task resolves with (section 5).
- `effects` returns a list of at most 32 consequences. Each is a table with exactly one of the fields `need`, `incident`, `swap`, `to_seat` and `line`, which decides its kind.
- `after_minutes` is a number of 0 or more, finite; the host converts it to ticks, rounding half away from zero. `delta` is a finite number. `need` is a lowercase need name (`refreshment`, `bladder`, `rest`, `unease`, `boredom`); `incident` is an incident id (`accident`, `panic`, `food_demand`, `noise_complaint`, `disruptive_passenger`, `fight`); `line` and `to_seat` are strings.
- `target` is `"subject"`, `"neighbours"` or a passenger id. A `to_seat` move and an incident each name one passenger, never `"neighbours"`. A swap takes no `target`: its `swap` list holds exactly two entries, each `"subject"` or a passenger id, naming two different passengers.
- `"subject"` and `"neighbours"` need facts that name a `subject`.
- `describe` returns a string.
- `trigger` and `effects` may draw from `math.random`, which reads the random stream the host hands that call; `describe` and `choices` may not, and a draw there disables the module. So does a draw while the module loads.

**What a crew task reveals**. A module declares nothing about observation; the host derives it from the chosen branch, by one fixed rule. When a crew member starts an event's crew task, every passenger the branch's consequences target by name (`subject`, or a passenger id from the facts; never `neighbours`, whom the crew member does not speak to) has Unease revealed, plus every other need that a Need consequence of that branch lowers (a negative `delta`) on that passenger. The bands are read at the start of the task, before any of the branch's deltas land. A need the branch only raises is not revealed: a later cost, such as the Bladder rise that follows a drink, is not something the crew member reads at the seat. A choice with no crew, and a `leave` applied because nobody came (section 5), reveal nothing. This is the "event's crew task" row of `passengers.md` section 9 (OD3). For the M1 events it gives: `split-group`'s `swap` and `pair` and `nervous-flyer`'s `sit`, Unease only; `armrest-dispute`'s `calm` and `reseat`, Unease, and `drink`, Unease and Refreshment for both passengers; `restless-child`'s `pack` and `play`, Boredom and Unease for the child and Unease for the parent (and for the passenger in front on `play`).

The trigger checks the phase itself even though `phases` lists them and the host asks it only there: a meal complaint during boarding is a bug in the module, and the shipped-event test (`ShippedEventTests`, step X4) calls every trigger directly outside its phases to check it returns nothing.

## 2. Flight phases an event may name

The phases are the stage machine's stages (M1 step F1), in order. An event names them by these ids:

| Id | What the cabin is doing | Events here |
|---|---|---|
| `boarding` | Passengers find seats, bins fill, the aisle blocks | Seat conflicts, bags. Crew time here pushes cabin ready late. |
| `taxi-out` | Belted, sign on, crew finishing the secure check | Nervous flyers before takeoff. |
| `takeoff` | Crew seated | **Named by no M1 event**: a choice that needs crew cannot be worked while crew are seated. |
| `climb` | Sign on, then off; Unease peaks for nervous flyers | Nervous flyers, the first requests once the sign goes off. |
| `cruise` | The drinks round and meal, then quiet cruise: lights down, sleepers, lav traffic | Most events. Service running or not is a trigger condition, not a phase. |
| `descent` | Cabin secure, the last lav rush; Unease raised here and left unrelieved runs into the "end" of peak-end (the last 20 minutes before a passenger leaves the aircraft) | Worries about arrival, children's ears, the last requests. |
| `landing` | Crew seated | **Named by no M1 event**, as for `takeoff`. |
| `taxi-in` | Belted, some passengers standing early | Rare; nothing in M1. |
| `deboarding` | The aisle stands up at once, bins empty | Rare; nothing in M1. |

`pre-boarding` and `done` have no passengers aboard, so no event names them.

CONCEPT section 3 splits cruise into "cruise service" and "quiet cruise"; the engine has one `cruise` stage, so an event that belongs to one half reads whether a service round is running (section 7).

## 3. Pacing

Pillar 4: a competent normal flight is quiet, and the baseline must miss no incident on at least 90% of seeds. Events are exceptions.

| Rule | Value |
|---|---|
| Check cadence: how often the scheduler asks every enabled trigger | 30 sim seconds (first value, D3) |
| Events active at once | One. While one is unresolved, no trigger is asked. |
| Minimum gap after an event resolves before the next may fire | 5 sim minutes (first value, D3) |
| Times one event may fire in a flight | Once: after it fires, the scheduler stops asking its trigger for the rest of the flight. |
| Base chance a trigger fires on a check when its hard conditions hold | 0.001 (rare) to 0.02 (common in its conditions), rolled in the trigger with the host's `math.random` (first value, D3) |
| Crew work a choice may take | 1 to 6 minutes (first value, D3) |

A trigger never fires just because its conditions hold: it rolls its chance, so a condition that holds for an hour does not flood the flight. Soft conditions (a trait, a busy board, a child in the group) multiply the base chance; a trigger has one strong hard condition, never three required traits.

## 4. How auto-resolve reads a choice

M1 has no player, so crew make every choice (OD1). Each choice carries a `quality` in [0, 1], computed in `choices(facts)` from what the trigger saw and relative within the event; the host normalises it. The crew member picks choice `i` of `n` with chance `(1 − s)/n + s · q_i / Σq`, where `s` in [0, 1] rises with competence and falls with fatigue and some crew traits (the shape of `s` is in `docs/design/crew.md`). A rested, competent crew member reads the situation right more often than a tired one, and the M1 sweep checks exactly that (CONCEPT section 7).

What that asks of every event:

- **The best choice depends on the facts.** Each choice states the conditions under which it is the better one, and its quality follows those conditions. If one choice scores highest in every situation the trigger can see, the event is not a decision (the "one obviously best choice" anti-pattern) and the competence check has nothing to measure.
- **Every situation has a worse choice,** and a clear gap between the best and the worst, so a sharp crew member and a flat one pick measurably differently.
- **Quality is authored for every situation the trigger can see.** That was the accepted cost of OD1. A spec gives its qualities as a table over the facts, or as a base value with additive adjustments, each result clamped to [0, 1].

## 5. Consequences

Effects never change state; they return delayed consequences the engine applies at their tick.

- **When the clock starts.** For a choice that needs crew, `after_minutes` counts from the moment the crew member starts the task, so relief never lands before anyone arrives; for a choice with no crew, it counts from the choice.
- **When nobody comes.** A crew task nobody starts within the waiting limit, 10 sim minutes (first value, D3), times out: the event resolves with the effects of its `leave` choice (every event gives its `Leave it for now` choice the id `leave`), counted from the timeout, and the host journals a moment that nobody came, naming the task that kept each able crew member away. So every event's `Leave it for now` branch is also what neglect costs, and a spec writes it to stand on its own.
- **Stagger them.** A need change lands over the resolution in two or three steps, not at once: relief comes as the crew member talks, not the instant they are asked.
- **Open with a line.** Every branch opens with a line consequence at 0 minutes that sets the scene, so the report has words for it.
- **Touch the neighbours when it fits.** Relief spreads a little when it goes well; Unease spreads when it escalates.
- **An incident is a real exception.** A raised incident counts toward "incidents handled or missed"; it is raised only on the escalating branch, and only past a threshold the trigger recorded, so the baseline stays quiet.
- **Every consequence names its cause.** The host stamps each one with the event, the choice, the crew member who worked it and the passengers it targets, so the report can cite it as a moment.
- **No need without a lever.** A consequence only pumps a need that crew or a passenger's own activity can bring back down.

## 6. House style for event words

In M1 the words reach the text report (a moment cites the event, the choice and its lines); from M3 they reach the event-choice screen, and they meet `docs/GODOT_CONVENTIONS.md` section "Text a player reads".

- **Spelling and register.** British spelling, as the rest of the docs (behaviour, neighbour). Plain, observational, calm, present tense: what a crew member walking the aisle would notice, never what a meter reads. No jokes at a passenger's expense, no melodrama.
- **Passengers** are named by seat (`23A`, "the passenger in 23A") or by what a crew member can see ("a couple", "a parent and child", "the man at the window"). No invented personal names in M1. In the specs, `{name_seat}` stands for a seat label the module fills from the facts.
- **Crew** are named by where they work ("the aft crew member", "the forward crew member") until `docs/design/crew.md` names positions; a line never says which crew member is skilled or tired.
- **Needs** are shown through behaviour in scene lines ("keeps glancing at the lavatory", "has been awake since four"). A need's name (Refreshment, Bladder, Rest, Unease, Boredom), capitalised, appears only where the report or a meter names it as a cause.
- **Places**: "row 23", "the aft lavatory", "the forward galley", "the aisle". In player text the lav is "the lavatory"; "lav" stays in the docs and code.
- **Describe text**: one or two sentences, at most about 200 characters, ending with what makes it a decision.
- **Choice labels**: imperative, sentence case, no full stop, at most about 40 characters, and never a hint of the outcome or of crew skill. The crew-free choice is labelled exactly `Leave it for now`.
- **Scene lines** (line consequences): one sentence each, saying what someone nearby would notice.
- **No internal names** in any text: a state key, event id or choice id stays in the module.
- A word not in the docs goes to the owner as a proposal before it goes in a line.

The module's Lua follows these rules as well, the first two from S1's review of the Lua host and the rest from S3's event adapter:

- **No recursion through `pcall`, `xpcall` or a metamethod.** The depth at which a stack overflow comes there depends on the machine's native stack, so the same flight could overflow on one machine and not on another, and a golden hash would differ between Windows and Linux.
- **No `pcall` of your own around code that may overflow.** The module's `pcall` swallows the overflow, so the host never sees it and the Lua state is not rebuilt.
- **Facts record values, never `ctx`.** `ctx` is readable only while `trigger` runs; kept in the facts, an upvalue or a global and read from `describe`, `choices` or `effects`, it raises "ctx is readable only during trigger" and disables the module. Copy what a later function needs (an id, a seat label, a flag) into the facts.
- **Draw only in `trigger` and `effects`.** `describe` and `choices` are pure functions of the facts: whatever varies between flights is rolled in the trigger and recorded, or rolled in `effects`.
- **Pick the first that fits, in the order `ctx` gives.** Lists come in a fixed order (section 7), so a trigger that scans them and takes the first match replays the same on the same seed.
- **Spell names exactly.** A need name `ctx:need` does not know, or a trait name `ctx:has_trait` does not know, is a Lua error that disables the module. Copy trait ids from `passengers.md`.

## 7. What triggers read

A trigger reads the cabin through `ctx`, built in step S3 (`LuaEventContext`) over the Engine's `EventContext`, which F6 fills. It holds what the four M1 events read and nothing more. Every member is a read: scalars are fields (`ctx.stage`), everything else is a method called with a colon (`ctx:need(id, "unease")`). `ctx` is readable only while `trigger` runs (section 6).

**The flight.**

| Member | What it reads |
|---|---|
| `ctx.stage` | The stage, by the ids of section 2 (`boarding`, `taxi-out`, `cruise`...). |
| `ctx.seatbelt_sign` | Whether the seatbelt sign is lit. |
| `ctx.turbulence` | How rough the air is now: `none`, `light` or `moderate`. |
| `ctx.service_round_running` | Whether a service round is running (the split between cruise service and quiet cruise). |
| `ctx.longest_task_wait_minutes` | The longest wait, in sim minutes, of any task on the task board now; 0 when the board is empty. The "crew are stretched" signal. |
| `ctx.boarding_seated_share` | The share of the manifest seated, in [0, 1]. |

**Lists.** A list is read as a count and a 1-based index, `for i = 1, ctx.passenger_count do local id = ctx:passenger(i) ... end`. An index outside the list reads `nil`.

| Member | What it reads |
|---|---|
| `ctx.passenger_count`, `ctx:passenger(i)` | The passengers aboard, by id, in ascending passenger id. |
| `ctx.free_seat_count`, `ctx:free_seat(i)` | The free (unbooked) seats, by label (`23A`), in seat order: row by row from the front, left to right within a row. |
| `ctx:free_seat_class(i)` | The cabin class of free seat `i`: `business` or `economy`. |
| `ctx:free_seat_has_free_neighbour(i)` | Whether a seat side by side with free seat `i` is free too. |

**Per passenger**, by id. An id that is not aboard reads `nil`.

| Member | What it reads |
|---|---|
| `ctx:need(id, name)` | One of the five needs, 0 to 100, by its lowercase name: `refreshment`, `bladder`, `rest`, `unease`, `boredom`. Any other name is a Lua error. |
| `ctx:has_trait(id, name)` | Whether the passenger carries the trait with that id (`nervous_flyer`); a name no trait has is a Lua error. |
| `ctx:group(id)` | The passenger's group (booking) id. |
| `ctx:group_member_count(id)`, `ctx:group_member(id, i)` | The other members of the group, by id, in ascending passenger id; a count of 0 for a passenger travelling alone. |
| `ctx:age_band(id)` | `adult` or `child`. |
| `ctx:seat(id)` | The label of the passenger's seat. |
| `ctx:neighbour_count(id)`, `ctx:neighbour(id, i)` | The passengers in the adjacent seats and across the aisle (the reach of Unease contagion), by id, in ascending passenger id. |
| `ctx:front(id)` | The id of the passenger in the seat in front, or `nil` when that seat is empty or there is none. |
| `ctx:asleep(id)` | Whether the passenger is asleep. |
| `ctx:seated(id)` | Whether the passenger is in their seat. |

The fixed orders are what let a trigger that takes the first passenger or seat that fits replay the same flight on the same seed.

## 8. Brainstorm (M1)

The eight candidates weighed for M1, in the order recommended. The owner picked the first four marked **picked**; the rest stay as a pool for later milestones and are not specced.

1. **`split-group`** (boarding; the OD5 seat conflict). **Picked**; spec in section 10.
2. **`nervous-flyer`** (taxi-out, climb). **Picked**; spec in section 10.
3. **`armrest-dispute`** (cruise; the fight incident). **Picked**; spec in section 10.
4. **`restless-child`** (cruise; Boredom's crew lever). **Picked**; spec in section 10.
5. **`connection-worry`** (descent). A business traveller with a tight connection keeps asking when they will land; Unease close to the end window. Choices: `Check the arrival gate and reassure them` (crew), `Move them forward for a quick exit` (crew, seat move), `Leave it for now`.
6. **`spilled-drink`** (cruise, during the drinks round). The cart jolts and a drink goes over a passenger's lap; competes with the round for crew time. Choices: `Stop and help them clean up`, `Hand over napkins and carry on`, `Leave it for now`.
7. **`feeling-faint`** (climb, cruise, descent). A passenger goes pale and asks for help; raises a medical incident on the escalating branch and reads professions. Choices: `Bring water and sit with them`, `Ask for a doctor on board`, `Leave it for now`.
8. **`aisle-please`** (boarding). A nervous flyer in a window seat asks for the aisle; the alternative seat conflict. Choices: `Ask an aisle passenger to swap`, `Reassure them and settle them in`, `Leave it for now`.

## 9. Open

Nothing is open for the M1 events. The ids they use from other docs, and the one incident kind this doc owns:

| Id | Kind | Owner | Used by |
|---|---|---|---|
| `nervous_flyer` | trait (Unease ×1.5, baseline 15) | `passengers.md` | `nervous-flyer` |
| `short_tempered` | trait, event-only: no rate modifier and no baseline; only event triggers read it | `passengers.md` | `armrest-dispute` |
| `child`, `adult` | age band; a `child` also carries the `child` trait | `passengers.md` | `split-group`, `restless-child` |
| group id | the booking a passenger travels on; a child's parent is an `adult` with the same group id | `passengers.md` | `split-group`, `nervous-flyer`, `armrest-dispute`, `restless-child` |
| `panic` | incident kind (Unease) | `passengers.md` | `nervous-flyer` |
| `disruptive_passenger` | incident kind (Boredom), raised by the sustain gate, never by an event | `passengers.md` | named by `restless-child`'s check |
| `fight` | incident kind, raised only by events | this doc | `armrest-dispute` |

**`fight`.** Raised on the passenger the consequence targets, when two passengers' argument boils over. Handled when a crew member reaches the seat before its escalation deadline, 5 sim minutes (first value, D3): the crew member separates them, and the target and every passenger in its contagion reach (the other party among them) take Unease −15 at once and −10 three minutes later. Missed: the row watches it play out, and every passenger in the target's contagion reach takes Unease +15 at once and +10 five minutes later, which feeds contagion (first values, D3). No sustained need raises it, so it has no sustain window.

## 10. Event catalogue

All numbers in this section are first values, D3: thresholds, chances, minutes, deltas, offsets and qualities.

### `split-group`

**One-liner.** Two passengers travelling together find their seats rows apart, and one stops a crew member in the aisle to ask if they can sit together.

**Phases.** `boarding`.

**Trigger.**
- Hard: a group of exactly two whose seats are not side by side, the second of them has just sat down, and the trigger can offer at least one move: a `swapper` (a passenger seated beside the partner, travelling alone, seated, same class) or a free pair (two free seats side by side in the same class). Without either, it returns nothing.
- Soft: one of the pair in age band `child` ×2 (the other is then the `adult` of the same group).
- Base chance: 0.01 per check.
- Records: `subject` (the one who asks, who just sat), `partner`, `has_child`, `swapper` or nil, `pair` (two seat ids) or nil, `aisle_busy` (fewer than 70% of the manifest seated).

**Describe.** Adults: "Two passengers travelling together have seats in {subject_seat} and {partner_seat}. One of them stops a crew member in the aisle to ask if they can sit together." With a child: "A parent and child have seats in {subject_seat} and {partner_seat}, rows apart. The parent stops a crew member in the aisle to ask if they can sit together."

**Choices.**

| Id | Label | Crew | Minutes | Offered when |
|---|---|---|---|---|
| `swap` | `Ask a neighbour to swap` | yes | 3 | `swapper` exists |
| `pair` | `Move them to an empty pair` | yes | 3 | `pair` exists |
| `leave` | `Leave it for now` | no | 0 | always |

Effects:
- `swap`: 0, subject, line "The crew member leans in to {swapper_seat} and asks if they would mind moving." · 2, swap subject and swapper · 2, subject and partner, Unease −10 · 2, swapper, Unease +8 · 5, subject and partner, Unease −6.
- `pair`: 0, subject, line "The crew member points out two empty seats in row {pair_row}." · 2, subject to `pair[1]`, partner to `pair[2]` · 2, subject and partner, Unease −10 · 5, subject and partner, Unease −6.
- `leave`: 0, subject, line "They wave to each other over the seat backs." · 5, subject and partner, Unease +6 · 15, subject and partner, Unease +5. With a child, nothing more is added here: `passengers.md` already gives a child with no adult of their group in their seat group Unease ×1.3, and that adult ×1.2 (Context class), for as long as they sit apart. That lasting modifier is the child case's real cost, and the reason `leave` scores low for it; the seat moves in `swap` and `pair` are what end it.

Quality:

| Situation | `swap` | `pair` | `leave` |
|---|---|---|---|
| Aisle clear, adults | 0.7 | 0.9 | 0.3 |
| Aisle clear, child | 0.8 | 1.0 | 0.1 |
| Aisle busy, adults | 0.3 | 0.5 | 0.8 |
| Aisle busy, child | 0.5 | 0.7 | 0.35 |

When each is better:
- `pair` is best whenever it is offered and the aisle is clear, because it disturbs no stranger. It is offered only on a load with a free pair, so on a full flight it never appears.
- `swap` is best on a full flight with a clear aisle. It costs the swapper a little Unease.
- `leave` is best while the aisle is still crowded and the pair are adults. Three minutes of crew and two passengers moving in a busy aisle stall boarding and push cabin ready late, and two adults apart for a flight settle as their Unease decays.

**Exercises.** Unease, groups and children, the task board during boarding, and cabin ready. Seat moves change who sits next to whom, which moves contagion and lavatory climbs later.

**Anti-pattern check.**
- No dominant choice: the best is `pair`, `swap` or `leave` by situation.
- The phase is checked, and the trigger rolls a chance.
- One hard condition: a split pair with a move to offer.
- The branches differ.
- Every line is textured.
- No internal names reach text.
- Unease falls by itself (its half-life).
- Seat prompts stay rare: once per flight, and only for a real split.

### `nervous-flyer`

**One-liner.** A nervous flyer grips the armrests as the engines spool up.

**Phases.** `taxi-out`, `climb`.

**Trigger.**
- Hard: an awake passenger with the trait `nervous_flyer` and Unease ≥ 55 (its baseline is 15, so this takes a real push: engine noise, turbulence, a delay).
- Soft: turbulence now ×2.
- Base chance: 0.02 per check (the trait is rare).
- Records: `subject`; `companion` (a group member in an adjacent seat, or nil); `high` (Unease ≥ 75); `busy`; `turbulence`. `busy` is true in `taxi-out`, while the seatbelt sign is on, or when a task has waited 3 minutes or more.

**Describe.** "The passenger in {subject_seat} is gripping both armrests and staring at the seat back. They flinch at every new sound from the engines."

**Choices.**

| Id | Label | Crew | Minutes | Offered when |
|---|---|---|---|---|
| `sit` | `Sit with them for a minute` | yes | 2 | always |
| `companion` | `Leave it to their companion` | no | 0 | `companion` exists |
| `leave` | `Leave it for now` | no | 0 | always |

Effects:
- `sit`: 0, subject, line "The crew member crouches in the aisle and talks them through each noise." · 1, subject, Unease −12 · 2, subject, Unease −10 · 2, neighbours, Unease −4.
- `companion`: 0, subject, line "Their companion takes their hand and keeps talking." · 2, subject, Unease −8 · 6, subject, Unease −6 · 2, companion, Unease +5.
- `leave`: 0, subject, line "They sit rigid, eyes shut, breathing hard." · 3, subject, Unease +8 · 3, neighbours, Unease +4 · 8, subject, Unease +6 · if `high`, 10, subject, incident `panic`.

Quality:

| Situation | `sit` | `companion` | `leave` |
|---|---|---|---|
| Crew free, mild | 0.9 | 0.7 | 0.2 |
| Crew free, high | 1.0 | 0.4 | 0.05 |
| Crew busy, mild | 0.5 | 0.7 | 0.6 |
| Crew busy, high | 0.6 | 0.4 | 0.05 |

Turbulence lowers `leave` by 0.2, clamped at 0.

When each is better:
- `sit` is better when the crew are free, or whenever the passenger is near panic. When crew are busy it waits on the board while Unease climbs.
- `companion` is better when a companion sits beside them and the crew are busy. It costs the companion a little Unease and holds no one near panic.
- `leave` is better only when the case is mild, the crew are busy, and the air is smooth. The flyer's Unease runs over its baseline and decays.

**Exercises.** Unease, traits, contagion (neighbours), groups (companion), the task board against the secure check, turbulence, and a panic incident on one branch.

**Anti-pattern check.**
- No dominant choice across the four rows.
- The phase is checked: no crew-work choice lands in `takeoff`, because the event names only `taxi-out` and `climb`.
- One hard condition.
- The panic fires only from `high` on `leave`.
- Unease has levers.

### `armrest-dispute`

**One-liner.** Two strangers side by side start arguing over the armrest. (Recline is not modelled in M1, per `passengers.md`, so no line mentions it.)

**Phases.** `cruise`.

**Trigger.**
- Hard: two awake passengers in adjacent seats, not in one group, both with Unease ≥ 55.
- Soft: one has the event-only trait `short_tempered` ×3; either has Rest ≥ 70 ×1.5; the cabin is at least 90% full ×1.5.
- Base chance: 0.004 per check.
- Records:
  - `subject` (the one with the higher Unease) and `other`;
  - `fierce` (either has Unease ≥ 75);
  - `thirsty` (both have Refreshment ≥ 50);
  - `sleepy` (either has Rest ≥ 70);
  - `busy` (a service round is running, or a task has waited 3 minutes or more);
  - `free_seat` (a free seat in the same class, not beside either of them) or nil.

**Describe.** "{subject_seat} and {other_seat} are arguing over the armrest, and the voices are getting louder. The rows around have gone quiet to listen."

**Choices.**

| Id | Label | Crew | Minutes | Offered when |
|---|---|---|---|---|
| `calm` | `Step in and calm them down` | yes | 3 | always |
| `drink` | `Offer them both a drink` | yes | 2 | always |
| `reseat` | `Move one to a free seat` | yes | 4 | `free_seat` exists |
| `leave` | `Leave it for now` | no | 0 | always |

Effects:
- `calm`: 0, subject, line "The crew member stands at the row and speaks quietly to both of them." · 1, subject and other, Unease −10 · 3, subject and other, Unease −10 · 3, neighbours, Unease −5.
- `drink`: 0, subject, line "Two cups arrive, and the argument stops while they drink." · 2, subject and other, Refreshment −30 · 2, subject and other, Unease −8 · 5, subject and other, Unease −6 · 5, subject and other, Bladder +8 · 20, subject and other, Bladder +7. The Bladder steps are CONCEPT's drink pulse of +15 over 30 minutes.
- `reseat`: 0, other, line "The crew member offers {other_seat} a seat further back, and they take it." · 2, other to `free_seat` · 2, subject and other, Unease −15 · 5, subject and other, Unease −10 · 2, neighbours, Unease −5.
- `leave`: 0, subject, line "The voices rise over the armrest, and heads turn in the rows around." · 3, subject and other, Unease +8 · 3, neighbours, Unease +5 · 8, neighbours, Unease +3 · if `fierce`, 6, subject, incident `fight` (section 9); otherwise 10, subject and other, Unease +4.

Quality (base, plus adjustments, clamped to [0, 1]):

| Choice | Base | Adjustments |
|---|---|---|
| `calm` | 0.8 | `busy` −0.3; `fierce` +0.1 |
| `drink` | 0.35 | `thirsty` +0.5; `busy` −0.1; `fierce` −0.3 |
| `reseat` | 0.5 | `fierce` +0.5; `busy` −0.2 |
| `leave` | 0.25 | `sleepy` +0.45; `busy` +0.25; `fierce` sets it to 0.05 |

When each is better:
- `reseat` is best when the row is fierce and a seat is free. It is the most crew time, but it ends the row outright.
- `calm` is best when the row is fierce with no free seat, or mild with crew free. It takes 3 minutes of crew time.
- `drink` is best when both are thirsty and the row is mild. Its relief is smaller, and the Bladder bill comes due on the lavatory queue later.
- `leave` is best when the row is mild and one of them is about to sleep, or the round is running. A fierce row left alone becomes a fight.

**Exercises.** Unease and contagion; Refreshment and Bladder through the drink (pillar 3's cascade toward a lavatory wave); Rest as a signal; traits; the task board against the service round; a seat move; and the fight incident OD6 leaves to events.

**Anti-pattern check.**
- Each of the four choices is best in some situation.
- The phase is checked.
- One hard condition, with the trait soft.
- The fight is gated on `fierce`.
- The lines are textured.
- The Bladder rise has its lever (a lavatory visit).

### `restless-child`

**One-liner.** A bored child is kicking the seat in front, and the parent's shushing is not working.

**Phases.** `cruise`.

**Trigger.**
- Hard: an awake passenger in age band `child` with Boredom ≥ 70, whose group has an `adult` aboard (the parent).
- Soft: the passenger in front is asleep ×1.5; no service round is running ×1.5 (a passing cart is a distraction).
- Base chance: 0.01 per check.
- Records: `subject` (the child), `parent`, `front` (the passenger in the seat in front, or nil), `front_asleep`, `tired` (the child's Rest ≥ 60), and `stretched` (a service round is running, or a task has waited 3 minutes or more).

**Describe.** "The child in {subject_seat} has run out of things to do and is kicking the seat in front. The shushing from the next seat is not working."

**Choices.**

| Id | Label | Crew | Minutes | Offered when |
|---|---|---|---|---|
| `pack` | `Bring a colouring pack` | yes | 1 | always |
| `play` | `Stop and play a game with them` | yes | 4 | always |
| `leave` | `Leave it for now` | no | 0 | always |

Effects:
- `pack`: 0, subject, line "A crayon pack and a paper aeroplane land on the tray table." · 1, subject, Boredom −15 · 5, subject, Boredom −10 · 1, parent, Unease −5.
- `play`: 0, subject, line "The crew member crouches in the aisle for a round of I spy." · 2, subject, Boredom −20 · 4, subject, Boredom −20 · 4, parent, Unease −8 · 4, front, Unease −3.
- `leave`, when not `tired`:
  - 0, subject, line "The kicking goes on, and the seat in front jolts."
  - 3, front, Unease +6
  - if `front_asleep`: 3, front, Rest +8
  - 3, parent, Unease +6
  - 8, neighbours, Unease +3
  - 12, front, Unease +5
- `leave`, when `tired`: 0, subject, line "The kicking slows, and the child's head starts to nod." · 3, front, Unease +4 · 3, parent, Unease +3.

Quality (base, plus adjustments, clamped to [0, 1]):

| Choice | Base | Adjustments |
|---|---|---|
| `pack` | 0.7 | `stretched` +0.2; `tired` −0.2 |
| `play` | 0.9 | `stretched` −0.5; `tired` −0.3 |
| `leave` | 0.2 | `tired` +0.5; `stretched` +0.15; `front_asleep` −0.15 |

Resulting best choice:

| Situation | Best |
|---|---|
| Crew free, awake child | `play` (0.9) |
| Stretched, awake child | `pack` (0.9) |
| Crew free, tired child | `leave` (0.7) |
| Stretched, tired child | `leave` (0.85) |

When each is better:
- `play` is best when the crew are free and the child is wide awake. It gives the biggest and longest relief, but costs 4 minutes of an aisle crew member.
- `pack` is best while the round is running. It is quick, and the relief is modest: a child's Boredom climbs back at twice the rate.
- `leave` is best when the child is tired enough to fall asleep on their own. Leaving a wide-awake child costs the seat in front its Unease, and its sleep if it was asleep.

**Exercises.** Boredom's only crew lever in M1, children, groups (the parent), Rest and Unease of the seat in front, neighbours, and the task board against the service round.

**Anti-pattern check.**
- The best choice varies over the four situations.
- The phase is checked.
- One hard condition.
- The `leave` branches differ by `tired`, not by a coin flip.
- Boredom has a lever: the pack, the game, and sleep.
- No incident is raised here: a child left bored long enough reaches `disruptive_passenger` through the sustain gate (Boredom ≥ 90 for 10 minutes, `passengers.md`), which this event exists to head off.
