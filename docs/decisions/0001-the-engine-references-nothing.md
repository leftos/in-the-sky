# 0001. The Engine references nothing

- **Date:** 2026-09-26
- **Status:** Accepted
- **Source:** [rewrite decisions](../plans/2026-09-26-rewrite-decisions.md) §1 Engine (solution layout), §1 Repo and workflow (platforms), §3 What to avoid (first item)

## Context

In OpenPax the simulation shares an assembly with Godot and the tests. NUnit is referenced by `OpenPax.csproj`, `Global.X` is used 113 times across 63 files, `ConfigManager` has 178 call sites, and `GlobalVars.IsTestEnvironment` branches out of production code.

## Decision

`Sky.Engine` has no project or package references: no Godot, no Lua runtime, no IO. State is passed in explicitly. The Engine defines the ports `IClockSource`, `ISimFeed` and `IBehaviorScripts`, and other projects implement them. Each src project has a `tests/` twin.

| Project | Holds |
|---|---|
| `Sky.Engine` | World, fixed tick, needs, utility scoring, task board, executor, journal; defines the ports `IClockSource`, `ISimFeed` and `IBehaviorScripts`. References nothing. |
| `Sky.Content` | Schemas, loader, validator. |
| `Sky.Scripting` | The Lua host, implementing the Engine's ports. |
| `Sky.Session` | `ISkySession`: the client pulls views from it and pushes commands to it. |
| `Sky.Sim` | Headless runner, seed sweeps, balance CSV output. |
| `Sky.SimConnect` | The MSFS adapter (Windows-only). |
| `Sky.Voice` | TTS, the bundled small model, subtitles. |
| `Sky.Client` | Godot: draws views, holds no rules. |

The repo root also carries `sky.ps1`, `tools/gate.ps1` and `tools/test-all.ps1`. Tests use xunit.v3 on Microsoft.Testing.Platform, with CsCheck for property tests.

## Consequences

- Engine, Sim and tests run anywhere, and CI runs on Linux. Only the SimConnect adapter and Windows TTS are Windows-only, and the client builds without them.
- A flight runs without a view: M1 is a headless cabin flight in `Sky.Sim` with no Godot.
- The client draws views and holds no rules.
- `Sky.Scripting` hosts Lua-CSharp, chosen by the M0 research and spike ([0010](./0010-lua-csharp-at-decision-points-and-250-ms-ticks.md)).
