# 0005. Real-unit cabin layouts and a derived navigation graph

- **Date:** 2026-09-26
- **Status:** Accepted
- **Source:** [rewrite decisions](./rewrite-decisions.md) §1 Engine (cabin geometry), §3 What to avoid (stringly-typed types and coordinate arithmetic)

## Context

The owner's main complaint with OpenPax's layouts is that seats had to be 1 or 2 cells wide and seat columns had to line up across every row. OpenPax also uses stringly-typed object and seat types (`"Seat"`, `"Economy"`) and scatters `+1`/`-1` arithmetic between a 0-based and a 1-based coordinate space.

## Decision

- Layouts are authored in physical units: seat pitch and width in inches, aisle position and width.
- Each row lays out its own seat groups, so a 2-2 business row can sit in front of a 3-3 economy row.
- The engine derives a navigation graph from the layout (an aisle slot at each row, seat nodes on lateral edges, galley, lav and door nodes) and runs all logic on it.
- Capacity and reservations on the nodes give aisle blocking, and a squeeze rule handles passing.
- Positions in inches are used only for drawing.
- There is one coordinate space, replacing OpenPax's 0-based and 1-based pair.

## Consequences

- Mixed cabins with different seat groups per row are expressible without a grid.
- Simulation logic never reads inch positions; it works on the nav graph.
- Aisle blocking and passing come from node capacity, reservations and the squeeze rule.
