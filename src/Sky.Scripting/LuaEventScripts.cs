using System.Globalization;
using Lua;
using Sky.Engine.Ports;
using Sky.Engine.Randomness;

namespace Sky.Scripting;

/// <summary>
/// Evaluates events by calling each event module's <c>trigger</c>, <c>describe</c>, <c>choices</c> and <c>effects</c>
/// through the Lua host (<c>docs/design/events.md</c> section 1), reading what they return into the Engine's records.
/// The facts table a trigger returns stays here, under the handle the Engine is given, until it is released.
/// </summary>
/// <remarks>
/// A module that throws, runs out of budget, or returns a malformed result from any of its four functions is disabled
/// for the flight, with the field named, and the call returns nothing; so is a module whose table is malformed at load.
/// A trigger and an effects call draw only from the stream they are handed; describe and choices draw nothing.
/// </remarks>
internal sealed class LuaEventScripts
{
    private const string TriggerFunction = "trigger";
    private const string DescribeFunction = "describe";
    private const string ChoicesFunction = "choices";
    private const string EffectsFunction = "effects";
    private const int MinChoices = 2;
    private const int MaxChoices = 4;
    private const string LeaveChoice = "leave";

    private static readonly string[] Functions = [TriggerFunction, DescribeFunction, ChoicesFunction, EffectsFunction];

    private readonly LuaHost host;
    private readonly string[] eventIds;
    private readonly bool[][] phases;
    private readonly Dictionary<string, int> indexById = new(StringComparer.Ordinal);
    private readonly Dictionary<int, Recorded> recorded = [];
    private readonly LuaEventContext context;
    private readonly ConsequenceReader reader = new();
    private readonly LuaValue[] contextArgument = new LuaValue[1];
    private readonly LuaValue[] factsArgument = new LuaValue[1];
    private readonly LuaValue[] effectsArguments = new LuaValue[2];
    private readonly LuaValue[] results = new LuaValue[1];
    private int nextHandle = 1;

    /// <summary>Loads the flight's event modules and disables the ones whose table is malformed.</summary>
    /// <param name="host">The host that owns the flight's Lua state.</param>
    /// <param name="events">The flight's event modules, in load order.</param>
    /// <param name="activityIds">The ids of the activity modules already loaded into the host, which no event may reuse.</param>
    /// <param name="traitNames">One name per trait, indexed by trait id; the array is shared, never written.</param>
    /// <exception cref="ArgumentException">
    /// An event id is blank, repeated, or an activity's id. The message names the index.
    /// </exception>
    internal LuaEventScripts(LuaHost host, IReadOnlyList<EventModule> events, string[] activityIds, string[] traitNames)
    {
        this.host = host;
        eventIds = ValidateEvents(events, activityIds, indexById);
        phases = new bool[eventIds.Length][];
        context = new LuaEventContext(host, traitNames);
        contextArgument[0] = context;
        for (int index = 0; index < eventIds.Length; index++)
        {
            phases[index] = new bool[LuaPassengerFacts.StageNames.Length];
            Load(index, events[index].Source);
        }
    }

    /// <summary>Gets the id of every event module, disabled ones included, in load order.</summary>
    internal IReadOnlyList<string> EventIds => eventIds;

    /// <summary>Asks one event's trigger whether it fires, with the host drawing from <paramref name="random"/> only.</summary>
    /// <param name="eventId">A loaded event's id.</param>
    /// <param name="ctx">What the trigger reads.</param>
    /// <param name="random">The stream the trigger draws from.</param>
    /// <returns>The recorded facts, or <see langword="null"/> when the event did not fire.</returns>
    /// <exception cref="ArgumentException">The event was never loaded, or the context cannot be rendered.</exception>
    internal EventFacts? Trigger(string eventId, in EventContext ctx, SimRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);
        int index = IndexOf(eventId);
        // The context is validated before the phase check, which indexes the phase table by the validated stage.
        context.SetContext(ctx);
        if (host.IsDisabled(eventId) || !phases[index][(int)ctx.Stage])
        {
            return null;
        }

        host.SetRandom(random);
        context.IsLive = true;
        bool called;
        try
        {
            called = host.TryCall(eventId, TriggerFunction, contextArgument, results);
        }
        finally
        {
            context.IsLive = false;
            host.ClearRandom();
        }

        return called ? ReadFacts(eventId, results[0]) : null;
    }

    /// <summary>Gets the scene of recorded facts.</summary>
    /// <param name="facts">Facts a trigger returned and that are not yet released.</param>
    /// <returns>The scene, or empty when the event is or becomes disabled.</returns>
    /// <exception cref="InvalidOperationException">The facts were released or never recorded here.</exception>
    internal string Describe(EventFacts facts)
    {
        if (!CallWithFacts(facts, DescribeFunction))
        {
            return string.Empty;
        }

        if (results[0].Type == LuaValueType.String)
        {
            return results[0].Read<string>();
        }

        host.DisableModule(facts.EventId, $"{DescribeFunction} returned {results[0].TypeToString()}, not a string");
        return string.Empty;
    }

    /// <summary>Reports whether an event is disabled for the flight, and why.</summary>
    /// <param name="eventId">A loaded event's id.</param>
    /// <param name="reason">The host's reason, or <see langword="null"/> when the event is enabled.</param>
    /// <returns><see langword="true"/> when the event is disabled.</returns>
    /// <exception cref="ArgumentException">The event was never loaded.</exception>
    internal bool IsEventDisabled(string eventId, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? reason)
    {
        IndexOf(eventId);
        return host.TryGetDisabledReason(eventId, out reason);
    }

    /// <summary>Gets the choices the event offers for recorded facts, and records their ids as the ones on offer.</summary>
    /// <param name="facts">Facts a trigger returned and that are not yet released.</param>
    /// <returns>Two to four choices, or empty when the event is or becomes disabled.</returns>
    /// <exception cref="InvalidOperationException">The facts were released or never recorded here.</exception>
    internal IReadOnlyList<EventChoice> Choices(EventFacts facts)
    {
        if (!CallWithFacts(facts, ChoicesFunction))
        {
            return [];
        }

        reader.Reset();
        EventChoice[] choices = ReadChoices(results[0]);
        IReadOnlyList<EventChoice> settled = Settle(facts.EventId, choices);
        if (settled.Count > 0)
        {
            recorded[facts.Handle].Offered = Array.ConvertAll(choices, choice => choice.Id);
        }

        return settled;
    }

    /// <summary>Gets the consequences of a chosen branch, with the host drawing from <paramref name="random"/> only.</summary>
    /// <param name="facts">Facts a trigger returned and that are not yet released.</param>
    /// <param name="choiceId">The chosen choice's id.</param>
    /// <param name="random">The stream the effects draw from.</param>
    /// <returns>The consequences, or empty when the event is or becomes disabled.</returns>
    /// <exception cref="InvalidOperationException">
    /// The facts were released or never recorded here, or the event is enabled and <paramref name="choiceId"/> is not
    /// among the choices <see cref="Choices"/> last offered for them.
    /// </exception>
    internal IReadOnlyList<DelayedConsequence> Effects(EventFacts facts, string choiceId, SimRandom random)
    {
        ArgumentNullException.ThrowIfNull(choiceId);
        ArgumentNullException.ThrowIfNull(random);
        Recorded entry = Lookup(facts);
        if (host.IsDisabled(facts.EventId))
        {
            return [];
        }

        RequireOffered(facts, entry, choiceId);
        effectsArguments[0] = entry.Table;
        effectsArguments[1] = choiceId;
        host.SetRandom(random);
        bool called;
        try
        {
            called = host.TryCall(facts.EventId, EffectsFunction, effectsArguments, results);
        }
        finally
        {
            host.ClearRandom();
        }

        if (!called)
        {
            return [];
        }

        reader.Reset();
        IReadOnlyList<DelayedConsequence> consequences = reader.ReadEffects(results[0], entry.Subject);
        return Settle(facts.EventId, consequences);
    }

    /// <summary>Drops recorded facts.</summary>
    /// <param name="facts">Facts a trigger returned and that are not yet released.</param>
    /// <exception cref="InvalidOperationException">The facts were released already or never recorded here.</exception>
    internal void Release(EventFacts facts)
    {
        Lookup(facts);
        recorded.Remove(facts.Handle);
    }

    private static void RequireOffered(EventFacts facts, Recorded entry, string choiceId)
    {
        if (entry.Offered is null || Array.IndexOf(entry.Offered, choiceId) < 0)
        {
            throw new InvalidOperationException(
                $"Choice '{choiceId}' was not offered for event '{facts.EventId}' under handle {facts.Handle}; "
                    + "effects take the id of one of the choices Choices returned for these facts."
            );
        }
    }

    private static string[] ValidateEvents(IReadOnlyList<EventModule> events, string[] activityIds, Dictionary<string, int> indexById)
    {
        string[] ids = new string[events.Count];
        for (int index = 0; index < events.Count; index++)
        {
            string id = events[index].Id;
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException($"Event {index} has a blank module id; every event needs a name.", nameof(events));
            }

            if (Array.IndexOf(activityIds, id) >= 0)
            {
                throw new ArgumentException($"Event {index} repeats the activity module id '{id}'; module ids are unique.", nameof(events));
            }

            if (!indexById.TryAdd(id, index))
            {
                throw new ArgumentException($"Event {index} repeats the module id '{id}'; module ids are unique.", nameof(events));
            }

            ids[index] = id;
        }

        return ids;
    }

    private void Load(int index, string source)
    {
        string eventId = eventIds[index];
        // A module that failed to load is disabled by the host with its own reason, which is the true first one.
        if (!host.LoadModule(eventId, source))
        {
            return;
        }

        reader.Reset();
        ReadId(eventId);
        ReadPhases(eventId, phases[index]);
        foreach (string function in Functions)
        {
            LuaValue value = host.GetField(eventId, function);
            if (value.Type != LuaValueType.Function)
            {
                reader.Fail(function, value.TypeToString(), "a function");
            }
        }

        if (reader.Fault is not null)
        {
            host.DisableModule(eventId, reader.Fault);
        }
    }

    private void ReadId(string eventId)
    {
        LuaValue id = host.GetField(eventId, "id");
        if (id.Type != LuaValueType.String)
        {
            reader.Fail("id", id.TypeToString(), "a string");
        }
        else if (!string.Equals(id.Read<string>(), eventId, StringComparison.Ordinal))
        {
            reader.Fail("id", ConsequenceReader.Show(id), $"'{eventId}'");
        }
    }

    private void ReadPhases(string eventId, bool[] stages)
    {
        const string expected = "a list of stage names";
        LuaValue value = host.GetField(eventId, "phases");
        if (value.Type != LuaValueType.Table)
        {
            reader.Fail("phases", value.TypeToString(), expected);
            return;
        }

        LuaTable list = value.Read<LuaTable>();
        if (list[1].Type == LuaValueType.Nil)
        {
            reader.Fail("phases", "an empty list", expected);
        }

        for (int index = 1; list[index].Type != LuaValueType.Nil; index++)
        {
            string field = string.Create(CultureInfo.InvariantCulture, $"phases[{index}]");
            stages[reader.Name(list[index], field, LuaPassengerFacts.StageNames, "a stage name")] = true;
        }
    }

    private EventFacts? ReadFacts(string eventId, LuaValue result)
    {
        if (result.Type == LuaValueType.Nil)
        {
            return null;
        }

        if (result.Type != LuaValueType.Table)
        {
            host.DisableModule(eventId, $"{TriggerFunction} returned {result.TypeToString()}, not a table or nil");
            return null;
        }

        LuaTable table = result.Read<LuaTable>();
        LuaValue subject = table["subject"];
        int? subjectId = null;
        if (subject.Type != LuaValueType.Nil)
        {
            if (!context.IsPassengerId(subject, out int id))
            {
                host.DisableModule(eventId, $"'subject' is {ConsequenceReader.Show(subject)}, not the id of a passenger aboard");
                return null;
            }

            subjectId = id;
        }

        int handle = nextHandle++;
        recorded.Add(handle, new Recorded(eventId, table, subjectId));
        return new EventFacts(eventId, handle, subjectId);
    }

    private EventChoice[] ReadChoices(LuaValue result)
    {
        LuaTable? list = reader.Result(result, ChoicesFunction, "a list of choices");
        if (list is null)
        {
            return [];
        }

        int count = 0;
        while (count <= MaxChoices && list[count + 1].Type != LuaValueType.Nil)
        {
            count++;
        }

        if (count is < MinChoices or > MaxChoices)
        {
            reader.Fail(ChoicesFunction, $"a list of {count}", $"a list of {MinChoices} to {MaxChoices} choices");
            return [];
        }

        var choices = new EventChoice[count];
        for (int index = 0; index < count; index++)
        {
            choices[index] = ReadChoice(
                list[index + 1],
                string.Create(CultureInfo.InvariantCulture, $"choices[{index + 1}]"),
                choices.AsSpan(0, index)
            );
        }

        // The timeout of a task nobody starts resolves with the crew-free leave choice (events.md section 5).
        if (Array.FindIndex(choices, choice => choice.Id == LeaveChoice && !choice.NeedsCrew) < 0)
        {
            reader.Fail(ChoicesFunction, $"a list with no crew-free '{LeaveChoice}' choice", "a list with one");
        }

        return choices;
    }

    private EventChoice ReadChoice(LuaValue value, string path, ReadOnlySpan<EventChoice> earlier)
    {
        LuaTable? table = reader.Table(value, path);
        if (table is null)
        {
            return new EventChoice(string.Empty, string.Empty, false, 0.0, 0.0);
        }

        string id = reader.Text(table, "id", path);
        foreach (EventChoice choice in earlier)
        {
            if (string.Equals(choice.Id, id, StringComparison.Ordinal))
            {
                reader.Fail($"{path}.id", ConsequenceReader.Show(table["id"]), "an id unique among the choices");
            }
        }

        string label = reader.Text(table, "label", path);
        bool needsCrew = reader.Flag(table, "needs_crew", path);
        double minutes = needsCrew
            ? reader.Number(table, "minutes", path, (double.Epsilon, double.MaxValue), "a number of minutes above 0 for a crew choice")
            : reader.Number(table, "minutes", path, (0.0, 0.0), "0 for a choice with no crew");
        double quality = reader.Number(table, "quality", path, (0.0, 1.0), "a number in [0, 1]");
        return new EventChoice(id, label, needsCrew, minutes, quality);
    }

    private IReadOnlyList<T> Settle<T>(string eventId, IReadOnlyList<T> read)
    {
        if (reader.Fault is null)
        {
            return read;
        }

        host.DisableModule(eventId, reader.Fault);
        return [];
    }

    private bool CallWithFacts(EventFacts facts, string function)
    {
        factsArgument[0] = Lookup(facts).Table;
        // Only a trigger and an effects call draw; with no stream set, a draw here throws and disables the module.
        host.ClearRandom();
        return host.TryCall(facts.EventId, function, factsArgument, results);
    }

    private Recorded Lookup(EventFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (recorded.TryGetValue(facts.Handle, out Recorded? entry) && string.Equals(entry.EventId, facts.EventId, StringComparison.Ordinal))
        {
            return entry;
        }

        throw new InvalidOperationException(
            $"No facts of event '{facts.EventId}' are recorded under handle {facts.Handle}: they were released, or no trigger returned them."
        );
    }

    private int IndexOf(string eventId)
    {
        ArgumentNullException.ThrowIfNull(eventId);
        return indexById.TryGetValue(eventId, out int index)
            ? index
            : throw new ArgumentException($"No event module '{eventId}' was loaded for this flight.", nameof(eventId));
    }

    private sealed class Recorded(string eventId, LuaTable table, int? subject)
    {
        public string EventId { get; } = eventId;

        public LuaTable Table { get; } = table;

        public int? Subject { get; } = subject;

        /// <summary>Gets or sets the ids of the choices last offered for these facts; null until choices are offered.</summary>
        public string[]? Offered { get; set; }
    }
}
