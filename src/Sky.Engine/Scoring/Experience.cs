namespace Sky.Engine.Scoring;

/// <summary>
/// One passenger's experience of a flight by the peak-end rule (CONCEPT section 6):
/// <c>100 − (0.4 × peak + 0.3 × end + 0.3 × mean)</c> over their distress series, clamped to [0, 100]. The peak is the
/// highest distress held for 5 sim minutes, the end the mean over the last 20 sim minutes, the mean over the whole series.
/// </summary>
public static class Experience
{
    /// <summary>The lowest distress a sample may hold, and the lowest experience.</summary>
    internal const double Minimum = 0.0;

    /// <summary>The highest distress a sample may hold, and the highest experience.</summary>
    internal const double Maximum = 100.0;

    private const double PeakWeight = 0.4;
    private const double EndWeight = 0.3;
    private const double MeanWeight = 0.3;
    private const int PeakHoldMinutes = 5;
    private const int EndMinutes = 20;

    /// <summary>Computes one passenger's experience from their distress series.</summary>
    /// <param name="distress">The passenger's distress, in time order, each sample in [0, 100].</param>
    /// <param name="samplesPerSimMinute">How many samples the series holds per sim minute; at least 1.</param>
    /// <returns>The experience, in [0, 100]; 100 is a flight with no distress at all.</returns>
    /// <exception cref="ArgumentException">The series is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A sample is NaN or outside [0, 100], or the sample rate is below 1.</exception>
    public static double Compute(ReadOnlySpan<double> distress, int samplesPerSimMinute)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(samplesPerSimMinute, 1);
        RangeGuard.ThrowIfEmptyOrOutside(distress, Minimum, Maximum, nameof(distress));

        double peak = HeldPeak(distress, WindowSamples(PeakHoldMinutes, samplesPerSimMinute, distress.Length));
        int endSamples = WindowSamples(EndMinutes, samplesPerSimMinute, distress.Length);
        double end = Mean(distress[^endSamples..]);
        double mean = Mean(distress);

        double experience = Maximum - ((PeakWeight * peak) + (EndWeight * end) + (MeanWeight * mean));
        return Math.Clamp(experience, Minimum, Maximum);
    }

    /// <summary>
    /// The highest value the series stays at or above for <paramref name="windowSamples"/> consecutive samples: the
    /// maximum over every window of that length of the window's minimum, found with a monotonic deque in one pass.
    /// </summary>
    /// <param name="series">The series, not empty.</param>
    /// <param name="windowSamples">The hold window in samples, from 1 to the series length.</param>
    /// <returns>The held peak.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The window is below 1 or longer than the series.</exception>
    internal static double HeldPeak(ReadOnlySpan<double> series, int windowSamples)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowSamples);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(windowSamples, series.Length);

        // The deque holds indices of the current window whose values rise from front to back; its front is the minimum.
        int[] deque = new int[series.Length];
        int head = 0;
        int tail = 0;
        double peak = double.NegativeInfinity;
        for (int index = 0; index < series.Length; index++)
        {
            while (tail > head && series[deque[tail - 1]] >= series[index])
            {
                tail--;
            }

            deque[tail++] = index;
            if (deque[head] <= index - windowSamples)
            {
                head++;
            }

            if (index >= windowSamples - 1)
            {
                peak = Math.Max(peak, series[deque[head]]);
            }
        }

        return peak;
    }

    private static int WindowSamples(int minutes, int samplesPerSimMinute, int seriesLength) =>
        (int)Math.Min((long)minutes * samplesPerSimMinute, seriesLength);

    private static double Mean(ReadOnlySpan<double> series)
    {
        double sum = 0.0;
        foreach (double sample in series)
        {
            sum += sample;
        }

        return sum / series.Length;
    }
}
