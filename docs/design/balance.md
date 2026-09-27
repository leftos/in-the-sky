# Balance

Status: owned by `balance-analyst`. Seeded 2026-09-26 for M1 step D3 (`docs/plans/2026-09-26-m1-headless-cabin-flight.md`), from the owner-approved `docs/design/passengers.md` (D1) and `docs/design/crew.md` (D2) — both drafted and approved 2026-09-26 — and `docs/design/events.md` (D4), landed 2026-09-26. `Sky.Sim` does not exist yet: none of waves M1-A through M1-Z (except D1, D2, D4) has landed, so there is no seed to run and nothing here is measured. Every number below is a first value reached by arithmetic against CONCEPT's own targets, and is **computed, unmeasured** unless a row in section 6's Runs log says a seed sweep checked it. Amended 2026-09-26 by `balance-analyst` for three owner rulings landed after this doc's first draft: the thought reveal widened from check-ins to every crew interaction (`passengers.md` section 9, P12), the due galley break's hold raised from 35 to 50 and a crew member on a break made the last choice for pre-emption (`crew.md` ruled 13), and only a full break resetting the 60-minute no-break strain clock (`crew.md` ruled 14). Terms are in the glossary in [docs/README.md](../README.md).

How to read this doc: section 1 restates CONCEPT's targets (rates, the rate rule, the sustain window, the four outcome formulas, the section 7 acceptance numbers) — quoted, not this doc's to change. Section 2 collects every number `passengers.md` and `crew.md` mark `[D3]` or `(FV)`, one table per subsystem, each citing its source line and the outcome(s) it moves. Section 3 gives the numbers D1, D2 and D4 name as D3's to author outright — a formula, a cadence, a threshold nobody set a value for yet — rather than a marked constant. Section 4 is a scope note, not a correction, since no arithmetic error turned up in D1/D2's own worked examples. Section 5 lists what a future implementer needs that no scenario switch or CSV column covers yet. Section 6 is the empty Runs log Z6 appends to.

## 1. Targets (CONCEPT, not this doc's to change)

### 1.1 Need rates, the rate rule, the sustain window (CONCEPT section 4)

| Need | Base rate | Pulse, fall or pause |
|---|---|---|
| Refreshment | rises, full scale in 4 sim hours → **25.00 per hour** | a served drink −40, a meal −70, a call-button drink −30 (CONCEPT) |
| Bladder | rises, full scale in 6 sim hours → **16.67 per hour** | any drink (served, or riding with a meal) adds +15 spread over the next 30 minutes — the drink-to-Bladder pulse (CONCEPT) |
| Rest | rises by the body clock (passengers.md section 6: 5 per hour × a time-of-day factor, Trait class); falls only while asleep | falls 25 per hour with the cabin dimmed, 15 per hour with lights up (passengers.md `[D3]`) |
| Unease | not a clock: pulled toward the passenger's baseline, half-life ≈15 sim minutes → pull constant λ = ln 2 / 0.25 h ≈ **2.773 per hour** (per-tick factor 0.5^(1/3600), since 15 min = 3,600 ticks at 250 ms) | pushed by push sources (section 2.2) |
| Boredom | rises, full scale in 3 sim hours → **33.33 per hour** (children ×2.0, CONCEPT → 66.67 per hour); paused while asleep or on IFE | a drink pass −10, a meal pass −10, `stretch` −15, `chat` −20 per hour (passengers.md `[D3]`) |

Rate-modifier rule (CONCEPT section 4, section 8 question 2): `m = clamp(1 + Σ_class (Π_i m_i − 1), 0.2, 2.5)`. Floor **0.2x**, cap **2.5x**, six source classes: Trait, Context, Cascade, Service, Event, Phase.

Cascades (CONCEPT section 4, fixed, not D3's to change): Refreshment above 70 gives Unease ×1.1; Rest above 80 gives Unease ×1.2; Bladder above 70 while the passenger cannot reach a lav gives Unease ×1.3 — all Cascade class, so they multiply with each other and add as one delta against the other classes.

Sustain window: CONCEPT's working figure is 10 sim minutes. `passengers.md`'s incident table (section 2.13 below) answers the doc's own open question ("whether a window is per need is a D3 number") by setting it per kind: 10 minutes for four kinds, 5 minutes for `panic` (Unease is the fast-failing need, CONCEPT section 4).

### 1.2 The four outcome measures (CONCEPT section 6)

| Outcome | Formula |
|---|---|
| Experience | per passenger: `100 − (0.4 × peak + 0.3 × end + 0.3 × mean)`, peak held ≥5 sim minutes, end over the last 20 minutes before leaving, mean over the flight; flight measure: the **10th percentile**, shown with the median and the count under 40 |
| Incidents handled or missed | handled ÷ total, the median time from incident to crew arrival, every missed incident named |
| Crew strain | the **peak strain of the most-strained crew member**, plus the minutes any crew member spent over the redline |
| On-time doors | cabin ready against plan (departure), deboarding duration against a target (arrival); both in minutes late, 0 when on time |

### 1.3 M1 acceptance numbers (CONCEPT section 7)

| Check | Number |
|---|---|
| Baseline quiet | no incident missed on **at least 90%** of seeds |
| Lever checks | **200 seeds** each: service plan → Refreshment; one lav locked → Bladder and the experience 10th percentile; lighting → Rest; 4 crew vs 6 → strain and missed incidents |
| Invariant fuzz | **500 seeds**, no exceptions, no invariant failure |
| Performance | **64x** at full cabin: at least **256 ticks per wall-clock second** at 250 ms ticks |
| Events | at least **3** new events fire across a 500-seed sweep, every one auto-resolved; low-competence or high-fatigue crew pick an event's worse choice measurably more often than competent, rested crew |
| Pillar 4 (`passengers.md`'s own success criteria, carried here since it bounds the same sweeps) | no incident kind raises on more than **10%** of seeds; every kind raises on at least one seed of the reference or a lever variant; `nervous_flyer` passengers' mean peak Unease during takeoff/climb is at least **15** above passengers with no Unease trait; with contagion off, the 10th-percentile experience moves by no more than **5** points against contagion on |
| Gate and thoughts (`passengers.md`'s success criteria, added in the 2026-09-26 triage amendment, section 2.25/2.28 below) | every revealed thought traces to a crew interaction or a check-in walk at its reveal tick (fog-of-war, H4's test), and thoughts-off gives the same end-state hash as thoughts-on over seeds 1-16; at least **60%** of thoughts revealed in cruise on the reference are praise or `nothing to report`; each lever variant moves its matching thought kind over seeds 1-200; a 60-minute closed-concessions scenario raises mean starting Refreshment by at least **20** and at least doubles first-hour `refreshment` calls, over seeds 1-200 |

## 2. First values collected from `passengers.md` (D1) and `crew.md` (D2)

Every number in this section is copied from the two design docs, cited to the line it was read at (`Read` tool, 2026-09-26). None of it is authored here; section 3 is the numbers D3 authors outright.

### 2.1 Starting needs at boarding (`passengers.md:36`)

| Need | Draw at boarding | Moves |
|---|---|---|
| Refreshment | 10 to 40 | Experience (mean, since it is the largest single starting offset toward `food_demand`) |
| Bladder | 0 to 20 (most used the terminal) | Experience, and the lav-queue timing that Incidents (`accident`) depends on |
| Rest | from the body clock (section 2.11) | Experience |
| Unease | at the passenger's baseline (section 2.9) | Experience, Incidents (`panic`) |
| Boredom | 5 to 25 (the gate wait) | Experience, Incidents (`disruptive_passenger`) |
| Group draw spread | Refreshment and Boredom within 10 of each other for group members | shapes how correlated a group's Experience is, which the report's clustering (CONCEPT section 6) reads |

### 2.2 Unease push sources and one-off pulses (`passengers.md:38`)

| Source | Push | Moves |
|---|---|---|
| Being aboard | +6 per hour | Experience (the quiet-cruise floor every passenger drifts against) |
| Takeoff and landing | +30 per hour | Experience (the takeoff/climb peak CONCEPT's pillar 4 example reads) |
| Climb and descent | +10 per hour | Experience |
| Light turbulence | +30 per hour | Experience, Incidents (`panic`) |
| Moderate turbulence | +80 per hour | Experience, Incidents (`panic`), and contagion (section 2.10) |
| A delay past the planned pushback | +15 per hour, once more than 5 minutes late | Experience, Doors (a late pushback is what triggers this push in the first place) |
| The passenger's own call button unanswered | +20 per hour, once unanswered more than 5 minutes | Experience, Incidents (a missed call reads as slower crew arrival) |
| Woken from sleep | +5, a one-off pulse spread over 2 minutes | Experience, Rest (fewer sleep minutes recovered) |
| An incident seen nearby | the witness pulse, section 2.10 | Experience, contagion |

### 2.3 Seat comfort modifiers (`passengers.md:40`)

| Seat | Rest modifier | Unease modifier |
|---|---|---|
| Business | ×0.85 | ×0.9 |
| Economy middle | ×1.1 | ×1.1 |
| Economy window or aisle | ×1.0 | ×1.0 |

Moves: Experience (Rest and Unease trajectories differ by cabin class, so the report can attribute a bad Experience to seating). The doc calls this "a formula D3 owns" computed from pitch and width in inches; M1 ships only the reference layout (X1), and the M1 plan's own scope list excludes the layout editor and layout `extends`/`overrides` until M2 onward, so a continuous inches-to-modifier formula has nothing to interpolate against yet. These three reference-layout constants are the complete formula M1 needs; a continuous version is deferred to whichever step first ships a second layout, not ruled here.

### 2.4 Distress bands, read by sight (`passengers.md:42`)

| Band | Range |
|---|---|
| `calm` | under 30 |
| `uneasy` | 30 to 59 |
| `distressed` | 60 and over |

Moves: what the crew-observed view shows (M3+); in M1 it is a dev-surface field only, but it is also the anchor section 3.1's distress-weight formula is calibrated against.

### 2.5 Activities: durations and need effects (`passengers.md:48-58`)

| Activity | Duration | Need effect | Moves |
|---|---|---|---|
| `idle` | until the next decision point | none (Boredom rises at its base rate) | Experience (Boredom), and it is the activity the re-evaluation cadence (section 3.2) exists for |
| `sleep` | 20 to 90 min, or until Rest ≤10, or woken; falls 25/hour dimmed, 15/hour lights up | Rest down | Experience (Rest), the `lights-up` lever check (CONCEPT section 7) |
| `screen` | 15 to 60 min | Boredom paused | Experience (Boredom), Incidents (`disruptive_passenger`) |
| `chat` | 5 to 20 min; Boredom falls 20/hour | Boredom down | Experience |
| `lav_visit` | walk + queue + use, 2 to 5 min (child 3 to 6) + walk back; Bladder to 0 on completion | Bladder down | Experience, Incidents (`accident`), Doors (a queue in the aisle blocks a cart, CONCEPT pillar 3) |
| `stretch` | 3 to 8 min; Boredom −15 on completion | Boredom down | Experience |
| `call_crew` | instant press | posts a task (crew.md) | Incidents (median time to crew arrival) |
| `drink_served` | 3 to 6 min; Boredom −10 on the pass | Refreshment −40, Bladder +15 over 30 min (CONCEPT), Boredom −10 | Experience, Bladder's lav wave (Incidents, `accident`) |
| `meal_served` | 15 to 25 min, then tray down until collected; after it, Rest ×1.3 Service class for 60 min | Refreshment −70 (CONCEPT), Bladder +15, Boredom −10, then the post-meal Rest slump | Experience, Doors (a tray-down passenger cannot squeeze past for the secure check) |

### 2.6 Activity scoring thresholds (`passengers.md:60-67`)

| Activity / call reason | Threshold | Moves |
|---|---|---|
| `sleep` | scores from Rest 45 upward; halved with lights up, zero for `light_sleeper` with lights up; zero during boarding/deboarding, with the call light on, with a tray down, or within 10 min of being woken | Experience (Rest), `lights-up` lever check |
| `screen` | scores from Boredom 20 upward; zero when an outage event has removed it | Experience (Boredom) |
| `lav_visit` | scores from Bladder 55, rises steeply past 75; zero with the sign on except in cruise at Bladder ≥85 (defies the sign); descent Bladder ≥40 gets the last-chance bonus | Experience, Incidents (`accident`) |
| `stretch` | only in cruise with the sign off, from Boredom 50 | Experience |
| `call_crew: refreshment` | Refreshment ≥65, not served in the last 30 min, cart not in zone | Experience, crew catch-up drink (crew.md 2.19) |
| `call_crew: reassurance` | Unease ≥55 | Experience, Incidents (`panic`) |
| `call_crew: lav_permission` | Bladder ≥75 with the sign on | Experience, Incidents (`accident`) |
| `demanding` trait | lowers each of the three thresholds above by 15 | Experience, Incidents (more calls posted, sooner) |
| `patient` trait | raises each threshold by 15 | Experience, Incidents (fewer, later calls) |
| accept a served drink/meal | accept when Refreshment ≥15, decline otherwise | Experience (a passenger who declines at 12 stays hungry to descent, by design) |

### 2.7 Answering a call (`passengers.md:69`)

| Call reason | Effect when granted | Effect when refused |
|---|---|---|
| `refreshment` | −30 Refreshment, +15 Bladder pulse (CONCEPT) | — |
| `reassurance` | Unease −20 | — |
| `lav_permission` | the visit starts | Unease −10 for being acknowledged |

Moves: Experience, Incidents (a refused `lav_permission` at high Bladder is what turns a misread into an `accident`, section 2.14).

### 2.8 Movement, lav choice and overflow (`passengers.md:73, 75, 77`)

| Number | Value | Moves |
|---|---|---|
| Ticks per occupied seat crossed | 4 ticks | Experience (small, but stacks for a window/middle passenger past several trays), Doors |
| Ticks per crossed seat with a tray down | 12 ticks, plus the eater's Unease +3 | Experience |
| Wake chance when crossed by a neighbour | 0.6 (scaled ×2 for `light_sleeper`, ×0.3 for `heavy_sleeper`, capped at 1) | Experience (Rest, wake-up count), `lights-up` lever |
| Reading light beside a `light_sleeper` | halves their `sleep` score | Experience (Rest) |
| Lav pick cost | lowest path ticks + 3 minutes per person already queued | Experience, Incidents (`accident`), Doors (queue length feeds cart blocking) |
| Overflow-to-seat threshold | 8 minutes waiting with Bladder under 80 sends the passenger back to their seat | Experience, Incidents |

### 2.9 Traits (`passengers.md:81-100`)

Baseline Unease with no Unease trait: **10**. Every value below is `[D3]`.

| Trait | Modifiers | Unease baseline | Adult weight |
|---|---|---|---|
| `anxious` | Unease ×1.3 | 25 | 12 |
| `nervous_flyer` | Unease ×1.5 | 15 | 10 (5 on `business` trips) |
| `frequent_flyer` | Unease ×0.8, `sleep` bonus | 5 | 8 (30 on `business` trips) |
| `small_bladder` | Bladder ×1.3 | — | 10 |
| `big_appetite` | Refreshment ×1.25 | — | 10 |
| `restless` | Boredom ×1.4, `stretch` bonus | — | 12 |
| `patient` | Boredom ×0.8, Unease ×0.9, calls 15 later | — | 10 |
| `demanding` | Unease ×1.1, calls 15 earlier | — | 8 |
| `light_sleeper` | wake chance ×2 (capped 1), no sleep with lights up | — | 12 |
| `heavy_sleeper` | wake chance ×0.3 | — | 8 |
| `sociable` | Boredom ×0.9, `chat` bonus | — | 15 |
| `child` | Boredom ×2.0 (CONCEPT), Bladder ×1.2, Refreshment ×1.1 | 10 | given to every child |
| `short_tempered` | none (event-only) | — | 8 |

Moves: Experience directly (Unease baseline, and the Trait-class modifiers on every rate); Incidents (which kind a trait-heavy passenger reaches first — `nervous_flyer` toward `panic`, `small_bladder` toward `accident`); the pillar-4 legibility check (`nervous_flyer` peak Unease at least 15 above trait-free passengers, section 1.3).

### 2.10 Manifest distributions (`passengers.md:104-112`)

| Field | Shares | Moves |
|---|---|---|
| Load factor | 0.90 to 1.00 uniform (performance test forces 1.00) | Doors (deboarding target, section 3.5), Strain (backlog), all four via cabin fullness |
| Seat class | 10 to 12 of 12 business seats booked, rest economy | Experience (seat comfort, section 2.3) |
| Trip purpose, business booking | business 70%, leisure 30% | Unease baselines (business-heavy manifests skew calmer per trait weights) |
| Trip purpose, economy booking | business 20%, leisure 55%, visiting 25% | as above |
| Group size, business | 1 at 80%, 2 at 20% | Doors (split-group event rate), Experience |
| Group size, leisure | 1 at 20%, 2 at 45%, 3 at 15%, 4 at 15%, 5 at 5% | as above, and children (below) |
| Group size, visiting | 1 at 45%, 2 at 25%, 3 at 15%, 4 at 15% | as above |
| Children, leisure booking of 3+ | family 60%, adults-only group 40% | Experience (Boredom ×2, `restless-child` event rate), Incidents |
| Children, visiting booking of 3+ | family 70% | as above |
| Adult trait count | 0 traits 35%, 1 at 45%, 2 at 20% | Experience (how many passengers carry a Trait-class modifier at all) |
| Child trait count | `child` always, plus one more at 40% | as above |
| Professions | office_worker 40%, trades 15%, retired 12%, student 10%, teacher 8%, nurse 3%, doctor 2%, off_duty_crew 1%, other 9% | none in M1 (no rate modifier); feeds the report's words and later event triggers only |

### 2.11 Rest body clock (`passengers.md:114-124`)

| Number | Value | Moves |
|---|---|---|
| Wake time drawn per booking, shared within | 15 minutes | Experience (Rest correlation within a group) |
| Wake time range, `business` | 05:00 to 06:30 | Experience (Rest starting value) |
| Wake time range, `leisure` | 06:00 to 08:30 | as above |
| Wake time range, `visiting` | 05:30 to 08:00 | as above |
| Rest at boarding | 6 × hours awake, +15 if woken before 06:00, capped at 60 | Experience |
| Rise while awake, base | 5 per hour | Experience (already counted in section 1.1) |
| Body-clock factor | 1.0 before 12:00, 1.2 from 12:00-13:00, 1.5 from 13:00-16:00 (post-lunch dip) | Experience |
| Wake-up chance, crossed by a neighbour | 0.6 (also in section 2.8) | Experience |
| Wake-up chance, moderate turbulence | 0.3 per 5 minutes | Experience, Incidents (`panic` via contagion) |
| Wake-up chance, incident within witness reach | 0.5 | Experience |

### 2.12 Contagion and witnessing (`passengers.md:126-130`)

| Number | Value | Moves |
|---|---|---|
| Contagion threshold | neighbours above Unease 40 push this passenger | Experience, Incidents (`panic`) |
| Contagion weight, beside | w = 0.25 | as above; also the "no avalanche" check (section 1.3, ≤5-point 10th-percentile move with contagion off) |
| Contagion weight, across the aisle | w = 0.15 | as above |
| Witness pulse, incident raised | +8 | Experience |
| Witness pulse, incident missed | +15 | Experience, Incidents |

### 2.13 Need failures and incidents (`passengers.md:132-146`)

Reset margin: a passenger cannot raise the same incident kind again until the need drops below its threshold minus **30**.

| Kind | Need | Threshold | Sustain window | Handling effect | Escalation deadline | Missed consequence |
|---|---|---|---|---|---|---|
| `accident` | Bladder | ≥90 | 10 min | crew get the passenger to a lav ahead of the queue | 5 min | Bladder to 0, Unease +40, a clean-up task |
| `panic` | Unease | ≥80 | 5 min | crew at the seat: Unease −30 | 8 min | Unease held ≥90 for 10 min (feeds contagion/witnessing) |
| `food_demand` | Refreshment | ≥90 | 10 min | crew bring a drink or snack: Refreshment −30 | 10 min | walk to the galley: aisle traffic, Unease +15, a crew strain step |
| `noise_complaint` | Rest | ≥90, while awake | 10 min | mediate: target no `chat` for 30 min, or complainant sleep bonus | 10 min | an argument: Unease +20 for both, witnessing |
| `disruptive_passenger` | Boredom | ≥90 | 10 min | crew engage: Boredom −40 | 15 min | disruption runs 30 min: neighbours Unease ×1.2, wake chance 0.3/5 min |

Moves: Incidents directly (this table is the whole "handled or missed" measure); Experience (every missed consequence is an Unease or need spike); the pillar-4 ≤10%-of-seeds ceiling per kind.

### 2.14 Need-read accuracy (`passengers.md:173-183`)

| Number | Value | Moves |
|---|---|---|
| Read accuracy formula | `a = min(0.3 + 0.35c + 0.35e, 0.95)`, c = competence, e = empathy | Incidents (a misread delays the three crew decisions that consult a read, crew.md 2.19) |
| Misreadable ranges | 40 to 54 (`wants` could read `fine`), 75 to 89 (`urgent` could read `wants`) | as above |
| Direction | always toward fine | as above; the pillar-4 legibility check (accuracy >0.8 crews read the true band more often than <0.6, by more than the spread) |

### 2.15 Split-group modifier (`passengers.md:204`)

| Number | Value | Moves |
|---|---|---|
| Child with no adult of their group in their seat group | Unease ×1.3 | Experience, and what makes OD5's `split-group` event (events.md) matter after boarding |
| Those adults | Unease ×1.2 | as above |

### `crew.md` (D2), continuing the same numbering

### 2.16 Task claim and hold priorities (`crew.md:61-79`)

All Claim and Hold values below are `(FV)`. Hold above claim (the cart, business hand service, the galley break) is what lets a task defend itself while running; everywhere else the two are equal (ruled 1). Ruled 13 raised the due break's hold from 35 to 50 (claim stays 45) so it holds above its claim as ruled 1 requires, and made a crew member on a break the last choice for any pre-emption: among the crew a task may pre-empt, whoever is not on a break goes first, then whoever holds lowest, then whoever is nearest; a call button is the one task that still reaches a break, and only once no one else can take it. Moves: Strain (pre-emption steps, section 2.19) and Incidents (a low-claim task waiting behind a cart's hold is CONCEPT's traced miss) for the whole table; Doors specifically for Secure check and Door station; Experience specifically for the drinks/meal round and the two catch-up tasks.

| Task | Claim | Hold |
|---|---|---|
| Secure check | 90 | 90 |
| Incident response, severe | 85 | 85 |
| Event work | 70 | 70 |
| Back to your seat | 62 | 62 |
| Incident response, minor | 65 | 65 |
| Call button | 60 | 60 |
| Check-in follow-up | 57 | 57 |
| Catch-up drink | 55 | 55 |
| Boarding/deboarding help | 50 | 50 |
| Door station | 50 | 50 |
| Galley break, when due | 45 | 50 |
| Catch-up service | 45 | 45 |
| Business hand service | 42 | 60 |
| Drinks round, meal (cart) | 40 | 75 |
| Lav check | 30 | 30 |
| Check-in walk | 20 | 20 |
| Galley break, routine | 10 | 35 |

### 2.17 Task timings and cadences within the table above (`crew.md:64-79, 87, 89, 164`)

| Task / rule | Number | Moves |
|---|---|---|
| Incident response, severe | dwell 3 min | Incidents (time to crew arrival) |
| Incident response, minor | dwell 2 min | as above |
| Event work | posted task withdrawn after 10 min unstarted, into `leave` effects | Incidents, Experience (an event's escalating branch) |
| Call button | dwell 20 s | Incidents |
| Check-in follow-up | posted 10 min after an `urgent` Unease read; dwell 60 s; Unease −15 (Warm trait ×1.4, dwell ×1.5) | Experience, Incidents (`panic`) |
| Catch-up drink | posted unless a round reaches the row within 10 min | Experience |
| Boarding/deboarding help | posted after 30 s stowing/retrieving; cuts remaining bin time by 40% | Doors |
| Galley break, when due | posted at strain ≥50 and 30 min since the last break | Strain |
| Business hand service | posted at round start minus 3 min; drinks 60 s, meal 120 s per row | Experience (business rows), Doors (a late purser meets the carts) |
| Lav check | every 30 min in cruise per open lav, or at 20 uses since the last check; 60 s at the lav | Experience (Bladder, lav condition, section 2.20) |
| Check-in walk | every 30 min in cruise; stop 30 s at an uneasy/distressed passenger, Unease −15 | Experience, Incidents (`panic`) |
| Galley break, routine | posted 45 min since the last break, in cruise | Strain |
| Spill time | 3 min before an idle crew member outside the zone may claim | Incidents, Strain |
| Notice cap | 10 min before the zone lead resolves an unnoticed event | Incidents (a late-noticed event's consequences can land before resolution, per ruling 2's accepted worst case) |

The "since the last break" clocks in the two galley-break rows above (30 min for a due break, 45 for a routine one) count from the end of any break, full or cut short. That is a different clock from the 60-minute no-break multiplier in section 2.20's strain table: crew.md's ruling 14 ties that one specifically to the end of the last **full** break (one that ran its 10-minute minimum), so a break cut short still gives its −3-per-minute strain relief (section 2.20) but leaves the multiplier's clock running.

### 2.18 Crew model: fatigue, traits, roster (`crew.md:101-124`)

| Number | Value | Moves |
|---|---|---|
| Fatigue rise | 6 per sim hour on duty, ×2 while strain is over the redline | Strain (via focus), Incidents (via auto-resolve, section 2.19) |
| Fatigue fall, galley break | −0.2 per minute | Strain |

| Crew trait | Effect |
|---|---|
| Steady | fatigue term in focus halved; pre-emption strain steps halved |
| Brisk | service time per row ×0.85; on-task strain rate ×1.15 |
| Warm | check-in/call-button Unease relief ×1.4; those dwells ×1.5 |
| Short fuse | focus ×0.7 at or over the redline; pre-emption strain steps ×1.5 |

| Crew | Competence | Empathy | Trait | Starting fatigue |
|---|---|---|---|---|
| purser | 0.85 | 0.70 | Steady | 20 |
| fa2 | 0.70 | 0.40 | Brisk | 30 |
| fa3 | 0.60 | 0.80 | Warm | 15 |
| fa4 | 0.45 | 0.30 | Short fuse | 35 |
| fa5 | 0.75 | 0.50 | none | 10 |
| fa6 | 0.35 | 0.75 | Warm | 25 |

Moves: Incidents (event auto-resolve, section 2.19), Experience (need-read accuracy, section 2.14), Strain (fatigue feeds strain's ×1.5-over-60-min-without-a-break term, section 2.19). fa6 is the named deliberate mismatch the pillar-4 legibility check depends on.

### 2.19 Auto-resolve focus and pick chance (`crew.md:150-158`)

| Number | Value | Moves |
|---|---|---|
| Focus formula constants | `f = clamp(c × (1 − 0.6 × F/100) × t, 0.05, 0.95)`; 0.6, 0.05 and 0.95 are FV | Incidents (which choice crew pick on an event) |
| Pick-chance exponent | quality raised to the 4th power (computed by repeated multiplication, R6) | as above; this is what turns a 0.9-vs-0.7 quality gap into a 73%/27% split, per the doc's own worked example |

### 2.20 Strain sources and the redline (`crew.md:168-183`)

| Source | Change | Moves |
|---|---|---|
| Time on task (any claimed task but a break, not while seated) | +0.45 per sim minute × (1 + F/100); ×1.5 once 60 min since the end of the last **full** break — one that ran its 10-minute minimum; a break cut short does not reset this clock (ruled 14) | Strain |
| Pre-emption | +5; a second within 10 min adds +8 more | Strain |
| Backlog in own zone | +0.1 per sim minute per task waiting >3 min, capped at +0.3 | Strain, Incidents |
| Arriving at a severe incident | +6 | Strain |
| Idle, not on break | −0.2 per sim minute | Strain |
| Galley break | −3 per sim minute | Strain |
| Redline | 70 | Strain (the whole "minutes over the redline" measure); over it, task durations ×1.15 and fatigue rises ×2 (feeding back into focus, section 2.19) |
| Galley break minimum/cap | lasts at least 10 min, ends at 15 min or strain ≤15; at most 2 crew on break at once with six crew, 1 with four | Strain |

### 2.21 Lav condition (`crew.md:203-205`)

| Number | Value | Moves |
|---|---|---|
| Untidy threshold | about 20 uses since the last check | Experience (Bladder, via a longer visit), Incidents (`accident`) |
| Untidy visit-time penalty | ×1.25 | as above |
| Untidy Unease push | +3 per use | Experience |

### 2.22 Service plan reference defaults and round timings (`crew.md:209-236`)

| Field | Reference default | Moves |
|---|---|---|
| Round order | drinks, then meal | Experience |
| Drinks start | sign-off +5 min | Experience |
| Meal start | sign-off +40 min | Experience |
| Direction | front to back, both rounds | Experience, the `service-back-to-front` lever check |
| Cart travel speed | half walking pace | Doors, Experience |
| Service time per row | drinks 40 s, meal 70 s, both crew working | Experience |
| Seatbelt-sign pause | carts hold in place; if the sign stays on past 10 min the carts return to the galley | Experience, Doors |
| Landing secure check posted | sign-on in descent, 2:12, 12 min before landing | Doors |
| Cart stow at the farthest row | under a minute at cart pace | Doors |
| Cart in the aisle at descent start, target | at most 10% of reference seeds | Doors (this is the acceptance bound crew.md leaves for Z6 to check) |
| Zone landing check complete before touchdown, target | at least 99% of reference seeds | Doors |

### 2.23 The squeeze rule beyond carts (`crew.md:244`)

| Number | Value | Moves |
|---|---|---|
| Squeeze cost | +4 ticks (1 s) on the passer's traversal | Experience, Doors |

### 2.24 Event pacing (`events.md` section 3, D4, already marked "first value, D3")

`events.md`'s status line hands every number it marks this way to this doc from now on. The pacing parameters below are the ones the M1 plan names by name ("event pacing"); the four event specs' own thresholds, chances, minutes, deltas and qualities (`events.md` section 10, plus the `fight` incident's numbers in section 9) stay written where they are, owned by this doc, and are not duplicated into a table here — a future tuning pass edits them in `events.md` and logs the result in section 6 below.

| Number | Value | Moves |
|---|---|---|
| Check cadence | 30 sim seconds | Incidents (how often a trigger gets a chance to fire) |
| Events active at once | 1 (no trigger is asked while one is unresolved) | Incidents |
| Minimum gap after a resolution | 5 sim minutes | Incidents (spacing keeps the baseline quiet, pillar 4) |
| Times one event may fire | once per flight | Incidents |
| Base trigger chance | 0.001 (rare) to 0.02 (common), rolled on `math.random` | Incidents (the 3-events-in-500-seeds acceptance target) |
| Crew work per choice | 1 to 6 minutes | Strain, Doors (competes with the round) |
| Event-work waiting limit | 10 sim minutes (same number as crew.md's event-work row, section 2.17) | Incidents |

### `passengers.md` (D1), amended 2026-09-26 by `game-designer` for the owner's triage of the OpenPax goals (CONCEPT T9-T12)

This amendment landed in the main checkout while this doc was being seeded (git diff, 2026-09-26); the four subsections below cover every `[D3]` mark it added, appended here rather than renumbered into 2.1-2.24 to avoid touching already-cited line numbers.

### 2.25 Gate conditions and their starting-need effects (`passengers.md`, CONCEPT T11, ruling P9)

Two new required scenario fields with no default: `gate_delay_minutes` (integer, 0 to 240) and `concessions_open` (boolean). D is the delay in minutes; every number below is `[D3]`.

| Effect | Formula | Worked: 0 min, open (the reference) | Worked: 60 min, closed |
|---|---|---|---|
| Refreshment added at boarding | +10 if outlets closed, plus min(D × r, 20) with r = 0.05/min open, 0.25/min closed | +0 | +25 (boards 35 to 65) |
| Boredom added at boarding | min(0.2 × D, 20) | +0 | +12 |
| Unease added at boarding (above baseline, then pulled back by the half-life) | min(0.25 × max(D − 15, 0), 15) | +0 | +11 (a `nervous_flyer` boards at about 26) |
| "Late and fed up" modifier | at D ≥30, Unease ×1.1, Context class, from boarding until first served (a round, a catch-up, or an answered `refreshment` call) | not applied | applied |

Moves: Experience (Refreshment, Boredom and Unease starting points, and the Context-class Unease modifier until first served); Incidents (`food_demand` and `panic` reach their thresholds sooner on a delayed, concessions-closed manifest); explicitly **not** Doors (`passengers.md`: "the timeline and the cabin-ready plan are not moved"). The reference scenario is the 0-minute, concessions-open row, so every other number in this doc's sections 1-3 already assumes the gate has no effect; a `gate-delay` lever variant is a candidate for a future Z6/Z7 sweep once X2 ships the field, alongside `passengers.md`'s own new success criterion (a 60-minute closed-concessions scenario raises mean starting Refreshment by at least 20 and at least doubles first-hour `refreshment` calls, over seeds 1-200).

### 2.26 Belongings (`passengers.md`, CONCEPT T9, ruling P10)

Drawn on their own roll, independent of the 0-to-2 personality traits.

| Belonging | Effect | Blunts | Share |
|---|---|---|---|
| `sleep_kit` | `sleep` not halved with lights up (a `light_sleeper` with one is halved, not zeroed); wake chances ×0.5; a lit reading light beside does not halve `sleep` | the lighting plan, and neighbours climbing past | 15% of adults, 25% on `business` trips; never a child |
| `own_device` | `screen` stays available when an outage event removes the seat's IFE | IFE-outage events, and Boredom on a flight that loses IFE | 35% of adults, 60% on `business` trips, 40% of children |

Moves: Experience (Rest via the `lights-up` lever check, and Boredom via IFE-outage events); this is also why the `lights-up` lever's expected effect size (section 1.3's 30%-of-sleep-minutes target) now has a `sleep_kit`-carrying minority that resists it, worth watching once Z6 measures the lever check.

### 2.27 The calming source: `off_duty_crew` as negative contagion (`passengers.md`, CONCEPT T10, ruling P11)

An awake `off_duty_crew` passenger (1% of adults, already logged in section 2.10) whose own Unease is under 40 gives every passenger in their contagion reach an Event-class modifier of `1 − w_calm` on Unease.

| Number | Value | Moves |
|---|---|---|
| w_calm, beside | 0.15 | Experience, and the contagion-avalanche check (section 1.3): a calming source now works against, not just alongside, a panicking one |
| w_calm, across the aisle | 0.10 | as above |

Worked check (from `passengers.md`): a nervous flyer beside both a panicking passenger at Unease 100 (contagion ×1.25, section 2.12) and a calm off-duty pilot (×0.85) composes within the Event class to ×1.0625 ≈ ×1.06, matching the doc's own figure. At 1% of adults (about 1.6 passengers on the reference load), this is a local moment, not a cabin-wide effect; it is under the contagion system switch.

### 2.28 Thoughts catalogue (`passengers.md` section 10, CONCEPT T12, ruling P12)

Thoughts are information only (`passengers.md`: "nothing in the simulation reads a thought... outside the end-state hash and the outcome records"), so unlike every other number in this doc, none of the entries below move any of the four scored outcomes; they shape the M2+ observed view and feed a balance-CSV column counting grumbles born by lever tag, one column a tag (owner-ruled, P12; `passengers.md` section 10). Every threshold, salience and duration is `[D3]`.

| Kind | Valence | Born when | Lever tag | Salience | Lasts |
|---|---|---|---|---|---|
| `lav_queue_long` | grumble | joins a lav queue with 3+ ahead, or overflows into the aisle | `lavs` | 2 | 20 min |
| `stuck_behind_cart` | grumble | waits 2 min or more behind a cart | `service_plan` | 2 | 20 min |
| `still_waiting_for_drinks` | grumble | Refreshment ≥65 and no round has reached the row | `service_plan` | 2 | 30 min |
| `served_late` | grumble | served with Refreshment ≥70 | `service_plan` | 3 | 30 min |
| `drink_welcome` | praise | served with Refreshment <70 | `service_plan` | 1 | 15 min |
| `late_and_fed_up` | grumble | boards carrying the section 2.25 modifier | `service_plan` | 2 | until first served |
| `call_ignored` | grumble | own call light on for 5 min | `crew_staffing` | 3 | 30 min |
| `call_answered_fast` | praise | own call answered within 2 min | `crew_staffing` | 1 | 15 min |
| `lav_untidy` | grumble | finishes a visit to an untidy lav | `crew_staffing` | 2 | 20 min |
| `saw_incident` | grumble | takes a witness pulse | `crew_staffing` | 3 | 30 min |
| `woken_up` | grumble | woken by a neighbour, crew or turbulence | `moment` (a neighbour woke them) else `lighting_plan` | 2 | 30 min |
| `too_bright_to_sleep` | grumble | Rest ≥60 with `sleep` halved or zeroed by the lights | `lighting_plan` | 2 | 30 min |
| `good_nap` | praise | wakes on their own after 40+ min asleep | `lighting_plan` | 1 | 20 min |
| `scared_of_bumps` | grumble | in turbulence, Unease ≥55 | `check_in_cadence` | 3 | 20 min |
| `reassured` | praise | a check-in stop, follow-up or answered `reassurance` call lowers Unease | `check_in_cadence` | 2 | 30 min |
| `bored_no_screen` | grumble | Boredom ≥60 and `screen` unavailable | `moment` | 2 | 30 min |

`announcement_policy` has no M1 kind, since M1 has no announcements. `passengers.md` also gained four new success criteria this amendment touches (fog-of-war revealed by any crew interaction, not only check-ins — the owner widened the reveal in a follow-up ruling, P12 — thoughts-off gives the same end-state hash over seeds 1-16, at least 60% of cruise thoughts are praise or `nothing to report` on the reference, and each lever variant moves the matching thought kind over seeds 1-200) — all structural pass/fail checks Z1/Z6 run once `Sky.Sim` exists, not first values for this doc to compute.

## 3. First values D3 authors

These are the numbers D1, D2 and D4 name as open for D3 rather than mark with a placeholder constant: "a formula D3 owns," "D3's formula," "D3 sets the numbers," "whether a window is per need is a D3 number." Each is computed by arithmetic against section 1's targets, exactly as D1 and D2's own `[D3]`/`(FV)` numbers were, and stands as a first value for Z6 to measure and Z7 to tune.

### 3.1 Distress weights and comfort thresholds

CONCEPT section 4 defines Distress as "a weighted combination of the needs above their comfort thresholds" but names neither the weights nor the thresholds; passengers.md's `calm`/`uneasy`/`distressed` bands (section 2.4) are what a computed Distress value must land inside for the worked examples in passengers.md's own "Notes for D3" to hold.

Formula: `distress = clamp(Σ_i weight_i × max(0, need_i − threshold_i), 0, 100)`.

| Need | Comfort threshold | Weight per point of excess | Why this threshold |
|---|---|---|---|
| Unease | 40 | 1.5 | matches the contagion push threshold (section 2.12), and CONCEPT calls Unease "where the other needs converge" |
| Bladder | 55 | 1.2 | matches the `lav_visit` scoring threshold (section 2.6) |
| Refreshment | 65 | 1.0 | matches the `call_crew: refreshment` threshold |
| Rest | 45 | 0.8 | matches the `sleep` scoring threshold |
| Boredom | 50 | 0.6 | matches the `stretch` scoring threshold; Boredom is the mildest of the five (no need-read decision consults it) |

Check against the bands: Unease alone at 60 (the contagion-threshold passenger, excess 20) gives distress 30 — exactly the `uneasy` floor. Unease alone at 85 (the worked turbulence example in passengers.md's notes) gives distress 67.5 — `distressed`, matching a near-panic reading by sight. A baseline mid-flight passenger (Refreshment 50, Bladder 40, Rest 30, Unease 20, Boredom 60) gives distress 6 — `calm`, keeping the quiet baseline quiet. Moves: Experience (the peak/end/mean inputs) and the crew-observed distress band (M3+).

### 3.2 The decision re-evaluation cadence

F4 (the M1 plan) gives passengers a "staggered re-evaluation cadence" alongside "when an activity ends," with no number. `idle` is the one activity with no drawn duration (section 2.5: "until the next decision point"), so this cadence is what makes an idle passenger notice a need has crossed a scoring threshold (Bladder past 55, Refreshment past 65) without a full tick-by-tick re-score of 180 passengers.

Value: **2 sim minutes (480 ticks)**, staggered per passenger by `passenger id mod 480`, so the 180 re-evaluations spread evenly across the 2-minute window instead of landing on one tick (keeps R6's per-id iteration order deterministic and keeps the per-tick cost flat). Derivation: at the fastest push rates a needs-off-cadence passenger sees (Unease under moderate turbulence, ≈1.33 per minute combined with the aboard baseline before any trait multiplier), 2 minutes between checks is at most a 2.7-point miss — well inside the 15-point band `passengers.md`'s misread ranges already tolerate elsewhere, and well under the 5/10-minute sustain windows an incident needs to raise. Moves: Experience (how promptly a passenger acts once a need crosses a threshold), and indirectly Incidents (a longer cadence would let more needs coast past their threshold unaddressed for longer).

### 3.3 The F7 task-wait invariant threshold

CONCEPT section 7 and crew.md's F7 both name the rule ("no task waits on the board longer than a set limit while any crew member able to do it is idle") without a number; it is distinct from the event-work waiting limit (10 min, section 2.17, which governs an event's own fallback) and from spill time (3 min, section 2.17, which governs when an outside-zone crew member becomes able to claim at all).

Value: **2 sim minutes (480 ticks)**. Derivation: `TaskBoard` claims the instant a crew member is idle and able (E2), so a correctly-working board should never show any able-idle wait longer than the same tick; this threshold is a generous multiple of that (enough headroom for same-tick posting/idling order and for the one tick a pre-empted task needs to return to the board) while still being far short of the 5 to 15-minute escalation deadlines an incident needs to fail, so a real starvation bug fails the 500-seed fuzz clearly rather than hiding in noise. Moves: the F7 invariant itself (pass/fail across the fuzz), and indirectly Incidents (a board that starves an able crew member is the same defect the "both aft crew were on the cart" moment describes, but as a bug rather than a design trade).

### 3.4 Verdict thresholds (Smooth, Rough, Bad)

CONCEPT section 6 rules that the report shows four verdict words with no composite grade, but leaves each outcome's thresholds open. Derived from numbers already fixed elsewhere in this doc:

| Outcome | Smooth | Rough | Bad |
|---|---|---|---|
| Experience (10th percentile, 0-100) | ≥70 | 40 to 69 | <40 |
| Incidents (handled ÷ total) | 100% handled | <100% handled, ≥80% handled | <80% handled |
| Crew strain | peak <60 and 0 min over the redline (70) | peak 60-84, or more than 0 and at most 15 min over the redline (minutes are fractional: one tick over leaves Smooth) | peak ≥85, or >15 min over the redline |
| On-time doors | worse of the two lateness figures ≤2 min | 3 to 10 min | more than 10 min |

Derivation: Experience's Bad floor of 40 is passengers.md's own "would complain" cutoff (CONCEPT section 6), so a flight whose worst-served tenth sits below the complain line reads Bad by construction; Smooth's 70 leaves clear daylight above it for Rough. Incidents' Smooth tier is exactly pillar 4's own quiet baseline (no missed incident); Bad's 80% line means one in five incidents went unhandled, a different order of failure from an occasional miss. Strain's bands use crew.md's own stated expectation that six-crew cart pairs peak "around 40" on the baseline (so 60 already has 20 points of headroom before Rough) and the redline (70) itself as the Rough/Bad hinge; 15 minutes over the redline is about 10% of the reference flight's 150 minutes, matching pillar 4's 10%-of-seeds ceiling elsewhere. Doors' 10-minute Bad line is a plain operational-delay cutoff, with 2 minutes as the "close enough to call it on time" band. Moves: which word the M1 text report prints under each outcome; none of the underlying measures. These are the newest numbers in this doc and the best candidates for a quick owner read-through once Z6 has real seed distributions to show against them.

### 3.5 The deboarding duration target formula

CONCEPT section 6 calls for "a target set from the layout and load"; crew.md's reference timeline states its own expected deboarding time (about 13 minutes, door open to last passenger off) but says explicitly "the target is D3's formula."

Formula: `target_minutes = 2.0 + boarded_passengers / 15`, i.e. a 2-minute setup (the time from door-open to the first passenger reaching the aisle) plus one passenger every 4 seconds through the single forward door once flowing (15 passengers per minute, a round number in line with published single-aisle deplaning rates).

| Load | Boarded passengers | Target |
|---|---|---|
| Full (1.00, the performance test's load) | 180 | 2.0 + 12.0 = **14.0 min** |
| Reference mid-load (≈0.95) | ≈171 | 2.0 + 11.4 = **13.4 min** |
| Low end of the manifest range (0.90) | 162 | 2.0 + 10.8 = **12.8 min** |

Cross-check: crew.md's own informal expectation (T=2:31 door open to about T=2:44 last off, ≈13 minutes) sits inside this range for a near-full reference load, which is the only check available before Sky.Sim exists to measure an actual deboarding run. Moves: On-time doors (arrival side); this is a placeholder target only, since the real deboarding time the sim produces will depend on per-passenger bin retrieval and the single-aisle squeeze rules in far more detail than this aggregate formula models (section 3.6 below is the mechanic-level number Z6 will actually exercise).

### 3.6 Bin stow and retrieval base time

Bin help (crew.md, section 2.17) cuts "remaining bin time" by 40% once a passenger has stood for 30 seconds, but no base bin time is set anywhere for boarding stow or deboarding retrieval; F3 (BoardingFlow) needs one to run at all.

| Number | Value | Derivation |
|---|---|---|
| Boarding stow time | 20 seconds, drawn 10 to 30 s | a 20 s mean puts a meaningful tail of passengers past the 30 s bin-help trigger without making it the common case, so bin help is an exception, not routine (pillar 4) |
| Deboarding retrieval time | 10 seconds, drawn 5 to 15 s | retrieval (lifting a bag down) is faster than stowing (finding space, closing the bin); the deboarding formula (section 3.5) already treats the door, not the bin, as the aggregate bottleneck, so this stays a small per-passenger term |

Moves: Doors, both ends (cabin ready at departure, deboarding duration at arrival); the two numbers are independent estimates from section 3.5's aggregate target — one is a mechanic F3 actually runs, the other a scoring threshold — and Z6 is what reconciles them once a real boarding/deboarding run exists.

### 3.7 The reference layout's geometry (X1)

OD2 sets the reference narrowbody's seat map (crew.md:23): rows 0-2 are 2-2 business (12 seats), rows 3-30 are 3-3 economy (168 seats), one aisle, the forward door and forward lav ahead of row 0, the galley and aft lav behind row 30. `CabinLayout` (`src/Sky.Engine/Cabin/CabinLayout.cs`) needs every dimension in inches that OD2 and crew.md leave unstated: `CabinWidthInches`, each `CabinRow.PitchInches`, each class's `SeatSpec.WidthInches`, the `Aisle.CenterInches`/`WidthInches`, each `SeatGroup.LeftInches`, and each `CabinFixture.DistanceInches`. None of this is measured — `Sky.Sim` does not exist, so nothing has walked this aisle yet; X1's own `ShippedContentTests` (the directory loads and validates, OD2's seat count, a pinned ASCII dump) and K2's `LayoutValidator` (seat groups fit inside the walls and the aisle, every seat reachable) are the first checks these numbers face, and once F3 and H1 exist, Z6 reads them again indirectly through the boarding and deboarding minutes they set (section 3.5's target, the on-time-doors outcome).

| Field | Value | Published/typical/judged | Reason |
|---|---|---|---|
| Cabin width | 146 in (3.70 m) | published | Airbus's own A320-family cross-section figure for interior cabin width |
| Aisle width | 20 in | published | 14 CFR 25.815's minimum aisle width for an aircraft with more than 20 passenger seats, the certified floor single-aisle jets are typically built to |
| Aisle centre | 73 in from the left wall | judged | half the cabin width; a constant-section fuselage does not taper across the seating rows, so one centre serves every row |
| Business row pitch (rows 0-2) | 36 in | typical | short/medium-haul 2-2 business or domestic-first recliner pitch, industry range 36-38 in; row 2's edge into row 3 (the business/economy transition) is provisionally set to this same 36 in too, since a row's pitch is its own occupant's legroom, not the row behind's |
| Economy row pitch (rows 3-30) | 31 in | typical | full-service legacy-carrier economy pitch, industry range 30-32 in; a low-cost carrier's 28-29 in does not fit CONCEPT's "one drinks round and one meal" full-service reference flight |
| Business seat width | 21 in | typical | 2-2 domestic business/first recliner seat, industry range 20-21 in |
| Economy seat width | 18 in | typical | Airbus markets the A320 family's standard economy seat at 18 in wide; kept typical here since it is a marketing figure, not a certified spec |
| Business seat-group left edge (both sides) | 21 in | judged | derived so the row is centred on the 73 in aisle: (146 − 2×21 − 20) / 2 |
| Economy seat-group left edge (both sides) | 9 in | judged | same method: (146 − 2×18×3 − 20) / 2 |
| Forward door distance | 40 in ahead of row 0's aisle slot | judged | a short boarding vestibule, no published figure found |
| Forward lav distance | 70 in ahead of row 0's aisle slot | judged | beyond the door, in the same vestibule |
| Aft galley distance | 40 in behind row 30's aisle slot | judged | mirrors the forward door's depth |
| Aft lav distance | 75 in behind row 30's aisle slot | judged | mirrors the forward lav's depth |
| Walking pace, `inches_per_tick` (`NavGraphBuilder.Build`, `src/Sky.Engine/Cabin/NavGraphBuilder.cs:14`) | 7.87 in per 250 ms tick (0.8 m/s) | typical | a crowded-aisle boarding pace, not a free-flow corridor speed: Fruin's pedestrian level-of-service D/E congested-flow range (about 0.6-1.2 m/s) and the aisle-walking speeds aviation boarding-simulation studies validate against real airline boarding (Steffen 2008; van Landeghem & Beuselinck 2002 both model aisle walking near 0.8-1.0 m/s), converted 0.8 m/s × 39.3701 in/m × 0.25 s/tick = 7.87401… in/tick, rounded to 3 significant figures; bin-stow time (section 3.6) is a separate stop, not part of this pace |

Sanity check: 3 business rows at 36 in plus 28 economy rows at 31 in is 976 in (81.3 ft, 24.8 m) of seating alone; adding the two vestibules (roughly 70-115 in each) lands the whole cabin around 27-29 m, inside the published range for an A320's cabin length (about 27.5 m) — the only cross-check available before a real boarding walk exists.

The link to boarding time: F3's `BoardingFlow` walks each passenger from the forward door to their row, so these pitches, fixture distances and the walking pace above are what set that walk in ticks (`edge ticks = max(1, ceil(inches / inches_per_tick))`, `NavGraphBuilder.Build`, `src/Sky.Engine/Cabin/NavGraphBuilder.cs:220`), and Z6 reads the result back out as the boarding-side half of section 3.5's target formula and the on-time-doors outcome once F3 and H1 run. Worked example, no queue and no stow stop: the forward door to row 30's aisle slot is 40 in (the fixture) plus 3 business edges at 36 in (108 in) plus 27 economy edges at 31 in (837 in) = 985 in total, `ceil(985 / 7.87401574803) = 126` ticks, 126 × 250 ms = **31.5 s** — this is the pure walking component only, before section 3.6's per-passenger stow time is added at the passenger's own row. This closes the request section 5 raised: `inches_per_tick` is now set, at this line.

### 3.8 `node_capacities` in `needs.json` (X1)

`NodeCapacitiesSpec` (`src/Sky.Content/Schema/NeedsSchema.cs`) needs one holder count per nav-graph node kind ADR 0005 derives from the layout: an aisle slot at each row, seat nodes, and the door, lav and galley fixture nodes. None of these is measured either; F7's `InvariantChecker` and Z1's 500-seed fuzz are the first checks that exercise them (capacity and reservations never exceeded on any tick), and `lav_queue` specifically is what Z6's one-lav-locked lever check and passengers.md's own canonical-story line (the aft lav queue's longest overflow into the aisle, on at least 60% of seeds) will tune once real sweeps exist.

| Field | Value | Published/typical/judged | Reason |
|---|---|---|---|
| `aisle_slot` | 1 | judged | one occupant (a cart or one walking or queuing passenger) holds a row's aisle slot at a time; a second person passes at the +4-tick squeeze cost (crew.md 2.23) instead of a bigger number here, so blocking (CONCEPT pillar 3's canonical story) still happens |
| `seat` | 1 | judged | one passenger per seat |
| `door` | 1 | judged | a doorway threshold is single-file, like a seat or a lav; the crew station beside it (crew.md:25) is a separate node "off the passenger path," so it never shares the door's capacity — diverges from `ContentTree`'s test fixture (2), which reads as an arbitrary test value, not a design choice |
| `lav` | 1 | judged | one occupant per lavatory at a time |
| `lav_queue` | 4 | judged | matches passengers.md's own thought trigger: `lav_queue_long` fires when a passenger "joins a lav queue with 3 or more ahead, or overflows into the aisle" (section 2.28); a capacity of 4 means the passenger who joins as the 4th (3 ahead) still fits on the queue node, and the next arrival is the one who overflows into the aisle slot behind it (passengers.md:94) — the thought's two clauses then map exactly onto the queue's last slot and its overflow boundary; diverges from `ContentTree`'s test fixture (2), sized for a much smaller test layout |
| `galley` | 4 | judged | covers the reference crew's up-to-2-on-break cap (crew.md 2.20, six-crew roster) plus a working crew member or a stretching passenger (passengers.md's `stretch` activity walks to the aft galley) without the galley itself becoming a bottleneck |

## 4. Scope note (not a flag)

Every worked example in `passengers.md` and `crew.md` was re-derived by hand while collecting section 2 (the rate-multiplier composition, the Unease equilibrium under moderate turbulence, the auto-resolve focus formula and its 80%/7% and 46%/26% pick chances, and the quality-to-percentage split in the fourth-power worked example): all reproduce exactly as stated, so no number is flagged as wrong. The one open item found — the seat-comfort formula "D3 owns" (section 2.3) — is a scope gap, not an error: `passengers.md` already gives the three reference-layout constants M1 needs, and a continuous pitch-and-width formula has no second layout to calibrate against until the layout editor (M2 onward, already out of M1's scope). Section 2.3 records that as the resolution rather than an open question.

## 5. Open requests for implementers

None open. Section 3.7's `inches_per_tick` row (added 2026-09-27, orchestrator steer) answers the one request this section briefly carried: `BoardingFlow` (F3) now has a walking pace to turn the reference layout's pitches and fixture distances into ticks. Otherwise none yet — `Sky.Sim` does not exist, so there is no scenario key or CSV column to be missing from. This section is for a lever with no scenario switch, or an outcome with no balance-CSV column, once a run needs one.

## 6. Runs

Empty. Z6 (the M1 plan) appends one entry per sweep here: date, question, exact command, seeds, the numbers with their spread, and the verdict against the target in section 1 or section 3.

| Date | Question | Command | Seeds | Result | Verdict |
|---|---|---|---|---|---|
| — | — | — | — | — | — |
