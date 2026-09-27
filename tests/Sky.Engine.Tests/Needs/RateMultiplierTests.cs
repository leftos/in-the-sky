using CsCheck;
using Sky.Engine.Needs;

namespace Sky.Engine.Tests.Needs;

/// <summary>Pins the rate-modifier rule: factors in a class multiply, classes add, the result is clamped to [0.2, 2.5].</summary>
public sealed class RateMultiplierTests
{
    private const int Precision = 12;

    /// <summary>The CONCEPT worked example (two traits, a phase and a cascade) composes past the cap and stops at 2.5.</summary>
    [Fact]
    public void ConceptWorkedExampleCapsAtTwoPointFive()
    {
        RateModifier[] modifiers =
        [
            new(SourceClass.Trait, 1.3),
            new(SourceClass.Trait, 1.5),
            new(SourceClass.Phase, 1.4),
            new(SourceClass.Cascade, 1.2),
        ];

        Assert.Equal(RateMultiplier.Cap, RateMultiplier.Compose(modifiers), Precision);
    }

    /// <summary>Two classes each at 0.4 sum below zero, and the floor holds the rate at 0.2 rather than stopping it.</summary>
    [Fact]
    public void TwoClassesAtPointFourGiveTheFloor()
    {
        RateModifier[] modifiers = [new(SourceClass.Context, 0.4), new(SourceClass.Service, 0.4)];

        Assert.Equal(RateMultiplier.Floor, RateMultiplier.Compose(modifiers), Precision);
    }

    /// <summary>Two factors in one class multiply rather than add.</summary>
    [Fact]
    public void FactorsInOneClassMultiply()
    {
        RateModifier[] modifiers = [new(SourceClass.Trait, 0.5), new(SourceClass.Trait, 0.5)];

        Assert.Equal(0.25, RateMultiplier.Compose(modifiers), Precision);
    }

    /// <summary>A class whose product overflowed to infinity is still zeroed by a later zero factor, so the floor holds.</summary>
    [Fact]
    public void OverflowingClassThenZeroFactorGivesTheFloor()
    {
        RateModifier[] modifiers = [new(SourceClass.Trait, 1e200), new(SourceClass.Trait, 1e200), new(SourceClass.Trait, 0.0)];

        Assert.Equal(RateMultiplier.Floor, RateMultiplier.Compose(modifiers), Precision);
    }

    /// <summary>Classes add as deviations from 1 rather than multiplying: 1 + (3 − 1) + (0.4 − 1) = 2.4, not 1.2.</summary>
    [Fact]
    public void ClassesAddAsDeltasAroundOne()
    {
        RateModifier[] modifiers = [new(SourceClass.Trait, 3.0), new(SourceClass.Service, 0.4)];

        Assert.Equal(2.4, RateMultiplier.Compose(modifiers), Precision);
    }

    /// <summary>A class at zero subtracts a whole 1 and another class's boost adds back, with no per-class floor or cap.</summary>
    [Fact]
    public void ZeroInOneClassAndBoostInAnotherGiveHalf()
    {
        RateModifier[] modifiers = [new(SourceClass.Trait, 0.0), new(SourceClass.Phase, 1.5)];

        Assert.Equal(0.5, RateMultiplier.Compose(modifiers), Precision);
    }

    /// <summary>No modifiers leave the rate unchanged.</summary>
    [Fact]
    public void NoModifiersGiveOne() => Assert.Equal(1.0, RateMultiplier.Compose([]), Precision);

    /// <summary>A negative, NaN or infinite factor, or an undefined class, is rejected, naming the public parameter.</summary>
    /// <param name="sourceClass">The modifier's class.</param>
    /// <param name="factor">The modifier's factor.</param>
    [Theory]
    [InlineData(SourceClass.Event, -0.1)]
    [InlineData(SourceClass.Trait, double.NaN)]
    [InlineData(SourceClass.Phase, double.PositiveInfinity)]
    [InlineData((SourceClass)99, 1.0)]
    public void InvalidModifierThrows(SourceClass sourceClass, double factor)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            RateMultiplier.Compose([new RateModifier(sourceClass, factor)])
        );

        Assert.Equal("modifiers", exception.ParamName);
    }

    /// <summary>The per-class slot count covers every defined source class.</summary>
    [Fact]
    public void ClassCountCoversEverySourceClass() => Assert.Equal(RateMultiplier.ClassCount, Enum.GetValues<SourceClass>().Length);

    /// <summary>Any set of valid modifiers composes into a multiplier within the floor and the cap.</summary>
    [Fact]
    public void AnyModifierSetComposesIntoFloorAndCap() => AssertWithinFloorAndCap(Gen.Double[0.0, 5.0]);

    /// <summary>Sets mixing zeros with factors up to <see cref="double.MaxValue"/>, which overflow a class, still compose into range.</summary>
    [Fact]
    public void AnyModifierSetIncludingExtremesComposesIntoFloorAndCap() =>
        AssertWithinFloorAndCap(
            Gen.OneOf(
                Gen.Const(0.0),
                Gen.Double[0.0, 5.0],
                // Gen.Double[0.0, double.MaxValue] yields +infinity, which Compose rightly rejects; scaling a unit double stays finite.
                Gen.Double[0.0, 1.0].Select(unit => unit * double.MaxValue),
                Gen.Const(double.MaxValue)
            )
        );

    /// <summary>One modifier whose factor lies within the floor and the cap composes to that factor.</summary>
    [Fact]
    public void OneModifierInRangeComposesToItsFactor() =>
        Gen.Select(Gen.Int[0, 5], Gen.Double[RateMultiplier.Floor, RateMultiplier.Cap])
            .Sample((cls, factor) => Math.Abs(RateMultiplier.Compose([new RateModifier((SourceClass)cls, factor)]) - factor) <= 1e-12);

    private static void AssertWithinFloorAndCap(Gen<double> factors) =>
        Gen.Select(Gen.Int[0, 5], factors, (cls, factor) => new RateModifier((SourceClass)cls, factor))
            .Array[0, 20]
            .Sample(modifiers =>
            {
                double multiplier = RateMultiplier.Compose(modifiers);
                return multiplier >= RateMultiplier.Floor && multiplier <= RateMultiplier.Cap;
            });
}
