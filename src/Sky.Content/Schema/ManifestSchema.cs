using Sky.Engine.Passengers;

namespace Sky.Content.Schema;

/// <summary>
/// <c>manifest.json</c>: every number the manifest generator draws by (<c>passengers.md</c> section 5). The trait parts of the
/// generator's rules are not here: they come from <c>traits.json</c>, which already carries them.
/// </summary>
public sealed record ManifestFile
{
    /// <summary>Gets the range the load factor is drawn from, uniformly, as a share of the layout's seats.</summary>
    public required DrawRange LoadFactor { get; init; }

    /// <summary>Gets the range the number of business passengers is drawn from, uniformly, before it is capped by the seats.</summary>
    public required IntRangeSpec BusinessBooked { get; init; }

    /// <summary>Gets how many rows, from the front, are business class.</summary>
    public required int BusinessRowCount { get; init; }

    /// <summary>Gets the trip purpose weights of a booking in business class.</summary>
    public required IReadOnlyList<WeightedOptionSpec<TripPurpose>> BusinessCabinPurposes { get; init; }

    /// <summary>Gets the trip purpose weights of a booking in economy class.</summary>
    public required IReadOnlyList<WeightedOptionSpec<TripPurpose>> EconomyCabinPurposes { get; init; }

    /// <summary>Gets the rules of a <see cref="TripPurpose.Business"/> booking.</summary>
    public required TripSpec BusinessTrip { get; init; }

    /// <summary>Gets the rules of a <see cref="TripPurpose.Leisure"/> booking.</summary>
    public required TripSpec LeisureTrip { get; init; }

    /// <summary>Gets the rules of a <see cref="TripPurpose.Visiting"/> booking.</summary>
    public required TripSpec VisitingTrip { get; init; }

    /// <summary>Gets the smallest booking that may be a family.</summary>
    public required int FamilyMinimumSize { get; init; }

    /// <summary>Gets how many adults a family has.</summary>
    public required int FamilyAdults { get; init; }

    /// <summary>Gets the most minutes a member wakes after their booking's wake time.</summary>
    public required int WakeSpreadMinutes { get; init; }

    /// <summary>Gets the weights of how many personality traits an adult draws.</summary>
    public required IReadOnlyList<WeightedOptionSpec<int>> AdultTraitCounts { get; init; }

    /// <summary>Gets the share of children who draw one more trait.</summary>
    public required double ChildExtraTraitShare { get; init; }

    /// <summary>Gets the professions an adult may draw, interned in declaration order.</summary>
    public required IReadOnlyList<ProfessionSpec> Professions { get; init; }
}

/// <summary>The draw rules of one trip purpose.</summary>
public sealed record TripSpec
{
    /// <summary>Gets the weights of a booking's size; every size is at least 1.</summary>
    public required IReadOnlyList<WeightedOptionSpec<int>> GroupSizes { get; init; }

    /// <summary>Gets the share of bookings large enough to be a family rather than a group of adults.</summary>
    public required double FamilyShare { get; init; }

    /// <summary>Gets the range, in minutes after midnight origin local, a booking's wake time is drawn from.</summary>
    public required IntRangeSpec WakeMinutes { get; init; }
}

/// <summary>One profession an adult may draw, declared in <c>manifest.json</c> and interned to a <see cref="ProfessionId"/>.</summary>
public sealed record ProfessionSpec
{
    /// <summary>Gets the profession's content id.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the weight it is drawn with, relative to the other professions'.</summary>
    public required double Weight { get; init; }

    /// <summary>Gets whether an adult on a business trip may draw it.</summary>
    public required bool OnBusinessTrips { get; init; }
}

/// <summary>An inclusive range of whole numbers.</summary>
/// <param name="Min">The lowest value.</param>
/// <param name="Max">The highest value.</param>
public sealed record IntRangeSpec(int Min, int Max);

/// <summary>A value and the weight it is drawn with.</summary>
/// <typeparam name="T">The value's type.</typeparam>
/// <param name="Value">The value.</param>
/// <param name="Weight">Its weight, relative to the other options' weights.</param>
public sealed record WeightedOptionSpec<T>(T Value, double Weight);
