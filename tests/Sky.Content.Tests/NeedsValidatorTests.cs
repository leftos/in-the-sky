using Sky.Content.Schema;
using Sky.Content.Validation;

namespace Sky.Content.Tests;

/// <summary>
/// Pins the needs validator: the distress band starts lie in [0, 100) with distressed above uneasy, and the contagion
/// threshold and calming threshold lie in [0, 100); each refusal names <c>needs.json</c>, the JSON path of the offending
/// value and what was expected (R10). Also pins that the validator pipeline runs these checks.
/// </summary>
public sealed class NeedsValidatorTests
{
    private const string NeedsFile = "needs.json";

    /// <summary>The minimal tree's needs pass.</summary>
    [Fact]
    public void MinimalNeedsPass()
    {
        using var tree = ContentTree.Minimal();

        new NeedsValidator().Validate(tree.Load());
    }

    /// <summary>An uneasy band start of 100 is refused at the field.</summary>
    [Fact]
    public void UneasyFromOfOneHundredIsRefused()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace(NeedsFile, "\"uneasy_from\": 30", "\"uneasy_from\": 100");

        ContentLoadException error = Refusal(tree);

        Assert.Equal(NeedsFile, error.File);
        Assert.Equal("$.distress_bands.uneasy_from", error.JsonPath);
        Assert.Contains("Expected a band start in [0, 100); got 100.", error.Message, StringComparison.Ordinal);
    }

    /// <summary>A negative uneasy band start is refused at the field.</summary>
    [Fact]
    public void NegativeUneasyFromIsRefused()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace(NeedsFile, "\"uneasy_from\": 30", "\"uneasy_from\": -1");

        ContentLoadException error = Refusal(tree);

        Assert.Equal(NeedsFile, error.File);
        Assert.Equal("$.distress_bands.uneasy_from", error.JsonPath);
        Assert.Contains("Expected a band start in [0, 100); got -1.", error.Message, StringComparison.Ordinal);
    }

    /// <summary>A distressed band start equal to the uneasy one is refused at the distressed field.</summary>
    [Fact]
    public void DistressedNotAboveUneasyIsRefused()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace(NeedsFile, "\"uneasy_from\": 30, \"distressed_from\": 60", "\"uneasy_from\": 60, \"distressed_from\": 60");

        ContentLoadException error = Refusal(tree);

        Assert.Equal(NeedsFile, error.File);
        Assert.Equal("$.distress_bands.distressed_from", error.JsonPath);
        Assert.Contains("Expected distressed_from above uneasy_from (60); got 60.", error.Message, StringComparison.Ordinal);
    }

    /// <summary>A contagion threshold of 100 is refused at the field.</summary>
    [Fact]
    public void ContagionThresholdOfOneHundredIsRefused()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace(NeedsFile, "\"threshold\": 40, \"beside_weight\"", "\"threshold\": 100, \"beside_weight\"");

        ContentLoadException error = Refusal(tree);

        Assert.Equal(NeedsFile, error.File);
        Assert.Equal("$.contagion.threshold", error.JsonPath);
        Assert.Contains("Expected a threshold in [0, 100); got 100.", error.Message, StringComparison.Ordinal);
    }

    /// <summary>A calming threshold below 0 is refused at the field.</summary>
    [Fact]
    public void CalmingThresholdBelowZeroIsRefused()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace(NeedsFile, "\"calming_threshold\": 40", "\"calming_threshold\": -1");

        ContentLoadException error = Refusal(tree);

        Assert.Equal(NeedsFile, error.File);
        Assert.Equal("$.contagion.calming_threshold", error.JsonPath);
        Assert.Contains("Expected a threshold in [0, 100); got -1.", error.Message, StringComparison.Ordinal);
    }

    /// <summary>A NaN uneasy band start is refused at the field; no JSON file carries NaN, so the loaded set is rebuilt.</summary>
    [Fact]
    public void NaNUneasyFromIsRefused()
    {
        using var tree = ContentTree.Minimal();
        ContentSet content = WithBands(tree.Load(), double.NaN, 60);

        ContentLoadException error = Assert.Throws<ContentLoadException>(() => new NeedsValidator().Validate(content));

        Assert.Equal(NeedsFile, error.File);
        Assert.Equal("$.distress_bands.uneasy_from", error.JsonPath);
        Assert.Contains("Expected a band start in [0, 100); got NaN.", error.Message, StringComparison.Ordinal);
    }

    /// <summary>The validator pipeline runs the needs checks, not only its own instance.</summary>
    [Fact]
    public void ContentValidatorRunsTheNeedsChecks()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace(NeedsFile, "\"uneasy_from\": 30", "\"uneasy_from\": 100");

        ContentLoadException error = Assert.Throws<ContentLoadException>(() => ContentValidator.Validate(tree.Load()));

        Assert.Equal(NeedsFile, error.File);
        Assert.Equal("$.distress_bands.uneasy_from", error.JsonPath);
    }

    /// <summary>The loaded set with other band starts, for the values no JSON file can carry.</summary>
    private static ContentSet WithBands(ContentSet content, double uneasyFrom, double distressedFrom) =>
        new()
        {
            Hash = content.Hash,
            Layouts = content.Layouts,
            Needs = content.Needs with { File = content.Needs.File with { DistressBands = new DistressBandsSpec(uneasyFrom, distressedFrom) } },
            Manifest = content.Manifest,
            ProfessionIds = content.ProfessionIds,
            Traits = content.Traits,
            TraitIds = content.TraitIds,
            Activities = content.Activities,
            ActivityIds = content.ActivityIds,
            ActivityModules = content.ActivityModules,
            Crew = content.Crew,
            Scenarios = content.Scenarios,
            Thoughts = content.Thoughts,
        };

    private static ContentLoadException Refusal(ContentTree tree)
    {
        ContentSet content = tree.Load();
        return Assert.Throws<ContentLoadException>(() => new NeedsValidator().Validate(content));
    }
}
