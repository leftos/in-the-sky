# 0009. The asset provenance ledger

- **Date:** 2026-09-26
- **Status:** Accepted
- **Source:** [rewrite decisions](../plans/2026-09-26-rewrite-decisions.md) §1 Asset provenance, §1 Scope (roadmap, M0)

## Context

OpenPax's credits have gaps. `Audio\Cabin\CabinChime.ogg` comes from a YouTube video with no license. The LimeZu spritesheets are a paid itch.io pack whose redistribution terms are unchecked. `LICENSE.txt` still has its `[year] [fullname]` placeholders.

## Decision

- Every art, audio, font, shader, data and model-weight file has an entry in one machine-readable ledger, `assets/PROVENANCE.toml`.
- Each entry records the path, origin (`original`, `library` or `generated`), SPDX license, author, and source URL with its retrieval date.
- A generated file also records the tool and version, the model id, the prompt (a file kept in the repo) and the creation date.
- Every file records any modifications.
- License allowlist: CC0 and public domain, CC-BY 3.0 and 4.0, CC-BY-SA, OFL for fonts, MIT and BSD for shaders and data.
- The ledger flags CC-BY-SA files, because derivatives of them must stay CC-BY-SA.
- A prek hook and a CI job fail the build on any asset without an entry, any license outside the allowlist, or any entry pointing at a missing file.
- `CREDITS.md` is generated from the ledger and never edited by hand.

## Consequences

- The provenance gate is part of M0.
- An asset cannot land without a ledger entry and an allowed license.
- The bundled small model's weights are ledger entries like any other asset ([0008](./0008-a-bundled-on-device-small-model-text-only.md)).
