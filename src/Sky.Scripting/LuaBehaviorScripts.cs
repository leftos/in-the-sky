using System.Globalization;
using Lua;
using Sky.Engine.Flight;
using Sky.Engine.Passengers;
using Sky.Engine.Ports;

namespace Sky.Scripting;

/// <summary>
/// Scores activities by calling each activity module's <c>utility</c> function through the Lua host: one module per
/// activity, one call per candidate, and one adapter reused for every call so a scoring loop allocates nothing.
/// </summary>
public sealed class LuaBehaviorScripts : IBehaviorScripts
{
    private const string UtilityFunction = "utility";

    private readonly LuaHost host;
    private readonly string[] moduleIds;
    private readonly string[] traitNames;
    private readonly LuaPassengerFacts adapter;
    private readonly LuaValue[] arguments = new LuaValue[1];
    private readonly LuaValue[] results = new LuaValue[1];

    /// <summary>Loads the flight's activity modules and disables the ones this class can never score.</summary>
    /// <param name="host">The host that owns the flight's Lua state.</param>
    /// <param name="activities">
    /// One module per activity; <c>activities[i]</c> is the module of <c>new ActivityId(i)</c>.
    /// </param>
    /// <param name="traitNames">One name per trait; <c>traitNames[i]</c> is the name of <c>new TraitId(i)</c>.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">
    /// An activity id or trait name is blank, or two activities carry the same id. The message names the index.
    /// </exception>
    public LuaBehaviorScripts(LuaHost host, IReadOnlyList<ActivityModule> activities, IReadOnlyList<string> traitNames)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(activities);
        ArgumentNullException.ThrowIfNull(traitNames);
        this.host = host;
        moduleIds = ValidateActivities(activities);
        this.traitNames = CopyTraitNames(traitNames);
        adapter = new LuaPassengerFacts(moduleIds, this.traitNames);
        arguments[0] = adapter;
        Load(activities);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">
    /// <paramref name="scores"/> holds a different number of slots than <paramref name="candidates"/>, or
    /// <paramref name="facts"/> holds a stage, turbulence level, activity or trait the adapter cannot render.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">A candidate has no activity module behind it.</exception>
    public void ScoreActivities(in PassengerFacts facts, ReadOnlySpan<ActivityId> candidates, Span<double> scores)
    {
        // A scoring call draws nothing (R6): with the stream gone, math.random in a utility throws and disables it.
        host.ClearRandom();
        if (scores.Length != candidates.Length)
        {
            throw new ArgumentException($"Expected one score per candidate ({candidates.Length}); got {scores.Length}.", nameof(scores));
        }

        RequireRenderableFacts(facts);
        RequireRenderableIds(facts);
        for (int index = 0; index < candidates.Length; index++)
        {
            if ((uint)candidates[index].Value >= (uint)moduleIds.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(candidates),
                    candidates[index].Value,
                    $"No activity module is loaded for activity id {candidates[index].Value}."
                );
            }
        }

        adapter.SetFacts(facts);
        for (int index = 0; index < candidates.Length; index++)
        {
            scores[index] = Score(moduleIds[candidates[index].Value]);
        }
    }

    private static string[] ValidateActivities(IReadOnlyList<ActivityModule> activities)
    {
        string[] ids = new string[activities.Count];
        HashSet<string> seen = new(StringComparer.Ordinal);
        for (int index = 0; index < activities.Count; index++)
        {
            string id = activities[index].Id;
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException($"Activity {index} has a blank module id; every activity needs a name.", nameof(activities));
            }

            if (!seen.Add(id))
            {
                throw new ArgumentException($"Activity {index} repeats the module id '{id}'; module ids are unique.", nameof(activities));
            }

            ids[index] = id;
        }

        return ids;
    }

    private static string[] CopyTraitNames(IReadOnlyList<string> traitNames)
    {
        string[] names = new string[traitNames.Count];
        for (int index = 0; index < traitNames.Count; index++)
        {
            string name = traitNames[index];
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException($"Trait {index} has a blank name; every trait needs a name.", nameof(traitNames));
            }

            names[index] = name;
        }

        return names;
    }

    private static void RequireRenderableFacts(in PassengerFacts facts)
    {
        if (facts.Needs is null)
        {
            throw new ArgumentException("Needs is null; the adapter reads the passenger's own need set.", nameof(facts));
        }

        if (!Enum.IsDefined(facts.Stage))
        {
            throw new ArgumentException($"Stage {(int)facts.Stage} is not a defined stage.", nameof(facts));
        }

        if (!Enum.IsDefined(facts.Turbulence))
        {
            throw new ArgumentException($"Turbulence {(int)facts.Turbulence} is not a defined level.", nameof(facts));
        }
    }

    private void RequireRenderableIds(in PassengerFacts facts)
    {
        if ((uint)facts.CurrentActivity.Value >= (uint)moduleIds.Length)
        {
            throw new ArgumentException(
                $"CurrentActivity {facts.CurrentActivity.Value} is outside the activity table of {moduleIds.Length}.",
                nameof(facts)
            );
        }

        foreach (TraitId trait in facts.Traits.Span)
        {
            if ((uint)trait.Value >= (uint)traitNames.Length)
            {
                throw new ArgumentException($"Trait {trait.Value} is outside the trait table of {traitNames.Length}.", nameof(facts));
            }
        }
    }

    private void Load(IReadOnlyList<ActivityModule> activities)
    {
        for (int index = 0; index < moduleIds.Length; index++)
        {
            string moduleId = moduleIds[index];
            // A module that failed to load is disabled by the host with its own reason; only a loaded module whose
            // table has no utility is disabled here, so the first reason a module gets is the true one.
            if (host.LoadModule(moduleId, activities[index].Source) && !host.HasFunction(moduleId, UtilityFunction))
            {
                host.DisableModule(moduleId, $"'{UtilityFunction}' is nil, not a function");
            }
        }
    }

    private double Score(string moduleId)
    {
        if (!host.TryCall(moduleId, UtilityFunction, arguments, results))
        {
            return 0.0;
        }

        LuaValue result = results[0];
        if (result.Type == LuaValueType.Number)
        {
            double score = result.Read<double>();
            if (double.IsFinite(score) && score >= 0.0)
            {
                return score;
            }
        }

        host.DisableModule(moduleId, $"{UtilityFunction} returned {Describe(result)}, not a score of 0 or more");
        return 0.0;
    }

    private static string Describe(LuaValue value) =>
        value.Type == LuaValueType.Number ? value.Read<double>().ToString(CultureInfo.InvariantCulture) : value.TypeToString();
}
