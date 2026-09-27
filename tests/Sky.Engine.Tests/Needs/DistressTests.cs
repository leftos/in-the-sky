using CsCheck;
using Sky.Engine.Needs;

namespace Sky.Engine.Tests.Needs;

/// <summary>Pins the distress formula D3 set: the weighted sum of each need's excess over its comfort threshold, clamped to 0 to 100.</summary>
public sealed class DistressTests
{
    private const int Precision = 9;

    private static readonly DistressTerm[] D3Terms =
    [
        new(Need.Unease, 40.0, 1.5),
        new(Need.Bladder, 55.0, 1.2),
        new(Need.Refreshment, 65.0, 1.0),
        new(Need.Rest, 45.0, 0.8),
        new(Need.Boredom, 50.0, 0.6),
    ];

    /// <summary>Unease alone at 60, excess 20, gives 20 × 1.5 = 30 — the uneasy band's floor.</summary>
    [Fact]
    public void UneaseAloneAtSixtyGivesThirty()
    {
        NeedSet needs = new(uneaseBaseline: 0.0);
        needs.Set(Need.Unease, 60.0);

        Assert.Equal(30.0, D3().Compute(needs), Precision);
    }

    /// <summary>A need at or below its comfort threshold contributes nothing, so a quiet cabin reads 0.</summary>
    [Fact]
    public void NeedsAtOrBelowTheirThresholdsGiveZero()
    {
        Distress distress = D3();
        NeedSet atThresholds = new(uneaseBaseline: 0.0);
        atThresholds.Set(Need.Unease, 40.0);
        atThresholds.Set(Need.Bladder, 55.0);
        atThresholds.Set(Need.Refreshment, 65.0);
        atThresholds.Set(Need.Rest, 45.0);
        atThresholds.Set(Need.Boredom, 50.0);

        Assert.Equal(0.0, distress.Compute(atThresholds), Precision);
        Assert.Equal(0.0, distress.Compute(new NeedSet(uneaseBaseline: 0.0)), Precision);
    }

    /// <summary>Every need at 100 sums to 253, which clamps to the top of the scale.</summary>
    [Fact]
    public void SumIsClampedToOneHundred()
    {
        NeedSet needs = new(uneaseBaseline: 0.0);
        foreach (Need need in Enum.GetValues<Need>())
        {
            needs.Set(need, 100.0);
        }

        Assert.Equal(100.0, D3().Compute(needs), Precision);
    }

    /// <summary>A term list missing a need is refused, naming the need.</summary>
    [Fact]
    public void MissingNeedIsRefused()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => new Distress(D3Terms[..^1]));

        Assert.Equal("terms", exception.ParamName);
        Assert.Contains(nameof(Need.Boredom), exception.Message, StringComparison.Ordinal);
    }

    /// <summary>A term list naming a need twice is refused, naming the need.</summary>
    [Fact]
    public void RepeatedNeedIsRefused()
    {
        DistressTerm[] repeated = [D3Terms[0], D3Terms[1], D3Terms[2], D3Terms[3], new DistressTerm(Need.Unease, 40.0, 1.5)];

        ArgumentException exception = Assert.Throws<ArgumentException>(() => new Distress(repeated));

        Assert.Equal("terms", exception.ParamName);
        Assert.Contains(nameof(Need.Unease), exception.Message, StringComparison.Ordinal);
    }

    /// <summary>A negative weight, a threshold at 100 and a weight that is not finite are refused, naming the terms.</summary>
    [Fact]
    public void OutOfRangeTermIsRefused()
    {
        ArgumentOutOfRangeException weight = Assert.Throws<ArgumentOutOfRangeException>(() => new Distress(WithBoredomWeight(-0.1)));
        ArgumentOutOfRangeException threshold = Assert.Throws<ArgumentOutOfRangeException>(() => new Distress(WithBoredomThreshold(100.0)));
        ArgumentOutOfRangeException notFinite = Assert.Throws<ArgumentOutOfRangeException>(() => new Distress(WithBoredomWeight(double.NaN)));

        Assert.Equal("terms", weight.ParamName);
        Assert.Equal("terms", threshold.ParamName);
        Assert.Equal("terms", notFinite.ParamName);
    }

    /// <summary>A null term is refused by its index, rather than left to a NullReferenceException.</summary>
    [Fact]
    public void NullTermIsRefused()
    {
        var terms = new DistressTerm[NeedSet.NeedCount];
        for (int index = 0; index < D3Terms.Length; index++)
        {
            terms[index] = D3Terms[index];
        }

        terms[^1] = null!;

        ArgumentException exception = Assert.Throws<ArgumentException>(() => new Distress(terms));

        Assert.Equal("terms", exception.ParamName);
        Assert.Contains("4", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Over any need values, distress stays in [0, 100] and raising one need never lowers it.</summary>
    [Fact]
    public void DistressStaysInRangeAndRisesWithAnyNeed()
    {
        Gen.Select(
                Gen.Double[0.0, 100.0].Array[NeedSet.NeedCount],
                Gen.Int[0, NeedSet.NeedCount - 1],
                Gen.Double[0.0, 100.0],
                (values, need, raised) => (Values: values, Need: (Need)need, Raised: raised)
            )
            .Sample(input => StaysInRangeAndRises(input.Values, input.Need, input.Raised));
    }

    private static bool StaysInRangeAndRises(double[] values, Need need, double raised)
    {
        Distress distress = D3();
        double before = distress.Compute(Build(values));
        if (!(before is >= 0.0 and <= 100.0))
        {
            return false;
        }

        double[] risen = (double[])values.Clone();
        risen[(int)need] = Math.Max(values[(int)need], raised);
        return distress.Compute(Build(risen)) >= before;
    }

    private static NeedSet Build(double[] values)
    {
        NeedSet needs = new(uneaseBaseline: 0.0);
        foreach (Need need in Enum.GetValues<Need>())
        {
            needs.Set(need, values[(int)need]);
        }

        return needs;
    }

    private static DistressTerm[] WithBoredomWeight(double weight) =>
        [D3Terms[0], D3Terms[1], D3Terms[2], D3Terms[3], new DistressTerm(Need.Boredom, 50.0, weight)];

    private static DistressTerm[] WithBoredomThreshold(double threshold) =>
        [D3Terms[0], D3Terms[1], D3Terms[2], D3Terms[3], new DistressTerm(Need.Boredom, threshold, 0.6)];

    private static Distress D3() => new(D3Terms);
}
