namespace Sky.Engine.Needs;

/// <summary>What a passenger is doing this tick, as far as the need update cares.</summary>
/// <param name="IsAsleep">The passenger is asleep: Rest falls instead of rising, and Boredom holds.</param>
/// <param name="OnIfe">The passenger is watching in-flight entertainment: Boredom holds.</param>
public readonly record struct NeedContext(bool IsAsleep, bool OnIfe);
