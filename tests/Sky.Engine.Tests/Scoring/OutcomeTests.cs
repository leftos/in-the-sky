using Sky.Engine.Scoring;
using Sky.Engine.Time;

namespace Sky.Engine.Tests.Scoring;

/// <summary>
/// Pins the incident, strain and doors outcomes and where each outcome's verdict turns, at the thresholds of
/// balance.md section 3.4 (test fixtures here, never code defaults).
/// </summary>
public sealed class OutcomeTests
{
    private const int Precision = 12;
    private const int Minute = SimTime.TicksPerSimMinute;

    private static readonly ExperienceThresholds ExperienceLines = new(smoothAtLeast: 70.0, badBelow: 40.0);
    private static readonly IncidentThresholds IncidentLines = new(badBelowShare: 0.8);
    private static readonly StrainThresholds StrainLines = new(smoothPeakBelow: 60.0, badPeakAtLeast: 85.0, badMinutesOver: 15.0);
    private static readonly DoorsThresholds DoorsLines = new(smoothAtMost: 2.0, badOver: 10.0);

    /// <summary>Cabin ready on its planned tick and deboarding on its target give 0 minutes late.</summary>
    [Fact]
    public void OnTimeDoorsAreZeroMinutesLate()
    {
        var doors = DoorsOutcome.From(
            cabinReadyTick: 30 * Minute,
            plannedCabinReadyTick: 30 * Minute,
            deboardingDurationTicks: 12 * Minute,
            targetDeboardingDurationTicks: 12 * Minute
        );

        Assert.Equal((0.0, 0.0, 0.0), (doors.CabinReadyMinutesLate, doors.DeboardingMinutesLate, doors.MinutesLate));
    }

    /// <summary>A cabin ready early and a deboarding faster than target count as 0 minutes late, not negative.</summary>
    [Fact]
    public void EarlyIsNotNegative()
    {
        var doors = DoorsOutcome.From(
            cabinReadyTick: 25 * Minute,
            plannedCabinReadyTick: 30 * Minute,
            deboardingDurationTicks: 8 * Minute,
            targetDeboardingDurationTicks: 12 * Minute
        );

        Assert.Equal((0.0, 0.0, 0.0), (doors.CabinReadyMinutesLate, doors.DeboardingMinutesLate, doors.MinutesLate));
    }

    /// <summary>Cabin ready 3 minutes late and deboarding 5 minutes over: the doors are 5 minutes late.</summary>
    [Fact]
    public void DoorsTakeTheWorseOfTheTwo()
    {
        var doors = DoorsOutcome.From(
            cabinReadyTick: 33 * Minute,
            plannedCabinReadyTick: 30 * Minute,
            deboardingDurationTicks: 17 * Minute,
            targetDeboardingDurationTicks: 12 * Minute
        );

        Assert.Equal((3.0, 5.0, 5.0), (doors.CabinReadyMinutesLate, doors.DeboardingMinutesLate, doors.MinutesLate));
    }

    /// <summary>Experience's 10th percentile: ≥70 Smooth, below 40 Bad, between Rough.</summary>
    /// <param name="p10">The 10th percentile.</param>
    /// <param name="expected">The verdict.</param>
    [Theory]
    [InlineData(70.0, Verdict.Smooth)]
    [InlineData(69.99, Verdict.Rough)]
    [InlineData(40.0, Verdict.Rough)]
    [InlineData(39.99, Verdict.Bad)]
    public void ExperienceVerdictFallsOnTheDocumentedSide(double p10, Verdict expected) =>
        Assert.Equal(expected, ExperienceSpread.From([p10]).VerdictFor(ExperienceLines));

    /// <summary>Incidents: all handled Smooth, below 80% handled Bad, between Rough.</summary>
    /// <param name="total">The incidents.</param>
    /// <param name="handled">The incidents handled.</param>
    /// <param name="expected">The verdict.</param>
    [Theory]
    [InlineData(10, 8, Verdict.Rough)]
    [InlineData(100, 79, Verdict.Bad)]
    [InlineData(5, 5, Verdict.Smooth)]
    public void IncidentVerdictFallsOnTheDocumentedSide(int total, int handled, Verdict expected)
    {
        IncidentRecord[] incidents = [.. Enumerable.Range(0, total).Select(index => new IncidentRecord($"i{index}", 0, null, index < handled))];

        Assert.Equal(expected, IncidentOutcome.From(incidents).VerdictFor(IncidentLines));
    }

    /// <summary>
    /// Strain: peak below 60 with no minutes over Smooth; peak from 85 or more than 15 minutes over Bad; between Rough.
    /// Minutes over the redline are fractional (one tick is 0.25 sim minutes), so any tick over the redline leaves Smooth.
    /// </summary>
    /// <param name="peak">The peak strain.</param>
    /// <param name="minutesOver">The minutes over the redline.</param>
    /// <param name="expected">The verdict.</param>
    [Theory]
    [InlineData(59.99, 0.0, Verdict.Smooth)]
    [InlineData(60.0, 0.0, Verdict.Rough)]
    [InlineData(50.0, 0.01, Verdict.Rough)]
    [InlineData(84.99, 0.0, Verdict.Rough)]
    [InlineData(85.0, 0.0, Verdict.Bad)]
    [InlineData(50.0, 15.0, Verdict.Rough)]
    [InlineData(50.0, 15.01, Verdict.Bad)]
    public void StrainVerdictFallsOnTheDocumentedSide(double peak, double minutesOver, Verdict expected) =>
        Assert.Equal(expected, StrainOutcome.From([new CrewStrainSummary(peak, minutesOver)]).VerdictFor(StrainLines));

    /// <summary>
    /// Doors: at most 2 minutes late Smooth, more than 10 Bad, between Rough. Lateness comes in whole ticks, so one tick
    /// past each line stands in for balance.md's 2.01 and 10.01.
    /// </summary>
    /// <param name="ticksLate">The ticks cabin ready came after plan.</param>
    /// <param name="expected">The verdict.</param>
    [Theory]
    [InlineData(2 * Minute, Verdict.Smooth)]
    [InlineData((2 * Minute) + 1, Verdict.Rough)]
    [InlineData(10 * Minute, Verdict.Rough)]
    [InlineData((10 * Minute) + 1, Verdict.Bad)]
    public void DoorsVerdictFallsOnTheDocumentedSide(long ticksLate, Verdict expected) =>
        Assert.Equal(
            expected,
            DoorsOutcome
                .From(cabinReadyTick: 1000 + ticksLate, plannedCabinReadyTick: 1000, deboardingDurationTicks: 0, targetDeboardingDurationTicks: 0)
                .VerdictFor(DoorsLines)
        );

    /// <summary>A flight with no incidents counts as all handled: share 1, no median, nothing missed, Smooth.</summary>
    [Fact]
    public void NoIncidentsIsSmooth()
    {
        var outcome = IncidentOutcome.From([]);

        Assert.Equal((0, 0, 1.0, (double?)null), (outcome.Total, outcome.Handled, outcome.HandledShare, outcome.MedianMinutesToCrew));
        Assert.Empty(outcome.Missed);
        Assert.Equal(Verdict.Smooth, outcome.VerdictFor(IncidentLines));
    }

    /// <summary>
    /// Crew reached four incidents after 4, 1, 3 and 2 minutes and never reached a fifth: the nearest-rank median of the
    /// four is rank 2, 2 minutes, not the averaged 2.5.
    /// </summary>
    [Fact]
    public void MedianMinutesToCrewIsNearestRank()
    {
        IncidentRecord[] incidents =
        [
            new("a", 100, 100 + (4 * Minute), true),
            new("b", 200, 200 + Minute, true),
            new("c", 300, null, false),
            new("d", 400, 400 + (3 * Minute), true),
            new("e", 500, 500 + (2 * Minute), false),
        ];

        Assert.Equal(2.0, IncidentOutcome.From(incidents).MedianMinutesToCrew);
    }

    /// <summary>The missed list names every unhandled incident in input order, and the counts and share follow.</summary>
    [Fact]
    public void MissedListsUnhandledIdsInOrder()
    {
        IncidentRecord[] incidents = [new("fight-14C", 10, null, false), new("panic-3A", 20, 30, true), new("spill-22F", 40, 90, false)];

        var outcome = IncidentOutcome.From(incidents);

        Assert.Equal(["fight-14C", "spill-22F"], outcome.Missed);
        Assert.Equal((3, 1), (outcome.Total, outcome.Handled));
        Assert.Equal(1.0 / 3.0, outcome.HandledShare, Precision);
    }

    /// <summary>Strain takes the highest peak and, from another crew member, the most minutes over the redline.</summary>
    [Fact]
    public void StrainTakesTheWorstCrewMember()
    {
        var strain = StrainOutcome.From([new(40.0, 12.0), new(75.0, 3.0), new(55.0, 0.0)]);

        Assert.Equal((75.0, 12.0), (strain.Peak, strain.MinutesOverRedline));
    }

    /// <summary>A null incident in the list is rejected by name of the list.</summary>
    [Fact]
    public void NullIncidentThrows()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => IncidentOutcome.From([new("a", 0, null, true), null!]));

        Assert.Equal("incidents", exception.ParamName);
    }

    /// <summary>Thresholds out of range, NaN or in the wrong order are rejected as they are made.</summary>
    [Fact]
    public void InvalidThresholdsThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExperienceThresholds(smoothAtLeast: 40.0, badBelow: 70.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExperienceThresholds(smoothAtLeast: 100.01, badBelow: 40.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new IncidentThresholds(badBelowShare: 1.01));
        Assert.Throws<ArgumentOutOfRangeException>(() => new IncidentThresholds(badBelowShare: double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StrainThresholds(smoothPeakBelow: 85.0, badPeakAtLeast: 60.0, badMinutesOver: 15.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StrainThresholds(60.0, 85.0, badMinutesOver: double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DoorsThresholds(smoothAtMost: 10.0, badOver: 2.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DoorsThresholds(smoothAtMost: -1.0, badOver: 2.0));
    }

    /// <summary>No crew, a negative or NaN strain, a crew arrival before the raise, a blank id or a negative tick is rejected.</summary>
    [Fact]
    public void InvalidOutcomeInputThrows()
    {
        Assert.Throws<ArgumentException>(() => StrainOutcome.From([]));
        Assert.Throws<ArgumentOutOfRangeException>(() => StrainOutcome.From([new(-1.0, 0.0)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => StrainOutcome.From([new(50.0, double.NaN)]));
        Assert.Throws<ArgumentException>(() => IncidentOutcome.From([new("late", 100, 99, true)]));
        Assert.Throws<ArgumentException>(() => IncidentOutcome.From([new(" ", 100, null, true)]));
        Assert.Throws<ArgumentException>(() => IncidentOutcome.From([new("early", -1, null, true)]));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DoorsOutcome.From(cabinReadyTick: -1, plannedCabinReadyTick: 0, deboardingDurationTicks: 0, targetDeboardingDurationTicks: 0)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DoorsOutcome.From(cabinReadyTick: 0, plannedCabinReadyTick: 0, deboardingDurationTicks: 0, targetDeboardingDurationTicks: -1)
        );
    }
}
