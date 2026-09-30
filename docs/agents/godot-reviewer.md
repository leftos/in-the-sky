# godot-reviewer overlay: In the Sky

Read by the user-level `godot-reviewer` agent before its procedure; it adds to that procedure and wins where it is more specific.

## The client

- The client project is `src/Sky.Client`; with no file list, review everything under `src/Sky.Client/Scenes` and `src/Sky.Client/Scripts`. The root under review is the main checkout, or a worktree at `../in-the-sky.wt/<slug>` beside it, and each godot call takes the `projectPath` of `src/Sky.Client` in the checkout under review.
- A build, only when a Release diagnostic is in question, runs as every build in this repo runs: `pwsh sky.ps1 build` through `tools/gate.ps1`, logging to `<root>/.tmp/godot-review-build.log`.
- A LAST PASS's stuck game is a flight, and a wrong command is one sent to the Session: "a flight or a screen stuck, a wrong command sent to the Session".

## Project rules

- `docs/ARCHITECTURE.md`, for what the client may hold and what the Session owns.
- `docs/decisions/0003-one-seeded-rng-root-and-a-display-rng.md` (the display RNG) and `docs/decisions/0007-crew-observed-player-views.md` (the crew-observed view).

## Checks

- **Sky.Client draws Session views and holds no rules.** It pulls views from `ISkySession` and pushes commands to it, and never reaches into `Sky.Engine` state. A need, a score, a choice's legality or a decision computed in the client is a finding.
- It draws from both view projections (crew-observed and true state) through the same code, so switching the setting changes the data, never the drawing path.
- Purely visual randomness draws from the display RNG, never a sim stream (ADR 0003). Positions in inches are used for drawing alone, and graph logic stays on the Session's side.
- The C# symbol search includes every implementer of a Session interface the diff touches.
