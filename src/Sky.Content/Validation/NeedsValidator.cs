using Sky.Content.Schema;

namespace Sky.Content.Validation;

/// <summary>
/// Checks <c>needs.json</c>'s distress bands and contagion thresholds, the ranges the Engine leaves to the content: the
/// uneasy and distressed band starts lie in [0, 100) with distressed above uneasy, and the contagion threshold and the
/// calming threshold lie in [0, 100). Every cascade, distress-term and sustain-gate threshold is refused by the Engine as it
/// loads, and seat comfort holds multipliers rather than thresholds.
/// </summary>
public sealed class NeedsValidator : IContentValidator
{
    private const string NeedsFileName = "needs.json";

    /// <summary>The lowest band start or threshold the content may set, inclusive.</summary>
    private const double Lowest = 0;

    /// <summary>The exclusive upper bound of a band start or threshold; 100 itself is refused.</summary>
    private const double Highest = 100;

    /// <inheritdoc/>
    public void Validate(ContentSet content)
    {
        ArgumentNullException.ThrowIfNull(content);
        DistressBandsSpec bands = content.Needs.File.DistressBands;
        CheckRange("$.distress_bands.uneasy_from", bands.UneasyFrom, "a band start");
        CheckRange("$.distress_bands.distressed_from", bands.DistressedFrom, "a band start");
        if (!(bands.DistressedFrom > bands.UneasyFrom))
        {
            string expected = $"Expected distressed_from above uneasy_from ({bands.UneasyFrom}); got {bands.DistressedFrom}.";
            throw new ContentLoadException(NeedsFileName, "$.distress_bands.distressed_from", expected, null);
        }

        ContagionSpec contagion = content.Needs.File.Contagion;
        CheckRange("$.contagion.threshold", contagion.Threshold, "a threshold");
        CheckRange("$.contagion.calming_threshold", contagion.CalmingThreshold, "a threshold");
    }

    private static void CheckRange(string path, double value, string what)
    {
        if (value is not (>= Lowest and < Highest))
        {
            throw new ContentLoadException(NeedsFileName, path, $"Expected {what} in [0, 100); got {value}.", null);
        }
    }
}
