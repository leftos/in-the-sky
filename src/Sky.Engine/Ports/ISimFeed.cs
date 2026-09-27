using Sky.Engine.Flight;

namespace Sky.Engine.Ports;

/// <summary>What the simulator reports at a tick.</summary>
/// <param name="Stage">The stage the flight is in.</param>
/// <param name="SeatbeltSignOn">Whether the seatbelt sign is lit.</param>
/// <param name="Turbulence">How rough the air is.</param>
public readonly record struct FeedObservation(FlightStage Stage, bool SeatbeltSignOn, Turbulence Turbulence);

/// <summary>The port the flight reads once a tick; the headless emulator and a simulator host both implement it.</summary>
public interface ISimFeed
{
    /// <summary>Reads what the simulator reports at <paramref name="tick"/>.</summary>
    /// <param name="tick">The tick being simulated.</param>
    /// <returns>The simulator's reading for that tick.</returns>
    FeedObservation Observe(long tick);
}
