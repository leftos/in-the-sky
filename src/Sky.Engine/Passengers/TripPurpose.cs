namespace Sky.Engine.Passengers;

/// <summary>Why a booking is flying; drawn once per booking by the manifest and shared by its members.</summary>
public enum TripPurpose
{
    /// <summary>A work trip: adults only, early risers, more frequent flyers.</summary>
    Business,

    /// <summary>A holiday, often a family.</summary>
    Leisure,

    /// <summary>Visiting friends and relatives, often a family.</summary>
    Visiting,
}
