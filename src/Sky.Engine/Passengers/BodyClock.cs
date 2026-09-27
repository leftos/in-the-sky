namespace Sky.Engine.Passengers;

/// <summary>One stretch of the origin-local day and the factor on Rest's rise while awake within it.</summary>
/// <param name="StartMinute">The first minute of the stretch, counted from midnight origin local.</param>
/// <param name="EndMinute">The minute the stretch ends at, exclusive.</param>
/// <param name="Factor">The Trait-class factor on Rest's rise while awake within the stretch.</param>
public readonly record struct RestRiseBand(int StartMinute, int EndMinute, double Factor);

/// <summary>
/// Every number of the Rest body clock (passengers.md section 6). Each field is checked as it is set; a bad value throws an
/// <see cref="ArgumentException"/> whose parameter name is the field's.
/// </summary>
public sealed record BodyClockRules
{
    /// <summary>The minutes in one origin-local day.</summary>
    public const int MinutesPerDay = 1440;

    /// <summary>Gets how far Rest has risen at boarding for each hour the passenger has been awake.</summary>
    public required double RestPerHourAwake
    {
        get;
        init => field = NeedRuleChecks.Amount(value, nameof(RestPerHourAwake));
    }

    /// <summary>
    /// Gets the minute after midnight before which a wake-up counts as a short night, in [0, 1440]: an exclusive bound, so
    /// 1440 makes every wake-up of the day a short night and 0 makes none.
    /// </summary>
    public required int EarlyWakeBeforeMinute
    {
        get;
        init => field = NeedRuleChecks.MinuteOfDay(value, MinutesPerDay, nameof(EarlyWakeBeforeMinute));
    }

    /// <summary>Gets the Rest a short night adds at boarding.</summary>
    public required double EarlyWakeRest
    {
        get;
        init => field = NeedRuleChecks.Amount(value, nameof(EarlyWakeRest));
    }

    /// <summary>Gets the most Rest a passenger boards with.</summary>
    public required double StartingRestCap
    {
        get;
        init => field = NeedRuleChecks.NeedValue(value, nameof(StartingRestCap));
    }

    /// <summary>
    /// Gets the stretches of the day with their factor on Rest's rise, in order and not overlapping; a minute outside every
    /// band takes <see cref="BodyClock.FactorOutsideBands"/>.
    /// </summary>
    public required IReadOnlyList<RestRiseBand> RestRiseBands
    {
        get;
        init => field = CheckBands(value, nameof(RestRiseBands));
    }

    private static IReadOnlyList<RestRiseBand> CheckBands(IReadOnlyList<RestRiseBand>? bands, string field)
    {
        ArgumentNullException.ThrowIfNull(bands, field);
        int previousEnd = 0;
        foreach (RestRiseBand band in bands)
        {
            if (band.StartMinute < previousEnd || band.EndMinute <= band.StartMinute || band.EndMinute > MinutesPerDay)
            {
                throw new ArgumentException(
                    $"{field} must run in order within [0, {MinutesPerDay}] without overlapping; [{band.StartMinute}, {band.EndMinute}) does not.",
                    field
                );
            }

            NeedRuleChecks.Amount(band.Factor, field);
            previousEnd = band.EndMinute;
        }

        return bands;
    }
}

/// <summary>The Rest body clock of a day departure: Rest at boarding from the hours awake, and the factor on its rise by the time of day.</summary>
public static class BodyClock
{
    /// <summary>The factor on Rest's rise at a minute no band covers.</summary>
    public const double FactorOutsideBands = 1.0;

    private const double MinutesPerHour = 60.0;

    /// <summary>
    /// Works out Rest at boarding: the rise per hour awake times the hours awake, plus the short-night Rest when the
    /// passenger woke before the early-wake minute, capped at the starting cap.
    /// </summary>
    /// <param name="rules">The body clock's numbers.</param>
    /// <param name="wakeMinute">The minute the passenger woke, counted from midnight origin local.</param>
    /// <param name="boardingStartMinute">The minute boarding starts, counted from midnight origin local.</param>
    /// <returns>Rest at boarding; the hours awake are fractional and never below 0.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="rules"/> is null.</exception>
    public static double StartingRest(BodyClockRules rules, int wakeMinute, int boardingStartMinute)
    {
        ArgumentNullException.ThrowIfNull(rules);
        double hoursAwake = Math.Max(boardingStartMinute - wakeMinute, 0) / MinutesPerHour;
        double shortNight = wakeMinute < rules.EarlyWakeBeforeMinute ? rules.EarlyWakeRest : 0.0;
        return Math.Min((rules.RestPerHourAwake * hoursAwake) + shortNight, rules.StartingRestCap);
    }

    /// <summary>Gets the factor on Rest's rise at a time of day: the covering band's, or <see cref="FactorOutsideBands"/>.</summary>
    /// <param name="rules">The body clock's numbers.</param>
    /// <param name="minuteOfDay">The time of day, in minutes after midnight origin local, in [0, 1440).</param>
    /// <returns>The Trait-class factor on Rest's rise while awake.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="rules"/> is null.</exception>
    public static double RestRiseFactor(BodyClockRules rules, double minuteOfDay)
    {
        ArgumentNullException.ThrowIfNull(rules);
        foreach (RestRiseBand band in rules.RestRiseBands)
        {
            if (minuteOfDay >= band.StartMinute && minuteOfDay < band.EndMinute)
            {
                return band.Factor;
            }
        }

        return FactorOutsideBands;
    }
}
