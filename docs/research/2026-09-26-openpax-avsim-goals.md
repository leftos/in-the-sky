# What the owner set out to deliver in OpenPax: the AVSIM thread (research, 2026-09-26)

Source: the AVSIM thread "OpenPax: Open-Source Passenger Add-On (Early Development)" ([page 1][p1], [page 2][p2], [page 3][p3]), fetched with `curl` on 2026-09-26 and parsed post by post. The thread has three pages and 39 posts (15, 15 and 9); `/page/4/` redirects to page 3. The original poster, and so the owner, is **Leftos** (profile 520242); 21 of the 39 posts are theirs (9, 8 and 4 by page), dated 2025-01-29 to 2025-07-29. Every owner post was read in full as text. Not readable: the screenshots and videos in the 2025-02-22, 2025-07-28 and 2025-07-29 posts, which the page serves as lazy-loaded placeholders, so what the ticket-sales UI and the seat map looked like is not recorded here. Other users' posts are used only as context for what the owner answered. Dates are the post's UTC timestamp; each link goes to the post's anchor. Terms are in the glossary in [docs/README.md](../README.md).

The comparison column reads [CONCEPT.md](../design/CONCEPT.md) as ruled on 2026-09-26. Where CONCEPT is silent but the kickoff record it defers to, [the rewrite decisions](../decisions/rewrite-decisions.md), settles the point, the verdict says so, since that record is part of the rewrite's position.

## Summary

- **Open, free and permissive was the premise, not a feature.** The opening post frames OpenPax as a freeware hobby project, open source so the community can continue or fork it, and later commits to MIT so anyone, commercial developers included, can take ideas and code. The rewrite carries this and goes further (public from the first push, MIT for everything, a provenance gate).
- **"Customizability is the name of the game": the player decides how much they handle.** Repeated in three posts: every aspect has automation options, shareable automation profiles, play as Captain with everything automated or as Lead Flight Attendant, a switch for realism against fun. CONCEPT carries the mechanism (every system has an off switch, crew act on their own, a player command only takes precedence) but not the named roles, automation profiles or per-feature automation choices.
- **Every passenger and every flight is unique ("Sims but in the sky").** Traits that touch every interaction, trait pairs, groups, professions, inventory, relationships, thoughts. CONCEPT keeps the aim through systems interacting, but narrows it hard: five needs, traits as rate modifiers, only Unease spreads, and no inventory, relationships or thoughts.
- **Any seat map, and the software works out the rest.** User-authored airframes (ASCII art or JSON), a shareable object library, automatic aisles, crew positions, doors and service zones, with optional overrides, any crew count from zero up. The rewrite carries the automatic derivation (the nav graph) and a layout editor at M6, and changes the authoring to physical units; ASCII becomes a debug dump.
- **Works with MSFS, will not require a flight simulator, and respects sim rate and pause.** Carried: MSFS feed with an off switch, the emulator, `IClockSource` driven by MSFS or time warp, 64x.
- **A normal companion footprint beside the sim.** The owner defaulted sprites to 16x16 and flagged the local TTS model's 3 GB so the add-on leaves the sim room. CONCEPT sets a tick-rate budget but no memory, VRAM or CPU budget for running beside MSFS, while the rewrite now bundles an on-device model. This is the clearest gap.
- **Crew are people with limits, and staffing choices matter.** Crew with needs, traits and fatigue; overworked best staff fail; flexible crew counts; fatigue carried to the next flight. CONCEPT carries staffing and strain strongly; carry-over between flights is deferred to "Later".
- **Continuity between flights.** Named by the owner as something they "really want" (schedule, lingering crew fatigue, company-level policies). The rewrite defers it (progression and saves are "Later"; CONCEPT only mentions AirlineOps and the roster).
- **Announcements with live, varied content.** Dynamic TTS announcements, user-editable scripts, subtitles for accessibility, weather from METAR, captain-only announcements for crew-less flights. The rewrite keeps the TTS and subtitle stack and swaps cloud LLMs and API keys for a bundled on-device model; user-editable scripts, weather and crew-less announcements are not mentioned.
- **The simulation must be legible to the player.** Need history on hover ("You don't have to guess"), a person details window, a cabin overview. CONCEPT moves per-passenger numbers to dev surfaces (pillar 5) and makes the report, traced to moments, the legibility tool for the player.

## Goals by theme

Each row is one goal, quoted from the owner. **Weight** is *priority* when the owner marked it as central, repeated it or promised it for the first alpha, and *passing* when it was a single mention, an aside, or offered as nice-to-have. **In CONCEPT** is one of: carried, carried differently (how), dropped, or not in CONCEPT (with where the rewrite does or does not settle it). Rows marked **Gap** are goals the rewrite currently misses or has not decided.

### 1. Openness, licensing and community

| Goal | Owner's words | Date, post | Weight | In CONCEPT |
|---|---|---|---|---|
| Freeware hobby project, not income | "this is going to be a hobby project that I'm not leaning on for income. This comes with positives (open-source, freeware) and negatives" | [2025-01-29][c5354045] | Priority (the premise) | Not in CONCEPT; the decisions record carries it (public repo, MIT). Price is never stated in the rewrite. |
| The code outlives the owner | "If and when I give up on the project, the code and assets will all be up on GitHub for the rest of the collaborators to continue working on it, and / or other folks to fork" | [2025-01-29][c5354045] | Priority | Carried by the decisions record, more strongly: public from the first push. Note the "and assets" promise: the rewrite's provenance ledger and allowlist make it keepable, where OpenPax's paid LimeZu pack could not be redistributed. |
| MIT, so anyone may reuse ideas and code, commercial developers included | "part of the reason I'm making this open-source is so that anyone from the community, commercial developer or hobbyist or otherwise, is free to take any of the ideas and implementations and put them in their own product as well. I'll be releasing it under the MIT license" | [2025-02-19][c5378402] | Priority | Carried (decisions record: "Everything is MIT-licensed: code, original art, Lua events and data"; CLAUDE.md non-negotiable). |
| Community-driven: PRs, mods, "break it so we can make it better together" | "Remember that this is a community-driven open-source project!"; "the OpenPax GitHub repo will be made public, so that folks can submit bug reports, pull requests, mods, etc." | [2025-02-06][c5363960], [2025-02-10][c5367899] | Priority | Carried differently: the repo is public now, content is Lua and data files, and the content authoring loop is M6. CONCEPT itself does not speak of modding or community content. |
| Repo stays private until a minimum viable product | "Just don't want to open the floodgates before things are at a minimum viable product, to avoid distractions" | [2025-02-10][c5367899] | Passing (a tactic) | Reversed by the owner: the rewrite is public from the first push. |
| A core team of collaborators | "What I am looking for, however, is collaborators ... I'm hoping to have a core team" | [2025-01-29][c5354045] | Priority at first; changed (see "How the stance changed") | Not in CONCEPT; the rewrite's team is the studio agents. |
| Keep the thread civil; no attacks on competing add-ons | "I would prefer if we don't take cheap shots at other devs, please"; "I would rather keep the thread focused on OpenPax" | [2025-02-22][c5382719], [2025-02-13][c5371589] | Principle | Not a CONCEPT matter. Relevant to public posts, which the owner approves. |

### 2. Player choice: automation, roles and customization

| Goal | Owner's words | Date, post | Weight | In CONCEPT |
|---|---|---|---|---|
| Not a micromanagement game: every aspect can be automated, and automation profiles are shareable | "Every aspect of gameplay will have automation options, and automation profiles will be shareable to provide different gameplay experiences" | [2025-02-06][c5363960] | Priority (promised for the first alpha) | Carried differently. Pillar 1 makes the flight run with nobody watching and a player command only takes precedence; pillar 6 gives each system an off switch. There is no per-lever manual/automatic choice and no shareable profile. **Gap** if the owner still wants profiles. |
| Play as Captain (everything automated, scored on the flight and emergent situations) or as Lead Flight Attendant, "and every experience in between" | "You want to play as a Captain, having all of the crew-and-passenger interactions automated, simply being scored on how well you did on your flight ... You want to play as a Lead Flight Attendant, coordinating your crewmates ... Both of those experiences, and every experience in between will ship in the first public alpha" | [2025-02-06][c5363960] | Priority | Carried differently. CONCEPT has one role, the stage manager, and scores only what the cabin controls (question 9 rules out blaming the cabin for pilot and ATC delays). A Captain mode that scores the flying is dropped; a "light companion to MSFS" (pillar 6) is its nearest descendant. **Gap**: no ruling on a Captain mode. |
| The player sets the line between realism and fun | "Customizability remains the name of the game ... You'll get to decide where the line between 'realism' and 'unrealistic but fun gameplay' is for you, and tune the experience to your preferences" | [2025-02-16][c5374820]; also [2025-02-10][c5367899] ("Customizability and being able to cater your experience to how YOU want to play is the name of the game") | Priority (stated three times) | Carried narrowly: the off switches (pillar 6) and the observed/true view setting. No difficulty or realism settings beyond those. |
| Individual passenger needs catered to "where reasonable"; use your best crew on an anxious passenger; offer an out-of-band drink | "You'll be able to use the best of your crew to calm anxious passengers down, offer them an out-of-band beverage" | [2025-02-08][c5365520] | Priority | Carried differently: pillar 2 forbids per-passenger control; the same outcome comes from policies (check-in cadence, call-button priority) and moments (an event about that passenger). |
| Handle call-button and special situations yourself or leave them to crew | "Same as everything else, you'll have the choice of either handling those situations manually, or leaving your crew to automatically respond to them" | [2025-02-22][c5382761] | Priority | Carried: crew auto-resolve events (M1), the player may answer a moment (M3). |

### 3. Simulator compatibility, standalone play and time

| Goal | Owner's words | Date, post | Weight | In CONCEPT |
|---|---|---|---|---|
| Works with MSFS via SimConnect | "Using SimConnect to interact with MSFS" | [2025-01-29][c5354045] | Priority | Carried (MSFS feed with an off switch, pillar 6; M5 in the decisions record). |
| Other simulators through community adapters | "it should be able to support any and all flight sims that expose those variables! ... someone could easily write an XPlane adapter, a P3D adapter" | [2025-02-06][c5363981] | Priority in principle, owner will not build them | Not in CONCEPT. The decisions record's `ISimFeed` port makes an adapter possible, but no document says other sims are a goal. **Gap** (small): state it, or state that it is not a goal. |
| Will not require a flight simulator; a standalone game | "OpenPax will NOT require a flight simulator ... will also be able to be used as a standalone game!" | [2025-02-06][c5363960] | Priority (the post's headline) | Carried: the emulator and standalone mode (decisions record, `IClockSource`); CONCEPT's fantasy says "maybe you, in MSFS". |
| Sim rate and pause are honoured; standalone players can speed up, slow down and pause | "everything in OpenPax will pause when you pause, and will go faster when you're using sim rate > 1x ... you'll be able to speed up / slow down / pause at any time, so you can get through any boring stages quicker" | [2025-02-06][c5363984] | Priority | Carried (time warp in the core loop; 64x pinned in the M1 done list; MSFS clock drives the sim). |
| An internal flight emulator so community layout authors can test without flying | "an internal basic flight emulation to help with app development and debugging, as well as airframe and airport development by the community, so that you don't need to do a whole flight in MSFS just to test a seat map" | [2025-01-29][c5354045] | Priority | Carried (the emulator; `Sky.Sim` runs a flight headless). |
| SimBrief import and dispatch | "Simbrief import & dispatch" | [2025-01-29][c5354045] | Listed as core, never mentioned again | Not in the rewrite anywhere. **Gap** (undecided). |
| Optional GSX integration for boarding and deboarding | "Optional GSX integration re: passenger boarding / deboarding" | [2025-01-29][c5354045] | Listed as core, never mentioned again | Not in the rewrite anywhere. **Gap** (undecided). |
| Ticket and cargo sales respect MZFW / MTOW | "Ticket and cargo sales taking into account MZFW / MTOW" | [2025-01-29][c5354045] | Listed as core, never mentioned again | Not in the rewrite; cargo is absent entirely. **Gap** (undecided; belongs with the booking market). |

### 4. Performance and footprint beside the simulator

| Goal | Owner's words | Date, post | Weight | In CONCEPT |
|---|---|---|---|---|
| Keep resource use reasonable so the sim has room | "for people that are looking to use it as a flight sim add-on, it's important that its VRAM usage (and general resource usage) remain reasonable, so the flight sim can have the breathing room it needs. So OpenPax now offers 3 different character sprite resolutions ... with the default being the lowest" | [2025-02-22][c5382761] | Priority ("it's important") | Not in CONCEPT. The rewrite's only performance target is ticks per second (64x). **Gap**: no memory, VRAM or CPU budget for running beside MSFS. |
| Heavy local models are opt-in because of their cost | Kokoro "bloats OpenPax's memory usage to 3GB (from 500MB ...) ... so probably will only be used by those with beefier PCs"; small language models: "the system requirements are just way too high (e.g. 8GB RAM for just the LM)" | [2025-02-10][c5367899], [2025-02-12][c5370896] | Priority (it decided what shipped) | Carried differently, and in tension: the decisions record bundles an on-device small model, with its size and CPU speed left to M4. **Gap**: the rewrite's model choice has no stated resource ceiling. |

### 5. Cabin simulation: passengers, crew and the world

| Goal | Owner's words | Date, post | Weight | In CONCEPT |
|---|---|---|---|---|
| Every passenger and crew member is unique; traits shape every interaction ("Sims but in the sky") | "A big thing about OpenPax is that every passenger and crew member will be unique ... Those traits will impact every single interaction between two people on the aircraft"; "Every passenger is going to be unique, and every flight also unique as a result" | [2025-02-06][c5363960], [2025-02-08][c5365520] | Priority (the identity of the product) | Carried differently. Pillar 3 keeps "stories from systems touching each other", but traits act as rate modifiers, and only Unease spreads between neighbours (question 7). Trait-pair interaction effects are not in CONCEPT. CONCEPT's own worst case for the ruled need set is "too few meters to tell passengers apart". |
| Needs that interconnect and are shaped by personality | "Passengers with individual needs (hunger, thirst, bladder, comfort, entertainment, anxiety, fatigue, health), personalities, and behaviors"; "Passenger status incremental updates affected by passenger personality and interconnected needs" | [2025-01-29][c5354045] | Priority | Carried differently: five needs (Refreshment, Bladder, Rest, Unease, Boredom), comfort as a seat modifier, health as an event-raised incident, cascades into Unease. |
| Duty free and a Shopping need | "Duty free is here for international flights, and with it comes a new passenger need called Shopping (I know it's silly, but not everything has to be serious)" | [2025-02-22][c5382761] | Passing (self-described as silly) | Dropped (option C rejected; Shopping named as a meter with no lever). |
| Initial needs depend on the airports, time of day, concessions and delays | "Initial passenger need status influenced by departure and arrival airport (e.g. size of airport and inferred concession options, departure and arrival time and whether concessions are open, boarding / flight delays)" | [2025-01-29][c5354045] | Priority (core list) | Carried in part: Rest follows the body clock at origin and trip purpose; airport size and concessions are not mentioned. |
| Passenger groups (families, couples, bachelor parties, sports teams, corporate teams) who sit together and influence each other | "Different passenger groups, from couples and nuclear families to bachelor parties and youth sports teams ... A family will want to buy seats that are close together ... you might need to see if any other passengers are willing to give up their seat" | [2025-02-16][c5374820]; first raised [2025-02-06][c5363960] | Priority | Carried differently: groups are in the manifest generator (decisions record); a split group surfaces as a seat-swap moment (question 3); a rowdy group would be a Lua event. |
| Professions matter (doctors help in medical emergencies, off-duty crew calm others, same-profession networking) | "Doctors will be able to assist with medical emergencies. Retired pilots and active flight crew on a transfer flight will have a positive effect on passengers around them" | [2025-02-16][c5374820] | Priority (listed among "the things that make OpenPax unique") | Carried in part: profession is in the manifest; medical incidents are Lua events that read traits. A passenger helping the crew is not described. |
| Passenger inventory (earplugs, sleep masks, tablets, amenity kits, sanitizing wipes) | "Fun little details like that will make every passenger, and thus every flight, unique" | [2025-02-10][c5367899], [2025-02-16][c5374820] | Priority in those posts | Dropped (not in CONCEPT or the decisions record). |
| Relationships that change through interactions | "Passengers that came onboard as a group will have a high relationship score with each other ... relationship scores will change depending on the success of the interactions" | [2025-02-20][c5379296] | Priority in that post | Dropped. |
| Thoughts, thought bubbles and speech bubbles you can peek at | "little thought phrases are created, each with an importance factor and a natural fall-off timer, that you can take a peek at (if you want) per passenger" | [2025-02-08][c5365520] | Priority in that post | Dropped as a player surface; the report's moments and the dev inspector's decision scores take the explaining role. |
| Passengers act on their own (blinds, reading lights, stretching, bins, sleep, call button) | "It's little details that make the cabin feel alive"; "Passengers will be able to take initiative" | [2025-02-22][c5382761] | Priority | Carried: pillar 1 and utility-scored activities; the call button is in Unease and a service lever. Blinds and reading lights are not mentioned. |
| Pathfinding with obstacles: a cart in the aisle blocks the lav; people wait behind someone stowing a bag; passengers switch lavs when one is busy | "passenger can't access the bathroom or go back to their seat because a flight attendant pushing a service cart is in the aisle" | [2025-01-29][c5354045], [2025-02-06][c5363960], [2025-02-08][c5365520] | Priority | Carried, and central: CONCEPT's canonical story is the cart blocking the lav queue; node capacity, reservations and the squeeze rule. |
| Crew have needs, traits and fatigue; tired or anxious crew serve slower and err more; crew eat, drink and use lavs | "Crew has needs too, okay?! ... Crew that are too tired or too anxious might be slower performing services" | [2025-02-22][c5382761] | Priority | Carried differently: crew strain (time on task without a break, pre-emptions, fatigue) and fatigue weighting event choices. Crew do not have the passenger need set. |
| Picking the right staff matters, and overworking your best staff costs you | "Picking the right staff will have a big impact in passenger satisfaction. Overwork your best staff, however, and all the traits in the world won't be enough" | [2025-02-08][c5365520] | Priority | Carried: staffing from a roster (M3), strain as a scored outcome. |
| Any crew count, including none; the captain makes the announcements on crew-less flights | "Same seat map can be used with 1 crew member, 2, 3, 8, whatever you want ... you can also not staff a flight at all"; "In flights with passengers but no cabin crew, the Captain will be the one making all the announcements" | [2025-02-06][c5363960], [2025-02-12][c5370896] | Priority | Carried in part: crew count is a lever (4 against 6 in the M1 done list). A zero-crew flight is not mentioned. **Gap** (small): decide whether a flight with no cabin crew is supported. |
| Red-eye behaviour: dim lights, avoid non-critical announcements, help people sleep | "Red-eye flights will see crew avoid making announcements that aren't critical, diming the lights, and just generally doing what they can to help passengers sleep" | [2025-02-10][c5367899] | Priority | Carried as levers: lighting plan, announcement policy, a "do not wake for service" rule. |
| Special events with hard trade-offs | "Special events ... will require you to decide if and how to handle them, but there might be tough decisions to make, where prioritizing one situation might negatively impact another" | [2025-02-08][c5365520] | Priority | Carried: Lua events as moments, pre-emption on the task board, strain and missed incidents as the cost. |
| Optional fog of war: needs and traits hidden until crew interact | "Fog of war is still very much planned as an optional difficulty setting, where passenger needs and personality traits are hidden until your crew interact with them" | [2025-01-29][c5354045], [2025-02-08][c5365520], [2025-02-16][c5374820] | Priority (repeated; "the most complex" item left before alpha, [2025-03-03][c5392619]) | Carried differently: the crew-observed view is the default player view, with a setting for the true state (decisions record), and staleness is itself information. OpenPax framed it as an optional difficulty. |
| Realistic cabin lighting (sun position, sun shafts, airport light, mood lighting) | "It's all really subtle stuff, and who knows how many people will even notice" | [2025-02-20][c5379296] | Passing (by the owner's own account) | Carried as a lever (lighting plan); the visual treatment waits for the art direction at M2. |

### 6. Aircraft and content authoring

| Goal | Owner's words | Date, post | Weight | In CONCEPT |
|---|---|---|---|---|
| Users create, edit and share airframes easily (JSON or ASCII seat map), with a shareable object library | "all you have to do is ASCII art yourself a seat map ... and OpenPax takes care of everything else itself"; "That's literally all you have to do to add your own custom airframe" | [2025-01-29][c5354045], [2025-02-06][c5363960], [2025-02-06][c5363966] | Priority (first item in the core list; the proof of concept he was proudest of) | Carried differently: layouts in physical units with per-row seat groups, a layout editor at M6; ASCII is kept only as a debug dump. CONCEPT mentions layouts only as a lever (seat pitch, lav count). |
| Everything derived automatically (aisles, service zones, crew positions, doors), with optional user overrides | "Nothing is hardcoded in the seat map ... You don't have to worry about setting up crew placements, paths, any of that ... The plan, of course, is allowing users to override some of these things" | [2025-02-06][c5363960] | Priority | Carried for the derivation (the nav graph from the layout); overrides are not mentioned. |
| Services are configurable (intervals, triggers, dependencies) | "did I mention that even the services offered are going to be configurable?" | [2025-02-06][c5363966] | Priority | Carried as the service plan lever (order, direction, start time) set from the scenario file. |
| Airframe settings for seat comfort per class, IFE and Wi-Fi | "Airframe configuration re: seat comfort per class, IFE options, Wi-Fi options" | [2025-01-29][c5354045] | Priority (core list) | Carried: seat pitch and width feed Rest and Unease; IFE and Wi-Fi move Boredom. |
| Boarding strategies, set once for your company | Five strategies implemented; "once some sense of progression between flights is implemented, you should be able to just set it for your 'company'" | [2025-02-08][c5365520] | Priority | Carried: boarding order is a policy lever. |
| Data is easy to extend by editing text | "You want to add more cities that might not have specific pricing patterns ... It's going to be as easy as editing a text file" | [2025-02-16][c5374820] | Priority (as a principle) | Carried in spirit: Lua for behaviour, data files, `Sky.Content` with a validator. |

### 7. Economy and progression

| Goal | Owner's words | Date, post | Weight | In CONCEPT |
|---|---|---|---|---|
| Continuity between flights: a schedule, and crew fatigue that carries over | "I really want OpenPax to have a sense of continuity between flights, not just be something you can only use for an individual flight in a vacuum. A sense of progression" | [2025-02-06][c5363987] | Priority ("I really want"), moved after the first alpha ([2025-02-22][c5382761]) | Not in CONCEPT beyond the AirlineOps hooks; the decisions record puts saves and progression under "Later". **Gap** (deferred, not dropped). |
| A ticket sales simulation with pricing by distance, season, time of day, demand, wealth; day-by-day pricing decisions; seat choice by group | "The ticket sales simulator has its first iteration ready, and there's so, so much that went into it" | [2025-01-29][c5354045], [2025-02-16][c5374820], [2025-03-02][c5391827], [2025-07-29][c5533840] | Priority (the subject of the last three development posts) | Not in CONCEPT; the decisions record makes the booking market an AirlineOps backlog item. **Gap** (deferred) given how much of the owner's recent OpenPax attention it had. |
| Company management (buying airframes, crew, vendors, hubs, routes) | Listed under "Some blue sky stuff for down the road" | [2025-01-29][c5354045] | Passing (blue sky) | Carried as AirlineOps, the rewrite's later mode. |
| Crew sleeping quarters | "Eventually I want to add crew sleeping quarters, but again, that's likely to come much further down the line" | [2025-02-22][c5382761] | Passing | Not in the rewrite. |

### 8. Announcements, voice and accessibility

| Goal | Owner's words | Date, post | Weight | In CONCEPT |
|---|---|---|---|---|
| Dynamic spoken announcements (airline, flight number, cities, local time, altitude) | "Announcements will be dynamic ... And the only way to do that is by leveraging TTS tech" | [2025-02-10][c5367899] | Priority, though "not the meat of what OpenPax is about" ([2025-02-12][c5370896]) | Carried: announcements at M4 through the ported TTS stack; voice has an off switch (pillar 6). |
| User-customizable, parametrizable announcement scripts | "announcements already are user-customizable, including being able to add your own parametrizable (dynamic) announcement lines / scripts" | [2025-01-29][c5354045], [2025-02-10][c5367899] | Priority | Not in CONCEPT; the small model works "around a preset theme and rules" with authored templates as the fallback. **Gap** (small): whether players may write their own. |
| A range of TTS providers, from built-in Windows voices to local Kokoro to ElevenLabs with the user's own key, or none | "let's talk options OpenPax will ship with" | [2025-02-10][c5367899] | Priority | Carried differently: the provider registry is ported, Windows TTS stays Windows-only; the decisions record drops cloud LLM providers and API keys and does not say whether a paid cloud TTS stays. |
| Fresh announcement wording from cloud LLMs (OpenAI, Anthropic, Google) with the user's key, grounded to avoid hallucination | "each announcement contains an LLM ... prompt, and 3 providers are already supported given you have an API key" | [2025-02-12][c5370896] | Priority then | Reversed: no cloud LLM and no API-key setting ships; a bundled on-device model adds variety and can never change sim state. |
| Live destination weather in announcements | "pulling the live METAR of the destination airport ... interpreting it into plain English" | [2025-02-12][c5370896] | Priority in that post | Not in the rewrite; the small model brings up "topics about the destination or landmarks". **Gap** (small). |
| Subtitles, because accessibility matters | "because accessibility matters, announcements will optionally show up as 'subtitles'" | [2025-02-10][c5367899] | Priority ("accessibility matters") | Carried by the decisions record (the subtitle segmenter and duration calculator are ported); not in CONCEPT. |
| English numbers for non-English voices | "the TTS module receiving script with all numbers converted into English words" | [2025-02-12][c5370896] | Passing (a fix) | Carried (the TTS/subtitle text split is ported). |

### 9. Transparency to the player

| Goal | Owner's words | Date, post | Weight | In CONCEPT |
|---|---|---|---|---|
| You can see why a need moved | "You don't have to guess ... you'll be able to hover over a given need bar to see what all has been impacting it over a rolling window of time" | [2025-02-22][c5382761] | Priority | Carried differently: per-passenger numbers stay on dev surfaces (pillar 5); the player's "why" is the report, which traces each outcome to the moments and modifier classes that moved it. |
| A person details window and a cabin overview window | "The person details window is your one-stop shop"; "The cabin overview window will give you an in-depth high-level look at your passengers and crew as a whole" | [2025-02-20][c5379296] | Priority | Carried differently: the Glance step reads the crew-observed cabin, heatmaps are kept (decisions record); a per-person window survives only as the dev inspector. |
| Ticket sales explain why each group bought or did not | "super informative with regards to everything about each passenger and passenger group's factors that led them to buy or not buy tickets" | [2025-07-29][c5533840] | Priority | Not in the rewrite (no booking market yet). |
| Scoring (undesigned in OpenPax) | Listed under "Core stuff planned with no high-level design yet: Scoring"; the Captain is "scored on how well you did on your flight, as well as how you handled emergent situations" | [2025-01-29][c5354045], [2025-02-06][c5363960] | Priority, undesigned | Carried and far more developed: four outcomes side by side, spread not average, incidents handled or missed, crew strain, on-time doors, each traced to moments. |

### 10. Engine and process (passing)

| Goal | Owner's words | Date, post | Weight | In CONCEPT |
|---|---|---|---|---|
| Godot, with a wish to move to Unity if it caught up on C# | "If Unity ever supports newer C# versions, I'll likely migrate the project over, but for now, Godot has been serving the project's needs fine" | [2025-02-06][c5363960] | Passing | The rewrite stays on Godot (4.7.2 .NET). |
| Placeholder name, open to suggestions | "Nah, it's just a placeholder project name. Open for open source, and pax for passengers" | [2025-02-13][c5371112] | Passing | Renamed "In the Sky". (An observation, not a stated link: the name echoes the "Sims but in the sky" analogy the owner liked in [2025-02-06][c5363960].) |
| Stop adding features and ship an alpha | "I really need to stop adding things to the feature list, so that I can get to a public alpha test soon(tm)" | [2025-02-10][c5367899] | Priority (a self-correction) | Carried in the rewrite's method: M1 is a headless flight with a written done list, and later features wait for their milestone. |
| Logic is worthless until the player can see it | "all the great logic in the world doesn't help if it's not exposed to the user" | [2025-03-03][c5392619] | Principle | Carried differently: pillar 5 and the separate dev and player surfaces; M1 exposes it through the text report first. |

## Non-goals the owner stated

- **No feature requests yet**: "At the moment I am not looking for feature requests or suggestions, as I want to get all the main components done first before people get their hopes up" ([2025-01-29][c5354045]). Relaxed within a week (see below).
- **No local language model at launch**: "No local LM implementations at launch ... the system requirements are just way too high ... and the results not at the quality I'd want them to be" ([2025-02-12][c5370896]). The rewrite reverses this by bundling one; the owner's 2025 reasons (RAM and quality) are exactly the questions the decisions record leaves for M4.
- **Not income-driven**: "any income-driven motivation beyond donations won't be there" ([2025-01-29][c5354045]).
- **Not only for realism**: the owner treats realism as one end of a player setting, not a rule ([2025-02-16][c5374820]); duty free was added "not everything has to be serious" ([2025-02-22][c5382761]).
- **Other simulators are not the owner's job**: adapters are welcome from the community, the owner builds MSFS ([2025-02-06][c5363981]).

## Requests from other users, and what the owner did

| Request | By, date | Owner's answer | Reason given |
|---|---|---|---|
| Support X-Plane and other sims | Aglos77, [2025-02-06][c5363978] | Accepted in principle ([2025-02-06][c5363981]) | "As long as I (or anyone in the flight sim community, really) can figure out how to get the variables OpenPax needs out of it"; MSFS first because it is the sim the owner flies and SimConnect is familiar; open source means someone else can write the adapter. A second X-Plane mention (Aglos77, [2025-03-02][c5392061]) got no reply. |
| Is the name "OpenPax" final? (a Linux project uses it) | verbal, [2025-02-12][c5371052] | Accepted: the name can change ([2025-02-13][c5371112]) | "just a placeholder project name"; invited suggestions for the public alpha. |
| Name the developer behind the harassing sock puppet | KL Oo, [2025-02-13][c5371262] | Declined ([2025-02-13][c5371589]) | "I would rather keep the thread focused on OpenPax, if that's okay, so that it doesn't get trashed and closed like last time"; pointed to a PM to the site owner. |
| (Implicit) cheering OpenPax as a replacement for a competitor | UAL4life, [2025-02-22][c5382263] | Rejected the framing ([2025-02-22][c5382719]) | "cheap shots at other devs ... will only serve derail it." |
| A competitor's developer denies involvement, calls competition healthy | FPVSteve (Self-Loading Cargo), [2025-02-19][c5377862] | Agreed, and restated the MIT commitment ([2025-02-19][c5378402]) | Open source so anyone, commercial developers included, can reuse the ideas. |
| Is it only UI work now? | Andayle, [2025-03-02][c5392394] | Answered ([2025-03-03][c5392619]) | Logic must be exposed to matter; about ten items left before the first public alpha, fog of war the hardest. |
| Offers to help (Fiorentoni, creator of Realistic Pax & Cargo; JonathanC) | [2025-01-29][c5354051], [2025-01-29][c5354231] | No reply in the thread (the owner had asked for PMs) | Not stated. By [2025-03-03][c5392619] the owner calls it "a solo project so far". |
| Praise for user-made seat maps (the reason one user left Passenger2) | environmental_ice, [2025-01-29][c5354237] | No direct reply; the next update leads with custom airframes | Not stated. |
| Is it still in development? | Juan LLobera, [2025-05-21][c5472317] | Answered two months later with the 2025-07-28 update ([2025-07-29][c5533840]) | Burn-out from Godot's UI framework and time spent on VATSIM controlling. |

The owner also asked the thread for input three times: personality traits and passenger groups ([2025-02-06][c5363960]), missing boarding strategies ([2025-02-08][c5365520]) and a new name ([2025-02-13][c5371112]). No user answered any of them in the thread.

## How the stance changed over the thread

1. **Feature requests.** Closed in the opening post ([2025-01-29][c5354045]), then invited for traits, groups and boarding strategies a week later ([2025-02-06][c5363960], [2025-02-08][c5365520]).
2. **Team.** From "hoping to have a core team" ([2025-01-29][c5354045]) to "This has been a solo project so far ... I'll get the project past the finish line solo if I have to" ([2025-03-03][c5392619]).
3. **Add-on to game.** The emulator started as a development tool ([2025-01-29][c5354045]) and became a product promise: "will also be able to be used as a standalone game!" ([2025-02-06][c5363960]).
4. **Scope.** The owner kept adding (inventory, groups, professions, relationships, lighting, duty free) while saying "I really need to stop adding things to the feature list" ([2025-02-10][c5367899]); progression moved to after the first alpha ([2025-02-22][c5382761]); ticket sales went from one bullet in the opening list to the whole of the last three development posts ([2025-03-02][c5391827], [2025-07-29][c5533840], [2025-07-29][c5533900]).
5. **Pace.** "development will slow down a bit" ([2025-02-22][c5382761]), then a gap from March to July and "fighting Godot's UI framework burned me out on working on OpenPax for a few months" ([2025-07-29][c5533840]). The rewrite's avoid-list item "UI built in code" (decisions record, section 3) is the lesson in code; the thread shows its cost in the owner's motivation.
6. **Language models.** Cloud LLMs with user keys shipped as options and local models were ruled out on resource grounds ([2025-02-12][c5370896]); the 2026 rewrite reverses both.
7. **Openness timing.** Private repo until an MVP ([2025-02-10][c5367899]); the rewrite is public from the first push.

## Goals CONCEPT misses or carries differently

Misses (not in CONCEPT, and not settled anywhere else in the rewrite):

- A resource budget (RAM, VRAM, CPU) for running beside MSFS, which the owner called important; it now matters more because the rewrite bundles an on-device model.
- Shareable automation profiles and a per-feature choice between manual and automatic, and the named Captain and Lead Flight Attendant ways to play.
- SimBrief import and dispatch.
- GSX integration for boarding and deboarding.
- Cargo, and weight limits (MZFW / MTOW) on sales.
- Whether other simulators (X-Plane, P3D) are a goal for community adapters.
- Flights with no cabin crew, and the captain-only announcements that go with them.
- Player-written announcement scripts, and live destination weather in announcements.
- Passenger inventory, relationships and thoughts (dropped by omission rather than by a ruling).
- Passengers who help (doctors, off-duty crew) and airport-driven initial needs (concessions, delays).
- User overrides of the automatic layout derivation (crew positions, service zones).

Deferred rather than missed (the decisions record puts them under "Later" or AirlineOps): continuity and progression between flights, crew fatigue that carries over, company-level policies, the ticket sales simulation, company management.

Carried differently:

- Per-passenger care becomes policies and moments (pillar 2), including seating (question 3).
- Unique passengers through trait-pair interactions become traits as rate modifiers, five needs and Unease-only contagion.
- The nine OpenPax needs become five, with comfort a seat modifier, health an incident and Shopping dropped.
- Crew needs become crew strain and fatigue.
- Optional fog of war becomes the default crew-observed view with a setting for the truth.
- Player-facing transparency (need history, person and cabin windows, thoughts) moves to dev surfaces, and the report traced to moments becomes the player's explanation.
- Customizability becomes the off switch for each system; the realism-versus-fun setting is narrower.
- ASCII and JSON seat-map authoring becomes physical-unit layouts with a layout editor at M6; ASCII stays as a debug dump.
- Cloud LLM wording with user keys becomes a bundled on-device model with templates as the fallback.
- Scoring, undesigned in OpenPax and including the flying for a Captain, becomes four cabin-controlled outcomes side by side.

[p1]: https://www.avsim.com/forums/topic/663197-openpax-open-source-passenger-add-on-early-development/
[p2]: https://www.avsim.com/forums/topic/663197-openpax-open-source-passenger-add-on-early-development/page/2/
[p3]: https://www.avsim.com/forums/topic/663197-openpax-open-source-passenger-add-on-early-development/page/3/
[c5354045]: https://www.avsim.com/forums/topic/663197-openpax-open-source-passenger-add-on-early-development/#findComment-5354045
[c5354051]: https://www.avsim.com/forums/topic/663197-openpax-open-source-passenger-add-on-early-development/#findComment-5354051
[c5354231]: https://www.avsim.com/forums/topic/663197-openpax-open-source-passenger-add-on-early-development/#findComment-5354231
[c5354237]: https://www.avsim.com/forums/topic/663197-openpax-open-source-passenger-add-on-early-development/#findComment-5354237
[c5363960]: https://www.avsim.com/forums/topic/663197-openpax-open-source-passenger-add-on-early-development/#findComment-5363960
[c5363966]: https://www.avsim.com/forums/topic/663197-openpax-open-source-passenger-add-on-early-development/#findComment-5363966
[c5363978]: https://www.avsim.com/forums/topic/663197-openpax-open-source-passenger-add-on-early-development/#findComment-5363978
[c5363981]: https://www.avsim.com/forums/topic/663197-openpax-open-source-passenger-add-on-early-development/#findComment-5363981
[c5363984]: https://www.avsim.com/forums/topic/663197-openpax-open-source-passenger-add-on-early-development/#findComment-5363984
[c5363987]: https://www.avsim.com/forums/topic/663197-openpax-open-source-passenger-add-on-early-development/#findComment-5363987
[c5365520]: https://www.avsim.com/forums/topic/663197-openpax-open-source-passenger-add-on-early-development/#findComment-5365520
[c5367899]: https://www.avsim.com/forums/topic/663197-openpax-open-source-passenger-add-on-early-development/#findComment-5367899
[c5370896]: https://www.avsim.com/forums/topic/663197-openpax-open-source-passenger-add-on-early-development/page/2/#findComment-5370896
[c5371052]: https://www.avsim.com/forums/topic/663197-openpax-open-source-passenger-add-on-early-development/page/2/#findComment-5371052
[c5371112]: https://www.avsim.com/forums/topic/663197-openpax-open-source-passenger-add-on-early-development/page/2/#findComment-5371112
[c5371262]: https://www.avsim.com/forums/topic/663197-openpax-open-source-passenger-add-on-early-development/page/2/#findComment-5371262
[c5371589]: https://www.avsim.com/forums/topic/663197-openpax-open-source-passenger-add-on-early-development/page/2/#findComment-5371589
[c5374820]: https://www.avsim.com/forums/topic/663197-openpax-open-source-passenger-add-on-early-development/page/2/#findComment-5374820
[c5377862]: https://www.avsim.com/forums/topic/663197-openpax-open-source-passenger-add-on-early-development/page/2/#findComment-5377862
[c5378402]: https://www.avsim.com/forums/topic/663197-openpax-open-source-passenger-add-on-early-development/page/2/#findComment-5378402
[c5379296]: https://www.avsim.com/forums/topic/663197-openpax-open-source-passenger-add-on-early-development/page/2/#findComment-5379296
[c5382263]: https://www.avsim.com/forums/topic/663197-openpax-open-source-passenger-add-on-early-development/page/2/#findComment-5382263
[c5382719]: https://www.avsim.com/forums/topic/663197-openpax-open-source-passenger-add-on-early-development/page/2/#findComment-5382719
[c5382761]: https://www.avsim.com/forums/topic/663197-openpax-open-source-passenger-add-on-early-development/page/2/#findComment-5382761
[c5391827]: https://www.avsim.com/forums/topic/663197-openpax-open-source-passenger-add-on-early-development/page/3/#findComment-5391827
[c5392061]: https://www.avsim.com/forums/topic/663197-openpax-open-source-passenger-add-on-early-development/page/3/#findComment-5392061
[c5392394]: https://www.avsim.com/forums/topic/663197-openpax-open-source-passenger-add-on-early-development/page/3/#findComment-5392394
[c5392619]: https://www.avsim.com/forums/topic/663197-openpax-open-source-passenger-add-on-early-development/page/3/#findComment-5392619
[c5472317]: https://www.avsim.com/forums/topic/663197-openpax-open-source-passenger-add-on-early-development/page/3/#findComment-5472317
[c5533840]: https://www.avsim.com/forums/topic/663197-openpax-open-source-passenger-add-on-early-development/page/3/#findComment-5533840
[c5533900]: https://www.avsim.com/forums/topic/663197-openpax-open-source-passenger-add-on-early-development/page/3/#findComment-5533900
