# Crew

Status: M1 step D2, drafted by `game-designer` and approved by the owner; the owner ruled its eleven open questions and one further crew rule (recorded under "Owner rulings"), and two rulings made in passengers.md (D1) that reach crew are folded in (need reads, "back to your seat"). Rulings 15 and 16 cover a crew member covering more than one zone, with no limit, the Floater trait and "understaffed". It builds on [CONCEPT.md](./CONCEPT.md) (pillars, the need set, scoring, section 8's rulings) and on M1's owner decisions OD1 to OD4 and ruling R14 ([the M1 rulings](../decisions/m1-rulings.md)). Passenger behaviour (activities, traits, which interaction reveals which need) is in passengers.md (D1); this doc names only where crew touch it. Terms are in the glossary in [docs/README.md](../README.md); the terms this doc coins are listed at its foot.

**First values.** Every number marked **(FV)** is a first value for balance-analyst's D3: unmeasured, set here so the systems have a shape, and owned from then on by [balance.md](./balance.md). A choice the owner ruled for this doc is marked **(ruled n)**, pointing into "Owner rulings"; a choice still open is marked **(OQ n)** and listed under "Open questions for the owner", with the text describing the recommended option until the owner rules.

## Goal

A flight nobody watches is run by six crew who behave like a crew: they work their zones, push carts that block the aisle, answer what they can reach, fall behind where they are thin, get tired, and make event calls that are right more often when they are sharp and rested. The M1 outcome is measurable: changing crew count and zones, or the service plan, moves strain, missed incidents and Refreshment across a sweep (CONCEPT section 7). The M3 outcome is felt: a player who moves one crew member or turns the cart around sees the cabin answer, and the report says which call did it.

## Pitch

The crew are the actors the player stages. Every lever that touches them sets a condition (who covers which rows, when the carts roll and which way), never an order. Three mechanisms carry the design, and each exists to touch other systems:

- **The task board with hold priority.** A running task defends itself at a priority of its own, so a cart does not drop for every call button; the call button waits, Unease climbs, and the report can say why.
- **Zones with spill.** Crew take work in their zone first and outside it only once it has waited. A zone whose crew are both on the cart is uncovered, and the gap between two carts is a place nobody can reach.
- **Focus.** One number from competence, fatigue and traits decides how sharply a crew member reads an event (OD1). Strain feeds fatigue, so a crew member run into the ground in the service makes worse calls in the quiet cruise after it.

## Core mechanics

### The reference cabin and its zones

The reference narrowbody (OD2): one aisle; rows numbered from 0 at the front (R13); rows 0 to 2 are 2-2 business (12 seats), rows 3 to 30 are 3-3 economy (168 seats). Ahead of row 0: the forward door and the forward lav. Behind row 30: the galley and the aft lav. All carts start and end in the aft galley.

Two new node kinds are needed on the nav graph (a B1 requirement, not a player-facing choice): a **crew station** beside the forward door, off the passenger path, and the **jumpseats** (two at the forward door, four at the galley) where crew sit for taxi, takeoff and landing.

A **zone** is a set of rows plus stations (door, lav, galley). Zones decide who answers call buttons, walks check-ins, does lav checks and secure checks, and who resolves an event. **Cart spans** are separate: they say which rows each cart serves. Crew count, zones and cart spans are one lever (OD4), set together in the scenario.

**Six crew (the reference).** Three zones of 60 seats each.

| Crew | Position | Zone | Cart | Other duties |
|---|---|---|---|---|
| purser | Purser | F: rows 0-10, forward door, forward lav | none | Door station at boarding and deboarding; business hand service |
| fa2 | Galley | A: rows 21-30, galley, aft lav | none | Galley support during rounds, catch-up service |
| fa3 | Mid | M: rows 11-20 | Cart 1 (rows 3-16) | |
| fa4 | Mid | M: rows 11-20 | Cart 1 | |
| fa5 | Aft | A: rows 21-30, galley, aft lav | Cart 2 (rows 17-30) | |
| fa6 | Aft | A: rows 21-30, galley, aft lav | Cart 2 | |

During a round, zones M and A lose four of their five crew to the carts. The purser covers everything forward of cart 1 and fa2 everything aft of cart 2; the rows between the carts are reachable by nobody who is not on a cart. That gap is intended: it is where the canonical missed call button comes from, and it is what zone assignment in M3 lets the player change.

**Four crew (the `four-crew` variant).** Two zones, one cart.

| Crew | Position | Zone | Cart | Other duties |
|---|---|---|---|---|
| purser | Purser | F: rows 0-15, forward door, forward lav | none | As above |
| fa4 | Forward | F: rows 0-15, forward door, forward lav | Cart 1 (rows 3-30) | |
| fa5 | Aft | A: rows 16-30, galley, aft lav | Cart 1 | |
| fa6 | Galley | A: rows 16-30, galley, aft lav | none | Galley support, catch-up service |

One cart serves all 28 economy rows, so each round takes about twice as long, fa4 walks aft to the galley to meet the cart, and the meal round runs into descent (see "Rounds in descent"). The four are chosen so their mean competence (0.60) is close to the six's (0.62), so the variant measures staffing and not skill.

Two cart rules for two carts in one aisle: the cart whose first row is farther from the galley leaves first, and a cart never passes another (R14 applies to carts too).

The **zone lead** is the first crew member listed for a zone (purser, fa3, fa5 for six crew; purser, fa5 for four). A covering member (below) may lead any or all of the zones they cover. The validator (K2, K2b) checks that every row belongs to exactly one zone, every zone has at least one crew member, and the listing rules below.

### Covering more than one zone (ruled 15, 16)

The owner's rulings: "It should be doable for understaffed flights, and have the appropriate consequences on the strain it puts on the crew member" (ruling 15), and "If the user wants they could try staffing a whole narrow body with one crew member and see what happens. No zone limit. Crew trait that allows them to handle 2 zones just as fine. The expectation is a member per zone, anything below that is understaffed." (ruling 16). A scenario may list one crew member in any number of zones, up to every zone of the cabin: a **covering member**, and the number of zones they are listed in is their **zones covered**, N. Everyone else has N = 1. Covering is how a thin crew reaches a cabin without leaving a zone to spill alone, and it is paid for in the covering member's strain, which the report names.

**Understaffed.** A flight is **understaffed** when it has fewer crew than zones (the crew count, which counts a covering member once, below the number of zones): one member per zone is the expectation. Six crew in three zones and four in two are fully staffed; one crew member in three zones is understaffed. Covering is allowed on any flight, understaffed or not: a fully staffed flight may still list a floater in two zones and pay the strain. Understaffed is stated once, in the report's flight header (H3: "Crew: 2 for 3 zones, understaffed"), and is never a moment: it is a condition set before boarding, and the moments it causes (a redline crossing while covering, a task that spilled) already name the covering. It is also a column of the balance CSV (I2: crew count and zone count), so a sweep can split by it.

**What "their zone" means.** Every crew rule that reads "the crew member's zone" reads "any zone they cover" for a covering member. Each rule, one by one:

- **Claiming** ("Who claims"). A covering member claims a task in any covered zone at once; the spill time applies only to tasks outside all of them. Among crew who may claim, the nearest still claims, with no preference for or against the covering member, so they take the work of every covered zone whenever they are the closest free hand. Most of the consequence comes from this, and it needs no rule of its own.
- **Pre-emption.** A task's pre-emption candidates are its zone's crew, and a covering member is among them in every covered zone, under ruling 13's order unchanged.
- **The zone lead and the notice cap.** An event that reaches the notice cap is resolved by the subject zone's lead; a covering member may lead any or all of their zones, and resolves the late events of each they lead, at a focus their strain lowers (through fatigue over the redline).
- **Event work and its fallback.** Event work is in the subject's zone, so a covering member may claim it at once in any covered zone; the 10-minute "Leave it for now" fallback is unchanged.
- **Secure checks and check-in walks.** Both are posted once per zone and claimed from the board like any task, so a covering member may walk several zones' checks or walks in turn. At boarding, a crew member goes to their jumpseat once no secure check in any zone they cover is waiting or running, so a covering member who walks several zones' checks is seated later and cabin ready can wait on them, a moment like any other ("fa5 walked the secure checks of M and A; cabin ready at 11:03").
- **Service rounds and carts.** Not zone-bound: a cart's two crew are named on the cart and its span is its own. A covering member may push a cart; while they do, every zone they cover loses them.
- **Everything else** (catch-up service, catch-up drinks, check-in follow-ups, bin help, call buttons, "back to your seat", minor and severe incidents, lav checks) follows the claiming rule above through the task's zone.

**The home zone.** A crew member's **home zone** is the first zone in `$.crew.zones` that lists them. Any rule that needs one zone per crew member (which jumpseat they take, where they wait when idle, the zone a moment files them under) uses the home zone; no claim, backlog or lead rule does.

**The strain it costs (the owner's consequence).** Two terms grow with the zones covered, both on top of the extra work the member claims. Both read the member's **extra zones**, E = N − 1 − (1 if the member is a Floater, else 0), never below 0:

1. **Backlog across every covered zone.** The backlog row of the strain table counts waiting tasks in all N zones; its cap is the per-zone cap times (1 + E): +0.3 per sim minute at E = 0, +0.6 at E = 1, +0.9 at E = 2 (the per-task rate and per-zone cap are the existing FV; the scaling is the rule).
2. **The covering factor.** The time-on-task rate is multiplied by 1 + 0.2 × E (**0.2 per extra zone, provisional, FV**), on every on-task tick, after the trait factor: 1.2 for two zones, 1.4 for three, 1.6 for four (Brisk covering two zones: 1.15 × 1.2 = 1.38). It stands for the divided attention of answering to several zones, and it makes covering cost strain even on a flight whose board stays short.

Both numbers (0.2 per extra zone, and the cap scaling by 1 + E) are balance-analyst's to measure and own from then on, in `balance.md` section 2.20 (strain sources and the redline), beside the backlog row. `CrewStrain` (E4) takes the member's extra zones E as a construction input and applies both terms itself, with the per-extra-zone factor as a new `StrainSettings` value beside `BacklogCapPerMinute`; F5 computes E from the scenario and the trait, and counts the member's backlog across every covered zone. The redline moment names the covering ("fa5 crossed the redline at 12:20, covering zones M and A, after 58 minutes on the cart"), so a player who staffs the flight (M3) sees the cost of the choice they made.

**The Floater trait (`floater`).** A crew trait (see the trait table): the member covers one extra zone as if it were their own, so covering two zones costs no covering strain (E = 0: factor 1, backlog cap +0.3, though the backlog still counts tasks in both zones). From three zones on, a Floater pays for each zone past the second (three zones: E = 1, factor 1.2). Its other side is what makes it a trait and not a free upgrade: a Floater keeps moving, so their check-in stop and call-button dwells are ×0.8 and the Unease relief of those is ×0.85 (FV), a covering skill that costs the passenger in front of them a little. Its content field is the number of zones the trait carries free (1), beside the trait's other factors in `crew.json`.

**A flight with fewer crew than roles.** Covering lets a scenario fly with one crew member, so every duty named for a position needs someone:
- **The purser's duties** (door station, business hand service, events with no subject seat) fall to the crew member with id `purser` when one flies, else to the lead of the first zone in `$.crew.zones`.
- **A round with no cart for some economy rows** (no carts at all, or cart spans that leave rows out) serves those rows by catch-up service: one catch-up service task per passenger in them, posted at the round's start, claimed from the board like any other. A one-crew flight therefore serves its economy cabin by hand, one passenger at a time, and the board, the strain curve and Refreshment show what that costs.
- **Galley breaks.** At most a third of the crew (rounded down, at least 1; FV) are on break at once: 2 with six crew, 1 with four, 1 with one, in which case a break leaves the cabin with nobody free and every call waits for its end or cuts it (ruling 13).

**What the validator accepts and refuses (K2b).** Each refusal is a `ContentLoadException` naming the scenario file and the path; the first failure stops, as in K2.

| Case | Verdict | Path | Sentence |
|---|---|---|---|
| A member in one zone | Accepted | | |
| A member in any number of different zones, up to every zone, leading none, some or all | Accepted | | |
| A zone whose only crew member is a covering member | Accepted | | |
| A flight with fewer crew than zones (understaffed), down to one crew member | Accepted | | |
| A member listed twice in the same zone | Refused | the second listing, `$.crew.zones[z].crew[i]` | `Expected each crew member once in a zone; "fa5" is listed twice in zone "A".` |
| A zone with no crew | Refused (K2, unchanged) | `$.crew.zones[z].crew` | `Expected at least one crew member in the zone; got none.` |
| Two zones with the same id | Refused | the later zone's `$.crew.zones[z].id` | `Expected each zone id once; "A" is already the id of $.crew.zones[1].` |
| A cart crew member in no zone | Refused | `$.crew.carts[c].crew[i]` | `Expected every cart crew member in a zone; "fa7" is in none.` |
| A cart without exactly two different crew members | Refused | `$.crew.carts[c].crew` | `Expected two different crew members on a cart; got 1.` (the count of distinct ids listed) |

The crew count still equals the distinct crew across the zones, so a covering member counts once (K2, unchanged). The zone-id check exists because the sentences above name zones by id; the cart-in-a-zone check because every crew member needs a home zone; the two-crew check because a cart runs with two crew meeting at the galley (the service plan, step 1), and a one-crew flight has no carts rather than a cart one person cannot push.

### Task types and priorities

Every task carries a **claim priority** (its place on the board) and a **hold priority** (what it defends once running). A posted task pre-empts a running one only when its claim priority is above the running task's hold. E2's pre-emption compares the new task's claim against the running task's hold rather than its claim; for every task but the cart, the business hand service and the galley break the two are equal **(ruled 1)**.

| Task | Claim (FV) | Hold (FV) | Crew | Posted when | Zone | What it does |
|---|---|---|---|---|---|---|
| Secure check | 90 | 90 | 1 per zone | Last passenger through the door; seatbelt sign on in descent | Each zone | Walk the zone's rows; done when every passenger in it is seated. Completing the last zone's check at boarding is cabin ready **(ruled 4)**. In descent a zone's check cannot complete while a cart is in its rows |
| Incident response, severe (accident, panic) | 85 | 85 | 1 | An incident is raised (OD6; kinds in passengers.md (D1)) | The passenger's | Reach the passenger before the escalation deadline; dwell 3 min (FV) |
| Event work | 70 | 70 | As the choice says | An event choice with `needs_crew` is picked | The subject's | Walk there, run for the choice's `minutes`. Not started within 10 min (FV) of posting, it times out into the event's "Leave it for now" effects (see below) |
| Back to your seat | 62 | 62 | 1 | A passenger leaves their seat with the sign on in cruise (passengers.md (D1): only at Bladder 85 or more) | The seat's | Walk to the passenger and ask them back; how they respond is passengers.md (D1)'s |
| Incident response, minor (the other OD6 kinds) | 65 | 65 | 1 | An incident is raised | The passenger's | As severe; dwell 2 min (FV) |
| Call button | 60 | 60 | 1 | A passenger presses it (passengers.md (D1)) | The seat's | Walk to the row, dwell 20 s (FV); a drink request adds a galley round trip. Answering reveals needs (OD3) |
| Check-in follow-up | 57 | 57 | 1 | 10 min (FV) after an interaction reads the passenger's Unease as `urgent`, unless a follow-up for them is already on the board | The seat's | Walk to the row, dwell 60 s (FV): Unease -15 (FV; Warm x1.4) and Unease revealed again, so a second `urgent` read posts another |
| Catch-up drink | 55 | 55 | 1 | An interaction reads the passenger's Refreshment as `urgent`, unless they have a `refreshment` call open or a round will reach their row within 10 min (FV) | The seat's | Galley round trip, then the drink a `refreshment` call gives (passengers.md (D1)) |
| Boarding help, deboarding help (bin help) | 50 | 50 | 1 | A passenger has stood stowing or retrieving for 30 s (FV) | The row's | Stand at that row's slot; the passenger's remaining bin time is cut by 40% (FV) |
| Door station | 50 | 50 | purser | Boarding and deboarding start | F | Stand at the forward crew station; observes every passenger through the door |
| Galley break, when due | 45 | 50 **(ruled 13)** | 1 (self) | Strain at or above 50 and 30 min since the last break (FV) | Galley | See strain below |
| Catch-up service | 45 | 45 | 1 | A round ends with passengers it skipped (asleep, away) | The seat's | Hand service from the galley; same need effect as the round |
| Business hand service | 42 | 60 | purser | Round start minus 3 min (FV) | F | One galley trip, then rows 0-2 by hand: drinks 60 s, meal 120 s per row (FV) |
| Drinks round, meal (cart) | 40 | 75 | 2 per cart | The round's start time, once the previous round's carts are back | The cart span | See the service plan below |
| Lav check | 30 | 30 | 1 | Every 30 min in cruise per open lav, or when a lav reaches 20 uses since its last check (FV) | The lav's | 60 s at the lav (FV); resets the lav's condition **(ruled 6)** |
| Check-in walk | 20 | 20 | 1 per zone | Seatbelt sign off after takeoff, then every 30 min in cruise (FV) | Each zone | Walk the zone; stop 30 s (FV) at any passenger observed as uneasy or distressed: Unease -15 (FV) and needs revealed (OD3) |
| Galley break, routine | 10 | 35 | 1 (self) | 45 min since the last break, in cruise (FV) | Galley | See strain below |

Consequences of the hold priorities, which are the point of them:
- A call button (60) does not break a cart (hold 75); it waits for a crew member who is free. Only a severe incident or a secure check pulls a crew member off a cart.
- A cart posting (40), business hand service (42), catch-up service (45) and bin help (50) do not break a due galley break (hold 50), so a strained crew member who went on break ahead of a round stays on it and the round waits for its crew **(ruled 13)**. A routine break (hold 35) still gives way to a cart.
- A call button (60) does break a galley break of either kind, but only as the last choice: crew on a break are pre-empted only when no one else the task may pre-empt is available (see "Who claims"). Then the break's strain relief stops, and it counts as cut short (see strain below). The owner's words: "passengers come first."
- Event work (70), "back to your seat" (62), minor incidents (65) and the read-driven follow-up (57) and catch-up drink (55) wait behind a cart. That is CONCEPT section 6's traced miss: "both aft crew were on the cart; the task waited 14 minutes behind a lower-priority service task".
- A secure check (90) does break a cart, which is how a round still running in descent ends (see "Rounds in descent").

**Event work that nobody starts.** Event work sits at 70, above call buttons and minor incidents and below the cart's hold, so an event's crew task is the first thing a free crew member takes but never strips a cart. If no crew member has started it 10 min (FV) after it was posted, the task is withdrawn and the event's "Leave it for now" effects apply instead, as if that had been the choice. That is a moment naming the event, the choice that was picked, and what kept crew away ("nobody came: fa5 and fa6 were on cart 2, the task waited 10 min"). The 10 min runs from posting, so an event noticed late (ruled 2) can take up to 20 min from firing to its fallback.

**Who claims.** An idle crew member may claim a task in their own zone (any zone they cover, for a covering member; ruled 15, 16) at once, and one outside it once it has waited the **spill time**, 3 min (FV). Severe incidents and secure checks have no spill time. Among the crew who may claim, the nearest by path cost claims, ties to the lower crew id. Among tasks, the highest claim priority goes first, then the oldest (E2). A pre-emption picks among the zone's crew whose running task holds below the new task's claim, in this order **(ruled 13)**: first any crew member not on a galley break, before any on one; then, within each of those two groups, the one whose task holds lowest; then the nearest. So a crew member on a break is the last one a task takes: the others jump in so they can stay on it, and a break is cut only when no one else can take the task.

**Pre-emption and cleanup.** The pre-empted task returns to the board with its original posted tick and its progress. A cart that loses one of its two crew stops where it is with its brake on (the cleanup) and still fills its slot; the other crew member waits at it. A check-in walk or secure check resumes from the row it reached. A call button is re-posted.

**Seated phases.** From the moment a crew member finishes their part of the departure secure check (for a covering member, once no secure check in any covered zone is waiting or running; ruled 15, 16) until the seatbelt sign goes off after takeoff, and from the landing secure check until the aircraft stops at the gate, crew are in jumpseats. Seated crew claim nothing but severe incidents, and those only in the taxi-out and taxi-in stages. Seated crew are not idle for the task-wait invariant (F7).

**Able.** For the F7 invariant, a crew member is able to take a task when they are idle (no claimed task; a crew member on a galley break is not idle), not seated, and allowed to claim it under the zone and spill rules. A task's wait is posted tick to claimed tick. Physical reach is not part of "able": a crew member who claims a task and then cannot get past a cart is claimed and walking, and the incident outcome's time to crew arrival measures the rest.

### The crew model

Each crew member has four values and at most one crew trait. The M1 roster is content (X2); names and voice are event-writer's.

- **Competence**, 0 to 1, fixed for the flight: experience and skill. It sharpens event calls (focus, below) and, with empathy, how accurately a crew member reads a passenger's needs. It changes no task duration, so the Z6 check that competence matters stays clean.
- **Empathy**, 0 to 1, fixed for the flight: how closely a crew member attends to the person in front of them. It sets need-read accuracy with competence, and nothing else in M1.
- **Fatigue**, 0 to 100. Starts at the roster value (the legs flown before this one). Rises 6 per sim hour on duty (FV), twice as fast while strain is over the redline. A galley break lowers it by 0.2 per minute (FV), so a break mostly relieves strain, not fatigue. Fatigue flattens focus and speeds strain.
- **Strain**, 0 to 100, below.

Crew traits (FV), each touching at least two systems (pillar 3):

| Trait | Effect |
|---|---|
| Steady | The fatigue term in focus is halved; pre-emption strain steps are halved |
| Brisk | Service time per row x0.85; on-task strain rate x1.15 |
| Warm | Check-in stop and call-button Unease relief x1.4; those dwells x1.5 |
| Short fuse | Focus x0.7 while at or over the redline; pre-emption strain steps x1.5 |
| Floater (`floater`; ruled 16) | Covers one extra zone free of covering strain (see "Covering more than one zone"); check-in stop and call-button dwells x0.8, their Unease relief x0.85 |

The M1 roster (FV):

| Crew | Competence | Empathy | Trait | Starting fatigue |
|---|---|---|---|---|
| purser | 0.85 | 0.70 | Steady | 20 |
| fa2 | 0.70 | 0.40 | Brisk | 30 |
| fa3 | 0.60 | 0.80 | Warm | 15 |
| fa4 | 0.45 | 0.30 | Short fuse | 35 |
| fa5 | 0.75 | 0.50 | none | 10 |
| fa6 | 0.35 | 0.75 | Warm | 25 |

fa6 is the deliberate mismatch: junior but attentive to people, so fa6 reads needs better than competence alone would give and makes worse event calls than those reads deserve.

### Need reads

When a crew interaction reveals a need (OD3), the crew member records a band, and may misread it. The rule (which interaction reveals which need, the bands, read accuracy from competence and empathy, when a read can be wrong, and that crew act on the band they read) is stated once, in passengers.md (D1) section 9. What the crew model carries for it:

- **Competence** and **empathy**, both 0 to 1 (the crew model, above); fatigue, strain and crew traits do not enter a read.
- **A read stream per crew member, `crew/<id>/read`** (a name added to R5's list), so reads never shift auto-resolve's draws on `crew/<id>`.
- **Crew rules that consult a passenger's need read the recorded band, never the true value**, so a misread can delay help and cause a missed incident.
- **A misread errs toward fine** (ruled 10): it records the band one below, so a weak read misses need and never invents it.
- **Sight is exact**: the distress band, position and activity crew see are never misread; only a band revealed by interaction can be.

Three crew decisions consult a read, and no other crew rule does:

| Decision | Read that triggers it | What happens |
|---|---|---|
| `lav_permission` call | Bladder read `urgent` | Granted; any other read refuses it (passengers.md (D1) section 9 owns the rule) |
| Catch-up drink | Refreshment read `urgent` | A catch-up drink task is posted (table above) |
| Check-in follow-up | Unease read `urgent` | A check-in follow-up task is posted (table above) |

A misread `urgent` read as `wants` posts nothing, which is how a weak read turns into a thirsty passenger calling later or an uneasy one tipping into a panic.

### Auto-resolve: how an event choice is picked (OD1)

Each choice carries an authored `quality` q in [0, 1] for the facts the trigger saw (event-writer scores it; D4). The resolving crew member's **focus** is

`f = clamp(c × (1 − 0.6 × F / 100) × t, 0.05, 0.95)` (0.6, 0.05 and 0.95 FV)

where c is competence, F fatigue, and t the trait factor (Short fuse 0.7 at or over the redline, else 1; Steady halves the 0.6). The chance of choice i among n is

`p_i = (1 − f) / n + f × q_i⁴ / Σ_j q_j⁴`

and, when every q is 0, `p_i = 1 / n`. The first term is the even spread fatigue and traits flatten toward; the second is the sharpened read of the situation that competence pulls toward. The fourth power (FV) is computed by multiplication, so it is exact on every platform (R6), and it makes an authored gap count: qualities 0.9 and 0.7 split the sharpened term 73% to 27%. The pick is one `NextDouble` from `crew/<id>`, walked through the p in choice order.

Worked example, qualities 0.9, 0.5, 0.2 ("leave it for now" at 0.2):
- purser, competence 0.85, fatigue 20, Steady: f = 0.85 × 0.94 = 0.80; the best choice is picked 80% of the time, the worst 7%.
- fa6, competence 0.35, fatigue 60: f = 0.35 × 0.64 = 0.22; the best is picked 46%, the worst 26%.

**Who resolves, and when (ruled 2).** An event is resolved when a crew member first notices it, and the noticer resolves it. It is noticed at once when the subject is within a crew member's observation reach at the tick it fires, or when its trigger presses the subject's call button; otherwise it waits until a crew member's reach passes over the subject, or until the **notice cap**, 10 min (FV), when the zone lead resolves it. An event with no subject seat is resolved at once by the purser. Noticing uses the same reach as observation, reading lights included.

Each resolution is an outcome record (R11) with the resolver, f, every q and p, the pick, and whether a choice of higher quality existed, so the Z6 sweep can count "worse choice" as any pick below the event's best quality.

### Strain and the redline

Strain (FV throughout) accumulates from the sources CONCEPT section 6 names, plus one tie to the board:

| Source | Change |
|---|---|
| Time on task (any claimed task except a break; not while seated) | +0.45 per sim minute × (1 + F / 100); ×1.5 once 60 min have passed since the end of the last full break (one that ran its 10-minute minimum; **ruled 14**) |
| Pre-emption | +5; a second within 10 min adds +8 more (the "double pre-emption" moment) |
| Backlog in own zone | +0.1 per sim minute for each task in the crew member's zone waiting over 3 min, at most +0.3; for a covering member, tasks in every covered zone count and the cap is +0.3 × (1 + E), E the extra zones (ruled 15, 16) |
| Covering more than one zone | Time-on-task rate × (1 + 0.2 × E) (0.2 provisional), after the trait factor; E = zones covered − 1, less 1 for a Floater, never below 0 (ruled 15, 16) |
| Arriving at a severe incident | +6 |
| Idle, not on break | -0.2 per sim minute |
| Galley break | -3 per sim minute for every minute it runs, a break cut short included |

**Galley breaks.** A break is taken only at the galley (there is no forward galley), so crew forward of a cart in the aisle cannot reach one until the carts return. A break that nobody interrupts runs at least 10 min, and ends at 15 min or when strain falls to 15, whichever comes first after the 10. The 10 minutes are the break's own minimum, not protection: a task whose claim is above the break's hold (35 routine, 50 due) can still cut it, and does so only when no crew member off break can take that task (ruled 13). A break that runs its 10 minutes is a **full break**, and only a full break restarts the 60-minute no-break clock of the time-on-task row; a break cut short still lowers strain for the minutes it ran (ruled 14). A crew member whose breaks keep being cut therefore stays on the ×1.5 rate, and the redline moment names the cut breaks. At most 2 crew are on break at once with six crew, 1 with four; for any other count, a third of the crew rounded down, at least 1 (FV; ruled 16's one-crew flight). Breaks are never posted during boarding, the seated phases, or descent.

**The redline** is 70. Over it, a crew member's task durations are x1.15 and their fatigue rises twice as fast (which lowers focus, so a strained crew member makes worse event calls later). The strain outcome (CONCEPT section 6) reads each crew member's peak and the minutes any of them spent over the redline. Each crossing is a moment naming what drove it: the last break, the pre-emptions, the round.

Expected shape, to be measured: with six crew, the cart crew peak around 40 and nobody crosses the redline; with four, the cart pair runs two double-length rounds without a break and crosses it before the meal ends.

### Observation reach

Crew record what they see into `ObservationStore` every tick they are in a place (F5); what one observation captures is OD3's, and which interaction reveals which need is in passengers.md (D1).

| Crew position | Reach (FV) |
|---|---|
| Aisle slot at row r | Seats in rows r-1, r and r+1, both sides, window seats included; characters in aisle slots r-2 to r+2 |
| Aisle slot at row r, cabin lights dimmed | Seats in row r only, plus any seat whose reading light is on anywhere in the normal reach (rows r-1 to r+1); aisle slots r-1 to r+1 **(ruled 5)** |
| Aisle slot at row 0 or row 30 | Adds the lav beside it and its queue nodes |
| Forward crew station, forward jumpseats | The forward door, the forward lav and its queue, row 0 |
| Galley, aft jumpseats | The aft lav and its queue, rows 29 and 30 |

**Reading lights (ruled 5).** A passenger with their reading light on stays observable at the normal reach when the cabin is dimmed. When a passenger's reading light is on, and what it does to a `light_sleeper` beside it, is passengers.md (D1) section 3's switch-on rule.

Cart crew observe from their slots like anyone else, which makes a service round the densest observation pass of the flight; the stretches where nobody walks are what the crew-observed view shows as stale.

### Lav condition (ruled 6)

A lav counts its uses since its last check. At about 20 uses (FV) it is untidy: a visit takes x1.25 as long and each use pushes the user's Unease by +3 (FV; applied as passengers.md (D1) sets out). A lav check resets the count. This is what makes a lav check work rather than a walk: with the aft lav locked, the forward lav reaches untidy twice as fast, which lengthens its queue and pulls the purser to it more often.

### The service plan (OD4)

The service plan is an ordered list of rounds. For each round:

| Field | Values | Reference default (FV) |
|---|---|---|
| Kind | drinks, meal | drinks, then meal |
| Start time | Sim minutes after the seatbelt sign first goes off after takeoff | drinks +5, meal +40 |
| Direction | front to back, back to front | front to back for both |

**Order** is the order of the list: a round starts at its start time or when the previous round's carts are back in the galley, whichever is later **(ruled 3)**. So "order" means drinks first or meal first; the cabin sections are always served business first (by hand) and economy by the carts. The `service-back-to-front` variant flips direction on both rounds and changes nothing else.

How a round runs:
1. At the start time, each cart's two crew meet at the galley (a sync point, F5). A crew member who is busy delays their cart; the other waits at the galley.
2. The cart travels up the aisle to its first row (the span's first row in the plan's direction), filling each slot it passes, at half walking pace (FV).
3. It serves its span row by row: drinks 40 s, meal 70 s per 3-3 row with both crew working (FV). A served passenger gets the round's effect (drinks: Refreshment -40 and the Bladder pulse; meal: Refreshment -70; CONCEPT section 4), and the pass reveals needs (OD3). A passenger asleep or away from the seat is skipped, never woken **(ruled 7)**, and a catch-up service task is posted for them when the round ends.
4. It returns to the galley. The round is over when every cart is back.

The business rows are served by the purser's hand service, posted 3 min before each round starts so the one galley trip it needs happens before the carts fill the aisle. A purser who is late to it finds the carts in the way and serves business after they return; that is a moment, not a failure.

**Seatbelt sign on mid-round.** Carts stop where they are with the brake on and the crew hold at them; if the sign stays on past 10 min (FV), the carts return to the galley and the round resumes from the rows it reached once the sign goes off.

### Rounds in descent (ruled 8)

A round still running at descent start is not cut off: it runs on until the landing secure check. The owner ruled this against the recommended cutoff, so the design has to keep carts in the descent lav rush rare on the baseline and short when they happen.

1. **The plan keeps the baseline clear of descent.** With six crew the meal is expected back in the galley about 9 min before descent (1:56 against 2:05). Carts are in the aisle at descent only when the round has been held up: a turbulence pause, pre-empted cart crew, a long lav-queue block. The success criteria bound how often (at most 10% of reference seeds, FV); if Z6 measures more, the first tuning move is the meal's start time, not a new rule.
2. **From descent start, a round serves and does not catch up.** Carts keep serving row by row, but no catch-up service is posted for passengers the round skips after descent start; they miss it. Catch-ups posted earlier and not yet started are withdrawn at the landing secure check.
3. **The landing secure check ends the round.** It is posted when the sign comes on in descent (2:12, FV, 12 min before landing). Its claim (90) is above the cart's hold (75), so it pre-empts the cart crew, and the cart's cleanup is its stow: the crew finish the row they are serving, push the cart back to the galley, and then walk their secure check. Every passenger the cart had not reached misses the round, and that is one moment ("the meal cart was stowed at row 24 for the landing check; 36 passengers missed it").
4. **A zone's secure check waits for the aisle.** A zone's landing check cannot complete while a cart is in its rows. Stowing from the farthest row (row 3) takes under a minute at cart pace (FV), and a lav queue in the aisle holds the cart as it always does (squeeze rule 3), so the stow is the one place the descent lav rush can delay the check.
5. **Passengers blocked by the cart in descent** get the Bladder-to-Unease cascade as they do in cruise (CONCEPT section 4). Nothing extra is added, and carts get no give-way rule in descent **(ruled 11)**: the cascade is the cost the report traces, and it is what makes a late meal a real trade.
6. **Not secure at touchdown.** A zone whose landing check has not completed at touchdown is a moment naming what held it (a cart still stowing, a passenger in the lav). It is not an incident. On the baseline this should never happen; the success criteria bound it.

With four crew, the one-cart meal is expected to run past descent start (about 2:11 against 2:05) and still be serving when the landing check stows it at 2:12, so the four-crew variant shows the descent overlap on most seeds. That is the staffing lesson, not a baseline problem.

### The squeeze rule beyond carts (R14)

An aisle slot holds one character; a squeeze admits one more for the duration of a pass, at +4 ticks (1 s) on the passer's traversal (FV). R14 fixes that a cart fills its slot, cannot be squeezed past in either direction, and cannot enter an occupied slot. The rest:

1. **Squeeze passes a stopped character.** A walker follows a slower walker going the same way and never passes it. Walkers in opposite directions pass by squeeze, as does a walker reaching a character who is standing (a crew member answering a call, a passenger waiting).
2. **Boarding and deboarding are queues.** While a passenger stows or retrieves a bag, or stands in the deboarding line, other passengers do not squeeze past them. Crew may, which is how bin help reaches a stuck row against the flow.
3. **A queue in the aisle does not yield to a cart.** When a lav queue spills out of its queue nodes into aisle slots, walkers and crew squeeze past the queuers, but a cart waits until the slot ahead is clear. This is the canonical story: the drinks round fills the aft lav queue, and the queue stops the cart.
4. **A passenger in the row the cart fills cannot step out.** They wait in the seat until the cart moves on, which is the "cannot reach a lav" condition of CONCEPT section 4's Bladder cascade.
5. **Cart against walker.** When a walker going the other way stands in the slot a cart needs, and the walker cannot pass the cart, the walker gives way: back to their own seat if they are beside it, otherwise one slot back away from the cart, repeated until the cart can move. Only a queuer (rule 3) holds its ground.
6. **Seats are not the aisle.** Passing seated passengers to reach a window or middle seat is a lateral move on the seat edges, not a squeeze; its cost and what it does to a sleeper are passengers.md (D1)'s. Crew never enter seat nodes.

### The reference flight's timeline

A day departure; sim clock times are local at origin (FV). T is time from boarding start; boarding start to doors open is 2 h 31 min.

| T | Clock | Stage (F1) | Seatbelt sign | Crew |
|---|---|---|---|---|
| -0:20 | 10:10 | pre-boarding | on | At stations; galley loaded; no tasks |
| 0:00 | 10:30 | boarding | on | Purser on door station; bin help as posted |
| 0:26 | 10:56 | boarding | on | Planned last passenger through the door; secure checks posted |
| 0:30 | 11:00 | boarding | on | Planned cabin ready |
| 0:32 | 11:02 | taxi-out | on | Pushback at the later of 0:32 and cabin ready (H1); crew seated |
| 0:44 | 11:14 | takeoff | on | Seated |
| 0:45 | 11:15 | climb | on, off at 0:56 | Released at sign off; check-in walks posted |
| 1:01 | 11:31 | climb | off | Drinks round starts (sign off +5) |
| 1:05 | 11:35 | cruise | off | Service, call buttons, lav checks, breaks |
| 1:36 | 12:06 | cruise | off | Meal starts (sign off +40) |
| 2:05 | 12:35 | descent | off | A running round serves on without catch-ups |
| 2:12 | 12:42 | descent | on | Landing secure checks posted; any cart still out is stowed |
| 2:24 | 12:54 | landing | on | Seated |
| 2:25 | 12:55 | taxi-in | on | Seated until stop |
| 2:31 | 13:01 | deboarding | off | Doors open; purser on door station; bin help |
| about 2:44 | 13:14 | done | | Last passenger off; the target is D3's formula |

Expected round lengths, to be measured: with two carts the drinks round runs about 12 min and the meal about 20; with one cart, about 21 and 35, which runs the four-crew meal past descent start into the landing check. Turbulence comes from the flight emulator's `feed` stream (H1), with its first chance set in D3.

## Player-facing feedback

**Player surface (from M3; nothing in M1).** Per crew member: position, current task and a strain band (fine, stretched, over the redline), which serve the staffing and zone levers. The board as a count of waiting tasks by zone, with the oldest wait, serving zone assignment. The cart positions, serving the service plan. Staleness per zone ("nobody has walked rows 21-30 in 25 minutes"), serving check-in cadence. Competence and fatigue are roster values the player sees when staffing (M3); focus and the choice chances are never on the player surface.

**Dev surface (M1 through `Sky.Sim`, from M2 the inspector).** The board over time with every task's claim, hold, wait and pre-emptions; each crew member's strain and fatigue curves; every auto-resolve record (f, q, p, pick); cart minutes stopped and what stopped them; the balance CSV columns D3 names.

**The report (M1).** Crew moments: a redline crossing and what drove it; a missed incident with the task that held its crew and that task's wait; a cart stopped by a lav queue with its minutes; a round stowed for the landing check with the rows it never reached; an event task nobody started and its "Leave it for now" fallback; a zone not secure at touchdown and what held it; the last passenger seated and why, including a secure check held by a passenger in the lav; an event with its resolver and choice.

## Interactions

| With | Crew to it | It to crew |
|---|---|---|
| Needs (passengers.md (D1)) | Rounds and catch-ups lower Refreshment and pulse Bladder; check-ins and answered call buttons lower Unease; a cart in a row blocks the lav, triggering the Bladder-to-Unease cascade; an untidy lav lengthens visits and pushes Unease; "back to your seat" answers a passenger up with the sign on | Needs post call buttons, "back to your seat" tasks and incidents; sleepers and passengers away are skipped by the cart; reading lights widen reach in a dimmed cabin |
| Need reads (passengers.md (D1) section 9, OD3) | Competence and empathy, read on the `crew/<id>/read` stream | The read rule is D1's; the three crew decisions that consult a read (`lav_permission`, catch-up drink, check-in follow-up) act on the band read |
| Events (events.md, D4) | Focus picks the choice; a crew choice becomes event work; late notice delays resolution (ruled 2); event work nobody starts times out into "Leave it for now" | Event work competes with the service; consequences raise incidents and move needs |
| Task board and executor (E1, E2) | Hold priority, zones and spill decide claims; cart pairs meet at a sync point | Pre-emptions add strain; waits feed backlog strain |
| Nav graph and occupancy (B1, B3) | Carts fill slots; crew squeeze against boarding flow; crew stations and jumpseats are nodes | Queues stop carts; path cost decides who claims |
| Crew-observed view (F5, H4) | Every crew position observes its reach; service rounds are the densest pass | Staleness shows where zones are thin |
| Lighting plan | | Dimmed lights narrow reach to the crew member's own row, except for passengers with their reading light on (ruled 5; the reading-light rule is passengers.md (D1)'s) |
| Lav state (the `one-lav-locked` variant) | Lav checks reset condition | A locked lav is not checked; the open one goes untidy faster |
| Seatbelt sign and turbulence (feed) | | Pauses rounds; seats crew in taxi, takeoff and landing |
| Scoring (G2, G3) | Secure checks set cabin ready (ruled 4); arrival times set incident handling; strain and minutes over the redline are the strain outcome; bin help shortens deboarding | |

Requirements this puts on implementation steps, for the orchestrator to carry into briefs: B1 adds the crew station and jumpseat nodes; B3 distinguishes crew and passenger squeeze and the queue and stowing states (rules 1 to 3); E2 pre-empts on claim against hold; E3 takes the strain sources above, including backlog; S3's choice record carries `quality`; F6 records the resolution fields above, withdraws event work unstarted after 10 min into the "Leave it for now" effects, and resolves on notice; F5 records need reads through `crew/<id>/read`, posts the catch-up drink and check-in follow-up from `urgent` reads, and honours reading lights in the dimmed reach; the crew schema (K1, X2) carries empathy; D1 needs a reading-light rule. From rulings 15 and 16 (covering more than one zone): F5 reads "own zone" as any covered zone for claims, pre-emption candidates and backlog, seats a covering member only once every covered zone's secure check is done, gives every crew member a home zone, computes each member's extra zones E (the Floater included), gives the purser's duties to the first zone's lead when no `purser` flies, serves economy rows no cart spans by catch-up service, and caps breaks at a third of the crew (at least 1); `CrewStrain` (E4's) takes E and applies the covering factor and the scaled backlog cap, with the per-extra-zone factor added to `StrainSettings`; the crew schema (K1, X2) gains the trait field for zones carried free, and `crew.json` gains the `floater` trait (no M1 roster member carries it); F7's "able" follows the claim rule; G3's redline moment names the covering; H3's flight header says "understaffed" when crew are fewer than zones, and I2's balance CSV carries the crew and zone counts; K2b refuses the four new cases in the table (twice in a zone, a duplicate zone id, a cart crew member in no zone, a cart without two different crew); balance-analyst adds the covering factor and the cap scaling to `balance.md` section 2.20 (and the Floater's factors to 2.18) and measures them (Z6).

## Edge cases and failure modes

- **Needs switched off.** Rounds still run and still block the aisle; they move no need. No call buttons or need incidents are posted. Strain still accrues from rounds, walks and checks. The flight runs.
- **Events switched off.** No event work and no auto-resolve records; everything else is unchanged.
- **Contagion switched off.** No effect on crew.
- **A task nobody can physically reach** (a call button between two carts): it waits until a cart crew member is free or a cart moves; the invariant holds because no idle crew member was able to claim it before spill, and after spill the one who claims it is walking, not idle.
- **Cart deadlock.** Rule 5 (walkers give way) and rule 3 (queues drain into the lav) mean the only lasting block is a lav queue, which drains. If a cart has not moved for 5 min (FV), it is a moment and the invariant checker logs it by name, so fuzzing finds any case the rules missed.
- **A passenger in the lav at a secure check.** The check holds until they are seated; at departure that delays cabin ready and is the cited cause. At landing, a check not complete at touchdown is a moment, not an incident (see "Rounds in descent").
- **A cart stowing in descent against a lav queue.** The queue holds the cart (squeeze rule 3) and so holds that zone's landing check; the queue drains into the lav, so the hold ends, and if it outlasts touchdown it is the not-secure moment above.
- **An event noticed late and then left unstarted.** Up to 10 min to notice and 10 min unstarted: the fallback lands about 20 min after firing, and the moment says both waits.
- **One cart crew member pre-empted.** The cart holds with its brake on and keeps filling its slot until the crew member returns.
- **All crew in a zone on a break or a cart.** Spill takes over after 3 min; severe incidents spill at once.
- **The purser forward of the carts when a break is due.** No break until the carts return; the strain curve shows it.
- **A crew member over the redline for the rest of the flight.** Nothing collapses in M1; the minutes over the redline are the cost.
- **A scenario with a row in no zone or a zone with no crew.** Refused by the scenario validator (K2) naming the row or zone.
- **A covering member** (ruled 15, 16). Claims at once in every covered zone, strains faster by the extra zones (the covering factor and the scaled backlog cap), and is seated at boarding only once every covered zone's secure check is done. Listed twice in one zone, or on a cart with no zone, the scenario is refused (the K2b table above).
- **A covering member leading several zones, on a break or a cart.** Those zones' late events still resolve at the notice cap by that member (resolution needs no walk); their event work waits on the board and spills after 3 min like any other task.
- **One crew member for the whole cabin** (ruled 16). Accepted and flown: they cover every zone, do the purser's duties, serve economy by catch-up service with no carts, and break alone, leaving nobody free. Strain reaches the redline early and stays there, calls wait, and the flight still runs to done; the report's header says "understaffed" and the moments say what it cost. Nothing collapses in M1 (as for any crew member over the redline).
- **A Floater covering two zones.** No covering strain; the backlog still counts both zones' tasks at a one-zone cap, and the member still does two zones' work, so their strain rises with the load, not with the listing.
- **Needs switched off with a covering member.** The covering factor still applies to rounds, walks and checks; the backlog term is smaller because no call buttons post.

## Out of scope for M1

Crew needs (crew do not eat, sleep or use the lavs); crew names, voices and announcements; relations between crew members; cart stock and restocking trips; a collection pass after the meal; gate-checked bags and a "bins full" call; crew incidents or collapse; duty-time limits; player orders to crew (from M3 a player command only takes precedence over what crew would have done); the widebody's second aisle; check-in cadence, break policy, the do-not-wake rule and "last service cutoff" (CONCEPT section 3) as levers (fixed defaults here: rounds run on until the landing check; levers from M3).

## Owner rulings

Ruled by the owner from the ranked options below (ruling 12 came without options); the first option listed was the recommendation. The body of the doc describes the ruled option.

1. **Hold priority.** **Ruled: option 1, yes;** only the cart, the business hand service and the galley break hold above their claim. The due break's hold was amended to 50 by ruling 13.
2. **Event resolution.** **Ruled: option 1,** by the first crew member to notice, else the zone lead after 10 minutes.
3. **Service "order".** **Ruled: option 1,** the order of the rounds (drinks first or meal first).
4. **Cabin ready.** **Ruled: option 1,** the last zone's secure check completing.
5. **Dimmed lights and reach.** **Ruled: option 1, with an addition:** reach narrows to the crew member's own row, "but reading lights help illuminate certain passengers if they have it on" (owner's words): a passenger with their reading light on stays observable at the normal reach. The reading-light rule is passengers.md (D1)'s.
6. **Lav condition.** **Ruled: option 1,** in M1: untidy after about 20 uses without a check; a lav check fixes it.
7. **Sleepers and the cart.** **Ruled: option 1,** not woken; skipped and served afterwards.
8. **A round running at descent.** **Ruled: option 2, against the recommendation:** it runs on until the landing secure check. "Rounds in descent" is the design that keeps the baseline quiet under it.
9. **Carts for six crew.** **Ruled: option 1,** two carts, one per half of economy.
10. **Misread direction.** **Ruled: option 1,** a misread errs toward fine. Also ruled with it: fatigue does not blur reads, and the distress band seen by sight is exact.
11. **Carts giving way in descent.** **Ruled: option 1,** no extra give-way rule; timing and the landing-check stow keep it rare, and blocked passengers pay through the cascade.
12. **Read-driven crew decisions.** **Ruled:** besides `lav_permission`, a catch-up drink after a Refreshment read of `urgent` and a check-in follow-up after an Unease read of `urgent` consult a need read; their tasks and priorities are in the task table.
13. **A due break and pre-emption.** **Ruled:** the due galley break holds 50 (claim 45), amending ruling 1's first value of 35 so that the break holds above its claim as ruling 1 says; and a crew member on a break is the last choice for a pre-emption: any crew member the task may pre-empt who is not on a break goes first, then lowest hold, then nearest. A call button still cuts a break when nobody else can take it. The owner's words: "Crew prioritize taking care of passengers, if a due break is interrupted it is what it is, and it impacts the staff member, and other staff members should generally try to jump in so that the crew member on break can remain on break, but at the end of the day, passengers come first." Not taken: a cart posting waiting for a break to end (worst case: a special rule for one pair of tasks that the board's priorities cannot explain, with hand service and catch-ups still cutting the break).
14. **What resets the no-break clock.** **Ruled:** only a full break, one that ran its 10-minute minimum, restarts the 60-minute clock; a break cut short still lowers strain for the minutes it ran. Accepted worst case: a crew member in a call-heavy zone whose breaks keep being cut stays on the ×1.5 rate and can cross the redline. Not taken: any break tick resets the clock (worst case: a 30-second sit-down wipes the 60 minutes and call-button pressure on crew stays hidden).

15. **A crew member listed in two zones.** **Ruled, against the recommendation to refuse it:** "It should be doable for understaffed flights, and have the appropriate consequences on the strain it puts on the crew member". A covering member claims at once in every zone they cover, and pays a covering factor on the time-on-task rate and a scaled backlog cap (see "Covering more than one zone"). Not taken: refusing a member in two zones (worst case: a crew thinner than its zones has no way to cover them but spill, and the owner's understaffed case cannot be staged). Its first limit of two zones was lifted by ruling 16.
16. **How far covering goes, and what "understaffed" means.** **Ruled:** "If the user wants they could try staffing a whole narrow body with one crew member and see what happens. No zone limit. Crew trait that allows them to handle 2 zones just as fine. The expectation is a member per zone, anything below that is understaffed." So: no limit on the zones one member covers; the covering strain grows with every extra zone; the Floater trait carries one extra zone free; understaffed is fewer crew than zones, and covering is allowed whether or not the flight is understaffed. Not taken: covering only below a new per-layout standard complement (worst case: a field to author on every layout, and the same floater choice legal at five crew and refused at six); covering only with fewer crew than zones (worst case: a floater on a fully staffed flight is refused).

Also ruled in passengers.md (D1) and carried here: a crew interaction reveals a need as a band whose accuracy depends on the crew member (more experienced and more empathetic crew read more accurately), which gave the crew model empathy and "Need reads"; a "back to your seat" task exists for a passenger up with the sign on (Bladder 85 or more, cruise only); an event whose crew task is never started times out after a waiting limit (10 min, FV) into its "Leave it for now" effects, with a moment that nobody came.

The options as weighed:

1. **Does a running task defend itself at a hold priority above its claim priority?**
   1. Yes: each task has a claim and a hold priority, and only the cart (claim 40, hold 75), the business hand service (claim 42, hold 60) and the galley break (claim 10 or 45, hold 35) differ. Worst case: two numbers per task to tune, and a severe incident mis-set below 75 would never pull crew off a cart.
   2. No, priority only, with the cart above call buttons. Worst case: the cart then outranks call buttons on the board too, so an idle crew member takes a waiting round over a call button, and the service beats the passenger every time.
   3. No, priority only, with the cart below call buttons. Worst case: every call button pulls a crew member off the cart, rounds never finish on a busy flight, and the canonical "queue blocks the cart" story is drowned out by crew shuttling.
2. **When is an event resolved, and by whom?**
   1. When a crew member first notices it, by the noticer; the zone lead resolves it at a 10-minute notice cap. Worst case: events fired in an unwatched stretch resolve late, their consequences can land first, and the baseline misses more incidents, which D3 and Z7 must tune against.
   2. At fire time, by the zone lead. Worst case: observation and events never touch, so the check-in cadence lever in M3 has no effect on events.
   3. At fire time, by the nearest crew member. Worst case: the resolver is whoever happens to be near, so competence effects depend on where crew stand and the Z6 check gets noisier.
3. **What is "order" in the service plan (OD4)?**
   1. The order of the rounds (drinks then meal, or meal first). Worst case: in a flight with one of each, only two orders exist and the sweep may show little difference.
   2. The order of cabin sections (business first or economy first). Worst case: with 12 business seats and one aft galley, it barely moves any outcome.
   3. Both. Worst case: two knobs under one name, harder to attribute in the report.
4. **What sets cabin ready?**
   1. The last zone's secure check completing, which needs every passenger seated. Worst case: a slow crew or one stuck behind the boarding flow makes doors late on a flight whose passengers were all seated.
   2. The tick every boarded passenger is seated (F3's current wording). Worst case: crew play no part in on-time doors at departure, so staffing cannot move it.
5. **Do dimmed cabin lights narrow crew observation reach?**
   1. Yes, to the crew member's own row. Worst case: a lights-down cruise goes stale fast, so the M3 observed view feels broken at night unless check-ins are frequent.
   2. No. Worst case: the lighting plan only touches Rest, one system, which is thin by pillar 3.
6. **Is lav condition in M1?**
   1. Yes: a lav goes untidy after 20 uses without a check, lengthening visits and pushing Unease. Worst case: one more state and two more numbers to tune, and the `one-lav-locked` variant measures lav checks as well as lav count.
   2. No lav check in M1. Worst case: a named task type is dropped from D2's scope.
   3. Lav check as observation only. Worst case: a task that exists to produce data and changes nothing, which pillar 3 cuts.
7. **Does the cart wake sleepers?**
   1. No: sleepers and passengers away are skipped, and a catch-up task serves them after the round. Worst case: catch-ups add late work to the board and pull crew from breaks.
   2. Yes: the cart wakes everyone. Worst case: every round costs every sleeper their Rest, and the lighting lever loses much of its effect.
8. **What happens to a round still running at descent?**
   1. It is cut off; passengers not reached miss it. Worst case: on four crew, a quarter of the cabin misses the meal, which may read as harsh rather than as a staffing lesson.
   2. It runs on until the landing secure check. Worst case: carts are in the aisle as the descent lav rush starts, a strong story but a baseline that may not stay quiet.
9. **Two carts for six crew, or one?**
   1. Two carts, one per half of economy. Worst case: the direction lever moves Refreshment less, since the longest wait halves, so the `service-back-to-front` sweep may be too weak to pass.
   2. One cart, four crew free. Worst case: rounds take twice as long on the baseline, the six-crew meal runs close to the cutoff, and four crew differs from six less than it should.
10. **Which way does a misread need band err?**
    1. Toward fine: a junior or detached crew member misses need, and never invents it. Worst case: from M3 the observed view is systematically rosier than the truth under weak crew, so the player learns to distrust calm bands, which is the intended lesson but can feel unfair until the report shows who read it.
    2. Either way, at even odds. Worst case: weak crew raise false alarms as often as they miss, so the player cannot tell an understaffed zone from a noisy one.
    3. Toward the passenger's loudest need (the band of whichever need is highest). Worst case: needs a second read of the passenger per interaction, and misreads stop depending on the crew member.
11. **Do carts in descent yield to passengers heading for a lav?**
    1. No extra rule: the plan keeps the baseline clear of descent, the landing check stows any cart, and a blocked passenger pays through the Bladder-to-Unease cascade. Worst case: on seeds where a turbulence pause pushes the meal into descent, a handful of aft passengers are stuck until 2:12, and their peaks land in the worst tenth.
    2. Yes: in descent, a cart that has blocked a lav-bound walker for 1 min (FV) is pushed back to the galley and resumes after the aisle clears. Worst case: a cart shuttles back and forth through the lav rush and serves almost nothing, and the report's attribution gets muddy.
    3. Service in descent switches to hand service from the galley, no carts. Worst case: this is the cutoff under another name, which the owner ruled against.

## Open questions for the owner

None open.

## Success criteria

Measurable in `Sky.Sim` sweeps (Z6) unless marked otherwise.

- On the reference over 500 seeds, no crew member crosses the redline on at least 90% of seeds, and no cart stays stopped for more than 5 min on at least 90% of seeds.
- `four-crew` against the reference over seeds 1 to 200: the strain outcome's peak and minutes over the redline, and the count of missed incidents, are higher, by more than the spread across seeds.
- `service-back-to-front` against the reference: the Refreshment of the forward and aft thirds moves in opposite directions, and the rows served last change.
- Across the 500-seed sweep, resolutions by crew with focus under 0.4 pick a below-best choice more often than those with focus over 0.7, by more than the spread.
- On the reference over 500 seeds, a cart is in the aisle at descent start on at most 10% of seeds (FV), and every zone's landing secure check completes before touchdown on at least 99% (FV).
- On the reference over 500 seeds, event work times out into "Leave it for now" on at most 10% of event firings that needed crew (FV).
- Across the sweep, need reads by crew with accuracy over 0.8 match the true band more often than those under 0.6, by more than the spread.
- In the fuzz (Z1), no task waits past the limit while an able crew member is idle, and no cart is ever squeezed past.
- Test-level (F5, ServiceRoundTests): a front-to-back round serves row 3 before row 16; a lav queue in the aisle stops the cart; a call button posted mid-round is claimed only when a crew member is free.
- Test-level (F5, with `CrewStrain`; ruled 15, 16): a covering member claims a task in any covered zone with no spill wait, and a task outside all of them only after it; under the same scripted load, the strain their time-on-task term adds over 30 on-task minutes at the same fatigue is 1.2 times a one-zone member's for two zones and 1.4 times for three, and 1 times for a Floater in two zones; the backlog term of a three-zone member reaches +0.9 per minute and no more.
- Test-level (F5, flight): a one-crew scenario on the small test layout (every zone covered by one member, no carts) runs to done with every economy passenger offered a catch-up service and no invariant failure.
- Test-level (K2b, `ScenarioValidatorTests`): each refusal in the K2b table fails with its sentence and path, and each accepted case loads.
- Measured (Z6, once a scenario with a covering member exists to sweep): the covering member's peak strain and minutes over the redline are higher than the same member's in one zone, by more than the spread, and the call-button waits in the covered zone are shorter.
- Read-through (owner): a report on a four-crew seed reads as a staffing story ("the meal cart was stowed at row 24 for the landing check; fa5 crossed the redline at 12:20 after 58 minutes on the cart") rather than as numbers.

## Terms this doc coins

For the glossary: **claim priority**, **hold priority**, **zone**, **zone lead**, **cart span**, **spill time**, **focus**, **notice cap**, **crew station**, **jumpseat**, **catch-up service**, **lav condition**, **galley break**, **full break**, **bin help**, **empathy**, **need read**, **read accuracy**, **back to your seat**, **catch-up drink**, **check-in follow-up**, **stow** (a cart's return to the galley as its cleanup), **covering member**, **zones covered**, **extra zones**, **home zone**, **covering factor**, **understaffed** (fewer crew than zones), **Floater** (the `floater` crew trait).
