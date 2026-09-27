# In the Sky docs

Start here. In the Sky is a passenger and crew cabin simulator in Godot 4.7.2 .NET and C#, a greenfield rewrite of OpenPax.

| Question | Owner |
|---|---|
| What was decided at kickoff, and why? | [plans/2026-09-26-rewrite-decisions.md](./plans/2026-09-26-rewrite-decisions.md) |
| Which engineering decisions stand? | [decisions/README.md](./decisions/README.md) (the ADRs) |
| What is next? | [plans/MAIN.md](./plans/MAIN.md) |
| What does a word mean? | The glossary below |

## Glossary

| Term | Meaning |
|---|---|
| ADR | Architecture Decision Record: one numbered file in `docs/decisions/` stating an engineering decision, its context and its consequences. |
| Allowlist | The licenses an asset may carry: CC0 and public domain, CC-BY 3.0 and 4.0, CC-BY-SA, OFL for fonts, MIT and BSD for shaders and data. |
| Auto-resolve | Crew make an event's choice on their own, weighted by competence, traits and fatigue. It is used when no player is making the choice. |
| Concept pass | The short design sitting before any code that produces `docs/design/CONCEPT.md`. |
| Crew-observed view | The player's picture of the cabin, built from what crew have seen, which ages and goes stale. A setting switches it to the true state. |
| Dev inspector | A developer-only surface showing why a character chose what it did, from the top candidate scores each decision records. |
| Display RNG | The random source for purely visual draws, kept apart from the sim streams so drawing never changes the simulation. |
| Engine | `Sky.Engine`, the simulation library, which references no other project or package. |
| Invariant fuzzing | Running many seeded flights and checking after every tick that rules which must always hold still hold. |
| Journal | The record of a flight's inputs (tick counts per frame, commands, observations) that, with the seed, replays the flight exactly. |
| Nav graph | The graph the engine derives from a layout: aisle slots, seat nodes, galley, lav and door nodes, with capacity and reservations. |
| Named stream | A random sequence handed out by the one seeded RNG root under a fixed name, so adding draws in one system never shifts another's. |
| Port | An interface the Engine defines and another project implements (`IClockSource`, `ISimFeed`, `IBehaviorScripts`). |
| Provenance gate | The prek hook and CI job that fail the build on an asset without a ledger entry, a license outside the allowlist, or an entry pointing at a missing file. |
| Provenance ledger | `assets/PROVENANCE.toml`, one entry per asset recording origin, license, author and source. The gate checks it, and `CREDITS.md` is generated from it. |
| RNG root | The one seeded random source of a flight, which hands out named streams. |
| Sequence executor | The one runner for passenger and crew actions: it orders them by priority, interrupts a lower one for a higher, runs cleanup on interrupt, and holds sync points. |
| Session | `Sky.Session`'s `ISkySession`: the client pulls views from it and pushes commands to it. |
| Spike | A short, throwaway experiment on its own branch that measures something a decision depends on. |
| Squeeze rule | The nav graph rule that lets one character pass another in an aisle slot that is already occupied. |
| Stage manager | The player's role: they set the conditions the cabin plays out in, rather than moving people. |
| Sync point | A named moment where several characters' sequences wait for each other, such as two crew working one cart. |
| Task board | The prioritized list of work (services, call buttons, checks) that crew claim. A higher-priority task can pre-empt a lower one. |
| Tick | One fixed step of simulation time. The spike sets its size; 250 ms is the working figure. |
| Utility scoring | How a passenger picks the next activity: each candidate scores itself from needs, traits and context, and the highest score wins. |
| View projection | One of the Session's two ways of building what the client sees: from crew observations, or from the true state. |
