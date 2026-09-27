using Sky.Engine.Passengers;

namespace Sky.Engine.Manifest;

/// <summary>A flight's passengers and their bookings, as the manifest generator drew and seated them.</summary>
/// <param name="Passengers">The passengers, in seating order; a passenger's <see cref="ManifestPassenger.Id"/> is its index.</param>
/// <param name="Bookings">The bookings, in draw order, which is also seating order; a booking's id is its index.</param>
public sealed record PassengerManifest(IReadOnlyList<ManifestPassenger> Passengers, IReadOnlyList<Booking> Bookings);

/// <summary>One booking: passengers who bought their tickets together and fly as a group.</summary>
/// <param name="Id">The booking's id, its index in <see cref="PassengerManifest.Bookings"/>.</param>
/// <param name="TripPurpose">Why the booking is flying.</param>
/// <param name="SeatClass">The class the whole booking flies in.</param>
/// <param name="WakeMinute">The booking's wake time, in minutes after midnight origin local; each member wakes up to
/// <see cref="ManifestRules.WakeSpreadMinutes"/> after it.</param>
/// <param name="PassengerIds">The members' passenger ids, in the order they were seated.</param>
public sealed record Booking(int Id, TripPurpose TripPurpose, SeatClass SeatClass, int WakeMinute, IReadOnlyList<int> PassengerIds);

/// <summary>One passenger as the manifest fixes them for the flight (passengers.md section 1); starting needs are not here.</summary>
public sealed record ManifestPassenger
{
    /// <summary>Gets the character id, 0 to n - 1 in seating order (R31).</summary>
    public required int Id { get; init; }

    /// <summary>Gets the id of the passenger's booking.</summary>
    public required int BookingId { get; init; }

    /// <summary>Gets why the passenger's booking is flying.</summary>
    public required TripPurpose TripPurpose { get; init; }

    /// <summary>Gets whether the passenger is an adult or a child.</summary>
    public required AgeBand AgeBand { get; init; }

    /// <summary>Gets the class the passenger is booked in and sits in.</summary>
    public required SeatClass SeatClass { get; init; }

    /// <summary>Gets the passenger's profession; null for a child.</summary>
    public required ProfessionId? Profession { get; init; }

    /// <summary>
    /// Gets the passenger's traits: the <c>child</c> trait first for a child, then the personality traits in draw order, then
    /// the belongings in rule order.
    /// </summary>
    public required ReadOnlyMemory<TraitId> Traits { get; init; }

    /// <summary>Gets the <see cref="Cabin.NavGraph"/> seat node the passenger is booked into (R30).</summary>
    public required int SeatNode { get; init; }

    /// <summary>Gets the minute the passenger woke, counted from midnight origin local.</summary>
    public required int WakeMinute { get; init; }
}
