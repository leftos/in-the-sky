namespace Sky.Engine.Scoring;

/// <summary>
/// The spread of a flight's passenger experiences (CONCEPT section 6): the 10th percentile, which is the flight's measure,
/// beside the median and the count of passengers who would complain. Percentiles are nearest-rank.
/// </summary>
public sealed class ExperienceSpread
{
    private const double ComplainBelow = 40.0;
    private const int P10Percent = 10;

    private ExperienceSpread(double p10, double median, int countUnder40)
    {
        P10 = p10;
        Median = median;
        CountUnder40 = countUnder40;
    }

    /// <summary>The experience at nearest rank ⌈0.1 × n⌉ of the ascending sort: how the worst-served tenth fared.</summary>
    public double P10 { get; }

    /// <summary>The experience at nearest rank ⌈0.5 × n⌉ of the ascending sort.</summary>
    public double Median { get; }

    /// <summary>How many experiences are below 40, the passengers who would complain.</summary>
    public int CountUnder40 { get; }

    /// <summary>Takes the spread of a flight's experiences, leaving the caller's data in its order.</summary>
    /// <param name="experiences">Every passenger's experience, each in [0, 100].</param>
    /// <returns>The spread.</returns>
    /// <exception cref="ArgumentException">There are no experiences.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An experience is NaN or outside [0, 100].</exception>
    public static ExperienceSpread From(ReadOnlySpan<double> experiences)
    {
        RangeGuard.ThrowIfEmptyOrOutside(experiences, Experience.Minimum, Experience.Maximum, nameof(experiences));

        double[] sorted = experiences.ToArray();
        Array.Sort(sorted);
        int countUnder40 = 0;
        foreach (double experience in sorted)
        {
            if (experience < ComplainBelow)
            {
                countUnder40++;
            }
        }

        return new ExperienceSpread(NearestRank.At(sorted, P10Percent), NearestRank.Median(sorted), countUnder40);
    }

    /// <summary>Smooth at or above the Smooth line, Bad below the Bad line, otherwise Rough, on <see cref="P10"/>.</summary>
    /// <param name="thresholds">The experience thresholds.</param>
    /// <returns>The verdict.</returns>
    public Verdict VerdictFor(ExperienceThresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(thresholds);
        if (P10 >= thresholds.SmoothAtLeast)
        {
            return Verdict.Smooth;
        }

        return P10 < thresholds.BadBelow ? Verdict.Bad : Verdict.Rough;
    }
}
