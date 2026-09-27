using Lua;
using Sky.Engine.Randomness;

namespace Sky.Scripting.Tests;

/// <summary>Proves the Lua host's sandbox, budget, error recovery and random stream against small inline modules.</summary>
public sealed class LuaHostTests
{
    private const string ReturnsFortyTwo = "return { run = function() return 42 end }";

    /// <summary>No name outside the whitelist reaches a module, and the removed library fields are nil in its copies.</summary>
    [Theory]
    [InlineData("io")]
    [InlineData("os")]
    [InlineData("require")]
    [InlineData("load")]
    [InlineData("dofile")]
    [InlineData("debug")]
    [InlineData("math.randomseed")]
    [InlineData("_G")]
    [InlineData("getmetatable")]
    [InlineData("setmetatable")]
    [InlineData("rawget")]
    [InlineData("rawset")]
    [InlineData("print")]
    [InlineData("coroutine")]
    [InlineData("string.dump")]
    public void ForbiddenGlobalsAreNil(string expression)
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        Assert.True(host.LoadModule("probe", $"return {{ probe = function() return {expression} == nil end }}"));

        LuaValue[] results = Call(host, "probe", "probe", 1);

        Assert.True(results[0].Read<bool>());
    }

    /// <summary>The string methods still reach a module through the method syntax.</summary>
    [Fact]
    public void StringMethodSyntaxWorks()
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        Assert.True(host.LoadModule("upper", "return { run = function() return ('x'):upper() end }"));

        Assert.Equal("X", Call(host, "upper", "run", 1)[0].Read<string>());
    }

    /// <summary><c>string.rep</c>, called as a function or as a method, cannot build a string over the cap.</summary>
    [Theory]
    [InlineData("string.rep('x', 1048577)")]
    [InlineData("('x'):rep(1048577)")]
    public void StringRepIsCapped(string expression)
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        Assert.True(host.LoadModule("rep", $"return {{ run = function() return #{expression} end }}"));

        Assert.False(host.TryCall("rep", "run", [], new LuaValue[1]));

        Assert.True(host.TryGetDisabledReason("rep", out string? reason));
        Assert.Contains("over the cap of 1048576", reason, StringComparison.Ordinal);
    }

    /// <summary>A loop the script never leaves, bare or inside <c>pcall</c>, is stopped and disables its module alone.</summary>
    [Fact]
    public void InfiniteLoopIsStoppedByBudgetAndDisablesOnlyThatModule()
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        Assert.True(host.LoadModule("loop", "return { run = function() while true do end end }"));
        Assert.True(host.LoadModule("swallow", "return { run = function() while true do pcall(function() while true do end end) end end }"));
        Assert.True(host.LoadModule("fine", ReturnsFortyTwo));

        Assert.False(host.TryCall("loop", "run", [], new LuaValue[1]));
        Assert.False(host.TryCall("swallow", "run", [], new LuaValue[1]));

        Assert.True(host.IsDisabled("loop"));
        Assert.True(host.IsDisabled("swallow"));
        Assert.True(host.TryGetDisabledReason("loop", out string? reason));
        Assert.Equal("ran past its instruction budget of 1000000", reason);
        Assert.False(host.IsDisabled("fine"));
        Assert.Equal(42, Call(host, "fine", "run", 1)[0].Read<double>());
    }

    /// <summary>A caught <c>error()</c> leaves the value stack at its depth before the call, and the module disabled.</summary>
    [Fact]
    public void StackDepthIsRestoredAfterCaughtError()
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        Assert.True(host.LoadModule("thrower", "return { run = function(a, b) local x, y, z = a, b, a + b; error('failed on purpose') end }"));
        Assert.True(host.LoadModule("fine", ReturnsFortyTwo));
        int depthBefore = host.StackDepth;

        Assert.False(host.TryCall("thrower", "run", [1.0, 2.0], new LuaValue[1]));

        Assert.Equal(depthBefore, host.StackDepth);
        Assert.True(host.IsDisabled("thrower"));
        Assert.Equal(42, Call(host, "fine", "run", 1)[0].Read<double>());
        Assert.Equal(depthBefore, host.StackDepth);
    }

    /// <summary>A string error's reason is its text, without the library's prefix; any other error value gets a fixed phrase.</summary>
    [Theory]
    [InlineData("error('failed on purpose')", "failed on purpose")]
    [InlineData("error({})", "error with a non-string value")]
    public void ErrorReasonComesFromTheErrorValue(string statement, string expected)
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        Assert.True(host.LoadModule("thrower", $"return {{ run = function() {statement} end }}"));

        Assert.False(host.TryCall("thrower", "run", [], new LuaValue[1]));

        Assert.True(host.TryGetDisabledReason("thrower", out string? reason));
        Assert.EndsWith(expected, reason, StringComparison.Ordinal);
        Assert.DoesNotContain("Lua-CSharp", reason, StringComparison.Ordinal);
    }

    /// <summary>A .NET exception from a host function escapes the call and leaves the module enabled.</summary>
    [Fact]
    public void HostExceptionEscapesTryCall()
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        Assert.True(host.LoadModule("caller", "return { run = function(f) return f() end }"));
        int depthBefore = host.StackDepth;
        LuaFunction broken = new("broken", (_, _) => throw new InvalidOperationException("host bug"));

        Exception? escaped = Record.Exception(() => host.TryCall("caller", "run", [broken], new LuaValue[1]));

        Assert.NotNull(escaped);
        Assert.IsType<InvalidOperationException>(escaped as InvalidOperationException ?? escaped.InnerException);
        Assert.False(host.IsDisabled("caller"));
        Assert.Equal(depthBefore, host.StackDepth);
    }

    /// <summary>
    /// A host function that calls back into the host while a Lua call runs is refused, since the call's stack slice and
    /// budget hook serve one call at a time; the refusal escapes as a host bug and disables neither module.
    /// </summary>
    [Fact]
    public void RunReEnteredThrows()
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        Assert.True(host.LoadModule("outer", "return { run = function(f) return f() end }"));
        Assert.True(host.LoadModule("inner", "return { run = function() return 1 end }"));
        int depthBefore = host.StackDepth;
        LuaFunction reenter = new("reenter", (context, _) => new(context.Return(host.TryCall("inner", "run", [], new LuaValue[1]))));

        Exception? escaped = Record.Exception(() => host.TryCall("outer", "run", [reenter], new LuaValue[1]));

        Assert.NotNull(escaped);
        InvalidOperationException? refused = escaped as InvalidOperationException ?? escaped.InnerException as InvalidOperationException;
        Assert.NotNull(refused);
        Assert.StartsWith("LuaHost.Run re-entered", refused.Message, StringComparison.Ordinal);
        Assert.False(host.IsDisabled("outer"));
        Assert.False(host.IsDisabled("inner"));
        Assert.Equal(depthBefore, host.StackDepth);
        Assert.Equal(1.0, Call(host, "inner", "run", 1)[0].Read<double>());
    }

    /// <summary>A top-level error, a return that is not a table and a parse error each disable the module at load.</summary>
    [Theory]
    [InlineData("error('boom at load')", "boom at load")]
    [InlineData("return 5", "the module returned number, not a table")]
    [InlineData("return {", "")]
    public void BadModuleIsDisabledAtLoad(string source, string expected)
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);

        Assert.False(host.LoadModule("bad", source));

        Assert.True(host.TryGetDisabledReason("bad", out string? reason));
        Assert.NotEmpty(reason);
        Assert.Contains(expected, reason, StringComparison.Ordinal);
    }

    /// <summary>Calling a disabled module returns false and does not throw.</summary>
    [Fact]
    public void TryCallOnDisabledModuleReturnsFalse()
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        Assert.False(host.LoadModule("bad", "error('boom')"));

        Assert.False(host.TryCall("bad", "run", [], new LuaValue[1]));
    }

    /// <summary>Naming a field that is not a function disables the module with that reason.</summary>
    [Fact]
    public void TryCallOnNonFunctionFieldDisablesTheModule()
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        Assert.True(host.LoadModule("data", "return { value = 5 }"));

        Assert.False(host.TryCall("data", "value", [], new LuaValue[1]));

        Assert.True(host.TryGetDisabledReason("data", out string? reason));
        Assert.Equal("'value' is number, not a function", reason);
    }

    /// <summary>A module id the host never loaded is a caller bug, reported by every query.</summary>
    [Fact]
    public void UnknownModuleIdThrows()
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);

        Assert.Throws<ArgumentException>(() => host.IsDisabled("missing"));
        Assert.Throws<ArgumentException>(() => host.TryGetDisabledReason("missing", out _));
        Assert.Throws<ArgumentException>(() => host.HasFunction("missing", "run"));
        Assert.Throws<ArgumentException>(() => host.TryCall("missing", "run", [], new LuaValue[1]));
    }

    /// <summary>Unbounded recursion disables its module; modules loaded before it are reloaded and one loaded after still loads.</summary>
    [Fact]
    public void UnboundedRecursionDisablesModuleAndNextModuleStillLoads()
    {
        using LuaHost host = new(100 * LuaHost.StandardInstructionBudget);
        Assert.True(host.LoadModule("before", ReturnsFortyTwo));
        Assert.True(host.LoadModule("recurse", "local function f() return f() + 1 end return { run = function() return f() end }"));

        Assert.False(host.TryCall("recurse", "run", [], new LuaValue[1]));

        Assert.True(host.TryGetDisabledReason("recurse", out string? reason));
        Assert.Equal("stack overflow", reason);
        Assert.False(host.IsDisabled("before"));
        Assert.Equal(42, Call(host, "before", "run", 1)[0].Read<double>());
        Assert.True(host.LoadModule("after", ReturnsFortyTwo));
        Assert.Equal(42, Call(host, "after", "run", 1)[0].Read<double>());
    }

    /// <summary>After an overflow the state is rebuilt from source, so a module-level counter starts over.</summary>
    [Fact]
    public void RebuildRestartsModuleLevelState()
    {
        using LuaHost host = new(100 * LuaHost.StandardInstructionBudget);
        Assert.True(host.LoadModule("counter", "local n = 0 return { next = function() n = n + 1 return n end }"));
        Assert.True(host.LoadModule("recurse", "local function f() return f() + 1 end return { run = function() return f() end }"));
        Call(host, "counter", "next", 1);
        Assert.Equal(2, Call(host, "counter", "next", 1)[0].Read<double>());

        Assert.False(host.TryCall("recurse", "run", [], new LuaValue[1]));

        Assert.Equal(1, Call(host, "counter", "next", 1)[0].Read<double>());
    }

    /// <summary>
    /// <c>math.random()</c> is the set stream's <see cref="SimRandom.NextDouble"/>, <c>math.random(n)</c> is
    /// <c>1 + NextInt(n)</c> and <c>math.random(m, n)</c> is <c>m + NextInt(n - m + 1)</c>, draw for draw.
    /// </summary>
    [Fact]
    public void MathRandomDrawsFromTheSetStream()
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        host.SetRandom(new RngRoot(7).Stream("passenger/0"));
        Assert.True(
            host.LoadModule("dice", "return { roll = function() return math.random(), math.random(), math.random(10), math.random(3, 8) end }")
        );
        SimRandom expected = new RngRoot(7).Stream("passenger/0");

        LuaValue[] results = Call(host, "dice", "roll", 4);

        Assert.Equal(expected.NextDouble(), results[0].Read<double>());
        Assert.Equal(expected.NextDouble(), results[1].Read<double>());
        Assert.Equal(1 + expected.NextInt(10), results[2].Read<double>());
        Assert.Equal(3 + expected.NextInt(6), results[3].Read<double>());
    }

    /// <summary>A bound that is NaN, infinite or fractional raises a Lua error, which disables the module.</summary>
    [Theory]
    [InlineData("0/0")]
    [InlineData("1/0")]
    [InlineData("2.5")]
    public void MathRandomRejectsNonIntegerBounds(string bound)
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        host.SetRandom(new RngRoot(7).Stream("passenger/0"));
        Assert.True(host.LoadModule("dice", $"return {{ roll = function() return math.random({bound}) end }}"));

        Assert.False(host.TryCall("dice", "roll", [], new LuaValue[1]));

        Assert.True(host.TryGetDisabledReason("dice", out string? reason));
        Assert.Contains("number has no integer representation", reason, StringComparison.Ordinal);
    }

    /// <summary>A module that draws from <c>math.random</c> while it loads is disabled, stream or no stream.</summary>
    [Fact]
    public void MathRandomIsUnavailableWhileAModuleLoads()
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        host.SetRandom(new RngRoot(7).Stream("passenger/0"));

        Assert.False(host.LoadModule("eager", "local roll = math.random() return { run = function() return roll end }"));

        Assert.True(host.TryGetDisabledReason("eager", out string? reason));
        Assert.EndsWith("math.random is not available while a module loads", reason, StringComparison.Ordinal);
    }

    /// <summary>A global set, or a library field added, by one module is nil in another.</summary>
    [Fact]
    public void ModulesDoNotShareGlobals()
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        Assert.True(host.LoadModule("writer", "shared = 5 math.extra = 1 return { read = function() return shared, math.extra end }"));
        Assert.True(host.LoadModule("reader", "return { read = function() return shared, math.extra end }"));

        LuaValue[] written = Call(host, "writer", "read", 2);
        LuaValue[] read = Call(host, "reader", "read", 2);

        Assert.Equal(5, written[0].Read<double>());
        Assert.Equal(1, written[1].Read<double>());
        Assert.Equal(LuaValueType.Nil, read[0].Type);
        Assert.Equal(LuaValueType.Nil, read[1].Type);
    }

    private static LuaValue[] Call(LuaHost host, string moduleId, string functionName, int resultCount)
    {
        var results = new LuaValue[resultCount];
        Assert.True(host.TryCall(moduleId, functionName, [], results));
        return results;
    }
}
