namespace Sky.Engine.Needs;

/// <summary>What a passenger is doing this tick, and what is pushing their Unease, as far as the need update cares.</summary>
/// <param name="IsAsleep">The passenger is asleep: Rest falls instead of rising, and Boredom holds.</param>
/// <param name="OnIfe">The passenger is watching in-flight entertainment: Boredom holds.</param>
/// <param name="UneasePushPerHour">
/// The sum of the Unease push sources active this tick (turbulence, an unanswered call), per sim hour, finite and
/// non-negative; the caller sums them and the Unease multiplier scales the sum.
/// </param>
public readonly record struct NeedContext(bool IsAsleep, bool OnIfe, double UneasePushPerHour);
