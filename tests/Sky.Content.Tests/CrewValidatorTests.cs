using Sky.Content.Schema;
using Sky.Content.Validation;

namespace Sky.Content.Tests;

/// <summary>
/// Pins the crew validator: a crew trait's <c>free_zones</c>, the zones the trait covers free of covering strain
/// (<c>crew.md</c>: the Floater trait), is 0 or more, and a trait that leaves the field out reads 0; a roster entry's
/// <c>competence</c> and <c>empathy</c> lie in [0, 1] and its <c>starting_fatigue</c> in [0, 100] (<c>crew.md</c> "The crew
/// model"); every refusal names <c>crew.json</c>, the JSON path of the field and the trait or crew member (R10).
/// </summary>
public sealed class CrewValidatorTests
{
    private const string CrewFile = "crew.json";

    /// <summary>The fixture's one trait, ending at its last field.</summary>
    private const string SteadyTrait = """ "preemption_strain_factor": 0.5 }""";

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

    private static ContentLoadException Refusal(ContentSet content) =>
        Assert.Throws<ContentLoadException>(() => new CrewValidator().Validate(content));
}
