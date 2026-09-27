namespace Sky.Engine.Scoring;

/// <summary>
/// Nearest-rank percentiles (R36): the value at 1-based rank ⌈percent × n / 100⌉ of an ascending sort, worked out in
/// integer arithmetic so a whole rank never rounds up past itself.
/// </summary>
internal static class NearestRank
{
    private const int MedianPercent = 50;

    /// <summary>The value at the percentile's nearest rank.</summary>
    /// <param name="sorted">The values in ascending order, not empty.</param>
    /// <param name="percent">The percentile, from 1 to 100.</param>
    /// <returns>The value at that rank.</returns>
    internal static double At(IReadOnlyList<double> sorted, int percent)
    {
        long rank = (((long)percent * sorted.Count) + 99) / 100;
        return sorted[(int)rank - 1];
    }

    /// <summary>The value at the median's nearest rank, ⌈n / 2⌉: the lower of the two middle values when n is even.</summary>
    /// <param name="sorted">The values in ascending order, not empty.</param>
    /// <returns>The median.</returns>
    internal static double Median(IReadOnlyList<double> sorted) => At(sorted, MedianPercent);
}
