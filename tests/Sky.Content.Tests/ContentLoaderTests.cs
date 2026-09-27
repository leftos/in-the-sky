using Sky.Engine.Cabin;
using Sky.Engine.Passengers;

namespace Sky.Content.Tests;

/// <summary>
/// Pins the content loader: a minimal tree loads into Engine types with ids interned in declaration order, every
/// malformed, unknown, missing or Engine-refused value stops the load naming the file and the JSON path (R10), and the
/// content hash covers every file's path and bytes whatever the root or the order the files were written in.
/// </summary>
public sealed class ContentLoaderTests
{
    /// <summary>A minimal tree loads: the layout, activities with their Lua text, interned ids and the scenario's switches.</summary>
    [Fact]
    public void LoadsAMinimalContentTreeIntoEngineTypes()
    {
        using var tree = ContentTree.Minimal();

        ContentSet content = tree.Load();

        CabinLayout layout = content.Layouts["tiny"];
        Assert.Equal(148, layout.CabinWidthInches);
        Assert.Equal(3, Assert.Single(Assert.Single(layout.Rows).Groups).Seats.Count);
        Assert.Equal([FixtureKind.Door, FixtureKind.Lav, FixtureKind.Galley], layout.Fixtures.Select(fixture => fixture.Kind));
        Assert.Equal(["idle", "sleep"], content.ActivityModules.Select(module => module.Id));
        Assert.Equal(ContentTree.SleepModule, content.ActivityModules[1].Source);
        Assert.Equal(new ActivityId(1), content.ActivityIds["sleep"]);
        Assert.Equal(new TraitId(0), content.TraitIds["anxious"]);
        Assert.Equal(new TraitId(2), content.TraitIds["sleep_kit"]);
        Assert.NotNull(content.Traits.Traits[2].Share);
        Assert.Equal((true, true, true, false), SystemsOf(content.Scenarios["ref"]));
        Assert.Equal("steady", content.Crew.Roster[0].Trait);
        Assert.Null(content.Thoughts.Kinds[1].LastsMinutes);
        Assert.Matches("^[0-9a-f]{64}$", content.Hash);
    }

    /// <summary>A value of the wrong type deep in <c>needs.json</c> fails naming the file and the field's JSON path.</summary>
    [Fact]
    public void MalformedFileFailsNamingFileAndJsonPath()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace("needs.json", "\"unease_half_life_minutes\": 15", "\"unease_half_life_minutes\": \"slow\"");

        ContentLoadException error = Assert.Throws<ContentLoadException>(tree.Load);

        Assert.Equal("needs.json", error.File);
        Assert.Equal("$.rates.unease_half_life_minutes", error.JsonPath);
    }

    /// <summary>A field the schema does not know fails, naming the file and the field.</summary>
    [Fact]
    public void UnknownFieldFails()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace("scenarios/ref.json", "\"thoughts\": false", "\"thoughts\": false, \"weather\": true");

        ContentLoadException error = Assert.Throws<ContentLoadException>(tree.Load);

        Assert.Equal("scenarios/ref.json", error.File);
        Assert.Contains("weather", error.Message, StringComparison.Ordinal);
    }

    /// <summary>A required field left out fails, naming the file and the field.</summary>
    [Fact]
    public void MissingRequiredFieldFails()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace("scenarios/ref.json", "\"gate_delay_minutes\": 0,", string.Empty);

        ContentLoadException error = Assert.Throws<ContentLoadException>(tree.Load);

        Assert.Equal("scenarios/ref.json", error.File);
        Assert.NotNull(error.JsonPath);
        Assert.Contains("gate_delay_minutes", error.Message, StringComparison.Ordinal);
    }

    /// <summary>A forbidden pair naming a trait nobody declared fails, naming the id, the file and the JSON path.</summary>
    [Fact]
    public void UnknownTraitIdFailsNamingIt()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace("traits.json", "\"second\": \"calm\"", "\"second\": \"serene\"");

        ContentLoadException error = Assert.Throws<ContentLoadException>(tree.Load);

        Assert.Equal("traits.json", error.File);
        Assert.Equal("$.forbidden_pairs[0].second", error.JsonPath);
        Assert.Contains("serene", error.Message, StringComparison.Ordinal);
    }

    /// <summary>A crew member naming a crew trait nobody declared fails, naming the id and the JSON path.</summary>
    [Fact]
    public void UnknownCrewTraitIdFailsNamingIt()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace("crew.json", "\"trait\": \"steady\"", "\"trait\": \"stoic\"");

        ContentLoadException error = Assert.Throws<ContentLoadException>(tree.Load);

        Assert.Equal("crew.json", error.File);
        Assert.Equal("$.roster[0].trait", error.JsonPath);
        Assert.Contains("stoic", error.Message, StringComparison.Ordinal);
    }

    /// <summary>An activity id declared twice fails at the second declaration.</summary>
    [Fact]
    public void DuplicateActivityIdFails()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace("activities.json", "{ \"id\": \"sleep\"", "{ \"id\": \"idle\"");

        ContentLoadException error = Assert.Throws<ContentLoadException>(tree.Load);

        Assert.Equal("activities.json", error.File);
        Assert.Equal("$.activities[1].id", error.JsonPath);
        Assert.Contains("idle", error.Message, StringComparison.Ordinal);
    }

    /// <summary>An activity with no Lua module fails, naming the activity and the path the module was expected at.</summary>
    [Fact]
    public void MissingActivityModuleFails()
    {
        using var tree = ContentTree.Minimal();
        tree.Delete("activities/sleep.lua");

        ContentLoadException error = Assert.Throws<ContentLoadException>(tree.Load);

        Assert.Equal("activities/sleep.lua", error.File);
        Assert.Contains("\"sleep\"", error.Message, StringComparison.Ordinal);
    }

    /// <summary>A value the Engine's own constructor refuses becomes a load error naming the file and the JSON path.</summary>
    [Fact]
    public void EngineValidationErrorBecomesContentLoadException()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace("needs.json", "\"threshold\": 70, \"factor\": 1.5", "\"threshold\": 100, \"factor\": 1.5");

        ContentLoadException error = Assert.Throws<ContentLoadException>(tree.Load);

        Assert.Equal("needs.json", error.File);
        Assert.Equal("$.cascade_rules", error.JsonPath);
        Assert.IsType<ArgumentException>(error.InnerException, exactMatch: false);
    }

    /// <summary>Changing one byte of one file changes the hash.</summary>
    [Fact]
    public void HashChangesWhenOneByteChanges()
    {
        using var tree = ContentTree.Minimal();
        string before = tree.Load().Hash;
        tree.Replace("activities/idle.lua", "return 1", "return 2");

        string after = tree.Load().Hash;

        Assert.NotEqual(before, after);
    }

    /// <summary>The same files written in the opposite order into a different directory hash the same.</summary>
    [Fact]
    public void HashIgnoresEnumerationOrderAndRootLocation()
    {
        using var forward = ContentTree.Minimal();
        using var backward = ContentTree.From(ContentTree.MinimalFiles.Reverse());

        Assert.NotEqual(forward.Root, backward.Root);
        Assert.Equal(forward.Load().Hash, backward.Load().Hash);
    }

    /// <summary>The same bytes under another file name hash differently, because each file's path is hashed with it.</summary>
    [Fact]
    public void HashChangesWhenAFileIsRenamed()
    {
        using var first = ContentTree.Minimal();
        using var renamed = ContentTree.Minimal();
        first.Write("notes/a.json", "same bytes");
        renamed.Write("notes/b.json", "same bytes");

        Assert.NotEqual(first.Load().Hash, renamed.Load().Hash);
    }

    /// <summary>Files the loader never reads, such as desktop.ini or .DS_Store, leave the hash unchanged.</summary>
    [Fact]
    public void HashIgnoresFilesOtherThanJsonAndLua()
    {
        using var tree = ContentTree.Minimal();
        string before = tree.Load().Hash;
        tree.Write("desktop.ini", "[.ShellClassInfo]");
        tree.Write("layouts/.DS_Store", "finder state");

        string after = tree.Load().Hash;

        Assert.Equal(before, after);
    }

    /// <summary>Optional fields left out take their documented defaults: factors of 1 and empty lists, never 0 or null.</summary>
    [Fact]
    public void OmittedOptionalFieldsTakeTheirDefaults()
    {
        using var tree = ContentTree.Minimal();

        ContentSet content = tree.Load();

        Schema.CrewTraitSpec steady = content.Crew.Traits[0];
        Assert.Equal(0.5, steady.FocusFatigueWeightFactor);
        double[] omitted =
        [
            steady.FocusFactorOverRedline,
            steady.OnTaskStrainFactor,
            steady.ServiceTimeFactor,
            steady.UneaseReliefFactor,
            steady.DwellFactor,
        ];
        Assert.All(omitted, factor => Assert.Equal(1.0, factor));
        Schema.TraitSpec calm = content.Traits.Traits[1];
        Assert.Empty(calm.Modifiers);
        Assert.Equal(1.0, calm.WakeChanceFactor);
        Assert.Empty(content.Traits.Traits[2].Modifiers);
        Schema.ActivitySpec idle = content.Activities.Activities[0];
        Assert.Empty(idle.Effects);
        Assert.Empty(idle.Pauses);
    }

    /// <summary>An enum reads only from a string equal to a member name; numbers, other casing and flag lists fail.</summary>
    /// <param name="site">The JSON value written for the first activity's site.</param>
    /// <param name="loads">Whether the tree should load.</param>
    [Theory]
    [InlineData("7", false)]
    [InlineData("\"7\"", false)]
    [InlineData("\"lav\"", false)]
    [InlineData("\"Seat, Lav\"", false)]
    [InlineData("\"Nowhere\"", false)]
    [InlineData("\"Lav\"", true)]
    public void EnumsAcceptOnlyExactMemberNames(string site, bool loads)
    {
        using var tree = ContentTree.Minimal();
        tree.Replace("activities.json", "{ \"id\": \"idle\", \"site\": \"Seat\"", $"{{ \"id\": \"idle\", \"site\": {site}");

        if (loads)
        {
            Assert.Equal(Schema.ActivitySite.Lav, tree.Load().Activities.Activities[0].Site);
            return;
        }

        ContentLoadException error = Assert.Throws<ContentLoadException>(tree.Load);
        Assert.Equal("activities.json", error.File);
        Assert.Equal("$.activities[0].site", error.JsonPath);
    }

    /// <summary>A null entry in any list fails, naming the list and the entry's index.</summary>
    /// <param name="file">The file to change.</param>
    /// <param name="oldText">The list's opening, as the minimal tree writes it.</param>
    /// <param name="newText">The same opening with a null entry first.</param>
    /// <param name="jsonPath">The JSON path the failure should name.</param>
    [Theory]
    [InlineData("activities.json", "\"effects\": [", "\"effects\": [ null,", "$.activities[1].effects[0]")]
    [InlineData("scenarios/ref.json", "\"zones\": [", "\"zones\": [ null,", "$.crew.zones[0]")]
    [InlineData("scenarios/ref.json", "\"service_plan\": [", "\"service_plan\": [ null,", "$.service_plan[0]")]
    [InlineData("scenarios/ref.json", "\"locked_lavs\": []", "\"locked_lavs\": [ null ]", "$.locked_lavs[0]")]
    [InlineData("layouts/tiny.json", "\"rows\": [", "\"rows\": [ null,", "$.rows[0]")]
    [InlineData("traits.json", "\"modifiers\": [", "\"modifiers\": [ null,", "$.traits[0].modifiers[0]")]
    [InlineData("needs.json", "\"cascade_rules\": [", "\"cascade_rules\": [ null,", "$.cascade_rules[0]")]
    public void NullListElementFailsNamingItsIndex(string file, string oldText, string newText, string jsonPath)
    {
        using var tree = ContentTree.Minimal();
        tree.Replace(file, oldText, newText);

        ContentLoadException error = Assert.Throws<ContentLoadException>(tree.Load);

        Assert.Equal(file, error.File);
        Assert.Equal(jsonPath, error.JsonPath);
    }

    private static (bool Needs, bool Events, bool Contagion, bool Thoughts) SystemsOf(Schema.ScenarioFile scenario) =>
        (scenario.Systems.Needs, scenario.Systems.Events, scenario.Systems.Contagion, scenario.Systems.Thoughts);
}
