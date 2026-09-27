using CsCheck;
using Sky.Engine.Scoring;

namespace Sky.Engine.Tests.Scoring;

/// <summary>Pins the peak-end experience of one passenger and the nearest-rank spread of a flight's experiences.</summary>
public sealed class ExperienceTests
{
    private const int Precision = 9;
    private const double Baseline = 10.0;
    private const double Spike = 90.0;

    /// <summary>A spike held 4 sim minutes is shorter than the 5-minute hold, so the peak stays at the baseline.</summary>
    /// <param name="samplesPerSimMinute">The series' sample rate.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(240)]
    public void FourMinuteSpikeDoesNotSetThePeak(int samplesPerSimMinute)
    {
        double[] distress = SpikeSeries(spikeMinutes: 4, samplesPerSimMinute);

        Assert.Equal(Expected(peak: Baseline, end: Baseline, distress), Experience.Compute(distress, samplesPerSimMinute), Precision);
    }

    /// <summary>A spike held the full 5 sim minutes sets the peak.</summary>
    /// <param name="samplesPerSimMinute">The series' sample rate.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(240)]
    public void FiveMinuteSpikeSetsThePeak(int samplesPerSimMinute)
    {
        double[] distress = SpikeSeries(spikeMinutes: 5, samplesPerSimMinute);

        Assert.Equal(Expected(peak: Spike, end: Baseline, distress), Experience.Compute(distress, samplesPerSimMinute), Precision);
    }

    /// <summary>
    /// Ten minutes at 50 then twenty at 20: the end term is the last twenty minutes' 20, not the whole series' 30 nor a
    /// twenty-one-minute 21.4. Experience is 100 − (0.4 × 50 + 0.3 × 20 + 0.3 × 30) = 65.
    /// </summary>
    [Fact]
    public void EndIsTheLastTwentyMinutes()
    {
        double[] distress = [.. Enumerable.Repeat(50.0, 10), .. Enumerable.Repeat(20.0, 20)];

        Assert.Equal(65.0, Experience.Compute(distress, samplesPerSimMinute: 1), Precision);
    }

    /// <summary>
    /// A three-minute series is shorter than the hold, so its minimum is its peak, and its end is its whole mean:
    /// 100 − (0.4 × 40 + 0.3 × 60 + 0.3 × 60) = 48.
    /// </summary>
    [Fact]
    public void ShortSeriesUsesItsMinimumAsPeak() => Assert.Equal(48.0, Experience.Compute([40.0, 80.0, 60.0], samplesPerSimMinute: 1), Precision);

    /// <summary>An empty series, a sample rate below 1, or a sample that is NaN or outside [0, 100] is rejected.</summary>
    /// <param name="distress">The series.</param>
    /// <param name="samplesPerSimMinute">The sample rate.</param>
    [Theory]
    [InlineData(new double[] { }, 1)]
    [InlineData(new double[] { 10.0 }, 0)]
    [InlineData(new double[] { 10.0 }, -1)]
    [InlineData(new double[] { 10.0, -0.01 }, 1)]
    [InlineData(new double[] { 100.01 }, 1)]
    [InlineData(new double[] { double.NaN }, 1)]
    [InlineData(new double[] { double.PositiveInfinity }, 1)]
    public void InvalidInputThrows(double[] distress, int samplesPerSimMinute) =>
        Assert.ThrowsAny<ArgumentException>(() => Experience.Compute(distress, samplesPerSimMinute));

    /// <summary>An empty flight has no spread.</summary>
    [Fact]
    public void EmptySpreadThrows() => Assert.Throws<ArgumentException>(() => ExperienceSpread.From([]));

    /// <summary>Twenty passengers at 10 and 160 at 90: the 10th percentile is 10, the median 90, and 20 would complain.</summary>
    [Fact]
    public void SpreadOfTwentyAtTenAndOneSixtyAtNinety()
    {
        double[] experiences = [.. Enumerable.Repeat(90.0, 160), .. Enumerable.Repeat(10.0, 20)];

        var spread = ExperienceSpread.From(experiences);

        Assert.Equal((10.0, 90.0, 20), (spread.P10, spread.Median, spread.CountUnder40));
    }

    /// <summary>A hold window of 0 samples is rejected rather than read past the deque.</summary>
    [Fact]
    public void HeldPeakWindowOfZeroThrows() => Assert.Throws<ArgumentOutOfRangeException>(() => Experience.HeldPeak([10.0, 20.0], 0));

    /// <summary>A hold window longer than the series is rejected; the caller clamps it to the series length.</summary>
    [Fact]
    public void HeldPeakWindowLongerThanTheSeriesThrows() => Assert.Throws<ArgumentOutOfRangeException>(() => Experience.HeldPeak([10.0, 20.0], 3));

    /// <summary>Taking the spread sorts a copy and leaves the caller's experiences in their order.</summary>
    [Fact]
    public void SpreadLeavesTheCallerDataInOrder()
    {
        double[] experiences = [90.0, 10.0, 50.0];

        ExperienceSpread.From(experiences);

        Assert.Equal([90.0, 10.0, 50.0], experiences);
    }

    /// <summary>The one-pass deque peak equals a brute-force maximum over every window of the window's minimum.</summary>
    [Fact]
    public void DequePeakEqualsBruteForcePeak() => AssertDequeMatchesBruteForce(Gen.Double[0.0, 100.0]);

    /// <summary>
    /// The same on series of small whole numbers, where equal neighbours are common, so the deque's pop on an equal
    /// value is exercised.
    /// </summary>
    [Fact]
    public void DequePeakEqualsBruteForcePeakWithTies() => AssertDequeMatchesBruteForce(Gen.Int[0, 3].Select(value => (double)value));

    private static void AssertDequeMatchesBruteForce(Gen<double> values) =>
        Gen.Select(values.Array[1, 80], Gen.Int[1, 90])
            .Sample(
                (series, window) =>
                {
                    int windowSamples = Math.Min(window, series.Length);
                    return Experience.HeldPeak(series, windowSamples) == BruteForcePeak(series, windowSamples);
                }
            );

    private static double BruteForcePeak(double[] series, int windowSamples)
    {
        double peak = double.NegativeInfinity;
        for (int start = 0; start + windowSamples <= series.Length; start++)
        {
            peak = Math.Max(peak, series.Skip(start).Take(windowSamples).Min());
        }

        return peak;
    }

    /// <summary>Sixty minutes at the baseline with a spike to 90 from minute 10, clear of the last twenty minutes.</summary>
    private static double[] SpikeSeries(int spikeMinutes, int samplesPerSimMinute)
    {
        double[] minutes =
        [
            .. Enumerable.Repeat(Baseline, 10),
            .. Enumerable.Repeat(Spike, spikeMinutes),
            .. Enumerable.Repeat(Baseline, 50 - spikeMinutes),
        ];
        return [.. minutes.SelectMany(value => Enumerable.Repeat(value, samplesPerSimMinute))];
    }

    private static double Expected(double peak, double end, double[] distress) => 100.0 - ((0.4 * peak) + (0.3 * end) + (0.3 * distress.Average()));
}
