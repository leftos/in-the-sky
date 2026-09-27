using Sky.Engine.Time;

namespace Sky.Engine.Needs;

/// <summary>
/// One passenger's five needs, each clamped to [<see cref="Min"/>, <see cref="Max"/>], with the pulses still landing on
/// them and the baseline Unease is pulled toward. Unease is pushed up by the sources the context sums and by one-off
/// pushes, both scaled by the passenger's Unease multiplier, relieved unscaled, and pulled toward its baseline each tick.
/// </summary>
public sealed class NeedSet
{
    /// <summary>The number of needs, one slot each in the value and multiplier arrays.</summary>
    public const int NeedCount = (int)Need.Boredom + 1;

    /// <summary>The value of a need that is fine.</summary>
    public const double Min = 0.0;

    /// <summary>The value of a need at its worst.</summary>
    public const double Max = 100.0;

    private readonly double[] values = new double[NeedCount];

    private readonly List<Pulse> pulses = [];

    private readonly double uneaseBaseline;

    /// <summary>Starts every need at 0 except Unease, which starts at its baseline.</summary>
    /// <param name="uneaseBaseline">The value Unease is pulled toward, from the passenger's traits.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="uneaseBaseline"/> is outside [0, 100] or NaN.</exception>
    public NeedSet(double uneaseBaseline)
    {
        if (!(uneaseBaseline is >= Min and <= Max))
        {
            throw new ArgumentOutOfRangeException(
                nameof(uneaseBaseline),
                uneaseBaseline,
                $"The Unease baseline must be in [0, 100]; got {uneaseBaseline}."
            );
        }

        this.uneaseBaseline = uneaseBaseline;
        values[(int)Need.Unease] = uneaseBaseline;
    }

    /// <summary>Gets the current value of a need.</summary>
    /// <param name="need">The need to read.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="need"/> is not a defined need.</exception>
    public double this[Need need] => values[Index(need, nameof(need))];

    /// <summary>Gets the number of pulses still landing; a pulse leaves the count after its last tick.</summary>
    public int PendingPulseCount => pulses.Count;

    /// <summary>
    /// Sets a need outright, for a scenario's starting values. A value outside [0, 100] is rejected rather than clamped,
    /// because it comes from load data and a bad load must stop.
    /// </summary>
    /// <param name="need">The need to set.</param>
    /// <param name="value">The value, in [0, 100].</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="need"/> is undefined, or <paramref name="value"/> is outside [0, 100] or NaN.
    /// </exception>
    public void Set(Need need, double value)
    {
        int index = Index(need, nameof(need));
        if (!(value is >= Min and <= Max))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"Need {need} must be set within [0, 100]; got {value}.");
        }

        values[index] = value;
    }

    /// <summary>
    /// Changes a need by <paramref name="delta"/> at once, clamped to [0, 100]. On Unease only relief goes through here,
    /// unscaled; a push up on Unease goes through <see cref="PushUnease"/>, which applies the Unease multiplier.
    /// </summary>
    /// <param name="need">The need to change.</param>
    /// <param name="delta">The change, negative to relieve the need; not positive on Unease.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="need"/> is undefined, <paramref name="delta"/> is not finite, or <paramref name="delta"/> is positive on Unease.
    /// </exception>
    public void Add(Need need, double delta)
    {
        int index = Index(need, nameof(need));
        RequireFinite(delta, nameof(delta));
        if (need == Need.Unease && delta > 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(delta),
                delta,
                $"A push up on Unease must go through {nameof(PushUnease)}, which applies the Unease multiplier; got {delta}."
            );
        }

        values[index] = Math.Clamp(values[index] + delta, Min, Max);
    }

    /// <summary>Pushes Unease up at once by <paramref name="amount"/> times <paramref name="multiplier"/>, clamped to 100.</summary>
    /// <param name="amount">The one-off push, finite and non-negative.</param>
    /// <param name="multiplier">The passenger's composed Unease multiplier this tick, finite and non-negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="amount"/> or <paramref name="multiplier"/> is negative, NaN or infinite.
    /// </exception>
    public void PushUnease(double amount, double multiplier)
    {
        RequireFiniteNonNegative(amount, nameof(amount));
        RequireFiniteNonNegative(multiplier, nameof(multiplier));
        ref double unease = ref values[(int)Need.Unease];
        unease = Math.Clamp(unease + (amount * multiplier), Min, Max);
    }

    /// <summary>
    /// Queues <paramref name="amount"/> to land on a need over the next <paramref name="ticks"/> ticks; each tick lands the
    /// pulse's remainder divided by its remaining ticks, so the whole amount has landed when the last tick ends. On Unease,
    /// each positive slice is scaled by that tick's Unease multiplier and a negative slice is relief, unscaled; the pull
    /// toward the baseline works during the window too. A one-off push on Unease uses <see cref="PushUnease"/>.
    /// </summary>
    /// <param name="need">The need the pulse lands on.</param>
    /// <param name="amount">The total change, negative to relieve the need.</param>
    /// <param name="ticks">The ticks the pulse is spread over, at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="need"/> is undefined, <paramref name="amount"/> is not finite, or <paramref name="ticks"/> is below 1.
    /// </exception>
    public void AddPulse(Need need, double amount, int ticks)
    {
        _ = Index(need, nameof(need));
        RequireFinite(amount, nameof(amount));
        ArgumentOutOfRangeException.ThrowIfLessThan(ticks, 1);
        pulses.Add(new Pulse(need, amount, ticks));
    }

    /// <summary>
    /// Advances the needs one tick: the base changes and the Unease push scaled by their multipliers, then the pending
    /// pulses in the order added, then the Unease pull toward the baseline, then the clamp to [0, 100].
    /// </summary>
    /// <param name="rates">The flight's per-tick rates.</param>
    /// <param name="multipliers">
    /// One composed multiplier per need, indexed by <see cref="Need"/>. Each scales its need's base change, Rest's only
    /// while awake: Rest's fall asleep is the flight's rate exactly. The Unease entry scales this tick's push and the
    /// positive pulse slices on Unease, and leaves relief and the pull toward the baseline alone.
    /// </param>
    /// <param name="context">Whether the passenger is asleep or on IFE this tick, and the Unease push per hour on them.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rates"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="multipliers"/> does not hold exactly one entry per need.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A multiplier, or the context's Unease push, is negative, NaN or infinite.</exception>
    public void Tick(NeedRates rates, ReadOnlySpan<double> multipliers, NeedContext context)
    {
        ArgumentNullException.ThrowIfNull(rates);
        ValidateMultipliers(multipliers);
        RequireFiniteNonNegative(context.UneasePushPerHour, $"{nameof(context)}.{nameof(context.UneasePushPerHour)}");
        ApplyBaseChanges(rates, multipliers, context);
        ApplyPulses(multipliers[(int)Need.Unease]);
        ref double unease = ref values[(int)Need.Unease];
        unease = uneaseBaseline + ((unease - uneaseBaseline) * rates.UneasePullFactor);
        for (int index = 0; index < NeedCount; index++)
        {
            values[index] = Math.Clamp(values[index], Min, Max);
        }
    }

    private void ApplyBaseChanges(NeedRates rates, ReadOnlySpan<double> multipliers, NeedContext context)
    {
        values[(int)Need.Refreshment] += rates.RefreshmentPerTick * multipliers[(int)Need.Refreshment];
        values[(int)Need.Bladder] += rates.BladderPerTick * multipliers[(int)Need.Bladder];

        values[(int)Need.Rest] += context.IsAsleep ? -rates.RestFallPerTick : rates.RestRisePerTick * multipliers[(int)Need.Rest];

        if (!context.IsAsleep && !context.OnIfe)
        {
            values[(int)Need.Boredom] += rates.BoredomPerTick * multipliers[(int)Need.Boredom];
        }

        values[(int)Need.Unease] += context.UneasePushPerHour / SimTime.TicksPerSimHour * multipliers[(int)Need.Unease];
    }

    private void ApplyPulses(double uneaseMultiplier)
    {
        int kept = 0;
        for (int index = 0; index < pulses.Count; index++)
        {
            Pulse pulse = pulses[index];
            double share = pulse.Remaining / pulse.RemainingTicks;
            values[(int)pulse.Need] += pulse.Need == Need.Unease && share > 0.0 ? share * uneaseMultiplier : share;
            if (pulse.RemainingTicks > 1)
            {
                pulses[kept] = pulse with { Remaining = pulse.Remaining - share, RemainingTicks = pulse.RemainingTicks - 1 };
                kept++;
            }
        }

        pulses.RemoveRange(kept, pulses.Count - kept);
    }

    private static void ValidateMultipliers(ReadOnlySpan<double> multipliers)
    {
        if (multipliers.Length != NeedCount)
        {
            throw new ArgumentException($"Expected {NeedCount} multipliers, one per need; got {multipliers.Length}.", nameof(multipliers));
        }

        foreach (double multiplier in multipliers)
        {
            if (!double.IsFinite(multiplier) || multiplier < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(multipliers),
                    multiplier,
                    $"Need multipliers must be finite and non-negative; got {multiplier}."
                );
            }
        }
    }

    private static int Index(Need need, string paramName)
    {
        if ((uint)need >= NeedCount)
        {
            throw new ArgumentOutOfRangeException(paramName, need, $"Need {need} is not a defined Need.");
        }

        return (int)need;
    }

    private static void RequireFinite(double value, string paramName)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"The value must be finite; got {value}.");
        }
    }

    private static void RequireFiniteNonNegative(double value, string paramName)
    {
        if (!double.IsFinite(value) || value < 0.0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"The value must be finite and non-negative; got {value}.");
        }
    }

    private readonly record struct Pulse(Need Need, double Remaining, int RemainingTicks);
}
