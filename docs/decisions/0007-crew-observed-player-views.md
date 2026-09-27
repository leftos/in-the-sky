# 0007. Crew-observed player views

- **Date:** 2026-09-26
- **Status:** Accepted
- **Source:** [rewrite decisions](../plans/2026-09-26-rewrite-decisions.md) §1 Engine (visibility, solution layout)

## Context

The client pulls views from `ISkySession` and pushes commands to it ([0001](./0001-the-engine-references-nothing.md)). What those views show the player had to be decided.

## Decision

- The player's view is built from what crew have observed, which ages and goes stale.
- A setting switches it to the true state.
- The Session has two view projections from the start: the crew-observed one and the true state.

## Consequences

- The player sees the cabin through the crew, not through the simulation's own state, unless the setting says otherwise.
- Both projections exist from the first Session, so neither is added later around the other.
