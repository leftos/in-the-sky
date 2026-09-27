using Sky.Engine.Flight;
using Sky.Engine.Needs;
using Sky.Engine.Passengers;

namespace Sky.Engine.Ports;

/// <summary>
/// Everything one passenger's activity scoring reads: who they are, what the flight is doing to them, and the local
/// context of the seat they are in. Built once per scoring call and handed to <see cref="IBehaviorScripts"/> by
/// read-only reference; <see cref="Needs"/> is the passenger's own live set, read only during that call.
/// </summary>
public readonly record struct PassengerFacts
{
    /// <summary>Gets the passenger's five needs as they stand now.</summary>
    public required NeedSet Needs { get; init; }

    /// <summary>Gets the passenger's traits as a view over their own array; nothing is copied per call.</summary>
    public required ReadOnlyMemory<TraitId> Traits { get; init; }

    /// <summary>Gets the stage of the flight.</summary>
    public required FlightStage Stage { get; init; }

    /// <summary>Gets whether the seatbelt sign is lit.</summary>
    public required bool SeatbeltSignOn { get; init; }

    /// <summary>Gets how rough the air is.</summary>
    public required Turbulence Turbulence { get; init; }

    /// <summary>Gets the activity the passenger is on now.</summary>
    public required ActivityId CurrentActivity { get; init; }

    /// <summary>Gets whether the cabin lights are down.</summary>
    public required bool CabinDimmed { get; init; }

    /// <summary>Gets whether the passenger's call light is on.</summary>
    public required bool CallLightOn { get; init; }

    /// <summary>Gets whether the passenger's tray table is down.</summary>
    public required bool TrayDown { get; init; }

    /// <summary>Gets the sim minutes since the passenger was last woken, or positive infinity when never woken.</summary>
    public required double MinutesSinceWoken { get; init; }

    /// <summary>Gets whether a neighbour is chatting.</summary>
    public required bool NeighbourChatting { get; init; }

    /// <summary>Gets whether an awake member of the passenger's group sits in an adjacent seat.</summary>
    public required bool AwakeGroupMemberAdjacent { get; init; }

    /// <summary>Gets whether the seat's in-flight entertainment is available.</summary>
    public required bool IfeAvailable { get; init; }

    /// <summary>Gets the sim minutes since the passenger was last served, or positive infinity when never served.</summary>
    public required double MinutesSinceServed { get; init; }

    /// <summary>Gets whether the service cart is in the passenger's zone.</summary>
    public required bool CartInZone { get; init; }
}
