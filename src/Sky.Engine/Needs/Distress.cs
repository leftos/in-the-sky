namespace Sky.Engine.Needs;

/// <summary>One need's contribution to distress: how far above its comfort threshold it stands, and at what weight.</summary>
/// <param name="Need">The need this term reads.</param>
/// <param name="Threshold">The comfort threshold, in [0, 100); the need contributes only above it.</param>
/// <param name="Weight">Distress per point of excess, finite and at least 0.</param>
public sealed record DistressTerm(Need Need, double Threshold, double Weight);

/// <summary>
/// The flight's distress formula, validated once at construction: distress is the weighted sum of each need's excess
/// over its comfort threshold, clamped to 0 to 100. Built once per flight, so every need is checked to carry exactly
/// one term before any passenger is scored.
/// </summary>
public sealed class Distress
{
    private const double Min = 0.0;

    private const double Max = 100.0;

    private readonly DistressTerm[] terms;

    /// <summary>Validates <paramref name="terms"/> and copies them.</summary>
    /// <param name="terms">The terms, exactly one per need.</param>
    /// <exception cref="ArgumentNullException"><paramref name="terms"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="terms"/> holds a null term, names an undefined need, names a need twice, or leaves a need out.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">A threshold is outside [0, 100), or a weight is negative, NaN or infinite.</exception>
    public Distress(IReadOnlyList<DistressTerm> terms)
    {
        ArgumentNullException.ThrowIfNull(terms);
        this.terms = new DistressTerm[terms.Count];
        int[] counts = new int[NeedSet.NeedCount];
        for (int index = 0; index < terms.Count; index++)
        {
            Validate(terms[index], index, counts, nameof(terms));
            this.terms[index] = terms[index];
        }

        for (int index = 0; index < NeedSet.NeedCount; index++)
        {
            if (counts[index] != 1)
            {
                throw new ArgumentException($"Need {(Need)index} has {counts[index]} distress terms; every need needs exactly one.", nameof(terms));
            }
        }
    }

    /// <summary>Computes the passenger's distress from their needs, 0 to 100.</summary>
    /// <param name="needs">The passenger's needs, read but not changed.</param>
    /// <returns><c>clamp(Σ weight × max(0, need − threshold), 0, 100)</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="needs"/> is null.</exception>
    public double Compute(NeedSet needs)
    {
        ArgumentNullException.ThrowIfNull(needs);
        double sum = 0.0;
        foreach (DistressTerm term in terms)
        {
            sum += term.Weight * Math.Max(0.0, needs[term.Need] - term.Threshold);
        }

        return Math.Clamp(sum, Min, Max);
    }

    private static void Validate(DistressTerm term, int index, int[] counts, string paramName)
    {
        if (term is null)
        {
            throw new ArgumentException($"Term {index} is null.", paramName);
        }

        if ((uint)term.Need >= NeedSet.NeedCount)
        {
            throw new ArgumentException($"Distress term names need {term.Need}, which is not a defined need.", paramName);
        }

        if (counts[(int)term.Need] > 0)
        {
            throw new ArgumentException($"Need {term.Need} has more than one distress term.", paramName);
        }

        counts[(int)term.Need]++;

        if (!(term.Threshold >= Min && term.Threshold < Max))
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                term.Threshold,
                $"The distress threshold for {term.Need} must be in [0, 100); got {term.Threshold}."
            );
        }

        if (!double.IsFinite(term.Weight) || term.Weight < 0.0)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                term.Weight,
                $"The distress weight for {term.Need} must be finite and non-negative; got {term.Weight}."
            );
        }
    }
}
