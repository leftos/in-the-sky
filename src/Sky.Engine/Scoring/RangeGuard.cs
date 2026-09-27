using System.Globalization;

namespace Sky.Engine.Scoring;

/// <summary>The range checks the scoring inputs share, with every number in a message formatted culture-invariant.</summary>
internal static class RangeGuard
{
    /// <summary>Rejects a value that is NaN or outside [<paramref name="lowest"/>, <paramref name="highest"/>].</summary>
    /// <param name="value">The value.</param>
    /// <param name="lowest">The lowest value allowed.</param>
    /// <param name="highest">The highest value allowed.</param>
    /// <param name="parameterName">The public parameter the value came in as.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value is NaN or out of range.</exception>
    internal static void ThrowIfOutside(double value, double lowest, double highest, string parameterName)
    {
        if (!(value >= lowest && value <= highest))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                string.Create(CultureInfo.InvariantCulture, $"Every value must be a number in [{lowest}, {highest}]; found {value}.")
            );
        }
    }

    /// <summary>Rejects a value that is NaN, infinite or below <paramref name="lowest"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="lowest">The lowest value allowed.</param>
    /// <param name="parameterName">The public parameter the value came in as.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value is NaN, infinite or too low.</exception>
    internal static void ThrowIfBelow(double value, double lowest, string parameterName)
    {
        if (!(value >= lowest && double.IsFinite(value)))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                string.Create(CultureInfo.InvariantCulture, $"Every value must be at least {lowest}, finite; found {value}.")
            );
        }
    }

    /// <summary>Rejects no values at all, or any value that is NaN or outside [<paramref name="lowest"/>, <paramref name="highest"/>].</summary>
    /// <param name="values">The values: a series or a list.</param>
    /// <param name="lowest">The lowest value allowed.</param>
    /// <param name="highest">The highest value allowed.</param>
    /// <param name="parameterName">The public parameter the values came in as.</param>
    /// <exception cref="ArgumentException">There are no values.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A value is NaN or out of range.</exception>
    internal static void ThrowIfEmptyOrOutside(ReadOnlySpan<double> values, double lowest, double highest, string parameterName)
    {
        if (values.IsEmpty)
        {
            throw new ArgumentException("No values were given; at least one is needed.", parameterName);
        }

        foreach (double value in values)
        {
            ThrowIfOutside(value, lowest, highest, parameterName);
        }
    }
}
