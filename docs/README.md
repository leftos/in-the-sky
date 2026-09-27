# In the Sky docs

Start here. In the Sky is a passenger and crew cabin simulator in Godot 4.7.2 .NET and C#, a greenfield rewrite of OpenPax.

| Question | Owner |
|---|---|
| What was decided at kickoff, and why? | [plans/2026-09-26-rewrite-decisions.md](./plans/2026-09-26-rewrite-decisions.md) |
| What is next? | [plans/MAIN.md](./plans/MAIN.md) |
| What does a word mean? | The glossary below |

## Glossary

| Term | Meaning |
|---|---|
| Auto-resolve | Crew make an event's choice on their own, weighted by competence, traits and fatigue. It is used when no player is making the choice. |
| Concept pass | The short design sitting before any code that produces `docs/design/CONCEPT.md`. |
| Crew-observed view | The player's picture of the cabin, built from what crew have seen, which ages and goes stale. A setting switches it to the true state. |
| Engine | `Sky.Engine`, the simulation library, which references no other project or package. |
| Journal | The record of a flight's inputs (tick counts per frame, commands, observations) that, with the seed, replays the flight exactly. |
| Nav graph | The graph the engine derives from a layout: aisle slots, seat nodes, galley, lav and door nodes, with capacity and reservations. |
| Port | An interface the Engine defines and another project implements (`IClockSource`, `ISimFeed`, `IBehaviorScripts`). |
| Provenance ledger | `assets/PROVENANCE.toml`, one entry per asset recording origin, license, author and source. The gate checks it, and `CREDITS.md` is generated from it. |
| Spike | A short, throwaway experiment on its own branch that measures something a decision depends on. |
| Stage manager | The player's role: they set the conditions the cabin plays out in, rather than moving people. |
| Task board | The prioritized list of work (services, call buttons, checks) that crew claim. A higher-priority task can pre-empt a lower one. |
| Tick | One fixed step of simulation time. The spike sets its size; 250 ms is the working figure. |
| Utility scoring | How a passenger picks the next activity: each candidate scores itself from needs, traits and context, and the highest score wins. |
