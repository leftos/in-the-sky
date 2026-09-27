using Sky.Engine.Passengers;

namespace Sky.Engine.Manifest;

/// <summary>
/// Every number the manifest generator draws by (passengers.md sections 4 to 6). Each field is checked as it is set: weights
/// are finite and at least 0 with a positive total, shares lie in [0, 1], and ranges are ordered; a bad value throws an
/// <see cref="ArgumentException"/> whose parameter name is the field's.
/// </summary>
public sealed record ManifestRules
{
    /// <summary>Gets the range the load factor is drawn from, uniformly, as a share of the layout's seats.</summary>
    public required ShareRange LoadFactor
    {
        get;
        init => field = RuleChecks.Ordered(value, nameof(LoadFactor));
    }

    /// <summary>Gets the range the number of business passengers is drawn from, uniformly, before it is capped by the seats.</summary>
    public required IntRange BusinessBooked
    {
        get;
        init => field = RuleChecks.Ordered(value, 0, int.MaxValue - 1, nameof(BusinessBooked));
    }

    /// <summary>Gets how many rows, from the front, are business class.</summary>
    public required int BusinessRowCount
    {
        get;
        init => field = RuleChecks.AtLeast(value, 0, nameof(BusinessRowCount));
    }

    /// <summary>Gets the trip purpose weights of a booking in business class.</summary>
    public required IReadOnlyList<WeightedOption<TripPurpose>> BusinessCabinPurposes
    {
        get;
        init => field = RuleChecks.Options(value, nameof(BusinessCabinPurposes), RuleChecks.IsDefined);
    }

    /// <summary>Gets the trip purpose weights of a booking in economy class.</summary>
    public required IReadOnlyList<WeightedOption<TripPurpose>> EconomyCabinPurposes
    {
        get;
        init => field = RuleChecks.Options(value, nameof(EconomyCabinPurposes), RuleChecks.IsDefined);
    }

    /// <summary>Gets the rules of a <see cref="TripPurpose.Business"/> booking.</summary>
    public required TripPurposeRules BusinessTrip
    {
        get;
        init => field = RuleChecks.NotNull(value, nameof(BusinessTrip));
    }

    /// <summary>Gets the rules of a <see cref="TripPurpose.Leisure"/> booking.</summary>
    public required TripPurposeRules LeisureTrip
    {
        get;
        init => field = RuleChecks.NotNull(value, nameof(LeisureTrip));
    }

    /// <summary>Gets the rules of a <see cref="TripPurpose.Visiting"/> booking.</summary>
    public required TripPurposeRules VisitingTrip
    {
        get;
        init => field = RuleChecks.NotNull(value, nameof(VisitingTrip));
    }

    /// <summary>Gets the smallest booking that may be a family, as drawn before any truncation.</summary>
    public required int FamilyMinimumSize
    {
        get;
        init => field = RuleChecks.AtLeast(value, 1, nameof(FamilyMinimumSize));
    }

    /// <summary>Gets how many adults a family has; the rest of its members are children.</summary>
    public required int FamilyAdults
    {
        get;
        init => field = RuleChecks.AtLeast(value, 1, nameof(FamilyAdults));
    }

    /// <summary>Gets the most minutes a member wakes after their booking's wake time.</summary>
    public required int WakeSpreadMinutes
    {
        get;
        init => field = RuleChecks.AtLeast(value, 0, nameof(WakeSpreadMinutes));
    }

    /// <summary>Gets the weights of how many personality traits an adult draws.</summary>
    public required IReadOnlyList<WeightedOption<int>> AdultTraitCounts
    {
        get;
        init => field = RuleChecks.Options(value, nameof(AdultTraitCounts), count => count >= 0);
    }

    /// <summary>Gets the trait every child carries first, and no adult draws.</summary>
    public required TraitId ChildTrait { get; init; }

    /// <summary>Gets the share of children who draw one more trait from those a child may draw.</summary>
    public required double ChildExtraTraitShare
    {
        get;
        init => field = RuleChecks.Share(value, nameof(ChildExtraTraitShare));
    }

    /// <summary>Gets the drawable personality traits and their weights.</summary>
    public required IReadOnlyList<TraitRule> Traits
    {
        get;
        init => field = RuleChecks.Traits(value, nameof(Traits));
    }

    /// <summary>Gets the pairs of traits no passenger carries together.</summary>
    public required IReadOnlyList<TraitPair> ForbiddenPairs
    {
        get;
        init => field = [.. RuleChecks.NotNull(value, nameof(ForbiddenPairs))];
    }

    /// <summary>Gets the belongings, each rolled on its own after the traits.</summary>
    public required IReadOnlyList<BelongingRule> Belongings
    {
        get;
        init => field = RuleChecks.Belongings(value, nameof(Belongings));
    }

    /// <summary>Gets the professions an adult draws from; at least one must carry weight on a business trip.</summary>
    public required IReadOnlyList<ProfessionRule> Professions
    {
        get;
        init => field = RuleChecks.Professions(value, nameof(Professions));
    }

    /// <summary>Returns the rules of one trip purpose.</summary>
    /// <param name="purpose">The trip purpose.</param>
    /// <returns>Its rules.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="purpose"/> is not a defined trip purpose.</exception>
    public TripPurposeRules For(TripPurpose purpose) =>
        purpose switch
        {
            TripPurpose.Business => BusinessTrip,
            TripPurpose.Leisure => LeisureTrip,
            TripPurpose.Visiting => VisitingTrip,
            _ => throw new ArgumentOutOfRangeException(nameof(purpose), purpose, "Not a defined trip purpose."),
        };

    /// <summary>
    /// Checks what one field's own check cannot see: <see cref="ChildTrait"/> is not a drawable trait, no belonging is the
    /// child trait or a drawable trait, and no trait, belonging or profession is listed twice. Public because the content
    /// loader calls it once the rules are built from content, and turns its failure into a load error (R10).
    /// </summary>
    /// <exception cref="ArgumentException">An id clashes; the parameter name is the field that holds the clash.</exception>
    public void CheckAcrossFields()
    {
        HashSet<TraitId> traits = [];
        foreach (TraitRule rule in Traits)
        {
            RuleChecks.Unique(traits.Add(rule.Trait), $"trait {rule.Trait.Value}", nameof(Traits));
        }

        if (traits.Contains(ChildTrait))
        {
            throw new ArgumentException(
                $"{nameof(ChildTrait)} {ChildTrait.Value} is also a drawable trait; a child is given it, never draws it.",
                nameof(ChildTrait)
            );
        }

        traits.Add(ChildTrait);
        foreach (BelongingRule rule in Belongings)
        {
            RuleChecks.Unique(traits.Add(rule.Belonging), $"belonging {rule.Belonging.Value} (or a trait of that id)", nameof(Belongings));
        }

        HashSet<ProfessionId> professions = [];
        foreach (ProfessionRule rule in Professions)
        {
            RuleChecks.Unique(professions.Add(rule.Profession), $"profession {rule.Profession.Value}", nameof(Professions));
        }
    }
}

/// <summary>The rules of one trip purpose, checked field by field as <see cref="ManifestRules"/> is.</summary>
public sealed record TripPurposeRules
{
    private const int LastMinuteOfDay = (24 * 60) - 1;

    /// <summary>Gets the weights of a booking's size; every size is at least 1.</summary>
    public required IReadOnlyList<WeightedOption<int>> GroupSizes
    {
        get;
        init => field = RuleChecks.Options(value, nameof(GroupSizes), size => size >= 1);
    }

    /// <summary>
    /// Gets the share of bookings of at least <see cref="ManifestRules.FamilyMinimumSize"/> that are families rather than
    /// groups of adults. A <see cref="TripPurpose.Business"/> booking is never a family, whatever its share says.
    /// </summary>
    public required double FamilyShare
    {
        get;
        init => field = RuleChecks.Share(value, nameof(FamilyShare));
    }

    /// <summary>Gets the range, in minutes after midnight origin local, a booking's wake time is drawn from.</summary>
    public required IntRange WakeMinutes
    {
        get;
        init => field = RuleChecks.Ordered(value, 0, LastMinuteOfDay, nameof(WakeMinutes));
    }
}

/// <summary>A value and the weight it is drawn with.</summary>
/// <typeparam name="T">The value's type.</typeparam>
/// <param name="Value">The value.</param>
/// <param name="Weight">Its weight, relative to the other options' weights.</param>
public readonly record struct WeightedOption<T>(T Value, double Weight);

/// <summary>An inclusive range of whole numbers.</summary>
/// <param name="Min">The lowest value.</param>
/// <param name="Max">The highest value.</param>
public readonly record struct IntRange(int Min, int Max);

/// <summary>An inclusive range of shares.</summary>
/// <param name="Min">The lowest share.</param>
/// <param name="Max">The highest share.</param>
public readonly record struct ShareRange(double Min, double Max);

/// <summary>A drawable personality trait.</summary>
/// <param name="Trait">The trait.</param>
/// <param name="AdultWeight">Its weight for an adult, and for a child's extra trait.</param>
/// <param name="BusinessTripWeight">Its weight for an adult on a <see cref="TripPurpose.Business"/> trip.</param>
/// <param name="ChildMayDraw">Whether a child may draw it as their extra trait.</param>
public readonly record struct TraitRule(TraitId Trait, double AdultWeight, double BusinessTripWeight, bool ChildMayDraw);

/// <summary>Two traits no passenger carries together.</summary>
/// <param name="First">One trait.</param>
/// <param name="Second">The other.</param>
public readonly record struct TraitPair(TraitId First, TraitId Second);

/// <summary>A belonging and the share of passengers who carry it.</summary>
/// <param name="Belonging">The belonging's trait.</param>
/// <param name="AdultShare">The share of adults carrying it.</param>
/// <param name="BusinessTripShare">The share of adults on a <see cref="TripPurpose.Business"/> trip carrying it.</param>
/// <param name="ChildShare">The share of children carrying it.</param>
public readonly record struct BelongingRule(TraitId Belonging, double AdultShare, double BusinessTripShare, double ChildShare);

/// <summary>A profession an adult may draw.</summary>
/// <param name="Profession">The profession.</param>
/// <param name="Weight">Its weight.</param>
/// <param name="OnBusinessTrips">Whether an adult on a <see cref="TripPurpose.Business"/> trip may draw it.</param>
public readonly record struct ProfessionRule(ProfessionId Profession, double Weight, bool OnBusinessTrips);

/// <summary>The field checks of <see cref="ManifestRules"/> and <see cref="TripPurposeRules"/>.</summary>
internal static class RuleChecks
{
    public static bool IsDefined(TripPurpose purpose) => purpose is TripPurpose.Business or TripPurpose.Leisure or TripPurpose.Visiting;

    public static T NotNull<T>(T? value, string field)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value, field);
        return value;
    }

    public static void Unique(bool added, string what, string field)
    {
        if (!added)
        {
            throw new ArgumentException($"{field} lists {what} twice.", field);
        }
    }

    public static int AtLeast(int value, int lowest, string field) =>
        value >= lowest ? value : throw new ArgumentException($"{field} must be at least {lowest}; it is {value}.", field);

    public static double Share(double value, string field) =>
        value is >= 0 and <= 1 ? value : throw new ArgumentException($"{field} must be a share in [0, 1]; it is {value}.", field);

    public static double Weight(double value, string field) =>
        double.IsFinite(value) && value >= 0
            ? value
            : throw new ArgumentException($"{field} holds the weight {value}; a weight must be finite and at least 0.", field);

    public static ShareRange Ordered(ShareRange range, string field)
    {
        Share(range.Min, field);
        Share(range.Max, field);
        return range.Min <= range.Max ? range : throw new ArgumentException($"{field} runs from {range.Min} down to {range.Max}.", field);
    }

    public static IntRange Ordered(IntRange range, int lowest, int highest, string field)
    {
        if (range.Min < lowest || range.Max > highest)
        {
            throw new ArgumentException($"{field} must lie in [{lowest}, {highest}]; it is [{range.Min}, {range.Max}].", field);
        }

        return range.Min <= range.Max ? range : throw new ArgumentException($"{field} runs from {range.Min} down to {range.Max}.", field);
    }

    public static IReadOnlyList<WeightedOption<T>> Options<T>(IReadOnlyList<WeightedOption<T>>? options, string field, Func<T, bool> valid)
    {
        ArgumentNullException.ThrowIfNull(options, field);
        double total = 0;
        foreach (WeightedOption<T> option in options)
        {
            total += Weight(option.Weight, field);
            if (!valid(option.Value))
            {
                throw new ArgumentException($"{field} holds the value {option.Value}, which is out of range.", field);
            }
        }

        return total > 0 ? [.. options] : throw new ArgumentException($"{field} needs an option with a positive weight.", field);
    }

    public static IReadOnlyList<TraitRule> Traits(IReadOnlyList<TraitRule>? traits, string field)
    {
        ArgumentNullException.ThrowIfNull(traits, field);
        foreach (TraitRule trait in traits)
        {
            Weight(trait.AdultWeight, field);
            Weight(trait.BusinessTripWeight, field);
        }

        return [.. traits];
    }

    public static IReadOnlyList<BelongingRule> Belongings(IReadOnlyList<BelongingRule>? belongings, string field)
    {
        ArgumentNullException.ThrowIfNull(belongings, field);
        foreach (BelongingRule belonging in belongings)
        {
            Share(belonging.AdultShare, field);
            Share(belonging.BusinessTripShare, field);
            Share(belonging.ChildShare, field);
        }

        return [.. belongings];
    }

    public static IReadOnlyList<ProfessionRule> Professions(IReadOnlyList<ProfessionRule>? professions, string field)
    {
        ArgumentNullException.ThrowIfNull(professions, field);
        double total = 0;
        double businessTotal = 0;
        foreach (ProfessionRule profession in professions)
        {
            total += Weight(profession.Weight, field);
            businessTotal += profession.OnBusinessTrips ? profession.Weight : 0;
        }

        return total > 0 && businessTotal > 0
            ? [.. professions]
            : throw new ArgumentException($"{field} needs a profession with a positive weight open to business trips.", field);
    }
}
