using System.Numerics;

namespace Sky.Engine.Randomness;

/// <summary>A deterministic xoshiro256** generator whose state is filled by SplitMix64; one instance serves one named stream.</summary>
public sealed class SimRandom
{
    private const double UnitScale = 1.0 / (1UL << 53);

    private ulong state0;
    private ulong state1;
    private ulong state2;
    private ulong state3;

    /// <summary>Fills the state with four successive SplitMix64 outputs started at <paramref name="seed"/>.</summary>
    /// <param name="seed">The 64-bit seed; <see cref="RngRoot"/> derives it from the root seed and the stream name.</param>
    internal SimRandom(ulong seed)
    {
        ulong splitMixState = seed;
        state0 = SplitMix64(ref splitMixState);
        state1 = SplitMix64(ref splitMixState);
        state2 = SplitMix64(ref splitMixState);
        state3 = SplitMix64(ref splitMixState);
    }

    /// <summary>Sets the four state words directly, as the reference implementations do.</summary>
    internal SimRandom(ulong s0, ulong s1, ulong s2, ulong s3)
    {
        state0 = s0;
        state1 = s1;
        state2 = s2;
        state3 = s3;
    }

    /// <summary>Draws the next 64 random bits (xoshiro256**).</summary>
    /// <returns>A value uniform over the whole <see cref="ulong"/> range.</returns>
    public ulong NextUInt64()
    {
        ulong result = BitOperations.RotateLeft(state1 * 5, 7) * 9;
        ulong t = state1 << 17;

        state2 ^= state0;
        state3 ^= state1;
        state1 ^= state2;
        state0 ^= state3;

        state2 ^= t;
        state3 = BitOperations.RotateLeft(state3, 45);

        return result;
    }

    /// <summary>Draws a double from the top 53 bits of the next draw.</summary>
    /// <returns>A value in [0, 1).</returns>
    public double NextDouble() => (NextUInt64() >> 11) * UnitScale;

    /// <summary>Draws an integer as the high 64 bits of the next draw times <paramref name="maxExclusive"/>, one draw per call.</summary>
    /// <param name="maxExclusive">The exclusive upper bound; must be positive.</param>
    /// <returns>A value in [0, <paramref name="maxExclusive"/>), biased by at most maxExclusive / 2^64.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxExclusive"/> is zero or negative.</exception>
    public int NextInt(int maxExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxExclusive);
        return (int)Math.BigMul(NextUInt64(), (ulong)maxExclusive, out _);
    }

    /// <summary>Draws once and reports whether the draw fell below <paramref name="p"/>.</summary>
    /// <param name="p">The probability of <see langword="true"/>; 0 or less never succeeds, 1 or more always does.</param>
    /// <returns><see langword="true"/> with probability <paramref name="p"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="p"/> is NaN; nothing is drawn.</exception>
    public bool Chance(double p)
    {
        if (double.IsNaN(p))
        {
            throw new ArgumentOutOfRangeException(nameof(p), p, "A chance must be a number; NaN has no probability.");
        }

        return NextDouble() < p;
    }

    /// <summary>Advances a SplitMix64 state and returns its next output.</summary>
    internal static ulong SplitMix64(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;
        ulong z = state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
