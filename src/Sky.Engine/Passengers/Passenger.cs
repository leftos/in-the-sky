using Sky.Engine.Manifest;
using Sky.Engine.Needs;

namespace Sky.Engine.Passengers;

/// <summary>A passenger in the flight: who the manifest says they are, their needs, and where they are on the nav graph.</summary>
/// <param name="manifest">The passenger as the manifest fixes them.</param>
/// <param name="needs">The passenger's needs, at their starting values.</param>
/// <param name="lateAndFedUp">Whether the passenger boards late and fed up.</param>
/// <param name="crewCount">How many crew the flight carries; crew hold the executor's first character ids.</param>
public sealed class Passenger(ManifestPassenger manifest, NeedSet needs, bool lateAndFedUp, int crewCount)
{
    /// <summary>Gets the passenger's manifest id, 0 to n - 1 in seating order; it indexes the flight's passengers and streams.</summary>
    public int Id => Manifest.Id;

    /// <summary>Gets the passenger as the manifest fixes them.</summary>
    public ManifestPassenger Manifest { get; } = manifest ?? throw new ArgumentNullException(nameof(manifest));

    /// <summary>
    /// Gets the passenger's character id in the executor and occupancy: the crew count plus <see cref="Id"/>, since crew hold
    /// the ids below it. Initialised after <see cref="Manifest"/>, which has already refused a null manifest.
    /// </summary>
    public int CharacterId { get; } = crewCount >= 0 ? crewCount + manifest.Id : throw new ArgumentOutOfRangeException(nameof(crewCount));

    /// <summary>Gets the passenger's needs.</summary>
    public NeedSet Needs { get; } = needs ?? throw new ArgumentNullException(nameof(needs));

    /// <summary>Gets the nav graph node the passenger is on, or null before they board.</summary>
    public int? Node { get; internal set; }

    /// <summary>Gets whether the passenger has boarded: they are on the nav graph, and their needs tick.</summary>
    public bool IsBoarded => Node.HasValue;

    /// <summary>Gets whether the passenger carries the late-and-fed-up Unease modifier; it clears when they are first served.</summary>
    public bool LateAndFedUp { get; internal set; } = lateAndFedUp;
}
