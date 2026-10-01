# 0008. A bundled on-device small model, text only

- **Date:** 2026-09-26
- **Status:** Accepted
- **Source:** [rewrite decisions](./rewrite-decisions.md) §1 Engine (language model), §1 Scope (ported from OpenPax), §3 What to avoid (API keys in plain text), §4 Open questions (choice of bundled small model)

## Context

OpenPax stores API keys in plain text in `user://app_config.json` for its cloud LLM providers. Its TTS and subtitle stack is the only code ported, re-shaped for a bundled small model with the cloud LLM providers dropped.

## Decision

- A small model is bundled and runs on-device, in `Sky.Voice`.
- It adds variety to announcements around a preset theme and rules, and brings up topics about the destination or landmarks being overflown.
- Its output changes only the words spoken or shown, never sim state or the sim RNG.
- Authored templates are the fallback.
- No cloud LLM provider and no API-key setting ships. Cloud LLMs are for offline authoring by the team only.
- The model weights go in the provenance ledger ([0009](./0009-the-asset-provenance-ledger.md)) and must be redistributable.

## Open

Which model is bundled (license, size and CPU-only speed) is chosen at M4.

## Consequences

- A replay may word things differently; the simulation still replays exactly ([0004](./0004-the-journal-and-exact-replay.md)).
- There are no API keys to store.
