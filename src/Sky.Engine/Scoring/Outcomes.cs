using Sky.Engine.Time;

namespace Sky.Engine.Scoring;

/// <summary>One incident of a flight, as the score reads it.</summary>
/// <param name="Id">The incident's name, listed when it was missed.</param>
/// <param name="RaisedTick">The tick the incident was raised on.</param>
/// <param name="CrewArrivedTick">
/// The tick crew arrived at it; null when the incident resolved without crew. An arrival on a missed incident still enters
/// the median time to crew.
/// </param>
/// <param name="Handled">Whether crew resolved it before its consequence fired or its escalation deadline passed.</param>
public sealed record IncidentRecord(string Id, long RaisedTick, long? CrewArrivedTick, bool Handled);

/// <summary>A crew member's strain over the flight, as the score reads it.</summary>
/// <param name="Peak">The crew member's highest strain.</param>
/// <param name="MinutesOverRedline">The sim minutes the crew member spent over the strain redline.</param>
public readonly record struct CrewStrainSummary(double Peak, double MinutesOverRedline);

/// <summary>Incidents handled or missed (CONCEPT section 6): handled over total, the median time to crew, the missed by name.</summary>
public sealed class IncidentOutcome
{
    private IncidentOutcome(int total, int handled, double? medianMinutesToCrew, IReadOnlyList<string> missed)
    {
        Total = total;
        Handled = handled;
        MedianMinutesToCrew = medianMinutesToCrew;
        Missed = missed;
    }

    /// <summary>How many incidents the flight had.</summary>
    public int Total { get; }

    /// <summary>How many incidents crew handled.</summary>
    public int Handled { get; }

    /// <summary>The share of incidents handled; 1 when there were none, which counts as all handled.</summary>
    public double HandledShare => Total == 0 ? 1.0 : (double)Handled / Total;

    /// <summary>The nearest-rank median sim minutes from an incident to crew arrival, over those crew reached; null when none.</summary>
    public double? MedianMinutesToCrew { get; }

    /// <summary>The ids of the incidents crew did not handle, in input order.</summary>
    public IReadOnlyList<string> Missed { get; }

    /// <summary>Scores a flight's incidents.</summary>
    /// <param name="incidents">Every incident of the flight, in the order they are to be listed.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentNullException">An incident in the list is null.</exception>
    /// <exception cref="ArgumentException">An id is blank, a tick is negative, or crew arrived before the incident was raised.</exception>
    public static IncidentOutcome From(ReadOnlySpan<IncidentRecord> incidents)
    {
        List<double> minutesToCrew = [];
        List<string> missed = [];
        foreach (IncidentRecord incident in incidents)
        {
            ThrowIfInvalid(incident, nameof(incidents));
            if (incident.CrewArrivedTick is long arrived)
            {
                minutesToCrew.Add((double)(arrived - incident.RaisedTick) / SimTime.TicksPerSimMinute);
            }

            if (!incident.Handled)
            {
                missed.Add(incident.Id);
            }
        }

        minutesToCrew.Sort();
        double? median = minutesToCrew.Count == 0 ? null : NearestRank.Median(minutesToCrew);
        return new IncidentOutcome(incidents.Length, incidents.Length - missed.Count, median, [.. missed]);
    }

    /// <summary>Smooth when every incident was handled, Bad when the handled share is below the line, otherwise Rough.</summary>
    /// <param name="thresholds">The incident thresholds.</param>
    /// <returns>The verdict.</returns>
    public Verdict VerdictFor(IncidentThresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(thresholds);
        if (Handled == Total)
        {
            return Verdict.Smooth;
        }

        return HandledShare < thresholds.BadBelowShare ? Verdict.Bad : Verdict.Rough;
    }

    private static void ThrowIfInvalid(IncidentRecord incident, string parameterName)
    {
        if (incident is null)
        {
            throw new ArgumentNullException(parameterName, "The incident list holds a null incident.");
        }

        if (string.IsNullOrWhiteSpace(incident.Id) || incident.RaisedTick < 0 || incident.CrewArrivedTick < incident.RaisedTick)
        {
            throw new ArgumentException(
                $"Incident '{incident.Id}' needs a non-blank id, a raised tick of at least 0 and no crew arrival before it was raised.",
                parameterName
            );
        }
    }
}

/// <summary>Crew strain (CONCEPT section 6): the peak of the most strained crew member and the most minutes any one spent over the redline.</summary>
public sealed class StrainOutcome
{
    private StrainOutcome(double peak, double minutesOverRedline)
    {
        Peak = peak;
        MinutesOverRedline = minutesOverRedline;
    }

    /// <summary>The highest peak strain of any crew member.</summary>
    public double Peak { get; }

    /// <summary>The most sim minutes any one crew member spent over the redline.</summary>
    public double MinutesOverRedline { get; }

    /// <summary>Scores a flight's crew strain from each crew member's summary.</summary>
    /// <param name="crew">One summary per crew member, each value finite and non-negative.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentException">There are no crew.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A value is negative, NaN or infinite.</exception>
    public static StrainOutcome From(ReadOnlySpan<CrewStrainSummary> crew)
    {
        if (crew.IsEmpty)
        {
            throw new ArgumentException("There are no crew; strain needs at least one crew member's summary.", nameof(crew));
        }

        double peak = 0.0;
        double minutesOverRedline = 0.0;
        foreach (CrewStrainSummary member in crew)
        {
            RangeGuard.ThrowIfBelow(member.Peak, 0.0, nameof(crew));
            RangeGuard.ThrowIfBelow(member.MinutesOverRedline, 0.0, nameof(crew));
            peak = Math.Max(peak, member.Peak);
            minutesOverRedline = Math.Max(minutesOverRedline, member.MinutesOverRedline);
        }

        return new StrainOutcome(peak, minutesOverRedline);
    }

    /// <summary>
    /// Bad when the peak is at or above its line or the minutes over the redline are above theirs; Smooth when the peak is
    /// below the Smooth line with no minutes over the redline; otherwise Rough.
    /// </summary>
    /// <param name="thresholds">The strain thresholds.</param>
    /// <returns>The verdict.</returns>
    public Verdict VerdictFor(StrainThresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(thresholds);
        if (Peak >= thresholds.BadPeakAtLeast || MinutesOverRedline > thresholds.BadMinutesOver)
        {
            return Verdict.Bad;
        }

        return Peak < thresholds.SmoothPeakBelow && MinutesOverRedline == 0.0 ? Verdict.Smooth : Verdict.Rough;
    }
}

/// <summary>On-time doors (CONCEPT section 6): cabin ready against plan and deboarding against target, in sim minutes late.</summary>
public sealed class DoorsOutcome
{
    private DoorsOutcome(double cabinReadyMinutesLate, double deboardingMinutesLate)
    {
        CabinReadyMinutesLate = cabinReadyMinutesLate;
        DeboardingMinutesLate = deboardingMinutesLate;
    }

    /// <summary>The sim minutes cabin ready came after its planned tick; 0 when on time or early.</summary>
    public double CabinReadyMinutesLate { get; }

    /// <summary>The sim minutes deboarding took past its target duration; 0 when within it.</summary>
    public double DeboardingMinutesLate { get; }

    /// <summary>The worse of the two lateness figures.</summary>
    public double MinutesLate => Math.Max(CabinReadyMinutesLate, DeboardingMinutesLate);

    /// <summary>Scores a flight's doors.</summary>
    /// <param name="cabinReadyTick">The tick the cabin was ready.</param>
    /// <param name="plannedCabinReadyTick">The tick the cabin was planned to be ready.</param>
    /// <param name="deboardingDurationTicks">The ticks from door open to the last passenger off.</param>
    /// <param name="targetDeboardingDurationTicks">The target deboarding duration, in ticks.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A tick or duration is negative.</exception>
    public static DoorsOutcome From(long cabinReadyTick, long plannedCabinReadyTick, long deboardingDurationTicks, long targetDeboardingDurationTicks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cabinReadyTick);
        ArgumentOutOfRangeException.ThrowIfNegative(plannedCabinReadyTick);
        ArgumentOutOfRangeException.ThrowIfNegative(deboardingDurationTicks);
        ArgumentOutOfRangeException.ThrowIfNegative(targetDeboardingDurationTicks);
        return new DoorsOutcome(
            LateMinutes(cabinReadyTick, plannedCabinReadyTick),
            LateMinutes(deboardingDurationTicks, targetDeboardingDurationTicks)
        );
    }

    /// <summary>Smooth at or below the Smooth line, Bad above the Bad line, otherwise Rough, on <see cref="MinutesLate"/>.</summary>
    /// <param name="thresholds">The doors thresholds.</param>
    /// <returns>The verdict.</returns>
    public Verdict VerdictFor(DoorsThresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(thresholds);
        if (MinutesLate > thresholds.BadOver)
        {
            return Verdict.Bad;
        }

        return MinutesLate <= thresholds.SmoothAtMost ? Verdict.Smooth : Verdict.Rough;
    }

    private static double LateMinutes(long actual, long plan) => (double)Math.Max(0L, actual - plan) / SimTime.TicksPerSimMinute;
}
