# Development

The toolchain, the commands and their ceilings, the hooks, and the Godot setup. Terms are defined in the glossary in [README.md](./README.md).

## Toolchain

| Tool | Version | Where |
|---|---|---|
| .NET SDK | `global.json` names 10.0.401 and rolls forward to the latest feature band (`latestFeature`); the test runner is Microsoft.Testing.Platform | `dotnet` |
| CSharpier | 1.3.0, pinned in `.config/dotnet-tools.json` | `dotnet tool restore` once per clone |
| Godot .NET | 4.7.2 stable; `src/Sky.Client/Sky.Client.csproj` names `Godot.NET.Sdk/4.7.2` | `sky.ps1` reads `$env:GODOT_PATH` and falls back to `F:\Godot\Godot_console.exe` |
| PowerShell | 7 or later (`sky.ps1`, `tools/gate.ps1`, `tools/test-all.ps1` and the hook scripts all require it) | `pwsh` |
| PSScriptAnalyzer | the PowerShell module the `psscriptanalyzer` hook runs, under `PSScriptAnalyzerSettings.psd1`; the hook exits 1 with install instructions when it is missing | `Install-Module PSScriptAnalyzer` |
| uv | Python 3.13 and the pinned ruff, ty and pytest of `tools/provenance` (its `pyproject.toml` and `uv.lock`); the first `uv run` builds the project's `.venv`, which is ignored | `uv` |
| prek | the git hooks in `prek.toml` | `prek install` once per clone |
| gitleaks | the secrets scan the `gitleaks` hook runs on staged changes | `gitleaks` |
| gh | GitHub CLI | issues, pull requests |

## First clone

From the repo root:

1. `dotnet tool restore` (CSharpier).
2. `prek install` (the pre-commit and commit-msg hooks).
3. `pwsh ./sky.ps1 build`, then `pwsh tools/test-all.ps1`.
4. `pwsh ./sky.ps1 client` once before opening or driving the Godot client, so Godot has imported the project.

The repo is public (ADR 0009 and the rewrite decisions): no secrets go in the tree, and `.env` files are ignored.

## Everyday commands

Everything runs from the repo root through `sky.ps1`; `pwsh ./sky.ps1 help` lists every subcommand. Each gate runs through `tools/gate.ps1` (below), which writes the whole output to a log under `.tmp/`, prints the tail, exits with the command's own status, and kills a run that reaches its ceiling with its children (exit 124). A ceiling is a few times what the command takes today, so a run that reaches one has hung, not slowed: read its log, do not raise the ceiling.

| Command | Wraps | Log | Ceiling |
|---|---|---|---|
| `pwsh ./sky.ps1 build [-Release]` | `dotnet build InTheSky.slnx -c <Debug\|Release> -warnaserror` | `.tmp/build.log` | 300 s |
| `pwsh ./sky.ps1 test [-Project P] [-Filter "*XTests"]` | `dotnet test` over `InTheSky.slnx`, or `--project tests/Sky.<P>.Tests` with P in Engine, Content, Scripting, Session, Sim, SimConnect, Voice, Client; `-Filter` becomes `--filter-class`; the rest forwards, and `-- --timeout 2m` is appended unless the caller forwarded a `--` of their own | `.tmp/test.log` | 180 s; 60 s with `-Filter` |
| `pwsh ./sky.ps1 format [-Check]` | `dotnet csharpier format .`, then `dotnet format style` and `dotnet format analyzers` at `--severity info`; `-Check` uses `csharpier check` and `--verify-no-changes` and writes nothing | `.tmp/csharpier.log`, `.tmp/format.log`, `.tmp/analyzers.log` | 180 s each |
| `pwsh ./sky.ps1 analysis [-Check]` | for every project under `tools/` with a `pyproject.toml` (today `tools/provenance`): `uv run --directory <project>` over `ruff format .`, `ruff check . --fix`, `ty check .` and `pytest . -q`, stopping at the first that fails; `-Check` changes nothing (`ruff format --check`, no `--fix`). `--directory`, not `--project`: ruff, ty and pytest read their configuration from the directory they run in. With no project present it prints `analysis: no uv projects` and passes | `.tmp/<project>-{format,lint,types,tests}.log` | 120 s each |
| `pwsh ./sky.ps1 provenance [-Check]` | without `-Check`, regenerates `CREDITS.md` from `assets/PROVENANCE.toml` and then checks the ledger; with `-Check`, only checks (see "Provenance" below) | `.tmp/provenance-credits.log`, `.tmp/provenance-check.log` | 60 s each |
| `pwsh ./sky.ps1 hooks [prek args]` | `prek run --all-files`, or `prek run <args>` when arguments follow | | none |
| `pwsh ./sky.ps1 client` | the Godot client from a fresh clone, in this order: `dotnet build src/Sky.Client/Sky.Client.csproj -c Debug -warnaserror`, then `--headless --import --quit` (up to three passes on a cold cache, retried only while a pass ends red on the pre-import lines alone: `.godot/imported/`, and art under `res://Art/` that has no loader or fails to load yet), then `--headless --build-solutions --quit` with MSBuild node reuse and shared compilation off, so Godot's job empties when Godot quits. The import comes first because with `.godot/imported/` empty the solutions pass can die before it builds | `.tmp/client-dotnet-build.log`, `.tmp/client-import.log` (`-2`, `-3` for extra passes), `.tmp/client-build.log` | 300 s, 300 s a pass, 180 s |
| `pwsh ./sky.ps1 play` | the game in a window, for playing by hand: the client's Debug build, or the whole `client` order when `src/Sky.Client/.godot/imported/` is missing, then Godot started on the main scene without waiting for it. Launched directly, not through the godot MCP server, so it is an ordinary focused window with sound and real input | `.tmp/client-dotnet-build.log` | 300 s for the build; the game runs outside the gate |
| `pwsh ./sky.ps1 clean` | `dotnet clean InTheSky.slnx` | | none |
| `pwsh tools/test-all.ps1 [-Ceiling N]` | the whole gate (below) | `.tmp/test-all-<check>.log` | 600 s for the run |

Rules every command follows:

- MSBuild switches are written in dash form (`-warnaserror`, `-p:Name=Value`). Git Bash's path conversion rewrites the slash form (`/warnaserror`) into a Windows path and MSBuild reports MSB1008; the dash form reads the same from Bash and PowerShell.
- A test filter needs the full class name or a wildcard: `-Filter "*ReferenceTests"`. A bare class name runs zero tests.
- Never re-run a build or a test run to read its output differently: the log is under `.tmp/`.
- The formatter is CSharpier; `dotnet format style` and `dotnet format analyzers` cover what it does not. A bare `dotnet format` is never run, because its whitespace pass undoes what CSharpier wrote. All three must be clean.
- Warnings are errors (`TreatWarningsAsErrors` and `CodeAnalysisTreatWarningsAsErrors` in `Directory.Build.props`, analysis level `latest-recommended`). The cyclomatic-complexity ceiling is `CodeMetricsConfig.txt` at the root (`CA1502: 8`), which `Directory.Build.props` hands every project as an `AdditionalFiles` item.

## tools/gate.ps1

`pwsh tools/gate.ps1 -Log <path> -TimeoutSeconds <n> [-Tail <n>] -- <command> [args...]` runs one command under a ceiling. Gates run through it, never bare, for three reasons:

- **Context.** Every line a command prints lands in an agent's context and is re-read on every later turn, so the gate prints only the last lines (20 by default) and the whole output stays in the log. On a failure it first prints the log's failure-marker lines with their line numbers.
- **The real status.** A pipeline reports its last command's status, so `cmd | Select-Object -Last 20` passes a failed build. The gate exits with the command's own status, and also fails a zero exit whose log holds a known failure marker (`Build FAILED.`, `error CS…`, `: error `, a failed test summary), since a runner can print a green summary for stale binaries after a failed build.
- **Hangs.** A command still running at the ceiling is killed with every process it started (`taskkill /T`), the log gains a `gate: TIMED OUT` line, and the gate exits 124, the status coreutils `timeout` uses.

The command's standard error is appended to the log after it ends, so one file holds everything. Exit 2 means the gate could not read its own arguments. On Windows a bare command name (`uv`, `dotnet`) is started from the first file on `PATH` with an extension `PATHEXT` lists, which skips the extensionless bash shims a Claude Code plugin puts first on the Bash tool's `PATH`; `tools/test-all.ps1` resolves names the same way.

A command with no `sky.ps1` subcommand is written under the gate in full, for example `pwsh tools/gate.ps1 -Log .tmp/sim-run.log -TimeoutSeconds 120 -- dotnet run --project src/Sky.Sim -c Release --no-build`.

## tools/test-all.ps1

The whole gate, one table and one exit code. It builds `InTheSky.slnx` in Release with `-warnaserror` first and alone, under the gate at 300 s, because every check after it reads those binaries and the tests run with `--no-build`; a red build ends the run there. Then these run side by side as jobs, each under its own ceiling, their output held back and printed whole under its own heading once all have finished:

| Row | Runs | Ceiling |
|---|---|---|
| build | `dotnet build InTheSky.slnx -c Release -warnaserror` (alone, first) | 300 s |
| tests | `dotnet test InTheSky.slnx -c Release --no-build -- --timeout 2m` | 180 s |
| format | `dotnet csharpier check .`, `dotnet format style` and `dotnet format analyzers` with `--verify-no-changes --severity info` | 180 s |
| analysis | the four `sky.ps1 analysis -Check` steps over every uv project under `tools/`; the row reads `no uv projects` when there is none | 120 s |
| provenance | `uv run --project tools/provenance python -m provenance check` | 60 s |

A check that reaches its ceiling is stopped with every process it started and its row reads `timed out after <n> s`, while the others finish. `-Ceiling` (600 s by default) bounds the whole run. A red `timed out` row is a hang: read the check's log under `.tmp/` rather than raise the ceiling.

## CI

`.github/workflows/ci.yml` runs on every push and pull request to `main`, on `ubuntu-24.04` only, with read-only permissions and every action pinned by commit SHA:

- `lint`: actionlint and zizmor (annotations; no SARIF upload, which would need a write permission).
- `build-test`: the Release build with `-warnaserror`, the tests with `-- --timeout 2m`, CSharpier, `dotnet format style` and `dotnet format analyzers`, and `tools/hooks/Test-DocDrift.ps1`. The client builds from the `Godot.NET.Sdk` NuGet package, so CI needs no Godot.
- `analysis`: ruff, ty and pytest over `tools/provenance`, then the provenance check, every `uv run` with `--locked`.

`LuaCSharp` and `LuaCSharp.SourceGenerator` stay at 0.5.7, the version the boundary spike measured: the generator is referenced directly because the runtime package excludes its analyzers from flowing, and an upgrade of either is its own change that re-runs the golden-flight check (R7). `.github/dependabot.yml` opens one grouped pull request a week for each of NuGet, GitHub Actions and `tools/provenance`'s uv lock, holding each release back seven days.

## Provenance

Every asset has an entry in `assets/PROVENANCE.toml` recording its origin, license, author and source (ADR 0009), and `CREDITS.md` is generated from the ledger, never edited by hand. The checker is the uv project `tools/provenance`:

- `uv run --project tools/provenance python -m provenance check` reports every asset without an entry, every invalid entry (a license outside the allowlist, a file that is missing) and a `CREDITS.md` that no longer matches the ledger, one `PROVENANCE: <problem>` line each; exit 1 when it found problems, 2 when the repository could not be read.
- `uv run --project tools/provenance python -m provenance credits` rewrites `CREDITS.md` from the ledger.
- `pwsh ./sky.ps1 provenance` regenerates `CREDITS.md` and then checks; `pwsh ./sky.ps1 provenance -Check` only checks. After adding or changing an asset, add its ledger entry and run `pwsh ./sky.ps1 provenance`, then commit the asset, the entry and `CREDITS.md` together.
- The `provenance` prek hook and the `provenance` row of `tools/test-all.ps1` run the check, so a commit or a gate run fails on an asset the ledger does not cover.

The checker's own code is linted and tested by `pwsh ./sky.ps1 analysis`.

## Hooks

`prek install` once per clone; `pwsh ./sky.ps1 hooks` runs every hook over every file. `prek.toml` runs these on every commit, in this order: the fixers first (they re-stage what they change), then the readers, then the build, then the doc-drift check.

1. The builtin checks: `trailing-whitespace`, `end-of-file-fixer`, `check-merge-conflict`, `detect-private-key`, and `check-added-large-files` at 1024 KB.
2. `dotnet-format-style`: `dotnet format style` at severity info over the staged C# files, re-staging what it changed (`tools/hooks/dotnet-format-wrapper.ps1`).
3. `csharpier`: `dotnet csharpier format` over the staged C# files, re-staging what it changed (`tools/hooks/csharpier-wrapper.ps1`).
4. `psscriptanalyzer`: PSScriptAnalyzer over the staged PowerShell, under `PSScriptAnalyzerSettings.psd1`.
5. `line-length`: 150 characters for C#, PowerShell and Python (`tools/hooks/Test-LineLength.ps1`).
6. `gitleaks`: `gitleaks git --pre-commit --staged --redact`, the secrets scan over the staged changes.
7. `provenance`: the provenance check (above), on every commit whatever it stages.
8. `dotnet-build`: `dotnet build -p:TreatWarningsAsErrors=true`.
9. `doc-drift`: `tools/hooks/Test-DocDrift.ps1`, on every commit: `docs/ARCHITECTURE.md` names every `src/Sky.*` project in backticks, and `docs/TEST_ALMANAC.md` every `tests/Sky.*.Tests` project and every `*Tests` class. `pwsh tools/hooks/Test-DocDrift.ps1 -Update` rewrites the almanac's Counts table from the tree.

One hook runs at the commit-msg stage: `doc-drift-message` (`Test-DocDrift.ps1 -CommitMessage`) refuses a commit that stages anything under `src/Sky.Content/` without staging a file under `docs/design/` or carrying a body line starting `Docs: unchanged, <why>`. `prek install` installs both stages; a clone installed before the commit-msg stage was added runs `prek install` again.

The doc-drift hook reads the committing tree's docs, so work done in a worktree lands on `main` as a patch (`.claude/skills/sky-nextup/SKILL.md`, "Landing") rather than as a worktree commit.

The hook scripts under `tools/hooks/` are pwsh, not bash: prek launched from PowerShell on Windows can resolve a `bash` entry to the WSL stub, which has no dotnet.

Two things prek does not do: it stashes unstaged edits to tracked files but not untracked files, so a commit made while other work is half-done in the same tree builds a mixed tree; and its whitespace hooks `git add` whole files, so a partial commit can pick up unrelated hunks.

## The Godot client

`src/Sky.Client` is a Godot 4.7.2 .NET project listed in `InTheSky.slnx`, so `sky.ps1 build` compiles it; `sky.ps1 client` is what imports it and has Godot build it. It references `Sky.Session` and holds no rules (ADR 0001). `.godot/` is ignored and the `.cs.uid` files beside scripts are tracked. `user://` is the `In the Sky` folder under `%APPDATA%`, and `In the Sky-dev` for editor and headless runs (`use_custom_user_dir` with the `.editor` feature tag in `project.godot`).

Open it in the editor: `F:\Godot\Godot.exe --path src/Sky.Client --editor`, or the Godot at `$env:GODOT_PATH`.

### Scratch scenes

A scratch scene is a Debug-only scene under `src/Sky.Client/Scratch/` that shows one piece of the client in isolation. `Sky.Client.csproj` already compiles `Scratch/**` into the Debug assembly alone, and the folder holds only a `.gitkeep`: the scratch scenes, their base script and a way to run them headless arrive with the M2 client. Once they exist, scratch scenes are tracked, mended or deleted in the commit that breaks them, and anything the client draws or a player touches is looked at in one the moment it is built, driven through the godot MCP server.

### The godot MCP server

The `godot` MCP server is the owner's own ([github.com/leftos/godot-mcp](https://github.com/leftos/godot-mcp), checked out at `D:\godot-mcp`), registered per machine at local scope, never in a tracked `.mcp.json`. It is not needed until M2, when the client has something to drive. To set it up on a machine, run `pwsh run.ps1 install` in the godot-mcp checkout, which publishes the server to `%LOCALAPPDATA%\godot-mcp\godot-mcp.exe` and links its `godot-mcp` skill, then register it from this repo's root:

```
claude mcp add --scope local godot -e GODOT_PATH=F:/Godot/Godot_console.exe -- "C:\Users\<you>\AppData\Local\godot-mcp\godot-mcp.exe"
```

No Node is needed. After pulling `D:\godot-mcp`, stop the Claude sessions using the server and run `install` again; it fails while a session holds the exe.

An agent driving the client loads the `godot-mcp` skill first; every tool's arguments and edges are in `D:\godot-mcp\docs\TOOLS.md`. The server injects its bridge through an `override.cfg` beside `project.godot`, hidden by `.git/info/exclude` and removed by `stop_project` or `detach_project`; nothing is written into `project.godot`, and there is no addon in the repo. Friction with the server is filed upstream with `gh issue create -R leftos/godot-mcp`, one issue a friction.

## Layout

`src/Sky.Engine` references nothing and holds the simulation (ADR 0001); the other seven projects and their allowed references are in [ARCHITECTURE.md](./ARCHITECTURE.md), and every test class is in [TEST_ALMANAC.md](./TEST_ALMANAC.md).
