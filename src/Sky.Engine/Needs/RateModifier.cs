namespace Sky.Engine.Needs;

/// <summary>One factor on a need's rate, tagged with the class of source it comes from.</summary>
/// <param name="Class">The class of source the factor comes from.</param>
/// <param name="Factor">The multiplier on the rate: 1 leaves it unchanged, below 1 slows it, above 1 speeds it.</param>
public readonly record struct RateModifier(SourceClass Class, double Factor);
