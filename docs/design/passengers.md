# Passengers

Status: M1 step D1, drafted by `game-designer` 2026-09-26; its eight open questions were ruled by the owner on 2026-09-26 (section "Owner rulings", P1 to P8), and the doc follows them. It builds on the ruled concept ([CONCEPT.md](./CONCEPT.md), sections 2 to 8), the kickoff decisions ([the decisions doc](../plans/2026-09-26-rewrite-decisions.md)) and the owner decisions OD3 and OD6 at the foot of [the M1 subplan](../plans/2026-09-26-m1-headless-cabin-flight.md). Crew-side behaviour (task priorities, zones, the service plan's shape, the phase timeline, observation reach, the wake rule for service) is `crew.md (D2)`'s, written alongside this doc. Terms are in the glossary in [docs/README.md](../README.md).

**Numbers.** Every number here marked **[D3]** is a first value for balance-analyst's D3 (`balance.md`): unmeasured, to be computed there and tuned in Z7. A number marked **(CONCEPT)** is quoted from CONCEPT section 4 and is not this doc's to change. Ids in `code` are content strings (ruling R9), interned at load.

## Goal

A cabin of 180 people who are recognisably different from each other, whose behaviour the player can read from what crew see, and whose needs run through the levers: the drinks round shows up in the lav queue, the lighting plan shows up in who sleeps, and a nervous flyer two rows from a panic shows up in the report. A competent baseline flight has passengers going about their business with no incident; a bad call on a lever surfaces as a few named passengers having a bad flight, not as a cabin-wide number going red.

## Pitch

A passenger is a manifest record (trip purpose, group, age band, profession, traits, wake time) plus the five needs of CONCEPT section 4. At each decision point they pick one activity from a short list by utility scoring in Lua, with the keep-current bias; the activity moves one or two needs and puts them somewhere on the nav graph. Traits bend the rates and the Unease baseline; the body clock sets Rest; neighbours spread Unease; a need held past its failure threshold raises its own incident kind, which crew must reach before it escalates. Crew see behaviour and a distress band from the aisle, and learn actual needs only by interacting.

## Core mechanics

### 1. The passenger record

Drawn once by the manifest generator (F2) from the `manifest` stream, and fixed for the flight:

| Field | Values | Read by |
|---|---|---|
| Trip purpose | `business`, `leisure`, `visiting` (visiting friends and relatives) | manifest shares, wake time, trait weights, events |
| Group | a booking id; group size 1 to 5 | seating (F2), `chat`, split-group Unease, child lav escort, events (OD5) |
| Age band | `adult`, `child` (2 to 11) | the `child` trait, activity availability |
| Seat class | `business` (the 3 rows of 2-2), `economy` (the 28 rows of 3-3) (OD2) | seat comfort, service order (crew.md (D2)) |
| Profession | see section 5 | events only in M1 |
| Traits | 0 to 2 from the trait list, plus `child` for children | rate modifiers, Unease baseline, activity scoring |
| Wake time | origin-local clock time | the Rest body clock (section 6) |
| Starting needs | see section 2 | |

### 2. Needs as a passenger carries them

Base rates, the drink pulse, the half-life and the service deltas are CONCEPT's (section 4). This section adds only what the passenger side needs.

**Starting values at boarding [D3].** Refreshment drawn 10 to 40; Bladder 0 to 20 (most used the terminal); Rest from the body clock (section 6); Unease at the passenger's baseline; Boredom 5 to 25 (the gate wait). Group members draw Refreshment and Boredom within 10 of each other.

**How Unease moves.** Unease is not a clock (CONCEPT): it is pulled toward the passenger's baseline with a half-life of about 15 sim minutes (CONCEPT), and pushed up by push sources. The rate multiplier of the rule scales the sum of the pushes active this tick, so Trait, Cascade, Context and Event modifiers amplify whatever is pushing and create nothing from nothing. Push sources **[D3]**: being aboard, 6 per hour; takeoff and landing, +30 per hour; climb and descent, +10; light turbulence, +30; moderate turbulence, +80; a delay past the planned pushback, +15 per hour once more than 5 minutes late; the passenger's own call button unanswered for more than 5 minutes, +20. One-off pushes land as pulses spread over 2 minutes: woken from sleep +5, an incident seen nearby (section 7). A passenger with several Unease traits takes the highest baseline among them, not the sum.

**Seat comfort (Context class, CONCEPT section 4).** Computed from pitch and width in inches by a formula D3 owns; the reference layout's first values **[D3]**: a business seat gives Rest ×0.85 and Unease ×0.9; an economy middle seat gives Rest ×1.1 and Unease ×1.1; window and aisle seats in economy give ×1.0. Cabin temperature is not modelled in M1.

**Distress band.** For observation (section 9) distress is read in three bands **[D3]**: `calm` under 30, `uneasy` 30 to 59, `distressed` 60 and over.

### 3. Activities

Each scored activity is one Lua module under `Data/activities/` with a `utility(facts)` function (R8), using `ipairs` over the trait list and arithmetic only, and no randomness (R6): ties and jitter belong to the engine's keep-current bias and staggered cadence (F4). A module returns 0 when the activity is unavailable.

| Id | Moves | Duration [D3] | Where on the nav graph | Crew |
|---|---|---|---|---|
| `idle` | nothing; Boredom rises at its rate | until the next decision point | seat | no |
| `sleep` | Rest falls 25 per hour with the cabin dimmed, 15 per hour with lights up [D3]; Boredom paused (CONCEPT) | 20 to 90 min, or until Rest ≤ 10, or woken | seat | no |
| `screen` | Boredom paused (CONCEPT: IFE); every seat of the reference layout has seatback IFE (P1) | 15 to 60 min | seat | no |
| `chat` | Boredom falls 20 per hour [D3], for both members | 5 to 20 min | seat (with an adjacent awake group member) | no |
| `lav_visit` | Bladder to 0 on completion [D3] | walk + queue + use 2 to 5 min (child 3 to 6) + walk back | seat lateral path, aisle slots, lav queue, lav | no |
| `stretch` | Boredom −15 on completion [D3] | 3 to 8 min standing | aisle slots to the aft galley and back; galley | no |
| `call_crew` | presses the call button with a reason; effect on answer, below | instant press; the light stays on until answered | seat | yes: posts a call-button task (crew.md (D2)) |
| `drink_served` | Refreshment −40 (CONCEPT) over the drinking time; Bladder +15 over 30 min (CONCEPT); Boredom −10 on the service pass [D3] | 3 to 6 min | seat, tray down | yes: the drinks round delivers |
| `meal_served` | Refreshment −70 (CONCEPT) over the eating time; Bladder +15 (the drink with it); Boredom −10; after it, Rest ×1.3 Service class for 60 min [D3] (the post-meal slump) | 15 to 25 min, then tray down until crew collect it (crew.md (D2)) | seat, tray down | yes: the meal round delivers |

**Scoring shape** (thresholds [D3]; the modules implement these, D3 sets the numbers):
- `sleep` scores from Rest 45 upward, halved with lights up (zero for `light_sleeper` with lights up), zero during boarding and deboarding, with the call light on, with a tray down, or within 10 minutes of being woken; `frequent_flyer` adds a bonus; a chatting neighbour zeroes it for `light_sleeper` and halves it otherwise.
- `screen` scores from Boredom 20 upward and is zero when an outage event has taken IFE away from the seat (P1).
- `chat` needs an awake group member in an adjacent seat; `sociable` raises it.
- `lav_visit` scores from Bladder 55 and rises steeply past 75; zero while the seatbelt sign is on, except in cruise with Bladder at 85 or more, when the passenger defies the sign and crew get a "back to your seat" task (P8; the task is crew.md (D2)'s); in every other phase with the sign on (taxi, takeoff, climb, descent, landing) nobody leaves the seat. When descent begins, Bladder 40 or more gets a last-chance bonus (the "last lav rush" of CONCEPT section 3).
- `stretch` only in cruise with the sign off, from Boredom 50; `restless` raises it.
- `call_crew` scores when the call light is off and a reason holds: `refreshment` (Refreshment ≥ 65, not served in the last 30 min, and the cart not in the passenger's zone), `reassurance` (Unease ≥ 55), or `lav_permission` (Bladder ≥ 75 with the sign on). `demanding` lowers each threshold by 15, `patient` raises it by 15 [D3]. Children never call; their needs reach crew through incidents or observation.
- `drink_served` and `meal_served` are not picked by the passenger: crew offer them. The offer is a decision point where the module scores accepting against the current activity: accept when Refreshment is 15 or more [D3], decline otherwise. A passenger who declines the meal at Refreshment 12 is hungry by descent, which is a story, not a bug. The cart does not wake sleepers (owner, 2026-09-26, ruled in crew.md (D2)): an asleep passenger is skipped and served later by crew.md's rule, and is not offered anything while asleep.

**Answering a call** (passenger side; who answers and when is crew.md (D2)'s): `refreshment` gives a drink (−30 Refreshment and the +15 Bladder pulse, CONCEPT); `reassurance` gives Unease −20 [D3]; `lav_permission` either lets the passenger go (the lav visit starts at once) or is refused, giving Unease −10 [D3] for being acknowledged. A `refreshment` light is cleared when the cart serves the passenger.

**Flows, not activities.** Boarding (walk, stow in the aisle slot, sit), sitting belted through taxi, takeoff and landing, and deboarding are engine flows (F3), not scored activities: the passenger has no choice in them.

**Getting out of a seat.** A window or middle passenger's lateral path crosses the seats between them and the aisle (B1). Each occupied seat crossed adds 4 ticks [D3]; one with a tray down adds 12 [D3] and the eater's Unease +3. A sleeper crossed wakes with chance 0.6 [D3] (section 6). A child's `lav_visit` takes an adult of their group along through a sync point (E1; P7): the child's decision starts it, the adult's current activity is interrupted as by a higher-priority action, and the pair fills two queue places. With no adult of the group awake and reachable, the child waits and decides again at the next decision point.

**Reading light.** The switch-on rule: when the cabin is dimmed, an awake passenger's reading light switches on unless they are on `screen`, and it stays on while they are awake and off `screen`; it switches off when they fall asleep, start `screen`, or leave their seat, and every light switches off when the cabin lights come up. It is a state the engine sets from the activity and the lighting plan, not a choice the passenger scores. A lit reading light beside a `light_sleeper` halves their `sleep` score [D3]. Its other job is observation: with the cabin dimmed, crew see only their own row, except that a seat with its reading light on stays visible from further (owner, 2026-09-26, ruled in crew.md (D2), which sets both reaches).

**Choosing a lav and queuing.** The passenger picks the open lav with the lowest path ticks plus 3 minutes per person queued [D3]. If the lav queue node is full, they wait in the nearest aisle slot behind it, which is what blocks the cart (CONCEPT pillar 3's canonical story). A passenger overflowing for 8 minutes with Bladder under 80 [D3] goes back to their seat and decides again.

### 4. Traits

Trait-class modifiers (the rule of CONCEPT section 4: modifiers in one class multiply) and the Unease baseline. The baseline with no Unease trait is 10 [D3]. Every value is [D3].

| Id | Trait-class modifiers | Unease baseline | Behaviour hooks | Adult weight |
|---|---|---|---|---|
| `anxious` | Unease ×1.3 | 25 | | 12 |
| `nervous_flyer` | Unease ×1.5 | 15 | | 10 (5 for `business` trips) |
| `frequent_flyer` | Unease ×0.8 | 5 | `sleep` bonus | 8 (30 for `business` trips) |
| `small_bladder` | Bladder ×1.3 | | | 10 |
| `big_appetite` | Refreshment ×1.25 | | | 10 |
| `restless` | Boredom ×1.4 | | `stretch` bonus | 12 |
| `patient` | Boredom ×0.8, Unease ×0.9 | | calls 15 later | 10 |
| `demanding` | Unease ×1.1 | | calls 15 earlier | 8 |
| `light_sleeper` | | | wake chance ×2 (capped at 1); no sleep with lights up | 12 |
| `heavy_sleeper` | | | wake chance ×0.3 | 8 |
| `sociable` | Boredom ×0.9 | | `chat` bonus | 15 |
| `child` | Boredom ×2.0 (CONCEPT), Bladder ×1.2, Refreshment ×1.1 | 10 | never calls; lav visit escorted | given to every child, never drawn |

Pairs that never co-occur: `nervous_flyer` with `frequent_flyer`, `light_sleeper` with `heavy_sleeper`, `patient` with `demanding`. A child draws its one optional trait only from `anxious`, `restless`, `small_bladder`, `light_sleeper` and `heavy_sleeper`. CONCEPT's worked example (an `anxious` `nervous_flyer` in turbulence while tired: Trait 1.3 × 1.5) is reachable from this list.

Traits an event needs as a trigger condition (a heart condition for a medical event, say) are added by event-writer in D4 with no rate modifier; this list holds only traits the need system reads. One is ruled now: **`short_tempered`** (owner, 2026-09-26), an event-only trait with no rate modifier and no behaviour hook here, read by event triggers (a fight, say). Adult weight 8 [D3]; never on a child; it may co-occur with any other trait.

### 5. The manifest

Every share and range is [D3]. The generator draws bookings (a booking is a group) until the load is reached, then seats them by the scenario's fixed default order (F2: groups together, then by class), a child always beside an adult of their group when a seat group allows.

- **Load factor:** 0.90 to 1.00 of 180 seats, uniform (the performance test forces 1.00).
- **Seat class:** 10 to 12 of the 12 business seats booked; the rest of the load in economy.
- **Trip purpose** (per booking): in business, 70% `business`, 30% `leisure`; in economy, 20% `business`, 55% `leisure`, 25% `visiting`.
- **Group size** (per booking): `business` trips 1 at 80%, 2 at 20%; `leisure` 1 at 20%, 2 at 45%, 3 at 15%, 4 at 15%, 5 at 5%; `visiting` 1 at 45%, 2 at 25%, 3 at 15%, 4 at 15%.
- **Children:** a `leisure` booking of 3 or more is a family at 60% and a group of adults otherwise; a `visiting` one at 70%. A family has 2 adults and the rest children. `business` bookings have no children. By hand from these shares, about 11% of passengers are children (about 20 on a full flight), unmeasured.
- **Traits:** an adult draws 0 traits at 35%, 1 at 45%, 2 at 20%, by the weights in section 4 without replacement and skipping forbidden pairs; a child has `child` plus one more at 40%.
- **Professions** (adults): `office_worker` 40%, `trades` 15%, `retired` 12%, `student` 10%, `teacher` 8%, `nurse` 3%, `doctor` 2%, `off_duty_crew` 1%, `other` 9%; `business` trips never draw `retired` or `student`. Professions carry no modifier in M1: they exist for events ("is there a doctor on board?") and the report's words (P6).

### 6. The Rest body clock (day departure)

The reference flight is a day departure (CONCEPT section 8, question 10). The numbers below assume boarding starts 09:30 origin local; crew.md (D2) owns the timeline, and the clock moves with it.

- **Wake time** is drawn per booking and shared by its members within 15 minutes [D3]: `business` 05:00 to 06:30, `leisure` 06:00 to 08:30, `visiting` 05:30 to 08:00.
- **Rest at boarding** = 6 × hours awake, plus 15 if the passenger woke before 06:00 (a short night), capped at 60 [D3]. An early business traveller boards at about 39; a family that woke at 08:00 boards at about 9.
- **Rise while awake** = 5 per hour [D3] × a body-clock factor by origin-local time, applied as a Trait-class modifier (it is who the passenger is, so it multiplies with `child` rather than adding across classes): 1.0 before 12:00, 1.2 from 12:00 to 13:00, 1.5 from 13:00 to 16:00 (the post-lunch dip) [D3]. The post-meal slump (section 3) stacks on it as a Service-class ×1.3.
- **Falls only while asleep** (CONCEPT), at the `sleep` rates of section 3.
- **Wake-ups.** A sleeper wakes when crossed by a neighbour leaving their seat (0.6), when crew wake them on purpose (crew.md (D2); the cart never does), in moderate turbulence (0.3 per 5 minutes), or when an incident is raised within witness reach (0.5) [D3]; `light_sleeper` and `heavy_sleeper` scale each chance. Each wake-up counts on the passenger and is recorded, so the report can say "22C was woken three times by 22A".

The shape this gives: on a day flight most passengers do not sleep; after the meal, early risers and business travellers doze if the cabin is dimmed. The lighting lever works through the `sleep` score and the recovery rate, so the lights-up variant costs sleep minutes rather than raising Rest everywhere.

### 7. Unease contagion and witnessing

**Contagion (CONCEPT section 8, question 7: Unease only, adjacent seats and across the aisle, Event class).** Reach, for each passenger: the seats beside them in their own seat group, and the seat directly across the aisle in the same row. Rows ahead and behind are not in reach (P3). Each neighbour n in reach whose Unease is above 40 contributes an Event-class modifier on this passenger's Unease of 1 + w × (U_n − 40) / 60, with w = 0.25 beside, 0.15 across the aisle [D3]; the modifiers multiply within the Event class. Two neighbours at 100 give 1.5625, before the cap of 2.5. Because the multiplier scales pushes (section 2; P2), contagion amplifies a shared cause (turbulence, a delay) and fades in calm cruise: a panic in a calm cabin unsettles a row, and a panic in turbulence tips it. Asleep neighbours neither spread nor take contagion.

**Witnessing (P2, P3).** When an incident is raised at a seat, every awake passenger within 2 rows ahead or behind takes a one-off Unease pulse of +8; when an incident is missed and its consequence fires, +15 [D3]. This is what carries a panic "two rows away" (CONCEPT section 4's Unease story) without letting contagion chain the length of the cabin. Witnessing is under the contagion system switch.

### 8. Need failures and incidents (OD6)

Each need raises its own incident kind when it stays at or above its failure threshold for its sustain window (C3's `SustainGate`; 10 sim minutes is CONCEPT's working figure). An incident is handled when crew reach the passenger before the escalation deadline, and missed otherwise (CONCEPT section 6); how crew get there is crew.md (D2)'s. After an incident, the same passenger cannot raise the same kind again until that need has dropped below its threshold minus 30 [D3]. Fights still come only from events (OD6).

| Kind | Need | Raised at [D3] | What handling does [D3] | Deadline [D3] | Missed: the consequence [D3] |
|---|---|---|---|---|---|
| `accident` | Bladder | ≥ 90 for 10 min | crew get the passenger to a lav ahead of the queue | 5 min | the accident: Bladder to 0, Unease +40, a clean-up task (crew.md (D2)) |
| `panic` | Unease | ≥ 80 for 5 min | a crew member at the seat: Unease −30 | 8 min | the panic plays out: Unease held at 90 or more for 10 min, which feeds contagion and witnessing |
| `food_demand` | Refreshment | ≥ 90 for 10 min | crew bring a drink or snack: Refreshment −30 | 10 min | the passenger walks to the galley to demand food: aisle traffic, Unease +15, and a step of strain for the crew there (crew.md (D2)) |
| `noise_complaint` | Rest | ≥ 90 for 10 min while awake | crew mediate: the target cannot `chat` for 30 min, or the complainant is offered a sleep bonus | 10 min | an argument: complainant and target Unease +20; witnessing |
| `disruptive_passenger` | Boredom | ≥ 90 for 10 min | crew engage (a snack, a colouring kit, an IFE reset): Boredom −40 | 15 min | the disruption runs 30 min: neighbours in contagion reach take Unease ×1.2 (Context class) and a wake chance of 0.3 per 5 minutes |

A `noise_complaint` names a target (P4): the neighbour who most recently woke the passenger, else a chatting or disruptive neighbour in reach, else the cabin lighting. That target is how the report connects a Rest failure to its cause. On a day flight a `noise_complaint` is rare by design (section 6 caps Rest well below 90 for most passengers); it earns its place on night flights later.

Unease's threshold and window are set tighter than the others because a panic is a fast failure; the other four hold CONCEPT's working figure. Whether a window is per need is a D3 number, not a new mechanism.

### 9. What a crew observation captures (OD3)

**By sight**, for every seat in a crew member's observation reach (crew.md (D2) sets the reach from an aisle slot, narrowed to the crew member's own row with the cabin dimmed, except seats with a reading light on; section 3), stamped with the tick:
- where the passenger is: in the seat, or seen in the aisle, the lav queue, the lav or the galley (an empty seat records "empty" and the passenger's entry records where they were last seen);
- what they are doing: asleep, eating or drinking, on a screen, chatting, awake and idle, standing, queuing;
- the call light, on or off (its reason is not visible);
- the distress band (section 2);
- any active incident at the seat, always.

The five need values are never captured by sight.

**By interaction**, a need is revealed as a band **[D3]**: `fine` (under 40), `wants` (40 to 74), `urgent` (75 and over), stamped with the tick and the crew member who read it, ageing like everything else (P5):

| Interaction | Reveals |
|---|---|
| Boarding greeting at the door | Unease |
| Drinks round pass (served or declined) | Refreshment, Boredom |
| Meal round pass (served or declined) | Refreshment, Rest |
| Tray collection | Refreshment |
| Call button answered | the call's reason need (`refreshment` → Refreshment, `reassurance` → Unease, `lav_permission` → Bladder), and Unease |
| Crew reaching an incident | the failing need, and Unease |
| An event's crew task | the needs the event module declares it reveals (event-writer, D4), and Unease |

A sleeping passenger passed over reveals nothing beyond "asleep". The check-in walk reveals by sight only, which is what makes staleness honest: a walk shows that 23F looks uneasy, not why.

**The read rule (P5).** The owner's words: "A band based on experience, more experienced / empathetic crew get a more accurate value than junior / apathetic crew." This is the one statement of the rule; crew.md (D2) carries the attributes and points here.
- **Who reads well.** A crew member's read accuracy is `a = min(0.3 + 0.35 × c + 0.35 × e, 0.95)` [D3], where c is competence and e is empathy, both 0 to 1 from crew.md (D2). Nothing else enters it: not fatigue, strain or crew traits (owner, 2026-09-26).
- **When a read can be wrong.** Only when the true value lies within 15 above the lower edge of its band [D3]: 40 to 54 (a `wants` that could pass for `fine`) or 75 to 89 (an `urgent` that could pass for `wants`). There the read is right with chance a, and otherwise records the band one below. Anywhere else the read is exact: a passenger at 95 always reads `urgent`, and a `fine` is never misread.
- **Which way.** Always toward fine (owner, 2026-09-26): a weak read misses need and never invents it.
- **The draw** is one `NextDouble` from the reader's own stream `crew/<id>/read` (a name added to R5's list), taken only for a read in a misreadable range, so reads never shift auto-resolve's draws on `crew/<id>` and replay exactly.
- **Newest read wins.** A newer read of the same need replaces the older one whoever made it, so a senior's pass corrects a junior's.
- **Crew act on what they read (owner, 2026-09-26).** Any crew rule that consults a passenger's need consults the recorded band, never the true value, so a misread can delay help and turn into a missed incident. On the passenger side: a `lav_permission` call is granted when the Bladder read is `urgent` and refused otherwise; which crew.md (D2) rules also consult reads is crew.md's to name. Incidents and everything seen by sight, the distress band included, are exact (owner, 2026-09-26).

Worked, with crew.md (D2)'s roster: the purser (c 0.85, e 0.70) is right 84% of the time in a misreadable range, fa4 (0.45, 0.30) 56%, fa6 (0.35, 0.75) 69%. A Bladder at 82 read by fa4 comes back `wants` 44% of the time; a refused `lav_permission` then leaves the passenger in their seat as Bladder climbs through 90 toward `accident`.

The observation stores the band read, never the true one. The true band, and whether the read was a misread, go to the dev surface only; a misread that precedes a missed incident for the same passenger is a moment ("fa4 read 23F's Bladder as `wants` at 01:12; `accident` missed at 01:40").

## Player-facing feedback

**Player surface (M3 onward, nothing in M1).** The crew-observed view of section 9: position, activity, call light, distress band, revealed need bands as the crew member read them, each with its age. No need number reaches the player (pillar 5): the band tells the player whether to act, and the lever is the action.

**Dev surface (M1).** The decision log (top three candidates per decision, R16) through `Sky.Sim decisions`; the balance CSV's columns come from D3. Moments this doc feeds to the report (G3): a passenger's peak with its dominant need and driving classes; wake-ups with who crossed; a declined meal; a call light's wait; an incident with its target, its handler or why it was missed; a lav overflow into the aisle and what it blocked; a misread band before a missed incident; a passenger up with the sign on in cruise.

## Interactions

| System | Passengers → it | It → passengers |
|---|---|---|
| Crew and the task board (crew.md (D2)) | call-button tasks with reasons; incidents to reach; aisle bodies that block carts and crew; galley visitors (`stretch`, `food_demand`) in the crew's break space | service deltas; wake-ups; answered or unanswered calls; tray collection freeing window passengers |
| Service plan lever | acceptance and declines | Refreshment timing; the Bladder pulse and the lav wave 30 to 60 min later; the post-meal Rest slump |
| Lavs lever and layout | queue length, aisle overflow | `lav_visit` choice and walk length; a locked aft lav sends the aft cabin forward past the cart |
| Lighting lever | reading lights on with the cabin dimmed | `sleep` score and recovery rate; how far crew see |
| Crew count, zones and roster | how many calls and incidents wait | how fast calls and incidents are reached; how often seats are observed; how accurately revealed needs are read |
| Events (event-writer, D4) | trait, profession, group and need facts for triggers | delayed consequences on needs and incidents; the seat-conflict event (OD5) moves group adjacency, and so `chat`, child escort and split-group Unease |
| Feed (phase, sign, turbulence) | | Unease pushes; seatbelt limits on activities; the last lav rush |
| Scoring | distress series (experience), incidents (handled or missed), moments | |

A split group (a seat-conflict event unresolved, or seating that could not keep a family together) gives a child with no adult of their group beside them Unease ×1.3 and those adults Unease ×1.2, Context class [D3]. That is what makes OD5's event matter after boarding.

## Edge cases and failure modes

- **Needs switched off:** needs hold at their starting values and never fail; activities still score on the frozen values, so passengers board, sit, take service and deboard; distress is computed from the frozen values. No need incident raises.
- **Contagion switched off:** no Event-class contagion modifier and no witnessing pulses; the split-group and disruption modifiers stay (they are Context class, about seating and noise, not mood spreading).
- **Events switched off:** need incidents still raise (they are not events); nothing changes group adjacency after boarding.
- **A throwing activity module:** that activity scores as unavailable for the flight (S2); `idle` must never be the one to throw, so the engine falls back to `idle` if every module is disabled.
- **Asleep when the cart passes:** skipped, not woken; served later by crew.md (D2)'s rule. If they wake first with Refreshment high, their `refreshment` call is the other way back.
- **Up with the sign on in cruise (P8):** the passenger keeps going to the lav; the "back to your seat" task (crew.md (D2)) meeting them before they reach it sends them back, and their Bladder keeps its Unease cascade.
- **Served while in the lav or queuing:** the cart does not wait (crew.md (D2)); the passenger is unserved and their `refreshment` call later is the recovery path. Returning while the cart holds their row, they wait in the aisle slot behind it.
- **Both lavs locked or unreachable:** `lav_visit` scores zero; Bladder climbs with the "cannot reach a lav" Unease cascade (CONCEPT) and ends in `accident` incidents, which is the right signal for a broken lever setting.
- **Group member deboards or is absent:** `chat` becomes unavailable; no other effect.
- **Descent with a tray still down or a queue in the aisle:** cabin secure is crew.md (D2)'s; passengers return to seats when the sign comes on for landing, abandoning the queue.
- **Incident while asleep:** Bladder and Refreshment keep rising only as their rates allow (Boredom is paused, Rest falls), so a sleeper can raise `accident` on waking but not `disruptive_passenger` while asleep.

## Out of scope for M1

Lap infants (under 2, no seat), unaccompanied minors, assistance passengers, passenger-carried food and drink, walking to the galley to ask for water outside `food_demand`, alcohol, seat recline disputes, time zones and jet lag (the body clock uses origin time only), cabin temperature, WiFi, and a child running in the aisle (the disruption is modelled on the seat's neighbours only).

## Notes for D3 (arithmetic from CONCEPT's figures, unmeasured)

- **Bladder.** At CONCEPT's 100 per 6 hours, a passenger boarding at the mean start of 10 with a drink and a meal reaches 10 + 41.7 + 30 = 81.7 by doors open, 2.5 hours later. If every passenger visits once at about 3.5 minutes a visit, that is 630 lav-minutes against 180 (2 lavs over a 90-minute cruise window); even 30% visiting (54 visits, 189 lav-minutes) keeps both lavs busy for the whole window. Either the base rate comes down, or the lav threshold sits high enough that about a third visit and the rest hold to the gate below the failure threshold. The second keeps the lav wave as a story; the first makes it vanish.
- **Boredom.** At 100 per 3 hours, an adult with no `screen` reaches about 15 + 83 = 98 over the flight, and a child at 2x in about 1.3 hours. Boredom is quiet on the baseline only if `screen` is available to most seats; P1 puts IFE at every seat, so Boredom stays quiet until an outage event takes it away, and a child without IFE reaches `disruptive_passenger` range within the flight.
- **Unease.** With the pull's rate of ln 2 / 0.25 h ≈ 2.77 per hour, a steady push P holds Unease about P / 2.77 above baseline: moderate turbulence (86 per hour) on an `anxious` `nervous_flyer` (×1.95) holds about 60 over a baseline of 25, so about 85, which raises `panic` if it lasts 5 minutes. That is the intended top of the range, not the middle.

## Owner rulings

Ruled by the owner on 2026-09-26 from ranked options; options not taken are recorded with their worst case.

- **P1. IFE: seatback IFE at every seat, which an outage event can remove.** Not taken: personal devices for about 70% (worst case: a third of the cabin runs out of Boredom headroom on every baseline flight); no IFE (worst case: `disruptive_passenger` on most seeds).
- **P2. Contagion is an amplifier of the passenger's own Unease pushes (Event class), plus a witness pulse.** Not taken: contagion as a push of its own (worst case: a row stays high after its cause has gone, the avalanche of CONCEPT question 7).
- **P3. Contagion reach is the same row only (beside, and across the aisle); the witness pulse reaches 2 rows ahead and behind.** Not taken: seats ahead and behind in contagion reach (worst case: a chain along 28 rows).
- **P4. The Rest incident is `noise_complaint`, aimed at whoever kept the passenger awake.** Not taken: an untargeted `exhaustion` complaint (worst case: an incident with no cause, failing pillar 3).
- **P5. A revealed need is a band whose accuracy depends on the crew member who read it:** "A band based on experience, more experienced / empathetic crew get a more accurate value than junior / apathetic crew" (owner's words); the rule is in section 9. Follow-up rulings (owner, 2026-09-26): accuracy comes from competence and empathy only, never fatigue; a misread errs toward fine; crew act in M1 on the band they read; the distress band seen by sight is exact. Not taken: the exact value (worst case: numbers on the player surface that serve no lever, against pillar 5).
- **P6. Professions are kept with no modifiers; only events and the report's words read them.** Not taken: professions with rate modifiers (worst case: a second trait system); no professions in M1 (worst case: reopens a kickoff decision).
- **P7. A child's lav visit takes an adult from their group along, through a sync point.** Not taken: children go alone.
- **P8. A passenger leaves their seat with the sign on only at Bladder 85 or more and only in cruise; crew then get a "back to your seat" task, which crew.md (D2) owns.** Not taken: never leaving with the sign on (worst case: long turbulence turns into `accident` incidents the cabin cannot prevent).

Rulings from crew.md (D2) that this doc follows: with the cabin dimmed, crew observation narrows to their own row, and a passenger with a reading light on stays visible from further (section 3, section 9); the cart does not wake sleepers, who are skipped and served later (section 3).

## Success criteria

Measured with `Sky.Sim` on the reference scenario over seeds 1 to 500 unless named; every threshold is [D3].
- The canonical story shows: on at least 60% of seeds, the aft lav queue's longest overflow into the aisle falls 20 to 60 minutes after the drinks round reaches the aft zone.
- Every activity in section 3 is chosen by at least one passenger on at least 95% of seeds, and the median passenger chooses at least three different activities in a flight (decision log).
- Pillar 4: no incident kind raises on more than 10% of seeds; every kind raises on at least one seed of the reference or a lever variant.
- The lighting lever: the `lights-up` variant cuts total sleep minutes by at least 30% against the reference over seeds 1 to 200.
- Traits are legible: `nervous_flyer` passengers' mean peak Unease during takeoff and climb is at least 15 above passengers with no Unease trait.
- Contagion does not avalanche: with contagion off, the 10th-percentile experience moves by no more than 5 points against contagion on over the baseline seeds.
- Observation: every revealed need in an `ObservationStore` entry traces to an interaction of the section 9 table at its tick (F5's test).
- Read accuracy is legible: over seeds 1 to 200, in misreadable ranges, crew with accuracy over 0.8 read the true band more often than those under 0.6 by more than the spread; no misread is ever upward; and at least one missed incident across the sweep follows a misread of the same passenger's failing need.
