using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Sky.Content.Schema;
using Sky.Engine.Cabin;
using Sky.Engine.Manifest;
using Sky.Engine.Needs;
using Sky.Engine.Passengers;
using Sky.Engine.Time;
using ManifestTraitPair = Sky.Engine.Manifest.TraitPair;

namespace Sky.Content;

/// <summary>
/// Loads a content tree into a <see cref="ContentSet"/>. Under the content root: <c>layouts/&lt;id&gt;.json</c>,
/// <c>manifest.json</c>, <c>needs.json</c>, <c>traits.json</c>, <c>activities.json</c> with <c>activities/&lt;id&gt;.lua</c>
/// per activity, <c>crew.json</c>, <c>scenarios/&lt;id&gt;.json</c> and <c>thoughts.json</c>. Every failure stops the load with
/// a <see cref="ContentLoadException"/> naming the file and the JSON path (R10); ranges and cross-file references are the
/// validators' to check.
/// </summary>
public static class ContentLoader
{
    private const string NeedsFileName = "needs.json";
    private const string ManifestFileName = "manifest.json";
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
        ManifestFile manifest = Read(root, ManifestFileName, json.ManifestFile);
        Dictionary<string, int> professionIndex = Intern(ManifestFileName, "$.professions", manifest.Professions, profession => profession.Id);

        return new ContentSet
        {
            Layouts = ReadDirectory(root, LayoutsDirectory, json.CabinLayout, layout => layout.Id, CheckLayout),
            Needs = MapNeeds(Read(root, NeedsFileName, json.NeedsFile)),
            Manifest = MapManifest(manifest, traits, traitIndex),
            ProfessionIds = professionIndex.ToDictionary(pair => pair.Key, pair => new ProfessionId(pair.Value), StringComparer.Ordinal),
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
            Schema.TraitPair pair =
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
        NeedRates rates = Engine(NeedsFileName, "$.rates", () => new NeedRates(file.Rates));
        Cascades cascades = Engine(NeedsFileName, "$.cascade_rules", () => new Cascades(file.CascadeRules));
        Distress distress = Engine(NeedsFileName, "$.distress_terms", () => new Distress(file.DistressTerms));
        NodeCapacities capacities = Engine(NeedsFileName, "$.node_capacities", () => ToEngine(file.NodeCapacities));
        _ = Intern(NeedsFileName, "$.incidents", file.Incidents, incident => incident.Id);
        for (int position = 0; position < file.Incidents.Count; position++)
        {
            IncidentKindSpec incident = file.Incidents[position];
            long windowTicks = (long)incident.WindowMinutes * SimTime.TicksPerSimMinute;
            _ = Engine(NeedsFileName, $"$.incidents[{position}]", () => new SustainGate(incident.Threshold, windowTicks, file.IncidentResetMargin));
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

    private static T Engine<T>(string file, string path, Func<T> build)
    {
        try
        {
            return build();
        }
        catch (ArgumentException exception)
        {
            throw new ContentLoadException(file, path, $"Expected a value the Engine accepts; {exception.Message}", exception);
        }
    }

    private static ManifestRules MapManifest(ManifestFile file, TraitsFile traits, Dictionary<string, int> traitIndex)
    {
        ManifestRules rules = BuildManifest(file, ManifestTraitParts(traits, traitIndex));
        CheckAcrossFields(rules);
        return rules;
    }

    /// <summary>
    /// Builds the rules from both files. One <see cref="Engine{T}"/> call wraps the whole construction, because each field
    /// is checked by its own <c>init</c> accessor, which runs as the initializer assigns it: the parameter name the check
    /// reports is the field, and the field's JSON path is that name as the schema spells it, snake_case.
    /// </summary>
    /// <param name="file">The manifest file.</param>
    /// <param name="traitParts">The trait-side parts, read from <c>traits.json</c>.</param>
    /// <returns>The rules.</returns>
    /// <exception cref="ContentLoadException">A value the Engine refuses, named by its file and JSON path.</exception>
    private static ManifestRules BuildManifest(ManifestFile file, ManifestTraits traitParts)
    {
        try
        {
            return new ManifestRules
            {
                LoadFactor = new ShareRange(file.LoadFactor.Min, file.LoadFactor.Max),
                BusinessBooked = new IntRange(file.BusinessBooked.Min, file.BusinessBooked.Max),
                BusinessRowCount = file.BusinessRowCount,
                BusinessCabinPurposes = ToOptions(file.BusinessCabinPurposes),
                EconomyCabinPurposes = ToOptions(file.EconomyCabinPurposes),
                BusinessTrip = BuildTrip(file.BusinessTrip, "$.business_trip"),
                LeisureTrip = BuildTrip(file.LeisureTrip, "$.leisure_trip"),
                VisitingTrip = BuildTrip(file.VisitingTrip, "$.visiting_trip"),
                FamilyMinimumSize = file.FamilyMinimumSize,
                FamilyAdults = file.FamilyAdults,
                WakeSpreadMinutes = file.WakeSpreadMinutes,
                AdultTraitCounts = ToOptions(file.AdultTraitCounts),
                ChildTrait = traitParts.Child,
                ChildExtraTraitShare = file.ChildExtraTraitShare,
                Traits = traitParts.Traits,
                ForbiddenPairs = traitParts.Pairs,
                Belongings = traitParts.Belongings,
                Professions = ToProfessions(file.Professions),
            };
        }
        catch (ArgumentException exception)
        {
            (string source, string path) = Location(exception.ParamName);
            throw new ContentLoadException(source, path, $"Expected a value the Engine accepts; {exception.Message}", exception);
        }
    }

    private static TripPurposeRules BuildTrip(TripSpec trip, string path) =>
        Engine(
            ManifestFileName,
            path,
            () =>
                new TripPurposeRules
                {
                    GroupSizes = ToOptions(trip.GroupSizes),
                    FamilyShare = trip.FamilyShare,
                    WakeMinutes = new IntRange(trip.WakeMinutes.Min, trip.WakeMinutes.Max),
                }
        );

    /// <summary>Finds the file and JSON path a refused field belongs to, from the parameter name its check reports.</summary>
    /// <param name="field">The field's name, as the Engine's check reports it.</param>
    /// <returns>The file the field was read from, and its path in that file.</returns>
    private static (string File, string Path) Location(string? field) =>
        field switch
        {
            nameof(ManifestRules.Traits)
            or nameof(ManifestRules.Belongings)
            or nameof(ManifestRules.ForbiddenPairs)
            or nameof(ManifestRules.ChildTrait) => (TraitsFileName, "$.traits"),
            null => (ManifestFileName, "$"),
            _ => (ManifestFileName, $"$.{JsonName(field)}"),
        };

    /// <summary>Spells a member's name the way the schema's files do: snake_case, the serializer context's naming policy.</summary>
    /// <param name="member">The member's name.</param>
    /// <returns>The JSON property name.</returns>
    private static string JsonName(string member)
    {
        StringBuilder name = new(member.Length + 4);
        foreach (char character in member)
        {
            if (char.IsAsciiLetterUpper(character) && name.Length > 0)
            {
                name.Append('_');
            }

            name.Append(char.ToLowerInvariant(character));
        }

        return name.ToString();
    }

    private static IReadOnlyList<WeightedOption<T>> ToOptions<T>(IReadOnlyList<WeightedOptionSpec<T>> options) =>
        [.. options.Select(option => new WeightedOption<T>(option.Value, option.Weight))];

    private static IReadOnlyList<ProfessionRule> ToProfessions(IReadOnlyList<ProfessionSpec> professions) =>
        [
            .. professions.Select(
                (profession, position) => new ProfessionRule(new ProfessionId(position), profession.Weight, profession.OnBusinessTrips)
            ),
        ];

    /// <summary>
    /// Maps the trait parts of the manifest rules, which <c>traits.json</c> carries: the one trait every child is given,
    /// the traits an adult draws, the belongings and the forbidden pairs. Their ids are the file's interned indices.
    /// </summary>
    /// <param name="traits">The traits file.</param>
    /// <param name="traitIndex">Each trait's interned index, by content id.</param>
    /// <returns>The trait-side rules.</returns>
    /// <exception cref="ContentLoadException">
    /// The file has no child trait, or more than one, or a child-optional trait with no adult weight.
    /// </exception>
    private static ManifestTraits ManifestTraitParts(TraitsFile traits, Dictionary<string, int> traitIndex)
    {
        (IReadOnlyList<TraitRule> rules, IReadOnlyList<BelongingRule> belongings) = TraitParts(traits);
        IReadOnlyList<ManifestTraitPair> pairs =
        [
            .. traits.ForbiddenPairs.Select(pair => new ManifestTraitPair(new TraitId(traitIndex[pair.First]), new TraitId(traitIndex[pair.Second]))),
        ];
        return new ManifestTraits(ChildTrait(traits), rules, belongings, pairs);
    }

    private static TraitId ChildTrait(TraitsFile traits)
    {
        int found = 0;
        int position = 0;
        for (int index = 0; index < traits.Traits.Count; index++)
        {
            if (traits.Traits[index].GivenToEveryChild)
            {
                found++;
                position = index;
            }
        }

        if (found != 1)
        {
            string expected = $"Expected exactly one trait with given_to_every_child; found {found}.";
            throw new ContentLoadException(TraitsFileName, "$.traits", expected, null);
        }

        return new TraitId(position);
    }

    private static (IReadOnlyList<TraitRule> Traits, IReadOnlyList<BelongingRule> Belongings) TraitParts(TraitsFile traits)
    {
        List<TraitRule> rules = [];
        List<BelongingRule> belongings = [];
        for (int position = 0; position < traits.Traits.Count; position++)
        {
            TraitSpec spec = traits.Traits[position];
            if (spec.ChildOptional && spec.AdultWeight is null)
            {
                string expected = "Expected an adult_weight on a child_optional trait; a child's extra trait is drawn by it.";
                throw new ContentLoadException(TraitsFileName, $"$.traits[{position}].adult_weight", expected, null);
            }

            TraitId id = new(position);
            if (spec.Share is { } share)
            {
                belongings.Add(new BelongingRule(id, share.Adults, share.BusinessTrips, share.Children));
            }
            else if (!spec.GivenToEveryChild && spec.AdultWeight is { } weight)
            {
                rules.Add(new TraitRule(id, weight, spec.BusinessTripWeight ?? weight, spec.ChildOptional));
            }
        }

        return (rules, belongings);
    }

    private static void CheckAcrossFields(ManifestRules rules)
    {
        try
        {
            rules.CheckAcrossFields();
        }
        catch (ArgumentException exception)
        {
            (string source, string path) = Location(exception.ParamName);
            throw new ContentLoadException(source, path, $"Expected a value the Engine accepts; {exception.Message}", exception);
        }
    }

    /// <summary>The trait-side parts of the manifest rules, all read from <c>traits.json</c>.</summary>
    /// <param name="Child">The one trait every child is given.</param>
    /// <param name="Traits">The traits an adult draws.</param>
    /// <param name="Belongings">The belongings, each rolled on its own after the traits.</param>
    /// <param name="Pairs">The pairs of traits no passenger carries together.</param>
    private sealed record ManifestTraits(
        TraitId Child,
        IReadOnlyList<TraitRule> Traits,
        IReadOnlyList<BelongingRule> Belongings,
        IReadOnlyList<ManifestTraitPair> Pairs
    );
}
