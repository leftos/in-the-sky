# Decisions

One numbered Architecture Decision Record per engineering decision. The kickoff records below were written from [the rewrite decisions](../plans/2026-09-26-rewrite-decisions.md); each cites the section it came from.

| # | Decision |
|---|----------|
| [0001](./0001-the-engine-references-nothing.md) | The Engine references nothing; state is passed in explicitly, and other projects implement its ports |
| [0002](./0002-fixed-tick-and-iclocksource.md) | Fixed-size ticks driven by one `IClockSource`, sustaining 64x at full cabin; the spike sets the tick size |
| [0003](./0003-one-seeded-rng-root-and-a-display-rng.md) | One seeded RNG root hands out named streams, and display draws use a separate RNG |
| [0004](./0004-the-journal-and-exact-replay.md) | The journal records ticks per frame, commands and observations, so replay from seed and journal is exact |
| [0005](./0005-real-unit-layouts-and-a-derived-nav-graph.md) | Cabin layouts in inches with per-row seat groups, and a derived nav graph that all logic runs on |
| [0006](./0006-utility-scoring-and-a-task-board-on-one-executor.md) | Utility scoring for passengers and a task board for crew, on one sequence executor |
| [0007](./0007-crew-observed-player-views.md) | The player's view is built from what crew observed, with a setting for the true state |
| [0008](./0008-a-bundled-on-device-small-model-text-only.md) | A bundled on-device small model changes only words; no cloud LLM provider ships |
| [0009](./0009-the-asset-provenance-ledger.md) | Every asset has an entry in `assets/PROVENANCE.toml`, enforced by prek and CI |
| [0010](./0010-lua-csharp-at-decision-points-and-250-ms-ticks.md) | `Sky.Scripting` hosts Lua-CSharp; Lua runs only at decision points, under an instruction budget; the tick is 250 ms |

A new ADR takes the next free number. A superseded ADR is kept, and its status changes to `Superseded by NNNN` with a link to the ADR that replaces it; the new ADR names the one it supersedes.
