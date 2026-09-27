
<!-- errors run 2026-09-27T03:43:01.6096343Z -->
| Runtime | Case | Error caught per call | Exception type | Message | Next decision OK | Note |
|---|---|---|---|---|---|---|
| luacs | throw in utility | yes | Lua.LuaRuntimeException | Lua-CSharp: [string "err_throw"]:4: utility failed on purpose at r=55 | yes | ; stack 0 -> 5 |
| luacs | adapter callback throws | yes | Lua.LuaRuntimeException | adapter callback failed on purpose at r=55 (inner System.InvalidOperationException: adapter callback failed on purpose at r=55) | yes | ; stack 5 -> 9 |
| luacs | recursion 10,000 deep | no error raised |  |  | yes | no error; returned 10000; stack 9 -> 9 |
| moon | throw in utility | yes | MoonSharp.Interpreter.ScriptRuntimeException | utility failed on purpose at r=55 | yes |  |
| moon | adapter callback throws | yes | System.InvalidOperationException | adapter callback failed on purpose at r=55 | yes |  |
| moon | recursion 10,000 deep | no error raised |  |  | yes | no error; returned 10000 |
| luacs | unbounded recursion (child process) | see note | |  | | exit code 0; stdout: caught=True type=Lua.LuaRuntimeException message=stack overflow (inner Lua.LuaStackOverflowException: stack overflow) next-decision-ok=False ; stack 0 -> 524288; next decision threw Lua.LuaStackOverfl... |
| moon | unbounded recursion (child process) | see note | |  | | exit code 0; stdout: caught=True type=System.IndexOutOfRangeException message=Index was outside the bounds of the array. next-decision-ok=False ; next decision threw System.IndexOutOfRangeException: Index was outside the ... |
