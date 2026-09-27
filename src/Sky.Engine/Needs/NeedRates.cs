using Sky.Engine.Time;

namespace Sky.Engine.Needs;

/// <summary>The base need rates of a flight, per sim hour, and the Unease half-life.</summary>
public sealed record NeedRateSettings
{
    /// <summary>How far Refreshment rises in one sim hour.</summary>
    public required double RefreshmentPerHour { get; init; }

    /// <summary>How far Bladder rises in one sim hour.</summary>
    public required double BladderPerHour { get; init; }

    /// <summary>How far Rest rises in one sim hour awake.</summary>
    public required double RestRisePerHour { get; init; }

    /// <summary>How far Rest falls in one sim hour asleep.</summary>
    public required double RestFallPerHour { get; init; }

    /// <summary>How far Boredom rises in one sim hour awake and off IFE.</summary>
    public required double BoredomPerHour { get; init; }

    /// <summary>The sim minutes Unease takes to close half its gap to the passenger's baseline.</summary>
    public required double UneaseHalfLifeMinutes { get; init; }
}

/// <summary>A flight's need rates turned into per-tick changes, built once and shared by every passenger.</summary>
public sealed class NeedRates
{
    /// <summary>Builds the per-tick changes from hourly settings.</summary>
    /// <param name="settings">The hourly rates and the Unease half-life.</param>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A rate is negative, NaN or infinite, or the half-life is not positive and finite.</exception>
    public NeedRates(NeedRateSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        RefreshmentPerTick = PerTick(settings.RefreshmentPerHour, nameof(settings.RefreshmentPerHour), nameof(settings));
        BladderPerTick = PerTick(settings.BladderPerHour, nameof(settings.BladderPerHour), nameof(settings));
        RestRisePerTick = PerTick(settings.RestRisePerHour, nameof(settings.RestRisePerHour), nameof(settings));
        RestFallPerTick = PerTick(settings.RestFallPerHour, nameof(settings.RestFallPerHour), nameof(settings));
        BoredomPerTick = PerTick(settings.BoredomPerHour, nameof(settings.BoredomPerHour), nameof(settings));

        double halfLife = settings.UneaseHalfLifeMinutes;
        if (!double.IsFinite(halfLife) || halfLife <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                halfLife,
                $"{nameof(settings.UneaseHalfLifeMinutes)} must be positive and finite; got {halfLife}."
            );
        }

        UneasePullFactor = Math.Pow(0.5, 1.0 / (halfLife * SimTime.TicksPerSimMinute));
    }

    /// <summary>How far Refreshment rises in one tick.</summary>
    public double RefreshmentPerTick { get; }

    /// <summary>How far Bladder rises in one tick.</summary>
    public double BladderPerTick { get; }

    /// <summary>How far Rest rises in one tick awake.</summary>
    public double RestRisePerTick { get; }

    /// <summary>How far Rest falls in one tick asleep.</summary>
    public double RestFallPerTick { get; }

    /// <summary>How far Boredom rises in one tick awake and off IFE.</summary>
    public double BoredomPerTick { get; }

    /// <summary>The share of Unease's gap to its baseline that remains after one tick: <c>0.5^(1 / half-life ticks)</c>.</summary>
    public double UneasePullFactor { get; }

    private static double PerTick(double perHour, string name, string paramName)
    {
        if (!double.IsFinite(perHour) || perHour < 0.0)
        {
            throw new ArgumentOutOfRangeException(paramName, perHour, $"{name} must be finite and non-negative; got {perHour}.");
        }

        return perHour / SimTime.TicksPerSimHour;
    }
}
