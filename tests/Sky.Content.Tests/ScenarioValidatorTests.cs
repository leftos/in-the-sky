using Sky.Content.Validation;

namespace Sky.Content.Tests;

/// <summary>
/// Pins the scenario validator: the layout it names is loaded, the gate delay lies in 0 to 240 minutes, locked lavs are lavs,
/// the zones cover every row once with crew from the roster, the crew count matches the zones, stations are fixtures and
/// cart spans lie within the layout; each refusal names the scenario's file and the JSON path of the offending value (R10).
/// </summary>
public sealed class ScenarioValidatorTests
{
    private const string ScenarioFile = "scenarios/ref.json";

    private const string ZoneStationsAndCrew = """ "stations": [ "door-fwd", "lav-fwd" ], "crew": [ "purser", "fa2" ] }""";

    /// <summary>A scenario naming a layout that is not loaded is refused at its layout.</summary>
    [Fact]
    public void UnknownLayoutIsRefused()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace(ScenarioFile, "\"layout\": \"tiny\"", "\"layout\": \"wide\"");

        ContentLoadException error = Refusal(tree);

        Assert.Equal(ScenarioFile, error.File);
        Assert.Equal("$.layout", error.JsonPath);
    }

    /// <summary>A gate delay outside 0 to 240 minutes is refused at the field.</summary>
    /// <param name="minutes">The delay.</param>
    [Theory]
    [InlineData(-1)]
    [InlineData(241)]
    public void GateDelayOutOfRangeIsRefused(int minutes)
    {
        using var tree = ContentTree.Minimal();
        tree.Replace(ScenarioFile, "\"gate_delay_minutes\": 0", $"\"gate_delay_minutes\": {minutes}");

        ContentLoadException error = Refusal(tree);

        Assert.Equal(ScenarioFile, error.File);
        Assert.Equal("$.gate_delay_minutes", error.JsonPath);
    }

    /// <summary>A gate delay at either end of 0 to 240 minutes is accepted.</summary>
    /// <param name="minutes">The delay.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(240)]
    public void GateDelayAtTheBoundsIsAccepted(int minutes)
    {
        using var tree = ContentTree.Minimal();
        tree.Replace(ScenarioFile, "\"gate_delay_minutes\": 0", $"\"gate_delay_minutes\": {minutes}");

        new ScenarioValidator().Validate(tree.Load());
    }

    /// <summary>A locked lav that is the layout's door is refused at the entry.</summary>
    [Fact]
    public void LockedLavThatIsNotALavIsRefused()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace(ScenarioFile, "\"locked_lavs\": []", "\"locked_lavs\": [ \"door-fwd\" ]");

        ContentLoadException error = Refusal(tree);

        Assert.Equal(ScenarioFile, error.File);
        Assert.Equal("$.locked_lavs[0]", error.JsonPath);
    }

    /// <summary>A layout row no zone covers is refused at the zones, naming the row.</summary>
    [Fact]
    public void ZoneGapIsRefused()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace(
            "layouts/tiny.json",
            "\"rows\": [",
            """ "rows": [ { "pitch_inches": 30, "groups": [ { "left_inches": 2, "seats": [ { "label": "A", "width_inches": 18 } ] } ] },"""
        );

        ContentLoadException error = Refusal(tree);

        Assert.Equal(ScenarioFile, error.File);
        Assert.Equal("$.crew.zones", error.JsonPath);
        Assert.Contains("row 1", error.Message, StringComparison.Ordinal);
    }

    /// <summary>A second zone covering a row the first already covers is refused at the second zone.</summary>
    [Fact]
    public void ZoneOverlapIsRefused()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace(
            ScenarioFile,
            ZoneStationsAndCrew,
            $$"""{{ZoneStationsAndCrew}}, { "id": "A", "first_row": 0, "last_row": 0, "stations": [], "crew": [ "fa2" ] }"""
        );

        ContentLoadException error = Refusal(tree);

        Assert.Equal(ScenarioFile, error.File);
        Assert.Equal("$.crew.zones[1]", error.JsonPath);
    }

    /// <summary>A zone with no crew is refused at its crew list.</summary>
    [Fact]
    public void ZoneWithoutCrewIsRefused()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace(ScenarioFile, ZoneStationsAndCrew, """ "stations": [ "door-fwd", "lav-fwd" ], "crew": [] }""");

        ContentLoadException error = Refusal(tree);

        Assert.Equal(ScenarioFile, error.File);
        Assert.Equal("$.crew.zones[0].crew", error.JsonPath);
    }

    /// <summary>A zone crew id missing from <c>crew.json</c>'s roster is refused at the id.</summary>
    [Fact]
    public void UnknownCrewIdIsRefused()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace(ScenarioFile, ZoneStationsAndCrew, """ "stations": [ "door-fwd", "lav-fwd" ], "crew": [ "purser", "fa9" ] }""");

        ContentLoadException error = Refusal(tree);

        Assert.Equal(ScenarioFile, error.File);
        Assert.Equal("$.crew.zones[0].crew[1]", error.JsonPath);
        Assert.Contains("fa9", error.Message, StringComparison.Ordinal);
    }

    /// <summary>A crew count other than the number of distinct crew across the zones is refused at the count.</summary>
    [Fact]
    public void CrewCountMismatchIsRefused()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace(ScenarioFile, "\"count\": 2", "\"count\": 3");

        ContentLoadException error = Refusal(tree);

        Assert.Equal(ScenarioFile, error.File);
        Assert.Equal("$.crew.count", error.JsonPath);
    }

    /// <summary>A zone station that is no fixture of the layout is refused at the station.</summary>
    [Fact]
    public void UnknownStationIsRefused()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace(ScenarioFile, "\"door-fwd\", \"lav-fwd\" ]", "\"door-fwd\", \"lav-aft\" ]");

        ContentLoadException error = Refusal(tree);

        Assert.Equal(ScenarioFile, error.File);
        Assert.Equal("$.crew.zones[0].stations[1]", error.JsonPath);
    }

    /// <summary>A cart span running past the layout's last row is refused at its last row.</summary>
    [Fact]
    public void CartSpanOutsideTheLayoutIsRefused()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace(ScenarioFile, "\"carts\": [ { \"first_row\": 0, \"last_row\": 0", "\"carts\": [ { \"first_row\": 0, \"last_row\": 1");

        ContentLoadException error = Refusal(tree);

        Assert.Equal(ScenarioFile, error.File);
        Assert.Equal("$.crew.carts[0].last_row", error.JsonPath);
    }

    private static ContentLoadException Refusal(ContentTree tree)
    {
        ContentSet content = tree.Load();
        return Assert.Throws<ContentLoadException>(() => new ScenarioValidator().Validate(content));
    }
}
