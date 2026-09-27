using Sky.Content.Validation;

namespace Sky.Content.Tests;

/// <summary>
/// Pins the thought catalogue validator: every kind's lever tag, its neighbour lever tag when it has one, and its hook are
/// from the known lists, each refusal naming <c>thoughts.json</c> and the JSON path of the field (R10); and the whole
/// validator pipeline accepts the minimal tree.
/// </summary>
public sealed class ThoughtCatalogueValidatorTests
{
    private const string ThoughtsFile = "thoughts.json";

    /// <summary>A lever tag outside the known list is refused at the field.</summary>
    [Fact]
    public void UnknownLeverTagIsRefused()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace(ThoughtsFile, "\"lever_tag\": \"service_plan\"", "\"lever_tag\": \"seat_pitch\"");

        ContentLoadException error = Refusal(tree);

        Assert.Equal(ThoughtsFile, error.File);
        Assert.Equal("$.kinds[1].lever_tag", error.JsonPath);
        Assert.Contains("seat_pitch", error.Message, StringComparison.Ordinal);
    }

    /// <summary>A neighbour lever tag outside the known list is refused at the field.</summary>
    [Fact]
    public void UnknownNeighbourLeverTagIsRefused()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace(ThoughtsFile, "\"lever_tag\": \"crew_staffing\"", "\"lever_tag\": \"crew_staffing\", \"neighbour_lever_tag\": \"seatmates\"");

        ContentLoadException error = Refusal(tree);

        Assert.Equal(ThoughtsFile, error.File);
        Assert.Equal("$.kinds[0].neighbour_lever_tag", error.JsonPath);
    }

    /// <summary>A hook the engine does not emit is refused at the field.</summary>
    [Fact]
    public void UnknownHookIsRefused()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace(ThoughtsFile, "\"hook\": \"boarded\"", "\"hook\": \"landed\"");

        ContentLoadException error = Refusal(tree);

        Assert.Equal(ThoughtsFile, error.File);
        Assert.Equal("$.kinds[1].hook", error.JsonPath);
    }

    /// <summary>The whole pipeline, layouts, scenarios and the thought catalogue, accepts the minimal tree.</summary>
    [Fact]
    public void MinimalTreeValidates()
    {
        using var tree = ContentTree.Minimal();

        ContentValidator.Validate(tree.Load());
    }

    private static ContentLoadException Refusal(ContentTree tree)
    {
        ContentSet content = tree.Load();
        return Assert.Throws<ContentLoadException>(() => new ThoughtCatalogueValidator().Validate(content));
    }
}
