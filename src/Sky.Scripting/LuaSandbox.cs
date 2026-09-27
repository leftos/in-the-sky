using System.Globalization;
using Lua;
using Lua.Standard;
using Sky.Engine.Randomness;

namespace Sky.Scripting;

/// <summary>
/// Builds the whitelist environment each module runs in, the capped <c>string.rep</c>, and the <c>math.random</c> that
/// draws from the host's <see cref="SimRandom"/> in place of the runtime's own generator.
/// </summary>
internal static class LuaSandbox
{
    /// <summary>The longest string <c>string.rep</c> may build, in characters.</summary>
    internal const int MaxRepLength = 1_048_576;

    /// <summary>The base functions a module sees, shared with the state's global table.</summary>
    internal static readonly string[] BaseFunctions =
    [
        "assert",
        "error",
        "ipairs",
        "next",
        "pairs",
        "pcall",
        "select",
        "tonumber",
        "tostring",
        "type",
        "xpcall",
    ];

    /// <summary>The libraries a module sees, each as its own shallow copy so no module can change another's.</summary>
    internal static readonly string[] CopiedLibraries = ["string", "table", "math"];

    private static readonly LuaFunction CappedRep = new("rep", Rep);

    /// <summary>
    /// Creates a state holding the basic, string, table and math libraries, with <c>string.rep</c> capped and
    /// <c>string.dump</c> removed in the global string table, which the method syntax <c>("x"):rep(n)</c> also reads.
    /// </summary>
    /// <returns>A new state that no module has run in yet.</returns>
    internal static LuaState CreateState()
    {
        var state = LuaState.Create();
        state.OpenBasicLibrary();
        state.OpenStringLibrary();
        state.OpenTableLibrary();
        state.OpenMathLibrary();
        LuaTable strings = state.Environment["string"].Read<LuaTable>();
        strings["rep"] = CappedRep;
        strings["dump"] = LuaValue.Nil;
        return state;
    }

    /// <summary>
    /// Builds one module's environment from the whitelist, with <c>math.random</c> replaced and <c>math.randomseed</c>
    /// removed.
    /// </summary>
    /// <param name="state">The state whose global table the whitelisted values are taken from.</param>
    /// <param name="random">The host function that stands in for <c>math.random</c>.</param>
    /// <returns>A table holding only the whitelisted names.</returns>
    internal static LuaTable CreateEnvironment(LuaState state, LuaFunction random)
    {
        LuaTable globals = state.Environment;
        LuaTable environment = new();
        foreach (string name in BaseFunctions)
        {
            environment[name] = globals[name];
        }

        foreach (string name in CopiedLibraries)
        {
            environment[name] = Copy(globals[name].Read<LuaTable>());
        }

        LuaTable math = environment["math"].Read<LuaTable>();
        math["random"] = random;
        math["randomseed"] = LuaValue.Nil;
        return environment;
    }

    /// <summary>
    /// Creates the <c>math.random</c> replacement, drawing from the stream <paramref name="currentStream"/> returns at
    /// call time.
    /// </summary>
    /// <param name="currentStream">
    /// Returns the stream to draw from for the calling state, or throws a <see cref="LuaRuntimeException"/> when no draw
    /// is allowed.
    /// </param>
    /// <returns>
    /// A function with Lua 5.2's shapes: <c>random()</c> is <see cref="SimRandom.NextDouble"/>, <c>random(m)</c> is
    /// <c>1 + NextInt(m)</c>, and <c>random(m, n)</c> is <c>m + NextInt(n - m + 1)</c>; a bound that is not an integer
    /// raises a Lua error.
    /// </returns>
    internal static LuaFunction CreateRandom(Func<LuaState, SimRandom> currentStream) =>
        new("random", (context, _) => new(context.Return(Draw(context, currentStream(context.State)))));

    private static double Draw(LuaFunctionExecutionContext context, SimRandom stream) =>
        context.ArgumentCount switch
        {
            0 => stream.NextDouble(),
            1 => DrawInRange(context, 1, ReadBound(context, 0), stream),
            2 => DrawInRange(context, ReadBound(context, 0), ReadBound(context, 1), stream),
            _ => throw new LuaRuntimeException(context.State, "wrong number of arguments to 'random'"),
        };

    private static double DrawInRange(LuaFunctionExecutionContext context, double low, double high, SimRandom stream)
    {
        if (low > high)
        {
            throw new LuaRuntimeException(context.State, "bad argument to 'random' (interval is empty)");
        }

        double width = high - low + 1;
        if (width > int.MaxValue)
        {
            throw new LuaRuntimeException(context.State, "bad argument to 'random' (interval is too large)");
        }

        return low + stream.NextInt((int)width);
    }

    private static double ReadBound(LuaFunctionExecutionContext context, int index)
    {
        double bound = context.GetArgument<double>(index);
        if (!double.IsFinite(bound) || Math.Floor(bound) != bound)
        {
            string message = string.Create(
                CultureInfo.InvariantCulture,
                $"bad argument #{index + 1} to 'random' (number has no integer representation)"
            );
            throw new LuaRuntimeException(context.State, message);
        }

        return bound;
    }

    private static ValueTask<int> Rep(LuaFunctionExecutionContext context, CancellationToken cancellationToken)
    {
        string text = context.GetArgument<string>(0);
        double count = context.GetArgument<double>(1);
        string separator = context.HasArgument(2) ? context.GetArgument<string>(2) : string.Empty;
        LuaRuntimeException.ThrowBadArgumentIfNumberIsNotInteger(context.State, 2, count);
        if (count <= 0)
        {
            return new(context.Return(string.Empty));
        }

        double length = (text.Length * count) + (separator.Length * (count - 1));
        if (length > MaxRepLength)
        {
            string message = string.Create(
                CultureInfo.InvariantCulture,
                $"bad argument #2 to 'rep' (the result would be {length} characters, over the cap of {MaxRepLength})"
            );
            throw new LuaRuntimeException(context.State, message);
        }

        return new(context.Return(string.Join(separator, Enumerable.Repeat(text, (int)count))));
    }

    private static LuaTable Copy(LuaTable source)
    {
        LuaTable copy = new(source.ArrayLength, source.HashMapCount);
        foreach (KeyValuePair<LuaValue, LuaValue> entry in source)
        {
            copy[entry.Key] = entry.Value;
        }

        return copy;
    }
}
