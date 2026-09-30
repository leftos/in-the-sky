# playtester overlay: In the Sky

Read by the user-level `playtester` agent before it plays; it adds to what it does and wins where it is more specific.

## The client

- The client is `src/Sky.Client` in the checkout the brief gives; play starts from its main scene. The drive lessons are `docs/DEVELOPMENT.md`, "The Godot client" and "The godot MCP server".
- The Godot client arrives at M2. Until then there is nothing to play: say so in one line and return. From M2 the client draws the cabin and the dev inspector with no player commands, so a playtest judges whether a watcher can read the flight; the player's levers and event choices arrive at M3.

## The yardstick

Read `docs/design/CONCEPT.md` sections 1 to 3 (the fantasy, the pillars, the minute of play and the flight's phases) and the design doc for the surface under test. A finding names the decision or pillar the play falls short of. In particular:

- **A competent normal flight is quiet** (pillar 4): count every alert or prompt on a baseline flight.
- **The crew-observed view**: can you tell what crew have seen, how old it is, and where nobody has looked? Switch to the true state and compare what the two views show.
- **Watch it land**: after a lever or an event choice changes, can you see the cabin respond over the following minutes of sim time?
- **The dev inspector is a dev surface**: judge it for a developer's question ("why did 23A get up?"), never by player rules.

## How much to play

- A flight runs at its real length in sim time: use the game's own time warp across stretches where nothing needs the player, and note where you had to.
- Play the phases the brief names through; with no phases named, play boarding through the first service and deboarding. Note how long a phase took at each speed, and report the phases you played and at which speeds.
