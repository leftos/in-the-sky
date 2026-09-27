namespace Sky.Engine.Needs;

/// <summary>
/// Composes rate modifiers into one multiplier: factors within a class multiply, the classes' deviations from 1 add,
/// and the result is clamped to [<see cref="Floor"/>, <see cref="Cap"/>].
/// </summary>
public static class RateMultiplier
{
    /// <summary>The lowest multiplier any set of modifiers can compose into.</summary>
    public const double Floor = 0.2;

    /// <summary>The highest multiplier any set of modifiers can compose into.</summary>
    public const double Cap = 2.5;

    /// <summary>The number of source classes, one slot each in the per-class products.</summary>
    internal const int ClassCount = (int)SourceClass.Phase + 1;

    /// <summary>
    /// Composes <paramref name="modifiers"/> into one multiplier; no modifiers give 1. A zero factor zeroes its class
    /// whatever the class's running product, so an overflowing class still ends at zero.
    /// </summary>
    /// <param name="modifiers">
    /// The modifiers on the rate. The result depends on their order only in the last bit of the floating-point
    /// products, so callers build the span in a fixed order to keep runs deterministic.
    /// </param>
    /// <returns><c>clamp(1 + Σ_class (Π_i m_i − 1), Floor, Cap)</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A modifier has an undefined class, or a factor that is negative, NaN or infinite.</exception>
    public static double Compose(ReadOnlySpan<RateModifier> modifiers)
    {
        Span<double> products = stackalloc double[ClassCount];
        products.Fill(1.0);
        foreach (RateModifier modifier in modifiers)
        {
            Validate(modifier, nameof(modifiers));
            ref double product = ref products[(int)modifier.Class];
            product = modifier.Factor == 0.0 ? 0.0 : product * modifier.Factor;
        }

        double sum = 0.0;
        foreach (double product in products)
        {
            sum += product - 1.0;
        }

        return Math.Clamp(1.0 + sum, Floor, Cap);
    }

    private static void Validate(RateModifier modifier, string paramName)
    {
        if ((uint)modifier.Class >= ClassCount)
        {
            throw new ArgumentOutOfRangeException(paramName, modifier.Class, $"Rate modifier class {modifier.Class} is not a defined SourceClass.");
        }

        if (!double.IsFinite(modifier.Factor) || modifier.Factor < 0.0)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                modifier.Factor,
                $"Rate modifier factor for class {modifier.Class} must be finite and non-negative; got {modifier.Factor}."
            );
        }
    }
}
