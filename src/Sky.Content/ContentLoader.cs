using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Sky.Content.Schema;
using Sky.Engine.Cabin;
using Sky.Engine.Needs;
using Sky.Engine.Passengers;
using Sky.Engine.Time;

namespace Sky.Content;

/// <summary>
/// Loads a content tree into a <see cref="ContentSet"/>. Under the content root: <c>layouts/&lt;id&gt;.json</c>,
/// <c>needs.json</c>, <c>traits.json</c>, <c>activities.json</c> with <c>activities/&lt;id&gt;.lua</c> per activity,
/// <c>crew.json</c>, <c>scenarios/&lt;id&gt;.json</c> and <c>thoughts.json</c>. Every failure stops the load with a
/// <see cref="ContentLoadException"/> naming the file and the JSON path (R10); ranges and cross-file references are the
/// validators' to check.
/// </summary>
public static class ContentLoader
{
    private const string NeedsFileName = "needs.json";
    private const string TraitsFileName = "traits.json";
    private const string ActivitiesFileName = "activities.json";
    private const string CrewFileName = "crew.json";
    private const string ThoughtsFileName = "thoughts.json";
    private const string LayoutsDirectory = "layouts";
    private const string ScenariosDirectory = "scenarios";
    private const string ModulesDirectory = "activities";

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Loads every content file under <paramref name="directory"/> and hashes the tree.</summary>
    /// <param name="directory">The content root.</param>
    /// <returns>The loaded content.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="directory"/> is null.</exception>
    /// <exception cref="ContentLoadException">A file is missing, unreadable, malformed or refused.</exception>
    public static ContentSet Load(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        string root = FullRoot(directory);
        if (!Directory.Exists(root))
        {
            throw new ContentLoadException(".", null, $"Expected a content root directory at {root}; there is none.", null);
        }

        ContentJsonContext json = ContentJsonContext.Default;
        TraitsFile traits = Read(root, TraitsFileName, json.TraitsFile);
        Dictionary<string, int> traitIndex = Intern(TraitsFileName, "$.traits", traits.Traits, trait => trait.Id);
        CheckTraitLists(traits);
        CheckForbiddenPairs(traits, traitIndex);
        ActivitiesFile activities = Read(root, ActivitiesFileName, json.ActivitiesFile);
        Dictionary<string, int> activityIndex = Intern(ActivitiesFileName, "$.activities", activities.Activities, activity => activity.Id);
        CheckActivityLists(activities);
        ThoughtsFile thoughts = Read(root, ThoughtsFileName, json.ThoughtsFile);
        _ = Intern(ThoughtsFileName, "$.kinds", thoughts.Kinds, kind => kind.Id);

        return new ContentSet
        {
            Layouts = ReadDirectory(root, LayoutsDirectory, json.CabinLayout, layout => layout.Id, CheckLayout),
            Needs = MapNeeds(Read(root, NeedsFileName, json.NeedsFile)),
            Traits = traits,
            TraitIds = traitIndex.ToDictionary(pair => pair.Key, pair => new TraitId(pair.Value), StringComparer.Ordinal),
            Activities = activities,
            ActivityIds = activityIndex.ToDictionary(pair => pair.Key, pair => new ActivityId(pair.Value), StringComparer.Ordinal),
            ActivityModules = ReadModules(root, activities.Activities),
            Crew = ReadCrew(root),
            Scenarios = ReadDirectory(root, ScenariosDirectory, json.ScenarioFile, scenario => scenario.Id, CheckScenario),
            Thoughts = thoughts,
            Hash = ContentHash.Compute(root),
        };
    }

    private static string FullRoot(string directory)
    {
        try
        {
            return Path.GetFullPath(directory);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or IOException)
        {
            throw new ContentLoadException(".", null, $"Expected a usable content root path; got \"{directory}\": {exception.Message}", exception);
        }
    }

    private static T Read<T>(string root, string relative, JsonTypeInfo<T> typeInfo)
        where T : class
    {
        string path = Path.Combine(root, relative);
        if (!File.Exists(path))
        {
            throw new ContentLoadException(relative, null, "Expected this file under the content root; there is none.", null);
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            T? value = JsonSerializer.Deserialize(stream, typeInfo);
            return value ?? throw new ContentLoadException(relative, "$", "Expected a JSON object; got null.", null);
        }
        catch (JsonException exception)
        {
            throw new ContentLoadException(relative, exception.Path ?? "$", $"Expected JSON matching the schema; {exception.Message}", exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ContentLoadException(relative, null, $"Expected a readable file; {exception.Message}", exception);
        }
    }

    private static Dictionary<string, T> ReadDirectory<T>(
        string root,
        string directory,
        JsonTypeInfo<T> typeInfo,
        Func<T, string> idOf,
        Action<string, T> checkLists
    )
        where T : class
    {
        Dictionary<string, T> items = new(StringComparer.Ordinal);
        foreach (string name in JsonFileNames(root, directory))
        {
            string relative = $"{directory}/{name}";
            T item = Read(root, relative, typeInfo);
            string expected = Path.GetFileNameWithoutExtension(name);
            string id = idOf(item);
            if (!string.Equals(id, expected, StringComparison.Ordinal))
            {
                throw new ContentLoadException(relative, "$.id", $"Expected the id \"{expected}\", the file's name; got \"{id}\".", null);
            }

            checkLists(relative, item);
            items.Add(id, item);
        }

        return items;
    }

    private static string[] JsonFileNames(string root, string directory)
    {
        string full = Path.Combine(root, directory);
        if (!Directory.Exists(full))
        {
            throw new ContentLoadException(directory, null, "Expected this directory under the content root; there is none.", null);
        }

        try
        {
            return [.. Directory.EnumerateFiles(full, "*.json").Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal)];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ContentLoadException(directory, null, $"Expected a readable directory; {exception.Message}", exception);
        }
    }

    private static void NoNulls<T>(string file, string path, IReadOnlyList<T?> items)
        where T : class
    {
        for (int position = 0; position < items.Count; position++)
        {
            if (items[position] is null)
            {
                throw new ContentLoadException(file, $"{path}[{position}]", "Expected an entry; got null.", null);
            }
        }
    }

    private static void CheckLayout(string file, CabinLayout layout)
    {
        NoNulls(file, "$.rows", layout.Rows);
        for (int row = 0; row < layout.Rows.Count; row++)
        {
            IReadOnlyList<SeatGroup> groups = layout.Rows[row].Groups;
            NoNulls(file, $"$.rows[{row}].groups", groups);
            for (int group = 0; group < groups.Count; group++)
            {
                NoNulls(file, $"$.rows[{row}].groups[{group}].seats", groups[group].Seats);
            }
        }

        NoNulls(file, "$.aisles", layout.Aisles);
        NoNulls(file, "$.fixtures", layout.Fixtures);
    }

    private static void CheckScenario(string file, ScenarioFile scenario)
    {
        NoNulls(file, "$.service_plan", scenario.ServicePlan);
        NoNulls(file, "$.locked_lavs", scenario.LockedLavs);
        NoNulls(file, "$.lighting_plan", scenario.LightingPlan);
        NoNulls(file, "$.crew.zones", scenario.Crew.Zones);
        for (int zone = 0; zone < scenario.Crew.Zones.Count; zone++)
        {
            NoNulls(file, $"$.crew.zones[{zone}].stations", scenario.Crew.Zones[zone].Stations);
            NoNulls(file, $"$.crew.zones[{zone}].crew", scenario.Crew.Zones[zone].Crew);
        }

        NoNulls(file, "$.crew.carts", scenario.Crew.Carts);
        for (int cart = 0; cart < scenario.Crew.Carts.Count; cart++)
        {
            NoNulls(file, $"$.crew.carts[{cart}].crew", scenario.Crew.Carts[cart].Crew);
        }

        NoNulls(file, "$.timeline.entries", scenario.Timeline.Entries);
    }

    private static void CheckTraitLists(TraitsFile traits)
    {
        for (int position = 0; position < traits.Traits.Count; position++)
        {
            NoNulls(TraitsFileName, $"$.traits[{position}].modifiers", traits.Traits[position].Modifiers);
        }
    }

    private static void CheckActivityLists(ActivitiesFile activities)
    {
        for (int position = 0; position < activities.Activities.Count; position++)
        {
            NoNulls(ActivitiesFileName, $"$.activities[{position}].effects", activities.Activities[position].Effects);
        }

        NoNulls(ActivitiesFileName, "$.call_reasons", activities.CallReasons);
        for (int position = 0; position < activities.CallReasons.Count; position++)
        {
            NoNulls(ActivitiesFileName, $"$.call_reasons[{position}].granted", activities.CallReasons[position].Granted);
            NoNulls(ActivitiesFileName, $"$.call_reasons[{position}].refused", activities.CallReasons[position].Refused);
        }
    }

    private static Dictionary<string, int> Intern<T>(string file, string arrayPath, IReadOnlyList<T> items, Func<T, string> idOf)
        where T : class
    {
        Dictionary<string, int> index = new(StringComparer.Ordinal);
        for (int position = 0; position < items.Count; position++)
        {
            T item = items[position] ?? throw new ContentLoadException(file, $"{arrayPath}[{position}]", "Expected an object; got null.", null);
            string id = idOf(item);
            if (!index.TryAdd(id, position))
            {
                string first = $"{arrayPath}[{index[id]}]";
                throw new ContentLoadException(
                    file,
                    $"{arrayPath}[{position}].id",
                    $"Expected a unique id; \"{id}\" is already declared at {first}.",
                    null
                );
            }
        }

        return index;
    }

    private static void RequireKnown(string file, string path, string id, Dictionary<string, int> known, string expected)
    {
        if (!known.ContainsKey(id))
        {
            throw new ContentLoadException(file, path, $"Unknown id \"{id}\"; expected {expected}.", null);
        }
    }

    private static void CheckForbiddenPairs(TraitsFile traits, Dictionary<string, int> traitIndex)
    {
        const string Expected = "a trait declared in traits.json";
        for (int position = 0; position < traits.ForbiddenPairs.Count; position++)
        {
            string path = $"$.forbidden_pairs[{position}]";
            TraitPair pair =
                traits.ForbiddenPairs[position] ?? throw new ContentLoadException(TraitsFileName, path, "Expected a pair; got null.", null);
            RequireKnown(TraitsFileName, $"{path}.first", pair.First, traitIndex, Expected);
            RequireKnown(TraitsFileName, $"{path}.second", pair.Second, traitIndex, Expected);
        }
    }

    private static CrewFile ReadCrew(string root)
    {
        CrewFile crew = Read(root, CrewFileName, ContentJsonContext.Default.CrewFile);
        Dictionary<string, int> traitIndex = Intern(CrewFileName, "$.traits", crew.Traits, trait => trait.Id);
        _ = Intern(CrewFileName, "$.roster", crew.Roster, member => member.Id);
        for (int position = 0; position < crew.Roster.Count; position++)
        {
            if (crew.Roster[position].Trait is { } trait)
            {
                RequireKnown(CrewFileName, $"$.roster[{position}].trait", trait, traitIndex, "a crew trait declared in crew.json's traits");
            }
        }

        return crew;
    }

    private static ActivityModuleSource[] ReadModules(string root, IReadOnlyList<ActivitySpec> activities)
    {
        var modules = new ActivityModuleSource[activities.Count];
        for (int position = 0; position < activities.Count; position++)
        {
            modules[position] = ReadModule(root, activities[position].Id, position);
        }

        return modules;
    }

    private static ActivityModuleSource ReadModule(string root, string id, int position)
    {
        string declared = $"$.activities[{position}]";
        if (!IsFileNameId(id))
        {
            string expected = $"Expected an id of lowercase letters, digits, '_' and '-', since it names the module's file; got \"{id}\".";
            throw new ContentLoadException(ActivitiesFileName, $"{declared}.id", expected, null);
        }

        string relative = $"{ModulesDirectory}/{id}.lua";
        string path = Path.Combine(root, relative);
        if (!File.Exists(path))
        {
            string expected = $"Expected the Lua module of activity \"{id}\" ({ActivitiesFileName} at {declared}) at {relative}; there is none.";
            throw new ContentLoadException(relative, null, expected, null);
        }

        try
        {
            return new ActivityModuleSource(id, File.ReadAllText(path, StrictUtf8));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            throw new ContentLoadException(
                relative,
                null,
                $"Expected readable UTF-8 Lua source for activity \"{id}\"; {exception.Message}",
                exception
            );
        }
    }

    private static bool IsFileNameId(string id) => id.Length > 0 && id.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or '-');

    private static NeedsContent MapNeeds(NeedsFile file)
    {
        NoNulls(NeedsFileName, "$.cascade_rules", file.CascadeRules);
        NoNulls(NeedsFileName, "$.distress_terms", file.DistressTerms);
        NeedRates rates = Engine("$.rates", () => new NeedRates(file.Rates));
        Cascades cascades = Engine("$.cascade_rules", () => new Cascades(file.CascadeRules));
        Distress distress = Engine("$.distress_terms", () => new Distress(file.DistressTerms));
        NodeCapacities capacities = Engine("$.node_capacities", () => ToEngine(file.NodeCapacities));
        _ = Intern(NeedsFileName, "$.incidents", file.Incidents, incident => incident.Id);
        for (int position = 0; position < file.Incidents.Count; position++)
        {
            IncidentKindSpec incident = file.Incidents[position];
            long windowTicks = (long)incident.WindowMinutes * SimTime.TicksPerSimMinute;
            _ = Engine($"$.incidents[{position}]", () => new SustainGate(incident.Threshold, windowTicks, file.IncidentResetMargin));
        }

        return new NeedsContent(file, rates, cascades, distress, capacities);
    }

    private static NodeCapacities ToEngine(NodeCapacitiesSpec spec) =>
        new()
        {
            AisleSlot = spec.AisleSlot,
            Seat = spec.Seat,
            Door = spec.Door,
            Lav = spec.Lav,
            LavQueue = spec.LavQueue,
            Galley = spec.Galley,
        };

    private static T Engine<T>(string path, Func<T> build)
    {
        try
        {
            return build();
        }
        catch (ArgumentException exception)
        {
            throw new ContentLoadException(NeedsFileName, path, $"Expected a value the Engine accepts; {exception.Message}", exception);
        }
    }
}
