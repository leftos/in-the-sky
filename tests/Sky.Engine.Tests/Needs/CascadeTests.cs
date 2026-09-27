using Sky.Engine.Needs;

namespace Sky.Engine.Tests.Needs;

/// <summary>Pins the cascade rules D3 set: a source need strictly above its threshold adds one Cascade factor to its target.</summary>
public sealed class CascadeTests
{
    private const int Precision = 9;

    /// <summary>An absolute bound rather than rounding, since the composed product's last bit depends on factor order.</summary>
    private const double ComposeTolerance = 1e-12;

    private static readonly CascadeRule RefreshmentCascade = new(Need.Refreshment, Need.Unease, 70.0, 1.1, CascadeCondition.None);

    private static readonly CascadeRule RestCascade = new(Need.Rest, Need.Unease, 80.0, 1.2, CascadeCondition.None);

    private static readonly CascadeRule BladderCascade = new(Need.Bladder, Need.Unease, 70.0, 1.3, CascadeCondition.LavUnreachable);

    /// <summary>Refreshment above 70 adds one Cascade factor of 1.1 to Unease; exactly 70 adds nothing.</summary>
    [Fact]
    public void RefreshmentAboveSeventyAddsCascadeOnUnease()
    {
        Cascades cascades = D3();
        NeedSet needs = new(uneaseBaseline: 0.0);
        var buffer = new RateModifier[cascades.MaxModifiersPerTarget(Need.Unease)];

        needs.Set(Need.Refreshment, 75.0);
        int count = cascades.AppendModifiers(needs, Need.Unease, lavUnreachable: false, buffer);

        Assert.Equal(1, count);
        Assert.Equal(SourceClass.Cascade, buffer[0].Class);
        Assert.Equal(1.1, buffer[0].Factor, Precision);

        needs.Set(Need.Refreshment, 70.0);
        Assert.Equal(0, cascades.AppendModifiers(needs, Need.Unease, lavUnreachable: false, buffer));
    }

    /// <summary>The Bladder cascade fires only when the caller reports the passenger cannot reach a lav.</summary>
    [Fact]
    public void BladderCascadeNeedsLavUnreachable()
    {
        Cascades cascades = D3();
        NeedSet needs = new(uneaseBaseline: 0.0);
        needs.Set(Need.Bladder, 75.0);
        var buffer = new RateModifier[cascades.MaxModifiersPerTarget(Need.Unease)];

        Assert.Equal(0, cascades.AppendModifiers(needs, Need.Unease, lavUnreachable: false, buffer));

        Assert.Equal(1, cascades.AppendModifiers(needs, Need.Unease, lavUnreachable: true, buffer));
        Assert.Equal(1.3, buffer[0].Factor, Precision);
    }

    /// <summary>All three firing land in rule order and compose as a product inside the one Cascade class.</summary>
    [Fact]
    public void ThreeCascadesComposeByMultiplyingInOneClass()
    {
        Cascades cascades = D3();
        NeedSet needs = new(uneaseBaseline: 0.0);
        needs.Set(Need.Refreshment, 75.0);
        needs.Set(Need.Rest, 85.0);
        needs.Set(Need.Bladder, 75.0);
        var buffer = new RateModifier[cascades.MaxModifiersPerTarget(Need.Unease)];

        int count = cascades.AppendModifiers(needs, Need.Unease, lavUnreachable: true, buffer);

        Assert.Equal(3, count);
        Assert.Equal(1.1, buffer[0].Factor, Precision);
        Assert.Equal(1.2, buffer[1].Factor, Precision);
        Assert.Equal(1.3, buffer[2].Factor, Precision);
        Assert.Equal(1.1 * 1.2 * 1.3, RateMultiplier.Compose(buffer.AsSpan(0, count)), ComposeTolerance);
    }

    /// <summary>A target no rule names takes no modifiers.</summary>
    [Fact]
    public void OtherTargetGetsNoModifiers()
    {
        Cascades cascades = D3();
        NeedSet needs = new(uneaseBaseline: 0.0);
        needs.Set(Need.Refreshment, 75.0);
        needs.Set(Need.Rest, 85.0);
        Assert.Equal(0, cascades.MaxModifiersPerTarget(Need.Boredom));

        var buffer = new RateModifier[cascades.MaxModifiersPerTarget(Need.Boredom)];

        Assert.Equal(0, cascades.AppendModifiers(needs, Need.Boredom, lavUnreachable: true, buffer));
    }

    /// <summary>A buffer too small for the modifiers that fire is refused, naming it.</summary>
    [Fact]
    public void BufferTooSmallIsRefused()
    {
        Cascades cascades = D3();
        NeedSet needs = new(uneaseBaseline: 0.0);
        needs.Set(Need.Refreshment, 75.0);
        needs.Set(Need.Rest, 85.0);
        var oneShort = new RateModifier[1];
        RateModifier[] empty = [];

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            cascades.AppendModifiers(needs, Need.Unease, lavUnreachable: false, oneShort)
        );
        ArgumentException emptyBuffer = Assert.Throws<ArgumentException>(() =>
            cascades.AppendModifiers(needs, Need.Unease, lavUnreachable: false, empty)
        );

        Assert.Equal("buffer", exception.ParamName);
        Assert.Equal("buffer", emptyBuffer.ParamName);
    }

    /// <summary>A rule whose threshold is 100 is refused, naming the rule's index.</summary>
    [Fact]
    public void ThresholdAtOneHundredIsRefused()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            new Cascades([new CascadeRule(Need.Rest, Need.Unease, 100.0, 1.2, CascadeCondition.None)])
        );

        Assert.Equal("rules", exception.ParamName);
        Assert.Contains("0", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>A rule whose source and target are the same need is refused.</summary>
    [Fact]
    public void SameSourceAndTargetIsRefused()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            new Cascades([new CascadeRule(Need.Unease, Need.Unease, 70.0, 1.1, CascadeCondition.None)])
        );

        Assert.Equal("rules", exception.ParamName);
    }

    /// <summary>A rule factor of zero, below zero or NaN is refused, naming the rule's index.</summary>
    /// <param name="factor">The factor the rule carries.</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.5)]
    [InlineData(double.NaN)]
    public void FactorAtZeroIsRefused(double factor)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            new Cascades([new CascadeRule(Need.Rest, Need.Unease, 80.0, factor, CascadeCondition.None)])
        );

        Assert.Equal("rules", exception.ParamName);
        Assert.Contains("0", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>A buffer sized for the modifiers that fire, not for every rule, is accepted.</summary>
    [Fact]
    public void BufferSizedForWhatFiresIsAccepted()
    {
        Cascades cascades = D3();
        NeedSet needs = new(uneaseBaseline: 0.0);
        needs.Set(Need.Refreshment, 75.0);
        var buffer = new RateModifier[1];

        Assert.Equal(1, cascades.AppendModifiers(needs, Need.Unease, lavUnreachable: false, buffer));
        Assert.Equal(1.1, buffer[0].Factor, Precision);
    }

    /// <summary>A null rule is refused by its index, rather than left to a NullReferenceException.</summary>
    [Fact]
    public void NullRuleIsRefused()
    {
        var rules = new CascadeRule[2];
        rules[0] = RestCascade;
        rules[1] = null!;

        ArgumentException exception = Assert.Throws<ArgumentException>(() => new Cascades(rules));

        Assert.Equal("rules", exception.ParamName);
        Assert.Contains("1", exception.Message, StringComparison.Ordinal);
    }

    private static Cascades D3() => new([RefreshmentCascade, RestCascade, BladderCascade]);
}
