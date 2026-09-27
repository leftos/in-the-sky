using Sky.Engine.Manifest;
using Sky.Engine.Needs;
using Sky.Engine.Randomness;

namespace Sky.Engine.Passengers;

/// <summary>An inclusive range of need values a starting need is drawn from, uniformly.</summary>
/// <param name="Min">The lowest value.</param>
/// <param name="Max">The highest value.</param>
public readonly record struct NeedRange(double Min, double Max);

/// <summary>
/// Every number of the starting needs and the gate conditions (passengers.md section 2). Each field is checked as it is set;
/// a bad value throws an <see cref="ArgumentException"/> whose parameter name is the field's.
/// </summary>
public sealed record StartingNeedsRules
{
    /// <summary>Gets the range starting Refreshment is drawn from, before the gate conditions.</summary>
    public required NeedRange RefreshmentDraw
    {
        get;
        init => field = NeedRuleChecks.Range(value, nameof(RefreshmentDraw));
    }

    /// <summary>Gets the range starting Bladder is drawn from.</summary>
    public required NeedRange BladderDraw
    {
        get;
        init => field = NeedRuleChecks.Range(value, nameof(BladderDraw));
    }

    /// <summary>Gets the range starting Boredom is drawn from, before the gate conditions.</summary>
    public required NeedRange BoredomDraw
    {
        get;
        init => field = NeedRuleChecks.Range(value, nameof(BoredomDraw));
    }

    /// <summary>Gets how far a later member of a booking may draw Refreshment and Boredom from the first member's, either way.</summary>
    public required double GroupSpread
    {
        get;
        init => field = NeedRuleChecks.Amount(value, nameof(GroupSpread));
    }

    /// <summary>Gets the Refreshment added when the airside outlets were closed.</summary>
    public required double ClosedOutletRefreshment
    {
        get;
        init => field = NeedRuleChecks.Amount(value, nameof(ClosedOutletRefreshment));
    }

    /// <summary>Gets the Refreshment added per minute of gate delay while the outlets were open.</summary>
    public required double OpenRefreshmentPerMinute
    {
        get;
        init => field = NeedRuleChecks.Amount(value, nameof(OpenRefreshmentPerMinute));
    }

    /// <summary>Gets the Refreshment added per minute of gate delay while the outlets were closed.</summary>
    public required double ClosedRefreshmentPerMinute
    {
        get;
        init => field = NeedRuleChecks.Amount(value, nameof(ClosedRefreshmentPerMinute));
    }

    /// <summary>Gets the most Refreshment the gate delay adds, before the closed-outlet Refreshment.</summary>
    public required double DelayRefreshmentCap
    {
        get;
        init => field = NeedRuleChecks.Amount(value, nameof(DelayRefreshmentCap));
    }

    /// <summary>Gets the Boredom added per minute of gate delay.</summary>
    public required double BoredomPerMinute
    {
        get;
        init => field = NeedRuleChecks.Amount(value, nameof(BoredomPerMinute));
    }

    /// <summary>Gets the most Boredom the gate delay adds.</summary>
    public required double BoredomCap
    {
        get;
        init => field = NeedRuleChecks.Amount(value, nameof(BoredomCap));
    }

    /// <summary>Gets the Unease added above the baseline per minute of gate delay past the grace minutes.</summary>
    public required double UneasePerMinute
    {
        get;
        init => field = NeedRuleChecks.Amount(value, nameof(UneasePerMinute));
    }

    /// <summary>Gets the minutes of gate delay that add no Unease.</summary>
    public required double UneaseGraceMinutes
    {
        get;
        init => field = NeedRuleChecks.Amount(value, nameof(UneaseGraceMinutes));
    }

    /// <summary>Gets the most Unease the gate delay adds above the baseline.</summary>
    public required double UneaseCap
    {
        get;
        init => field = NeedRuleChecks.Amount(value, nameof(UneaseCap));
    }

    /// <summary>Gets the gate delay, in minutes, from which every passenger boards late and fed up.</summary>
    public required int LateAndFedUpMinutes
    {
        get;
        init => field = RuleChecks.AtLeast(value, 0, nameof(LateAndFedUpMinutes));
    }

    /// <summary>Gets the most Refreshment a passenger boards with, so nobody boards inside a failure window.</summary>
    public required double RefreshmentCeiling
    {
        get;
        init => field = NeedRuleChecks.NeedValue(value, nameof(RefreshmentCeiling));
    }

    /// <summary>Gets the Rest body clock's numbers.</summary>
    public required BodyClockRules BodyClock
    {
        get;
        init => field = RuleChecks.NotNull(value, nameof(BodyClock));
    }
}

/// <summary>What the cabin went through before boarding, and when boarding starts. Each field is checked as it is set.</summary>
public sealed record BoardingConditions
{
    /// <summary>Gets how many minutes passengers waited past the scheduled boarding time.</summary>
    public required int GateDelayMinutes
    {
        get;
        init => field = RuleChecks.AtLeast(value, 0, nameof(GateDelayMinutes));
    }

    /// <summary>Gets whether the airside food and drink outlets were open during the wait.</summary>
    public required bool ConcessionsOpen { get; init; }

    /// <summary>Gets the minute boarding actually starts, counted from midnight origin local; the flight's tick 0.</summary>
    public required int BoardingStartMinute
    {
        get;
        init => field = NeedRuleChecks.MinuteOfDay(value, BodyClockRules.MinutesPerDay - 1, nameof(BoardingStartMinute));
    }
}

/// <summary>What one trait does to a passenger's needs: its Trait-class factors and the Unease baseline it sets, if any.</summary>
public sealed record TraitEffect
{
    /// <summary>Gets the trait's Trait-class factor on each need it bends; a need not listed is unbent.</summary>
    public required IReadOnlyDictionary<Need, double> Factors
    {
        get;
        init => field = CheckFactors(value, nameof(Factors));
    }

    /// <summary>Gets the Unease baseline the trait sets, or null when it sets none.</summary>
    public double? UneaseBaseline
    {
        get;
        init => field = value is { } baseline ? NeedRuleChecks.NeedValue(baseline, nameof(UneaseBaseline)) : null;
    }

    private static IReadOnlyDictionary<Need, double> CheckFactors(IReadOnlyDictionary<Need, double>? factors, string field)
    {
        ArgumentNullException.ThrowIfNull(factors, field);
        foreach (KeyValuePair<Need, double> entry in factors)
        {
            if (!Enum.IsDefined(entry.Key))
            {
                throw new ArgumentException($"{field} holds a factor on {entry.Key}, which is not a defined Need.", field);
            }

            NeedRuleChecks.Amount(entry.Value, field);
        }

        return factors;
    }
}

/// <summary>The effects of every trait a flight's passengers may carry, and the Unease baseline of a passenger with no Unease trait.</summary>
public sealed record TraitEffects
{
    /// <summary>Gets the Unease baseline of a passenger none of whose traits sets one.</summary>
    public required double DefaultUneaseBaseline
    {
        get;
        init => field = NeedRuleChecks.NeedValue(value, nameof(DefaultUneaseBaseline));
    }

    /// <summary>Gets each trait's effect; every trait a passenger carries must be listed.</summary>
    public required IReadOnlyDictionary<TraitId, TraitEffect> Traits
    {
        get;
        init => field = RuleChecks.NotNull(value, nameof(Traits));
    }

    /// <summary>Gets a passenger's Unease baseline: the highest any of their traits sets, else the default.</summary>
    /// <param name="traits">The passenger's traits.</param>
    /// <returns>The baseline Unease is pulled toward.</returns>
    /// <exception cref="ArgumentException">A trait has no entry in <see cref="Traits"/>.</exception>
    public double UneaseBaselineOf(ReadOnlySpan<TraitId> traits)
    {
        double? highest = null;
        foreach (TraitId trait in traits)
        {
            if (EffectOf(trait).UneaseBaseline is { } baseline && (highest is null || baseline > highest))
            {
                highest = baseline;
            }
        }

        return highest ?? DefaultUneaseBaseline;
    }

    /// <summary>Gets one trait's effect.</summary>
    /// <param name="trait">The trait.</param>
    /// <returns>Its effect.</returns>
    /// <exception cref="ArgumentException">The trait has no entry in <see cref="Traits"/>.</exception>
    public TraitEffect EffectOf(TraitId trait) =>
        Traits.TryGetValue(trait, out TraitEffect? effect)
            ? effect
            : throw new ArgumentException($"Trait {trait.Value} has no entry in the trait effects.", nameof(trait));
}

/// <summary>One passenger's needs at boarding, and whether they board late and fed up.</summary>
public sealed record PassengerStart
{
    /// <summary>Gets starting Refreshment, never above the ceiling.</summary>
    public required double Refreshment { get; init; }

    /// <summary>Gets starting Bladder.</summary>
    public required double Bladder { get; init; }

    /// <summary>Gets starting Rest, from the body clock.</summary>
    public required double Rest { get; init; }

    /// <summary>Gets starting Unease: the baseline plus the gate offset.</summary>
    public required double Unease { get; init; }

    /// <summary>Gets the Unease baseline, from the passenger's traits.</summary>
    public required double UneaseBaseline { get; init; }

    /// <summary>Gets starting Boredom.</summary>
    public required double Boredom { get; init; }

    /// <summary>Gets whether the passenger boards late and fed up.</summary>
    public required bool LateAndFedUp { get; init; }

    /// <summary>Builds the passenger's need set at these values.</summary>
    /// <returns>A new need set.</returns>
    public NeedSet ToNeedSet()
    {
        var needs = new NeedSet(UneaseBaseline);
        needs.Set(Need.Refreshment, Refreshment);
        needs.Set(Need.Bladder, Bladder);
        needs.Set(Need.Rest, Rest);
        needs.Set(Need.Unease, Unease);
        needs.Set(Need.Boredom, Boredom);
        return needs;
    }
}

/// <summary>
/// Draws every passenger's starting needs (passengers.md section 2): the draw, the gate conditions on top of it, Rest from
/// the body clock and Unease from the passenger's baseline.
/// </summary>
public static class StartingNeeds
{
    /// <summary>
    /// Draws the starting needs of every passenger. A booking's first member draws Refreshment, Bladder and Boredom from
    /// their own stream; each later member draws, from their own stream, Refreshment and Boredom within the group spread of
    /// the first member's, clamped to the draw range, and Bladder afresh.
    /// </summary>
    /// <param name="manifest">The passengers and their bookings.</param>
    /// <param name="conditions">The gate conditions and the boarding start.</param>
    /// <param name="rules">The starting-needs numbers.</param>
    /// <param name="traits">The effect of every trait the passengers carry.</param>
    /// <param name="streams">Each passenger's <c>passenger/&lt;id&gt;</c> stream, indexed by passenger id.</param>
    /// <returns>Each passenger's starting needs, indexed by passenger id.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">
    /// The stream count is not the passenger count, a passenger carries a trait with no effect, or a passenger is in no booking.
    /// </exception>
    public static IReadOnlyList<PassengerStart> Draw(
        PassengerManifest manifest,
        BoardingConditions conditions,
        StartingNeedsRules rules,
        TraitEffects traits,
        IReadOnlyList<SimRandom> streams
    )
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(conditions);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(traits);
        ArgumentNullException.ThrowIfNull(streams);
        if (streams.Count != manifest.Passengers.Count)
        {
            throw new ArgumentException($"Expected one stream per passenger, {manifest.Passengers.Count}; got {streams.Count}.", nameof(streams));
        }

        var offsets = GateOffsets.Of(conditions, rules);
        var starts = new PassengerStart?[manifest.Passengers.Count];
        foreach (Booking booking in manifest.Bookings)
        {
            Drawn? first = null;
            foreach (int id in booking.PassengerIds)
            {
                Drawn drawn = first is { } lead ? DrawLaterMember(rules, lead, streams[id]) : DrawFirstMember(rules, streams[id]);
                first ??= drawn;
                ManifestPassenger passenger = manifest.Passengers[id];
                starts[id] = Start(passenger, drawn, offsets, rules, traits);
            }
        }

        return RequireAll(starts, nameof(manifest));
    }

    private static Drawn DrawFirstMember(StartingNeedsRules rules, SimRandom random)
    {
        double refreshment = Uniform(rules.RefreshmentDraw.Min, rules.RefreshmentDraw.Max, random);
        double bladder = Uniform(rules.BladderDraw.Min, rules.BladderDraw.Max, random);
        double boredom = Uniform(rules.BoredomDraw.Min, rules.BoredomDraw.Max, random);
        return new Drawn(refreshment, bladder, boredom);
    }

    private static Drawn DrawLaterMember(StartingNeedsRules rules, Drawn first, SimRandom random)
    {
        double refreshment = NearFirst(first.Refreshment, rules.RefreshmentDraw, rules.GroupSpread, random);
        double bladder = Uniform(rules.BladderDraw.Min, rules.BladderDraw.Max, random);
        double boredom = NearFirst(first.Boredom, rules.BoredomDraw, rules.GroupSpread, random);
        return new Drawn(refreshment, bladder, boredom);
    }

    private static double NearFirst(double first, NeedRange range, double spread, SimRandom random) =>
        Math.Clamp(first + Uniform(-spread, spread, random), range.Min, range.Max);

    private static double Uniform(double min, double max, SimRandom random) => min + (random.NextDouble() * (max - min));

    private static PassengerStart Start(ManifestPassenger passenger, Drawn drawn, GateOffsets offsets, StartingNeedsRules rules, TraitEffects traits)
    {
        double baseline = traits.UneaseBaselineOf(passenger.Traits.Span);
        return new PassengerStart
        {
            Refreshment = Math.Min(drawn.Refreshment + offsets.Refreshment, rules.RefreshmentCeiling),
            Bladder = drawn.Bladder,
            Rest = BodyClock.StartingRest(rules.BodyClock, passenger.WakeMinute, offsets.BoardingStartMinute),
            Unease = Math.Min(baseline + offsets.Unease, NeedSet.Max),
            UneaseBaseline = baseline,
            Boredom = Math.Min(drawn.Boredom + offsets.Boredom, NeedSet.Max),
            LateAndFedUp = offsets.LateAndFedUp,
        };
    }

    private static PassengerStart[] RequireAll(PassengerStart?[] starts, string paramName)
    {
        var all = new PassengerStart[starts.Length];
        for (int id = 0; id < starts.Length; id++)
        {
            all[id] = starts[id] ?? throw new ArgumentException($"Passenger {id} is in no booking.", paramName);
        }

        return all;
    }

    /// <summary>A booking member's drawn values, before the gate conditions.</summary>
    private readonly record struct Drawn(double Refreshment, double Bladder, double Boredom);

    /// <summary>What the gate conditions add to every passenger's starting needs, and the boarding start the body clock reads.</summary>
    private readonly record struct GateOffsets(double Refreshment, double Boredom, double Unease, bool LateAndFedUp, int BoardingStartMinute)
    {
        public static GateOffsets Of(BoardingConditions conditions, StartingNeedsRules rules)
        {
            double delay = conditions.GateDelayMinutes;
            bool open = conditions.ConcessionsOpen;
            double refreshmentPerMinute = open ? rules.OpenRefreshmentPerMinute : rules.ClosedRefreshmentPerMinute;
            double refreshment = (open ? 0.0 : rules.ClosedOutletRefreshment) + Math.Min(delay * refreshmentPerMinute, rules.DelayRefreshmentCap);
            double boredom = Math.Min(rules.BoredomPerMinute * delay, rules.BoredomCap);
            double unease = Math.Min(rules.UneasePerMinute * Math.Max(delay - rules.UneaseGraceMinutes, 0.0), rules.UneaseCap);
            bool lateAndFedUp = conditions.GateDelayMinutes >= rules.LateAndFedUpMinutes;
            return new GateOffsets(refreshment, boredom, unease, lateAndFedUp, conditions.BoardingStartMinute);
        }
    }
}

/// <summary>The field checks of the starting-needs and body-clock rules; a bad value throws naming the field.</summary>
internal static class NeedRuleChecks
{
    public static double Amount(double value, string field) =>
        double.IsFinite(value) && value >= 0 ? value : throw new ArgumentException($"{field} must be finite and at least 0; it is {value}.", field);

    public static double NeedValue(double value, string field) =>
        value is >= NeedSet.Min and <= NeedSet.Max ? value : throw new ArgumentException($"{field} must lie in [0, 100]; it is {value}.", field);

    public static int MinuteOfDay(int value, int highest, string field) =>
        value >= 0 && value <= highest ? value : throw new ArgumentException($"{field} must lie in [0, {highest}]; it is {value}.", field);

    public static NeedRange Range(NeedRange range, string field)
    {
        NeedValue(range.Min, field);
        NeedValue(range.Max, field);
        return range.Min <= range.Max ? range : throw new ArgumentException($"{field} runs from {range.Min} down to {range.Max}.", field);
    }
}
