using System.Globalization;
using Lua;
using Sky.Engine.Events;
using Sky.Engine.Needs;
using Sky.Engine.Ports;
using Sky.Engine.Time;

namespace Sky.Scripting;

/// <summary>
/// Reads what an event module returned into the Engine's records: an <c>effects</c> list into
/// <see cref="DelayedConsequence"/>s, and the typed fields its other results are read through. The first malformed field
/// becomes <see cref="Fault"/>, phrased <c>'{field}' is {what it is}, not {what it should be}</c>, and every read after
/// it returns a harmless default the caller discards.
/// </summary>
internal sealed class ConsequenceReader
{
    /// <summary>The Lua name of each need, indexed by <see cref="Need"/>.</summary>
    internal static readonly string[] NeedNames = ["refreshment", "bladder", "rest", "unease", "boredom"];

    /// <summary>The Lua name of each incident kind, indexed by <see cref="IncidentKind"/>.</summary>
    internal static readonly string[] IncidentNames = ["accident", "panic", "food_demand", "noise_complaint", "disruptive_passenger", "fight"];

    /// <summary>The most consequences one branch may return.</summary>
    internal const int MaxEffects = 32;

    private const string TargetField = "target";

    private static readonly string[] KindFields = ["need", "incident", "swap", "to_seat", "line"];

    /// <summary>Gets why the last read failed, or <see langword="null"/> when every field so far was well formed.</summary>
    internal string? Fault { get; private set; }

    /// <summary>Forgets the last fault, before a new result is read.</summary>
    internal void Reset() => Fault = null;

    /// <summary>Records a fault in a phrasing of the caller's own, unless an earlier one is already recorded.</summary>
    /// <param name="reason">Why the result is malformed.</param>
    internal void Fail(string reason) => Fault ??= reason;

    /// <summary>Records that a field holds the wrong thing, unless an earlier fault is already recorded.</summary>
    /// <param name="field">The field's path, such as <c>choices[1].quality</c>.</param>
    /// <param name="actual">What the field holds: its Lua type, or its value when the type was right.</param>
    /// <param name="expected">What it should hold, with its article.</param>
    internal void Fail(string field, string actual, string expected) => Fault ??= $"'{field}' is {actual}, not {expected}";

    /// <summary>Renders a Lua value for a fault: a string quoted, a number in invariant digits, anything else by its type.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The rendering.</returns>
    internal static string Show(LuaValue value) =>
        value.Type switch
        {
            LuaValueType.String => $"'{value.Read<string>()}'",
            LuaValueType.Number => value.Read<double>().ToString(CultureInfo.InvariantCulture),
            _ => value.TypeToString(),
        };

    /// <summary>Reads a function's result as a table.</summary>
    /// <param name="result">The first value the function returned.</param>
    /// <param name="function">The function's name, for the fault.</param>
    /// <param name="expected">What the function should return, with its article.</param>
    /// <returns>The table, or <see langword="null"/> after recording a fault.</returns>
    internal LuaTable? Result(LuaValue result, string function, string expected)
    {
        if (result.Type == LuaValueType.Table)
        {
            return result.Read<LuaTable>();
        }

        Fail($"{function} returned {result.TypeToString()}, not {expected}");
        return null;
    }

    /// <summary>Reads a value as a table.</summary>
    /// <param name="value">The value.</param>
    /// <param name="field">The value's path.</param>
    /// <returns>The table, or <see langword="null"/> after recording a fault.</returns>
    internal LuaTable? Table(LuaValue value, string field)
    {
        if (value.Type == LuaValueType.Table)
        {
            return value.Read<LuaTable>();
        }

        Fail(field, value.TypeToString(), "a table");
        return null;
    }

    /// <summary>Reads a field as a string.</summary>
    /// <param name="table">The table holding the field.</param>
    /// <param name="key">The field's key.</param>
    /// <param name="path">The table's path.</param>
    /// <returns>The string, or empty after recording a fault.</returns>
    internal string Text(LuaTable table, string key, string path)
    {
        LuaValue value = table[key];
        if (value.Type == LuaValueType.String)
        {
            return value.Read<string>();
        }

        Fail(Join(path, key), value.TypeToString(), "a string");
        return string.Empty;
    }

    /// <summary>Reads a field as a boolean.</summary>
    /// <param name="table">The table holding the field.</param>
    /// <param name="key">The field's key.</param>
    /// <param name="path">The table's path.</param>
    /// <returns>The boolean, or <see langword="false"/> after recording a fault.</returns>
    internal bool Flag(LuaTable table, string key, string path)
    {
        LuaValue value = table[key];
        if (value.Type == LuaValueType.Boolean)
        {
            return value.Read<bool>();
        }

        Fail(Join(path, key), value.TypeToString(), "a boolean");
        return false;
    }

    /// <summary>Reads a field as a number within a closed range, which a NaN is never within.</summary>
    /// <param name="table">The table holding the field.</param>
    /// <param name="key">The field's key.</param>
    /// <param name="path">The table's path.</param>
    /// <param name="range">The lowest and highest value allowed.</param>
    /// <param name="expected">What the field should hold, with its article, for the fault.</param>
    /// <returns>The number, or a value after recording a fault.</returns>
    internal double Number(LuaTable table, string key, string path, (double Min, double Max) range, string expected)
    {
        LuaValue value = table[key];
        if (value.Type != LuaValueType.Number)
        {
            Fail(Join(path, key), value.TypeToString(), "a number");
            return range.Min;
        }

        double number = value.Read<double>();
        if (!(number >= range.Min && number <= range.Max))
        {
            Fail(Join(path, key), Show(value), expected);
        }

        return number;
    }

    /// <summary>Reads a field as one of a fixed list of names.</summary>
    /// <param name="table">The table holding the field.</param>
    /// <param name="key">The field's key.</param>
    /// <param name="path">The table's path.</param>
    /// <param name="names">The names allowed, indexed by the value each stands for.</param>
    /// <param name="expected">What the field should hold, with its article, for the fault.</param>
    /// <returns>The name's index, or 0 after recording a fault.</returns>
    internal int Name(LuaTable table, string key, string path, string[] names, string expected) => Name(table[key], Join(path, key), names, expected);

    /// <summary>Reads a value as one of a fixed list of names.</summary>
    /// <param name="value">The value.</param>
    /// <param name="field">The value's path.</param>
    /// <param name="names">The names allowed, indexed by the value each stands for.</param>
    /// <param name="expected">What the value should be, with its article, for the fault.</param>
    /// <returns>The name's index, or 0 after recording a fault.</returns>
    internal int Name(LuaValue value, string field, string[] names, string expected)
    {
        if (value.Type != LuaValueType.String)
        {
            Fail(field, value.TypeToString(), expected);
            return 0;
        }

        int index = Array.IndexOf(names, value.Read<string>());
        if (index < 0)
        {
            Fail(field, Show(value), expected);
            return 0;
        }

        return index;
    }

    /// <summary>Reads an <c>effects</c> result: a list of consequences, each exactly one of the four kinds.</summary>
    /// <param name="result">The first value <c>effects</c> returned.</param>
    /// <param name="subject">The passenger the facts name as subject, which the <c>subject</c> and <c>neighbours</c> targets need.</param>
    /// <returns>The consequences in the module's order; empty after recording a fault.</returns>
    internal IReadOnlyList<DelayedConsequence> ReadEffects(LuaValue result, int? subject)
    {
        LuaTable? list = Result(result, "effects", "a list of consequences");
        List<DelayedConsequence> consequences = [];
        for (int index = 1; list is not null && list[index].Type != LuaValueType.Nil && Fault is null; index++)
        {
            if (index > MaxEffects)
            {
                Fail("effects", $"a list of more than {MaxEffects}", $"a list of at most {MaxEffects} consequences");
                break;
            }

            string path = string.Create(CultureInfo.InvariantCulture, $"effects[{index}]");
            LuaTable? entry = Table(list[index], path);
            if (entry is not null)
            {
                consequences.Add(Consequence(entry, path, subject));
            }
        }

        return Fault is null ? consequences : [];
    }

    private DelayedConsequence Consequence(LuaTable entry, string path, int? subject)
    {
        bool hasSubject = subject is not null;
        long ticks = Ticks(entry, path);
        return Kind(entry, path) switch
        {
            "need" => new NeedConsequence(
                ticks,
                Target(entry[TargetField], Join(path, TargetField), single: false, hasSubject),
                (Need)Name(entry, "need", path, NeedNames, "a need name"),
                Number(entry, "delta", path, (double.MinValue, double.MaxValue), "a finite number")
            ),
            "incident" => new IncidentConsequence(
                ticks,
                Target(entry[TargetField], Join(path, TargetField), single: true, hasSubject),
                (IncidentKind)Name(entry, "incident", path, IncidentNames, "an incident name")
            ),
            "swap" => Swap(entry, path, ticks, subject),
            "to_seat" => new SeatMoveConsequence(
                ticks,
                Target(entry[TargetField], Join(path, TargetField), single: true, hasSubject),
                Text(entry, "to_seat", path)
            ),
            _ => new LineConsequence(
                ticks,
                Target(entry[TargetField], Join(path, TargetField), single: false, hasSubject),
                Text(entry, "line", path)
            ),
        };
    }

    private long Ticks(LuaTable entry, string path)
    {
        double minutes = Number(entry, "after_minutes", path, (0.0, double.MaxValue), "a number of minutes of 0 or more");
        double ticks = Math.Round(minutes * SimTime.TicksPerSimMinute, MidpointRounding.AwayFromZero);
        if (ticks < long.MaxValue)
        {
            return (long)ticks;
        }

        Fail(Join(path, "after_minutes"), Show(minutes), "a number of minutes the tick count can hold");
        return 0;
    }

    private string Kind(LuaTable entry, string path)
    {
        string? kind = null;
        int count = 0;
        foreach (string field in KindFields)
        {
            if (entry[field].Type != LuaValueType.Nil)
            {
                kind ??= field;
                count++;
            }
        }

        if (count != 1)
        {
            Fail(path, $"a table with {count} of need, incident, swap, to_seat and line", "a table with exactly one");
        }

        return kind ?? "line";
    }

    private SeatSwapConsequence Swap(LuaTable entry, string path, long ticks, int? subject)
    {
        string field = Join(path, "swap");
        if (entry[TargetField].Type != LuaValueType.Nil)
        {
            Fail(Join(path, TargetField), Show(entry[TargetField]), "nil beside a swap, which names its passengers in 'swap'");
        }

        LuaTable? pair = Table(entry["swap"], field);
        if (pair is null)
        {
            return new SeatSwapConsequence(ticks, EventTarget.Subject, EventTarget.Subject);
        }

        if (pair[3].Type != LuaValueType.Nil)
        {
            Fail($"{field}[3]", Show(pair[3]), "nil: a swap names two passengers");
        }

        EventTarget first = Target(pair[1], $"{field}[1]", single: true, subject is not null);
        EventTarget second = Target(pair[2], $"{field}[2]", single: true, subject is not null);
        if (Fault is null && PassengerOf(first, subject) == PassengerOf(second, subject))
        {
            Fail($"{field}[2]", Show(pair[2]), $"a passenger other than {Show(pair[1])}: a swap names two passengers");
        }

        return new SeatSwapConsequence(ticks, first, second);
    }

    private static int PassengerOf(EventTarget target, int? subject) =>
        target.Kind == EventTargetKind.Subject ? subject.GetValueOrDefault() : target.PassengerId;

    private EventTarget Target(LuaValue value, string field, bool single, bool hasSubject)
    {
        if (value.Type == LuaValueType.Number)
        {
            return PassengerTarget(value, field);
        }

        EventTarget? target =
            value.Type != LuaValueType.String
                ? null
                : value.Read<string>() switch
                {
                    "subject" => EventTarget.Subject,
                    "neighbours" when !single => EventTarget.Neighbours,
                    _ => null,
                };
        if (target is null)
        {
            Fail(field, Show(value), single ? "a single passenger" : "a target");
            return EventTarget.Subject;
        }

        if (!hasSubject)
        {
            Fail(field, Show(value), "a target these facts can resolve: they name no subject");
        }

        return target.Value;
    }

    private EventTarget PassengerTarget(LuaValue value, string field)
    {
        double id = value.Read<double>();
        if (id >= 0 && id <= int.MaxValue && Math.Floor(id) == id)
        {
            return EventTarget.Passenger((int)id);
        }

        Fail(field, Show(value), "a passenger id");
        return EventTarget.Subject;
    }

    private static string Show(double number) => number.ToString(CultureInfo.InvariantCulture);

    private static string Join(string path, string key) => path.Length == 0 ? key : $"{path}.{key}";
}
