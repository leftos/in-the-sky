using System.Globalization;
using Lua;
using Lua.Runtime;
using Sky.Engine.Randomness;

namespace Sky.Scripting;

/// <summary>
/// Owns the one Lua state of a flight: loads each module into its own whitelist environment, runs every call under an
/// instruction budget, and disables for the rest of the flight any module that throws, runs out of budget or
/// overflows the stack.
/// </summary>
/// <remarks>
/// A module is Lua source that returns a table; the host calls that table's functions by name. After a stack overflow
/// the state is discarded, a new one is built, and every module still enabled is reloaded from its kept source in load
/// order, so module-level values start over.
/// </remarks>
public sealed class LuaHost : IDisposable
{
    /// <summary>The instructions between two fires of the count hook.</summary>
    public const int HookInterval = 1000;

    /// <summary>The standard per-call budget: 1,000 fires of the count hook, 1,000,000 instructions.</summary>
    public const int StandardInstructionBudget = 1_000_000;

    private const string LibraryMessagePrefix = "Lua-CSharp: ";

    private readonly int maxHookFires;
    private readonly Dictionary<string, LuaModule> modules = new(StringComparer.Ordinal);
    private readonly List<LuaModule> loadOrder = [];
    private readonly LuaFunction countHook;
    private readonly LuaFunction random;
    private LuaState state;
    private CancellationTokenSource budget = new();
    private SimRandom? stream;
    private int hookFires;
    private int generation;
    private bool loading;

    /// <summary>Creates a host with a fresh state and no modules.</summary>
    /// <param name="instructionBudget">
    /// The instructions one call may run before it is stopped, counted in whole <see cref="HookInterval"/>s;
    /// <see cref="StandardInstructionBudget"/> is the flight's.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="instructionBudget"/> is below <see cref="HookInterval"/>.</exception>
    public LuaHost(int instructionBudget)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(instructionBudget, HookInterval);
        maxHookFires = instructionBudget / HookInterval;
        countHook = new LuaFunction("budget", OnCountHook);
        random = LuaSandbox.CreateRandom(CurrentStream);
        state = LuaSandbox.CreateState();
    }

    /// <summary>Gets the depth of the Lua value stack, which every call leaves as it found it.</summary>
    internal int StackDepth => state.Stack.Count;

    /// <summary>Sets the stream that <c>math.random</c> draws from until the next call of this method.</summary>
    /// <param name="random">The stream, usually the named stream of the character being scored.</param>
    public void SetRandom(SimRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);
        stream = random;
    }

    /// <summary>
    /// Takes the stream away, so <c>math.random</c> throws until <see cref="SetRandom"/> is called again. A scoring call
    /// clears it: activity modules draw nothing (R6).
    /// </summary>
    internal void ClearRandom() => stream = null;

    /// <summary>
    /// Loads a module: compiles <paramref name="source"/> into its own environment and runs it under the budget, with
    /// <c>math.random</c> unavailable while it runs.
    /// </summary>
    /// <param name="moduleId">The id the module is called and reported by; unique for the flight.</param>
    /// <param name="source">Lua source that returns a table.</param>
    /// <returns><see langword="true"/> when the module loaded; <see langword="false"/> when it was disabled instead.</returns>
    /// <exception cref="ArgumentException"><paramref name="moduleId"/> is blank or already loaded.</exception>
    internal bool LoadModule(string moduleId, string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentNullException.ThrowIfNull(source);
        if (modules.ContainsKey(moduleId))
        {
            throw new ArgumentException($"Module '{moduleId}' is already loaded; module ids are unique for the flight.", nameof(moduleId));
        }

        LuaModule module = new(moduleId, source);
        modules.Add(moduleId, module);
        loadOrder.Add(module);
        return Instantiate(module);
    }

    /// <summary>Reports whether a module has been disabled for the flight.</summary>
    /// <param name="moduleId">A loaded module's id.</param>
    /// <returns><see langword="true"/> when the module threw, ran out of budget or overflowed the stack.</returns>
    /// <exception cref="ArgumentException"><paramref name="moduleId"/> was never loaded.</exception>
    public bool IsDisabled(string moduleId) => GetModule(moduleId).DisabledReason is not null;

    /// <summary>Gets why a module was disabled: the script's error text, the exhausted budget, or the stack overflow.</summary>
    /// <param name="moduleId">A loaded module's id.</param>
    /// <param name="reason">The reason, or <see langword="null"/> when the module is not disabled.</param>
    /// <returns><see langword="true"/> when the module is disabled.</returns>
    /// <exception cref="ArgumentException"><paramref name="moduleId"/> was never loaded.</exception>
    public bool TryGetDisabledReason(string moduleId, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? reason)
    {
        reason = GetModule(moduleId).DisabledReason;
        return reason is not null;
    }

    /// <summary>Reports whether an enabled module's table holds a function under <paramref name="functionName"/>.</summary>
    /// <param name="moduleId">A loaded module's id.</param>
    /// <param name="functionName">The field of the module's table.</param>
    /// <returns><see langword="false"/> when the module is disabled or the field is not a function.</returns>
    /// <exception cref="ArgumentException"><paramref name="moduleId"/> was never loaded.</exception>
    internal bool HasFunction(string moduleId, string functionName)
    {
        LuaModule module = GetModule(moduleId);
        return module.Table is not null && module.Table[functionName].Type == LuaValueType.Function;
    }

    /// <summary>Disables a module for the rest of the flight for a reason the caller knows and the host does not.</summary>
    /// <param name="moduleId">A loaded module's id.</param>
    /// <param name="reason">Why the module can no longer be called; the first reason a module was disabled with is kept.</param>
    /// <exception cref="ArgumentException"><paramref name="moduleId"/> was never loaded.</exception>
    internal void DisableModule(string moduleId, string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);
        Disable(GetModule(moduleId), reason);
    }

    /// <summary>
    /// Calls a function of a module's table under the budget. A call that throws, runs out of budget or overflows the
    /// stack disables the module, as does naming a field that is not a function. A .NET exception from a host function
    /// is a host bug, not a module fault: it escapes, wrapped in a <see cref="LuaRuntimeException"/>, and disables nothing.
    /// </summary>
    /// <param name="moduleId">A loaded module's id.</param>
    /// <param name="functionName">The field of the module's table to call.</param>
    /// <param name="arguments">The arguments, in order.</param>
    /// <param name="results">Receives the first results; slots the call did not fill are nil.</param>
    /// <returns><see langword="true"/> when the call returned; <see langword="false"/> when the module is or became disabled.</returns>
    /// <exception cref="ArgumentException"><paramref name="moduleId"/> was never loaded.</exception>
    internal bool TryCall(string moduleId, string functionName, ReadOnlySpan<LuaValue> arguments, Span<LuaValue> results)
    {
        LuaModule module = GetModule(moduleId);
        if (module.Table is null)
        {
            return false;
        }

        LuaValue function = module.Table[functionName];
        if (function.Type != LuaValueType.Function)
        {
            Disable(module, $"'{functionName}' is {function.TypeToString()}, not a function");
            return false;
        }

        return Invoke(module, function, arguments, results);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        state.Dispose();
        budget.Dispose();
    }

    private SimRandom CurrentStream(LuaState caller)
    {
        if (loading)
        {
            throw new LuaRuntimeException(caller, "math.random is not available while a module loads");
        }

        return stream ?? throw new LuaRuntimeException(caller, "math.random: no random stream is set on the host");
    }

    private LuaModule GetModule(string moduleId)
    {
        ArgumentNullException.ThrowIfNull(moduleId);
        return modules.TryGetValue(moduleId, out LuaModule? module)
            ? module
            : throw new ArgumentException($"No module '{moduleId}' was loaded into this host.", nameof(moduleId));
    }

    private bool Instantiate(LuaModule module)
    {
        LuaClosure chunk;
        try
        {
            chunk = state.Load(module.Source, module.Id, LuaSandbox.CreateEnvironment(state, random));
        }
        catch (Exception exception) when (exception is LuaParseException or LuaCompileException)
        {
            Disable(module, exception.Message);
            return false;
        }

        LuaValue[] returned = [LuaValue.Nil];
        bool ran;
        loading = true;
        try
        {
            ran = Invoke(module, chunk, [], returned);
        }
        finally
        {
            loading = false;
        }

        if (!ran)
        {
            return false;
        }

        if (!returned[0].TryRead(out LuaTable table))
        {
            Disable(module, $"the module returned {returned[0].TypeToString()}, not a table");
            return false;
        }

        module.Table = table;
        return true;
    }

    private bool Invoke(LuaModule module, LuaValue function, ReadOnlySpan<LuaValue> arguments, Span<LuaValue> results)
    {
        try
        {
            Run(function, arguments, results);
            return true;
        }
        catch (Exception) when (budget.IsCancellationRequested)
        {
            budget.Dispose();
            budget = new CancellationTokenSource();
            Disable(module, string.Create(CultureInfo.InvariantCulture, $"ran past its instruction budget of {maxHookFires * HookInterval}"));
        }
        catch (Exception exception) when (IsStackOverflow(exception))
        {
            Disable(module, "stack overflow");
            Rebuild();
        }
        catch (LuaRuntimeException exception) when (exception.InnerException is null or LuaRuntimeException)
        {
            Disable(module, DescribeError(exception));
        }

        return false;
    }

    private static string DescribeError(LuaRuntimeException exception)
    {
        LuaRuntimeException innermost = exception;
        while (innermost.InnerException is LuaRuntimeException inner)
        {
            innermost = inner;
        }

        if (innermost.ErrorObject.Type != LuaValueType.String)
        {
            return "error with a non-string value";
        }

        string message = innermost.ErrorObject.Read<string>();
        string located = innermost.Message;
        bool hasPosition = located.StartsWith(LibraryMessagePrefix, StringComparison.Ordinal) && located.EndsWith(message, StringComparison.Ordinal);
        return hasPosition ? located[LibraryMessagePrefix.Length..] : message;
    }

    private void Run(LuaValue function, ReadOnlySpan<LuaValue> arguments, Span<LuaValue> results)
    {
        LuaStack stack = state.Stack;
        int depth = stack.Count;
        hookFires = 0;
        state.SetHook(countHook, "", HookInterval);
        try
        {
            stack.Push(function);
            stack.PushRange(arguments);
            int count = Complete(state.CallAsync(depth, depth, budget.Token));
            ReadOnlySpan<LuaValue> returned = stack.AsSpan().Slice(depth, Math.Min(count, results.Length));
            returned.CopyTo(results);
            results[returned.Length..].Fill(LuaValue.Nil);
        }
        finally
        {
            stack.PopUntil(depth);
        }
    }

    private static int Complete(ValueTask<int> pending)
    {
        if (!pending.IsCompleted)
        {
            throw new InvalidOperationException(
                "A Lua call suspended; every host function is synchronous, so a call that does not complete at once is a host bug."
            );
        }

        return pending.GetAwaiter().GetResult();
    }

    private ValueTask<int> OnCountHook(LuaFunctionExecutionContext context, CancellationToken cancellationToken)
    {
        // Cancelling rather than throwing: the runtime checks the token right after the hook returns, and pcall rethrows
        // a cancellation where it would catch an error, so a script cannot swallow its own budget.
        hookFires++;
        if (hookFires >= maxHookFires)
        {
            budget.Cancel();
        }

        return new(context.Return());
    }

    private static bool IsStackOverflow(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current.GetType().FullName == "Lua.LuaStackOverflowException")
            {
                return true;
            }
        }

        return false;
    }

    private void Rebuild()
    {
        // The overflowed state is dropped, not disposed: its call stack may still hold frames, and Dispose throws then.
        state = LuaSandbox.CreateState();
        int rebuilt = ++generation;
        foreach (LuaModule module in loadOrder)
        {
            module.Table = null;
        }

        foreach (LuaModule module in loadOrder)
        {
            if (module.DisabledReason is null)
            {
                Instantiate(module);
            }

            if (generation != rebuilt)
            {
                return;
            }
        }
    }

    private static void Disable(LuaModule module, string reason)
    {
        module.DisabledReason ??= reason;
        module.Table = null;
    }

    private sealed class LuaModule(string id, string source)
    {
        public string Id { get; } = id;

        public string Source { get; } = source;

        public LuaTable? Table { get; set; }

        public string? DisabledReason { get; set; }
    }
}
