# 0006. Utility scoring for passengers and a task board for crew, on one sequence executor

- **Date:** 2026-09-26
- **Status:** Accepted
- **Source:** [rewrite decisions](../plans/2026-09-26-rewrite-decisions.md) §1 Engine (character AI, content), §2 What to keep (Behavior/Sequence composition), §3 What to avoid (no preemption model), §4 Open questions (Lua runtime, Lua boundary)

## Context

OpenPax has no preemption model: it has the clear-and-re-enqueue, `OverrideCurrentBehavior`, `ClearBehaviors` and `Abort` paths instead. Its Behavior/Sequence composition with named sync points and a `GetSyncGroup` hook (`Code/Characters/Sequences/Sequence.cs:38-74`) is kept as an idea.

## Decision

- Passengers choose their next activity by utility scoring over needs, traits and context, with a bias toward keeping the current one.
- Crew claim work from a prioritized task board.
- Both run on one sequence executor with priority, interrupt and cleanup on interrupt, and sync points.
- Each decision records its top candidate scores for a dev inspector.
- Lua is used for everything behavioral.

## Settled

Settled by [0010](./0010-lua-csharp-at-decision-points-and-250-ms-ticks.md): Lua runs only at decision points and event evaluation, on Lua-CSharp, under an instruction budget.

## Consequences

- Interruption goes through one executor with cleanup, not through separate clear and override paths.
- Why a character chose what it did can be read from the recorded top candidate scores.
