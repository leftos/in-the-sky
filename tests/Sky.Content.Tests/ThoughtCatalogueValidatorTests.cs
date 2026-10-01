using Sky.Content.Validation;

namespace Sky.Content.Tests;

/// <summary>
/// Pins the thought catalogue validator: every kind's lever tag and hook are from the known lists, and it lasts either
/// <c>lasts_minutes</c> or <c>until</c> an event of the closed list, never both and never neither, each refusal naming
/// <c>thoughts.json</c> and the JSON path of the field (R10); and the whole validator pipeline accepts the minimal tree.
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

    /// <summary>A kind with both lasts_minutes and until is refused at until, naming the kind.</summary>
    [Fact]
    public void KindWithBothMinutesAndUntilIsRefused()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace(ThoughtsFile, "\"lasts_minutes\": 30,", "\"lasts_minutes\": 30, \"until\": \"first_served\",");

        ContentLoadException error = Refusal(tree);

        Assert.Equal(ThoughtsFile, error.File);
        Assert.Equal("$.kinds[0].until", error.JsonPath);
        Assert.Contains("call_ignored", error.Message, StringComparison.Ordinal);
    }

    /// <summary>A kind with neither lasts_minutes nor until is refused at until, naming the kind.</summary>
    [Fact]
    public void KindWithNeitherMinutesNorUntilIsRefused()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace(ThoughtsFile, "\"lasts_minutes\": null, \"until\": \"first_served\"", "\"lasts_minutes\": null");

        ContentLoadException error = Refusal(tree);

        Assert.Equal(ThoughtsFile, error.File);
        Assert.Equal("$.kinds[1].until", error.JsonPath);
        Assert.Contains("late_and_fed_up", error.Message, StringComparison.Ordinal);
    }

    /// <summary>An until outside the closed list is refused at the field, naming the kind and the value.</summary>
    [Fact]
    public void UntilOutsideTheClosedListIsRefused()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace(ThoughtsFile, "\"until\": \"first_served\"", "\"until\": \"landed\"");

        ContentLoadException error = Refusal(tree);

        Assert.Equal(ThoughtsFile, error.File);
        Assert.Equal("$.kinds[1].until", error.JsonPath);
        Assert.Contains("late_and_fed_up", error.Message, StringComparison.Ordinal);
        Assert.Contains("landed", error.Message, StringComparison.Ordinal);
    }

    /// <summary>A kind with lasts_minutes null and until first_served is accepted and reads its until.</summary>
    [Fact]
    public void UntilFirstServedIsAccepted()
    {
        using var tree = ContentTree.Minimal();

        ContentSet content = tree.Load();
        new ThoughtCatalogueValidator().Validate(content);

        Assert.Equal("first_served", content.Thoughts.Kinds.Single(kind => kind.Id == "late_and_fed_up").Until);
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
