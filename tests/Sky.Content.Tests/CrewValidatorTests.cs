using System.Globalization;
using Sky.Content.Schema;
using Sky.Content.Validation;

namespace Sky.Content.Tests;

/// <summary>
/// Pins the crew validator: a crew trait's <c>free_zones</c>, the zones the trait covers free of covering strain
/// (<c>crew.md</c>: the Floater trait), is 0 or more, and a trait that leaves the field out reads 0; a roster entry's
/// <c>competence</c> and <c>empathy</c> lie in [0, 1] and its <c>starting_fatigue</c> in [0, 100] (<c>crew.md</c> "The crew
/// model"); every trait factor and every <c>fatigue</c> and <c>service</c> value lies in its inclusive range of
/// <c>balance.md</c> section 3.12, both ends accepted and NaN refused; every refusal names <c>crew.json</c>, the JSON path
/// of the field and the field (R10).
/// </summary>
public sealed class CrewValidatorTests
{
    private const string CrewFile = "crew.json";

    /// <summary>The fixture's one trait, ending at its last field.</summary>
    private const string SteadyTrait = """ "preemption_strain_factor": 0.5 }""";

    /// <summary>The fixture's one trait, whole.</summary>
    private const string SteadyTraitObject = """{ "id": "steady", "focus_fatigue_weight_factor": 0.5, "preemption_strain_factor": 0.5 }""";

    /// <summary>How far outside a bound the refused rows sit.</summary>
    private const double Step = 0.01;

    /// <summary>
    /// The inclusive ranges of <c>balance.md</c> section 3.12, written here rather than read from the validator so a
    /// widened range there fails a row; each path is where the fixture's <c>crew.json</c> holds the field.
    /// </summary>
    private static readonly (string Path, double Min, double Max)[] FieldRanges =
    [
        ("$.traits[0].dwell_factor", 0.25, 2.0),
        ("$.traits[0].unease_relief_factor", 0.5, 2.0),
        ("$.traits[0].service_time_factor", 0.5, 1.5),
        ("$.traits[0].focus_fatigue_weight_factor", 0, 1.6),
        ("$.traits[0].focus_factor_over_redline", 0.2, 1.0),
        ("$.traits[0].preemption_strain_factor", 0, 2.0),
        ("$.traits[0].on_task_strain_factor", 0.5, 1.5),
        ("$.service.cart_speed_factor", 0.5, 1.0),
        ("$.service.drinks_seconds_per_row", 20, 120),
        ("$.service.meal_seconds_per_row", 35, 105),
        ("$.service.sign_on_return_minutes", 1, 15),
        ("$.service.hand_service_lead_minutes", 0, 5),
        ("$.service.hand_drinks_seconds_per_row", 30, 120),
        ("$.service.hand_meal_seconds_per_row", 60, 240),
        ("$.fatigue.rise_per_hour", 0, 20),
        ("$.fatigue.over_redline_factor", 1, 4),
        ("$.fatigue.break_fall_per_minute", 0, 1),
    ];

    /// <summary>A trait that carries no free_zones reads 0 and is accepted.</summary>
    [Fact]
    public void FreeZonesDefaultsToZero()
    {
        using var tree = ContentTree.Minimal();

        ContentSet content = tree.Load();
        new CrewValidator().Validate(content);

        Assert.Equal(0, content.Crew.Traits.Single(trait => trait.Id == "steady").FreeZones);
    }

    /// <summary>A negative free_zones is refused at the field, naming the trait and the field.</summary>
    [Fact]
    public void NegativeFreeZonesIsRefused()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace(CrewFile, SteadyTrait, """ "preemption_strain_factor": 0.5, "free_zones": -1 }""");

        ContentSet content = tree.Load();
        ContentLoadException error = Refusal(content);

        Assert.Equal(CrewFile, error.File);
        Assert.Equal("$.traits[0].free_zones", error.JsonPath);
        Assert.Contains("free_zones", error.Message, StringComparison.Ordinal);
        Assert.Contains("steady", error.Message, StringComparison.Ordinal);
    }

    /// <summary>A competence above 1 is refused at the field, naming the crew member.</summary>
    [Fact]
    public void CompetenceAboveOneIsRefused()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace(CrewFile, "\"competence\": 0.85", "\"competence\": 1.5");

        ContentLoadException error = Refusal(tree.Load());

        Assert.Equal(CrewFile, error.File);
        Assert.Equal("$.roster[0].competence", error.JsonPath);
        Assert.Contains("purser", error.Message, StringComparison.Ordinal);
    }

    /// <summary>A negative empathy is refused at the field, naming the crew member.</summary>
    [Fact]
    public void NegativeEmpathyIsRefused()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace(CrewFile, "\"empathy\": 0.7", "\"empathy\": -0.1");

        ContentLoadException error = Refusal(tree.Load());

        Assert.Equal(CrewFile, error.File);
        Assert.Equal("$.roster[0].empathy", error.JsonPath);
        Assert.Contains("purser", error.Message, StringComparison.Ordinal);
    }

    /// <summary>A starting_fatigue above 100 is refused at the field, naming the crew member.</summary>
    [Fact]
    public void StartingFatigueAboveHundredIsRefused()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace(CrewFile, "\"starting_fatigue\": 20", "\"starting_fatigue\": 101");

        ContentLoadException error = Refusal(tree.Load());

        Assert.Equal(CrewFile, error.File);
        Assert.Equal("$.roster[0].starting_fatigue", error.JsonPath);
        Assert.Contains("purser", error.Message, StringComparison.Ordinal);
    }

    /// <summary>Both ends of each roster range are accepted: 0 and 1 for competence and empathy, 0 and 100 for fatigue.</summary>
    [Fact]
    public void BoundaryValuesAreAccepted()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace(CrewFile, "\"competence\": 0.85", "\"competence\": 0");
        tree.Replace(CrewFile, "\"competence\": 0.7", "\"competence\": 1");
        tree.Replace(CrewFile, "\"empathy\": 0.7", "\"empathy\": 1");
        tree.Replace(CrewFile, "\"empathy\": 0.4", "\"empathy\": 0");
        tree.Replace(CrewFile, "\"starting_fatigue\": 20", "\"starting_fatigue\": 0");
        tree.Replace(CrewFile, "\"starting_fatigue\": 30", "\"starting_fatigue\": 100");

        ContentSet content = tree.Load();
        new CrewValidator().Validate(content);

        Assert.Equal(0, content.Crew.Roster[0].Competence);
        Assert.Equal(1, content.Crew.Roster[1].Competence);
        Assert.Equal(1, content.Crew.Roster[0].Empathy);
        Assert.Equal(0, content.Crew.Roster[1].Empathy);
        Assert.Equal(0, content.Crew.Roster[0].StartingFatigue);
        Assert.Equal(100, content.Crew.Roster[1].StartingFatigue);
    }

    /// <summary>
    /// Gets one row just below the minimum and one just above the maximum of every field in <c>balance.md</c> section 3.12's
    /// table.
    /// </summary>
    public static TheoryData<string, double> OutsideRows
    {
        get
        {
            var rows = new TheoryData<string, double>();
            foreach ((string Path, double Min, double Max) range in FieldRanges)
            {
                rows.Add(range.Path, range.Min - Step);
                rows.Add(range.Path, range.Max + Step);
            }

            return rows;
        }
    }

    /// <summary>Gets both ends of every field in <c>balance.md</c> section 3.12's table.</summary>
    public static TheoryData<string, double> BoundRows
    {
        get
        {
            var rows = new TheoryData<string, double>();
            foreach ((string Path, double Min, double Max) range in FieldRanges)
            {
                rows.Add(range.Path, range.Min);
                rows.Add(range.Path, range.Max);
            }

            return rows;
        }
    }

    /// <summary>A value just outside its field's range is refused at the field, naming it.</summary>
    /// <param name="path">The field's JSON path in the fixture's <c>crew.json</c>.</param>
    /// <param name="value">The value written there.</param>
    [Theory]
    [MemberData(nameof(OutsideRows))]
    public void ValueOutsideItsRangeIsRefused(string path, double value)
    {
        using var tree = ContentTree.Minimal();
        SetValue(tree, path, value);

        ContentLoadException error = Refusal(tree.Load());

        Assert.Equal(CrewFile, error.File);
        Assert.Equal(path, error.JsonPath);
        Assert.Contains(path[(path.LastIndexOf('.') + 1)..], error.Message, StringComparison.Ordinal);
    }

    /// <summary>A value on either end of its field's range is accepted.</summary>
    /// <param name="path">The field's JSON path in the fixture's <c>crew.json</c>.</param>
    /// <param name="value">The value written there.</param>
    [Theory]
    [MemberData(nameof(BoundRows))]
    public void ValueOnEitherBoundIsAccepted(string path, double value)
    {
        using var tree = ContentTree.Minimal();
        SetValue(tree, path, value);

        new CrewValidator().Validate(tree.Load());
    }

    /// <summary>A NaN is refused at the field; no JSON file carries NaN, so the loaded set is rebuilt.</summary>
    [Fact]
    public void NotANumberIsRefused()
    {
        using var tree = ContentTree.Minimal();
        ContentSet loaded = tree.Load();
        CrewFile crew = loaded.Crew with { Service = loaded.Crew.Service with { CartSpeedFactor = double.NaN } };

        ContentLoadException error = Refusal(WithCrew(loaded, crew));

        Assert.Equal(CrewFile, error.File);
        Assert.Equal("$.service.cart_speed_factor", error.JsonPath);
        Assert.Contains("got NaN", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Writes <paramref name="value"/> at <paramref name="path"/> in the fixture's <c>crew.json</c>: a trait field replaces
    /// the one trait with a trait carrying that field alone, so the others read their default 1; a block field replaces the
    /// number the fixture holds there.
    /// </summary>
    private static void SetValue(ContentTree tree, string path, double value)
    {
        string field = path[(path.LastIndexOf('.') + 1)..];
        string number = value.ToString("R", CultureInfo.InvariantCulture);
        if (path.StartsWith("$.traits[0].", StringComparison.Ordinal))
        {
            tree.Replace(CrewFile, SteadyTraitObject, $$"""{ "id": "steady", "{{field}}": {{number}} }""");
            return;
        }

        string file = Path.Combine(tree.Root, CrewFile);
        string text = File.ReadAllText(file);
        string key = $"\"{field}\": ";
        int start = text.IndexOf(key, StringComparison.Ordinal);
        Assert.True(start >= 0, $"The fixture's crew.json holds no {field}.");
        start += key.Length;
        int end = text.IndexOfAny([',', ' ', '}', '\r', '\n'], start);
        tree.Write(CrewFile, string.Concat(text.AsSpan(0, start), number, text.AsSpan(end)));
    }

    /// <summary>The loaded set with its crew file replaced.</summary>
    private static ContentSet WithCrew(ContentSet content, CrewFile crew) =>
        new()
        {
            Hash = content.Hash,
            Layouts = content.Layouts,
            Needs = content.Needs,
            Manifest = content.Manifest,
            ProfessionIds = content.ProfessionIds,
            Traits = content.Traits,
            TraitIds = content.TraitIds,
            Activities = content.Activities,
            ActivityIds = content.ActivityIds,
            ActivityModules = content.ActivityModules,
            Crew = crew,
            Scenarios = content.Scenarios,
            Thoughts = content.Thoughts,
        };

    private static ContentLoadException Refusal(ContentSet content) =>
        Assert.Throws<ContentLoadException>(() => new CrewValidator().Validate(content));
}
