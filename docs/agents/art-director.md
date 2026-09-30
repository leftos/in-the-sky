# art-director overlay: In the Sky

Read by the user-level `art-director` agent before it reads the style guide; it adds to what it does and wins where it is more specific.

## The M2 gate

The art direction is open until M2, where the owner decides it with you. Until then, keep `art-direction.md` to what is already set and the questions the decision must answer, and write no brief for final art; a brief for a throwaway placeholder or a scratch scene is fine, marked so. The client is `src/Sky.Client` from M2.

## Seeding the style guide

Seed `docs/design/art-direction.md` from what is already set: the provenance rules (below); the recolourable liveries (marker colours in the SVGs swapped for palette colours before rasterizing, cached by path, palette and scale; `docs/plans/2026-09-26-rewrite-decisions.md` section 2); a 2D cabin view with positions in inches used only for drawing (section 1, "Cabin geometry"); purely visual randomness drawn from the display RNG; and the open questions for the M2 decision. The guide's palettes cover the cabin, the liveries, and player surfaces against dev surfaces; what reads at the cabin view's scale covers a seat, a passenger, a crew member, a cart in the aisle, a lav queue.

`docs/design/CONCEPT.md` section 2's pillars bear on the look: a quiet flight must look quiet, and a stale reading in the crew-observed view must look less certain than a fresh one.

## Provenance: every asset, no exceptions

Every asset has an entry in `assets/PROVENANCE.toml`, checked by the provenance gate. The rules are `docs/decisions/0009-the-asset-provenance-ledger.md` (the entry's fields, generated assets, the ledger and `CREDITS.md`), the glossary rows "Allowlist", "Provenance gate" and "Provenance ledger" in `docs/README.md`, and `docs/DEVELOPMENT.md` "Provenance". A brief names the provenance fields its entry will need; a review checks the asset against its ledger entry; a source whose license you cannot confirm from its own page is rejected, whatever it looks like. The report lists every asset whose entry is missing or incomplete.
