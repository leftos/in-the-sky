# Explore overlay: In the Sky

Read by the user-level `Explore` agent before its protocol; it adds to that protocol and wins where it is more specific.

## Where a drift fix belongs

A rule the code applies that no design doc states is a **design gap** (`docs/design/`). A file or class the architecture doc names wrongly is an **engineering gap** (`docs/ARCHITECTURE.md`). A decision the code contradicts is an **ADR question** (`docs/decisions/`). Each `DOC DRIFT` line names its class.

## Why it is built this way

`docs/decisions/` holds what the code must not become: the Engine referencing nothing, one seeded RNG root, the journal's exact replay, the crew-observed view. `docs/plans/2026-09-26-rewrite-decisions.md` holds the kickoff decisions and what was kept from OpenPax.

## Prior art in OpenPax, every subsystem exploration

`D:\openpax` is the read-only predecessor this rewrite replaces; read it, never edit it. For every subsystem the question touches, not only when asked:

1. Find how OpenPax built the same subsystem (Grep and Read under `D:\openpax`).
2. Find what its history shows was fixed there: `git -C D:/openpax log --oneline -- <paths>`, then `git -C D:/openpax show <sha>` for each fix whose subject names a bug, a deadlock, a race or a crash.
3. Check what `docs/plans/2026-09-26-rewrite-decisions.md` says the rewrite kept or dropped of it.

OpenPax is evidence of what went wrong, never a design to copy: the design docs and ADRs rule, and a lesson that contradicts them is reported as a question.

Before the closing sections, add a **`PRIOR ART`** section: how OpenPax did it (`D:\openpax` `path:line`), each bug its history shows fixed there with the commit sha and whether the change under exploration could repeat it, and what the rewrite kept or dropped. When OpenPax has nothing on the subsystem, say so in one line.
