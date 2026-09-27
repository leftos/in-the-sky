# Lua boundary and tick spike: results (2026-09-26)

Every number below was measured on the machine in the next section, on 2026-09-26 (local time), from the program in this folder. The raw outputs are in `results/`: `flight.md`, `determinism.md` and `errors.md` as the program wrote them, and BenchmarkDotNet's reports and log in `spikes/LuaBoundary/results/bdn/` on the `spike/lua-boundary` branch, with the program itself.

## Machine

| | |
|---|---|
| CPU | AMD Ryzen 7 7800X3D, 8 physical cores, 16 logical |
| Memory | 63 GB |
| OS | Windows 11 Pro 10.0.26200 (25H2) |
| .NET | SDK 10.0.401, runtime 10.0.12, x64 RyuJIT (x86-64-v4) |
| LuaCSharp | 0.5.7 (the newest version on NuGet) |
| MoonSharp | 3.0.0-beta.1 (the newest version on NuGet, a prerelease; the last stable is 2.0.0) |
| BenchmarkDotNet | 0.15.8 (the latest stable) |
| Godot | `F:\Godot\Godot_console.exe`, client built with `Godot.NET.Sdk/4.7.2` |

## What was run

The model is the brief's stand-in: 200 passengers with five needs (0 to 100) and two traits (`nervous`, `thirsty`), 10 activities with duration ranges and utility functions (needs, traits, a 0.15 "keep current" inertia, and a `math.random() * 0.2` jitter), and a per-tick need update whose rate multiplier follows the concept's rule (same class multiplies, classes add around 1, clamped to [0.2, 2.5]). The constants are in `Model.cs` and `scripts/model.lua`, written in the same operation order. A flight is 2.5 sim hours; at 250 ms that is 36,000 ticks. Passenger streams are `new Random(root.Next())` in passenger order from `new Random(seed)`; the Lua `math.random` is a host function drawing from the stream of the passenger being scored.

- **Interop.** Lua-CSharp gets a `[LuaObject]` source-generated adapter (`LuaPassenger`), called through the stack API (`Push`, `CallAsync(funcIndex, returnBase)`, `ReadStack`). MoonSharp gets userdata with a hand-written `IUserDataDescriptor` (a switch on the member name, no reflection), called with `Script.Call(fn, DynValue[])` and a reused argument array.
- **Sandbox.** Every chunk is loaded into its own environment table holding `assert`, `error`, `ipairs`, `next`, `pairs`, `pcall`, `select`, `tonumber`, `tostring`, `type`, `xpcall`, and copies of `string`, `table` and `math` (with `random` replaced and `randomseed` removed). No script sees the global table; no `io`, `os`, `require`, `load`, `loadstring`, `dofile` or `debug`. MoonSharp's script is created with `CoreModules.Preset_HardSandbox`.
- **Per-tick Lua** is `on_tick(p, dt)`: 13 adapter accesses per call (read activity and both traits, read and write five needs), 200 calls per tick.
- **Budgets.** Lua-CSharp: `SetHook(hook, "", 1000)`, a hook that counts fires and throws after 1,000 fires (1,000,000 instructions) in one call; it never fired past the limit. MoonSharp: a new coroutine per call with `AutoYieldCounter = 1000`, resumed until it is no longer force-suspended, aborting after 1,000 forced yields; never reached.
- **Decisions are rare in this model**: 2,641 decisions a flight for seeds 1 to 3 (about 13 per passenger over 2.5 hours), against 7,200,000 passenger-ticks.

Commands, all from the worktree root: `dotnet run -c Release --project spikes/LuaBoundary -- flight|determinism|errors|micro`. `flight` and `determinism` take an optional list of variants. The environment variable `LUABOUNDARY_MAX_TICKS` caps the ticks per flight; it was used for `moon-tick-budget` alone (see the flight notes).

## Mode `flight`

Per variant: one warm-up flight (seed 0), then seeds 1 to 3. Ticks/s is total ticks ÷ total wall seconds of the three flights. Target is 64x real time (256 ticks/s at 250 ms, 128 at 500 ms, 64 at 1000 ms), and headroom is ticks/s ÷ target. Bytes/tick is `GC.GetAllocatedBytesForCurrentThread` over the flight ÷ ticks. The Gen columns are collections summed over the three flights.

| Variant | Tick ms | Flight 1 s | Flight 2 s | Flight 3 s | Ticks/s | Target | Headroom | Bytes/tick | Gen0 | Gen1 | Gen2 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1. C# baseline | 250 | 0.21 | 0.22 | 0.20 | 169,853 | 256 | 663.49x | 0.0 | 0 | 0 | 0 |
| 2. LuaCSharp, decision-only | 250 | 0.23 | 0.22 | 0.24 | 156,083 | 256 | 609.70x | 0.0 | 0 | 0 | 0 |
| 3. LuaCSharp, per-tick | 250 | 35.78 | 34.67 | 39.77 | 980 | 256 | 3.83x | 0.0 | 0 | 0 | 0 |
| 4. MoonSharp, decision-only | 250 | 0.36 | 0.28 | 0.29 | 116,051 | 256 | 453.32x | 2,297.4 | 3 | 0 | 0 |
| 5. MoonSharp, per-tick | 250 | 126.10 | 139.73 | 131.53 | 272 | 256 | 1.06x | 7,694,468.0 | 16,557 | 109 | 3 |
| 6a. LuaCSharp, decision-only, count hook | 250 | 0.28 | 0.27 | 0.26 | 134,275 | 256 | 524.51x | 0.0 | 0 | 0 | 0 |
| 6b. LuaCSharp, per-tick, count hook | 250 | 40.54 | 39.66 | 38.59 | 909 | 256 | 3.55x | 0.0 | 0 | 0 | 0 |
| 6c. MoonSharp, decision-only, AutoYieldCounter | 250 | 0.49 | 0.71 | 0.59 | 60,203 | 256 | 235.17x | 156,233.8 | 805 | 802 | 802 |
| 6d. MoonSharp, per-tick, AutoYieldCounter (first 200 ticks) | 250 | 4.41 | 5.32 | 6.26 | 38 | 256 | 0.15x | 428,766,812.9 | 8,158 | 8,071 | 8,071 |
| 6d. MoonSharp, per-tick, AutoYieldCounter (first 200 ticks) | 500 | 4.23 | 5.24 | 4.46 | 43 | 128 | 0.34x | 428,765,554.9 | 7,886 | 7,799 | 7,799 |
| 6d. MoonSharp, per-tick, AutoYieldCounter (first 200 ticks) | 1000 | 4.44 | 5.22 | 5.34 | 40 | 64 | 0.62x | 428,884,778.5 | 7,605 | 7,518 | 7,518 |

Notes:
- Only 6d is under 256 ticks/s at 250 ms, so it is the only variant repeated at 500 and 1000 ms. It misses 64x at every tick size measured.
- 6d ran the first 200 ticks of each flight, not a whole flight. The full run was stopped after its warm-up flight had not finished in over 10 minutes (at 38 ticks/s a 36,000-tick flight takes about 16 minutes, so four flights at three tick sizes would take hours). Its flight columns are seconds for 200 ticks.
- Variant 5 passes at 250 ms with 1.06x headroom, allocates about 7.7 MB per tick (about 38 KB per `on_tick` call), and ran 16,557 Gen0 collections in three flights.
- 6c and 6d allocate about 2.1 MB per coroutine (see the microbenchmarks), which is what the Gen2 counts reflect.

## Mode `determinism`

Seed 42 at 250 ms, run twice in-process and once in a fresh process (the program re-invokes itself). The hash is SHA-256 over every need's IEEE bits, every current activity and the tick count. Full flights: 36,000 ticks and 2,705 decisions; end activities for the baseline were sit 38, sleep 52, drink 48, eat 1, lav 10, read 1, ife 37, walk 0, talk 10, call 3. C# baseline hash: `AE6C1F31685AB13C90D4F2DC1EC15BC83ECACA9F060F20ADC52B512376B86C24`.

| Variant | Run 1 | Run 2 | Fresh process | Three match | Equals C# baseline |
|---|---|---|---|---|---|
| 1. C# baseline | AE6C1F31685AB13C | AE6C1F31685AB13C | AE6C1F31685AB13C | yes | yes |
| 2. LuaCSharp, decision-only | AE6C1F31685AB13C | AE6C1F31685AB13C | AE6C1F31685AB13C | yes | yes |
| 3. LuaCSharp, per-tick | AE6C1F31685AB13C | AE6C1F31685AB13C | AE6C1F31685AB13C | yes | yes |
| 4. MoonSharp, decision-only | AE6C1F31685AB13C | AE6C1F31685AB13C | AE6C1F31685AB13C | yes | yes |
| 5. MoonSharp, per-tick | AE6C1F31685AB13C | AE6C1F31685AB13C | AE6C1F31685AB13C | yes | yes |
| 6a. LuaCSharp, decision-only, count hook | AE6C1F31685AB13C | AE6C1F31685AB13C | AE6C1F31685AB13C | yes | yes |
| 6b. LuaCSharp, per-tick, count hook | AE6C1F31685AB13C | AE6C1F31685AB13C | AE6C1F31685AB13C | yes | yes |
| 6c. MoonSharp, decision-only, AutoYieldCounter | AE6C1F31685AB13C | AE6C1F31685AB13C | AE6C1F31685AB13C | yes | yes |
| 6d. MoonSharp, per-tick, AutoYieldCounter (first 200 ticks) | 24F3C106A7E53A95 | 24F3C106A7E53A95 | 24F3C106A7E53A95 | yes | yes, against the baseline's first 200 ticks (`24F3C106A7E53A952AB6A86EBF721E31F40D2F8B86212BEE8A983B93EBD47483`) |

No Lua variant diverged from the baseline, so no divergence tick was searched for (the program steps both flights in lockstep only when the hashes differ). The model's scripts use only `+ - * /`, comparisons and the host `math.random`; no `math.sin`, `math.exp` or other transcendental was exercised.

**`pairs` order.** `scripts/pairs_order.lua` inserts 14 string keys in a scrambled order, then `1000`, `7`, `3.5`, removes `k3`, adds `k99`, `1`, `2`, and concatenates the keys in `pairs` order. Each runtime was run in two fresh states in-process and once in a fresh process.

| Runtime | `pairs` order | Second run same | Fresh process same |
|---|---|---|---|
| LuaCSharp | `1,2,7,k17,k42,k8,k1,k29,k11,k5,zeta,alpha,mid,k100,k64,k2,1000,3.5,k99` | yes | yes |
| MoonSharp | `k17,k42,k8,k1,k29,k11,k5,zeta,alpha,mid,k100,k64,k2,1000,7,3.5,k99,1,2` | yes | yes |

Both orders are stable on this machine. They differ between the runtimes: Lua-CSharp lists `1`, `2` and `7` first (its array part), and MoonSharp follows insertion order throughout. The Linux-CI check is unmeasured: everything here ran on Windows only.

## Mode `errors`

One state per runtime holds the model module and the error scripts, each in its own sandbox. After each case, the same state scores one decision for a fixed passenger (seeded stream 7), and the result is compared with the C# model's decision from the same inputs. Unbounded recursion runs in a child process so that a crash could not end the harness.

| Runtime | Case | Caught per call | Exception type at the host | State usable for the next decision | Note |
|---|---|---|---|---|---|
| LuaCSharp | `error()` in a utility function | yes | `Lua.LuaRuntimeException` ("[string "err_throw"]:4: utility failed on purpose at r=55") | yes, matches C# | `State.Stack.Count` went 0 → 5: the failed call's slots stay on the stack |
| LuaCSharp | adapter getter throws `InvalidOperationException` | yes | `Lua.LuaRuntimeException`, inner `System.InvalidOperationException` | yes, matches C# | stack 5 → 9 |
| LuaCSharp | recursion 10,000 deep | no error raised; returned 10000 | none | yes, matches C# | stack 9 → 9 |
| LuaCSharp | unbounded recursion | yes | `Lua.LuaRuntimeException` ("stack overflow"), inner `Lua.LuaStackOverflowException` | **no**: the next decision throws `LuaStackOverflowException` | stack 0 → 524,288 after the overflow |
| MoonSharp | `error()` in a utility function | yes | `MoonSharp.Interpreter.ScriptRuntimeException` ("utility failed on purpose at r=55") | yes, matches C# | |
| MoonSharp | adapter getter throws `InvalidOperationException` | yes | `System.InvalidOperationException`, unwrapped | yes, matches C# | |
| MoonSharp | recursion 10,000 deep | no error raised; returned 10000 | none | yes, matches C# | |
| MoonSharp | unbounded recursion | yes | `System.IndexOutOfRangeException` (from `FastStack.Push`) | **no**: the next decision throws `IndexOutOfRangeException` | |

Whether a host can reset either state after an overflow (for example by truncating Lua-CSharp's stack) is unmeasured.

## Microbenchmarks (`-- micro`)

BenchmarkDotNet 0.15.8, `[MemoryDiagnoser]`, in-process emit toolchain (`[InProcess]`), default run strategy. One call of the drink utility (activity 3) with the passenger adapter, and one `on_tick` call. Raw summary: `spikes/LuaBoundary/results/bdn/results/LuaBoundary.CallBenchmarks-report-github.md` on the `spike/lua-boundary` branch.

| Method | Mean | StdDev | Allocated per call |
|---|---|---|---|
| C# utility | 4.166 ns | 0.587 ns | 0 B |
| LuaCSharp utility, stack API | 619.473 ns | 47.971 ns | 0 B |
| LuaCSharp utility, `CallAsync(fn, LuaValue[])` array API | 626.001 ns | 39.899 ns | 48 B |
| LuaCSharp utility, stack API, count hook | 667.481 ns | 19.117 ns | 0 B |
| MoonSharp utility | 859.163 ns | 64.297 ns | 2,112 B |
| MoonSharp utility, coroutine with AutoYieldCounter | 93,041.543 ns | 11,131.696 ns | 2,100,039 B |
| C# on_tick | 34.422 ns | 3.533 ns | 0 B |
| LuaCSharp on_tick | 5,748.925 ns | 427.816 ns | 0 B |
| MoonSharp on_tick | 17,262.137 ns | 1,489.656 ns | 34,288 B |

Lua-CSharp's `ValueTask<int> CallAsync(funcIndex, returnBase)` completed synchronously on every call and allocated nothing (0 B). The array overload's 48 B is its result array.

## Trimming

- **Analyzer build** (`dotnet build -c Release -p:IsTrimmable=true -p:EnableTrimAnalyzer=true -p:EnableAotAnalyzer=true -p:EnableSingleFileAnalyzer=true`): 0 warnings. These analyzers look only at the spike's own code, and neither runtime annotates its API with `RequiresUnreferencedCode`/`RequiresDynamicCode`, so nothing surfaces at the call sites.
- **Trimmed publish** (`dotnet publish -c Release -r win-x64 -p:PublishTrimmed=true -p:TrimmerSingleWarn=false`), IL2xxx warnings by origin: 144 unique in total.
- **NativeAOT publish** (`-p:PublishAot=true -p:IlcSingleWarn=false`), IL3xxx warnings by origin.

| Origin | IL2xxx (trimmed publish) | IL3xxx (NativeAOT publish) |
|---|---|---|
| Lua-CSharp (`Lua.*`) | 0 | 0 |
| MoonSharp | 19: IL2075 ×10, IL2067 ×3, IL2055 ×2, IL2072 ×2, IL2026 ×1, IL2060 ×1 | 4: IL3050 ×4 |
| BenchmarkDotNet and its dependencies (CommandLine, Microsoft.Diagnostics/TraceEvent, Roslyn, System.Management, Gee.External.Capstone, the SimpleJson copy inside BenchmarkDotNet) | 125 | 42 |

The NativeAOT binary was also run: Lua-CSharp ran `pairs_order.lua` and printed the same order as under the JIT. MoonSharp failed with `ScriptRuntimeException: attempt to call a nil value` on the same script. The first AOT publish failed at the link step because the ILCompiler's `findvcvarsall` could not find `vswhere.exe` on PATH. With `C:\Program Files (x86)\Microsoft Visual Studio\Installer` on PATH it linked (that folder has since been added to the user PATH on this machine). A trimmed build of `Sky.Scripting` itself was not measured.

## Godot load check

`src/Sky.Client/Scratch/LuaLoadCheck.cs` is set as an autoload in `project.godot` on this branch, and `Sky.Client.csproj` references `LuaCSharp` and `MoonSharp` directly. At `_Ready` it runs `return function(p) return p.r / 100 * 0.9 + p.thirsty * 0.4 end`: on Lua-CSharp with a `[LuaObject]` adapter declared in the client (so the source generator ran in the Godot SDK build), and on MoonSharp with a table. It prints both results and quits. Sequence: `dotnet build src/Sky.Client -warnaserror` (Debug, clean), `Godot_console.exe --headless --path src/Sky.Client --import --quit` (exit 0), `--build-solutions --quit` (exit 0), `--headless --path src/Sky.Client --quit-after 5` (exit 0).

| Runtime | Loaded | Returned | Expected |
|---|---|---|---|
| LuaCSharp | yes | 0.615 | 0.615 (matches) |
| MoonSharp | yes | 0.615 | 0.615 (matches) |

This was the editor binary run headless, in Debug; an exported Windows build is unmeasured.

## What the data says

- **64x budget:** at 250 ms every decision-only variant clears 256 ticks/s by more than 450x (C# 169,853, Lua-CSharp 156,083, MoonSharp 116,051 ticks/s). Per-tick Lua-CSharp runs at 980 ticks/s (3.83x) and per-tick MoonSharp at 272 ticks/s (1.06x).
- **Cost of one C#-to-Lua call:** a utility call with the passenger adapter costs 619 ns and 0 B on Lua-CSharp (`[LuaObject]`, stack API), against 859 ns and 2,112 B on MoonSharp (hand-written descriptor) and 4.2 ns in C#. An `on_tick` costs 5.7 µs and 0 B against 17.3 µs and 34,288 B.
- **Lua-CSharp `ValueTask` sync path:** the stack-based `CallAsync` completed synchronously and allocated 0 B per call in a tight host loop; the array overload allocates 48 B for its result array.
- **Instruction budget:** Lua-CSharp's count hook at `count = 1000` added about 8% to a utility call (667 against 619 ns) and cut per-tick throughput from 980 to 909 ticks/s. MoonSharp's AutoYieldCounter with a coroutine per call costs 93 µs and 2.1 MB per call, which drops per-tick MoonSharp to 38 ticks/s (0.15x) but leaves decision-only at 235x. A mixed setup (budget at decision points only, per-tick path unbudgeted) was not run as a variant of its own and is unmeasured.
- **Byte-identical runs:** with `math.random` replaced, seed 42 gave the same end-state hash twice in-process and once in a fresh process on all nine variants, and every Lua variant equalled the C# baseline bit for bit. This is Windows only: the Linux-CI check is unmeasured, transcendental functions were not exercised, and journals as such were not built (the hash covers needs, activities and the tick count).
- **`pairs` order:** stable across two in-process runs and a fresh process on both runtimes on Windows. The two runtimes order the same table differently (Lua-CSharp puts integer keys 1, 2 and 7 first). Windows against Linux is unmeasured.
- **Errors:** a script `error()` and a throwing adapter callback are caught per call on both runtimes, and the next decision on the same state matches C#. Lua-CSharp leaves the failed call's stack slots behind (0 → 5 → 9), and MoonSharp passes the adapter's own exception type through unwrapped. 10,000-deep recursion completes on both. Unbounded recursion is caught on both (`LuaRuntimeException` wrapping `LuaStackOverflowException`; a raw `IndexOutOfRangeException` on MoonSharp), but it leaves both states unable to score the next decision.
- **Godot and trimming:** both runtimes load and return the expected value inside a headless Godot 4.7.2 run of `Sky.Client` (Debug); the exported Windows build is unmeasured. Under trimming, Lua-CSharp raises 0 IL2xxx and 0 IL3xxx warnings and runs as NativeAOT, while MoonSharp raises 19 IL2xxx and 4 IL3050 and fails its script under NativeAOT. A trim-analysed build of `Sky.Scripting` itself is unmeasured.
- **Lua 5.2 double-only arithmetic:** the model's double arithmetic matched C# bit for bit on both runtimes. Whether the content design needs integers or bitwise operators was not exercised and is unmeasured.
- **KeraLua:** not run. Both managed runtimes met the budget in both modes at 250 ms without an instruction budget, so the brief's condition for looking at it did not arise; its gains and costs are unmeasured.
