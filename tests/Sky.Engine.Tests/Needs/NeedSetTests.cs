using CsCheck;
using Sky.Engine.Needs;
using Sky.Engine.Time;

namespace Sky.Engine.Tests.Needs;

/// <summary>Pins the need set's per-tick update: base rates, pulses, the Unease pull, pauses and the [0, 100] clamp.</summary>
public sealed class NeedSetTests
{
    private const int Precision = 9;

    /// <summary>
    /// An absolute bound of 1e-12 rather than rounding to 12 places: 7,200 additions leave the value about 7e-13 short of
    /// 15, which rounding to 12 places reads as a miss while it lies well within 1e-12.
    /// </summary>
    private const double ExactTolerance = 1e-12;

    private static readonly NeedRateSettings ZeroRates = new()
    {
        RefreshmentPerHour = 0.0,
        BladderPerHour = 0.0,
        RestRisePerHour = 0.0,
        RestFallPerHour = 0.0,
        BoredomPerHour = 0.0,
        UneaseHalfLifeMinutes = 15.0,
    };

    private static readonly NeedContext Awake = new(IsAsleep: false, OnIfe: false);

    private static readonly NeedContext Asleep = new(IsAsleep: true, OnIfe: false);

    private static readonly NeedContext OnIfe = new(IsAsleep: false, OnIfe: true);

    /// <summary>Refreshment at 25 an hour is a quarter full after one sim hour and full after four.</summary>
    [Fact]
    public void RefreshmentReachesFullAfterFourSimHours()
    {
        NeedRates rates = new(ZeroRates with { RefreshmentPerHour = 25.0 });
        NeedSet needs = new(uneaseBaseline: 0.0);

        Run(needs, rates, SimTime.TicksPerSimHour, Awake, Ones());
        Assert.Equal(25.0, needs[Need.Refreshment], Precision);

        Run(needs, rates, 3 * SimTime.TicksPerSimHour, Awake, Ones());
        Assert.Equal(100.0, needs[Need.Refreshment], Precision);
    }

    /// <summary>A +15 Bladder pulse over 30 sim minutes is half landed at 15 minutes and lands whole at 30.</summary>
    [Fact]
    public void DrinkPulseLandsWholeAcrossThirtyMinutes()
    {
        NeedRates rates = new(ZeroRates);
        NeedSet needs = new(uneaseBaseline: 0.0);
        needs.AddPulse(Need.Bladder, 15.0, 30 * SimTime.TicksPerSimMinute);

        Run(needs, rates, 15 * SimTime.TicksPerSimMinute, Awake, Ones());
        Assert.Equal(7.5, needs[Need.Bladder], ExactTolerance);

        Run(needs, rates, 15 * SimTime.TicksPerSimMinute, Awake, Ones());
        Assert.Equal(15.0, needs[Need.Bladder], ExactTolerance);

        Run(needs, rates, SimTime.TicksPerSimMinute, Awake, Ones());
        Assert.Equal(15.0, needs[Need.Bladder], ExactTolerance);
    }

    /// <summary>Unease at 60 over a baseline of 20 closes half the gap in one 15-minute half-life.</summary>
    [Fact]
    public void UneaseHalvesTowardBaselineInOneHalfLife()
    {
        NeedRates rates = new(ZeroRates with { UneaseHalfLifeMinutes = 15.0 });
        NeedSet needs = new(uneaseBaseline: 20.0);
        needs.Set(Need.Unease, 60.0);

        Run(needs, rates, 15 * SimTime.TicksPerSimMinute, Awake, Ones());

        Assert.Equal(40.0, needs[Need.Unease], Precision);
    }

    /// <summary>Boredom rises awake and holds, without falling, while asleep or on IFE.</summary>
    [Fact]
    public void BoredomPausesWhileAsleepOrOnIfe()
    {
        NeedRates rates = new(ZeroRates with { BoredomPerHour = 30.0 });
        NeedSet needs = new(uneaseBaseline: 0.0);

        Run(needs, rates, SimTime.TicksPerSimHour, Awake, Ones());
        Assert.Equal(30.0, needs[Need.Boredom], Precision);

        Run(needs, rates, SimTime.TicksPerSimHour, Asleep, Ones());
        Assert.Equal(30.0, needs[Need.Boredom], Precision);

        Run(needs, rates, SimTime.TicksPerSimHour, OnIfe, Ones());
        Assert.Equal(30.0, needs[Need.Boredom], Precision);

        Run(needs, rates, SimTime.TicksPerSimHour, Awake, Ones());
        Assert.Equal(60.0, needs[Need.Boredom], Precision);
    }

    /// <summary>Rest rises while awake, falls only while asleep, and stops at 0.</summary>
    [Fact]
    public void RestFallsOnlyWhileAsleep()
    {
        NeedRates rates = new(ZeroRates with { RestRisePerHour = 10.0, RestFallPerHour = 40.0 });
        NeedSet needs = new(uneaseBaseline: 0.0);

        Run(needs, rates, 2 * SimTime.TicksPerSimHour, Awake, Ones());
        Assert.Equal(20.0, needs[Need.Rest], Precision);

        Run(needs, rates, 15 * SimTime.TicksPerSimMinute, Asleep, Ones());
        Assert.Equal(10.0, needs[Need.Rest], Precision);

        Run(needs, rates, SimTime.TicksPerSimHour, Asleep, Ones());
        Assert.Equal(0.0, needs[Need.Rest], Precision);
    }

    /// <summary>A multiplier of 2 doubles a one-hour Refreshment rise.</summary>
    [Fact]
    public void MultiplierScalesBaseChange()
    {
        NeedRates rates = new(ZeroRates with { RefreshmentPerHour = 25.0 });
        NeedSet needs = new(uneaseBaseline: 0.0);
        double[] multipliers = Ones();
        multipliers[(int)Need.Refreshment] = 2.0;

        Run(needs, rates, SimTime.TicksPerSimHour, Awake, multipliers);

        Assert.Equal(50.0, needs[Need.Refreshment], Precision);
    }

    /// <summary>The Rest multiplier scales its fall while asleep, and the Unease multiplier leaves the pull to baseline alone.</summary>
    [Fact]
    public void MultiplierScalesRestFallAndLeavesUneasePull()
    {
        NeedRates rates = new(ZeroRates with { RestFallPerHour = 20.0, UneaseHalfLifeMinutes = 15.0 });
        NeedSet needs = new(uneaseBaseline: 20.0);
        needs.Set(Need.Rest, 80.0);
        needs.Set(Need.Unease, 60.0);
        double[] multipliers = Ones();
        multipliers[(int)Need.Rest] = 2.0;
        multipliers[(int)Need.Unease] = 2.5;

        Run(needs, rates, 15 * SimTime.TicksPerSimMinute, Asleep, multipliers);

        Assert.Equal(70.0, needs[Need.Rest], Precision);
        Assert.Equal(40.0, needs[Need.Unease], Precision);
    }

    /// <summary>A multiplier span that is not one per need is rejected, naming the parameter.</summary>
    /// <param name="length">The span's length.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(6)]
    public void WrongMultiplierCountThrows(int length)
    {
        NeedRates rates = new(ZeroRates);
        NeedSet needs = new(uneaseBaseline: 0.0);
        double[] multipliers = new double[length];

        ArgumentException exception = Assert.Throws<ArgumentException>(() => needs.Tick(rates, multipliers, Awake));

        Assert.Equal("multipliers", exception.ParamName);
    }

    /// <summary>A negative or non-finite rate, or a half-life that is not positive, is rejected, naming the settings.</summary>
    /// <param name="rate">The Bladder rate per hour.</param>
    /// <param name="halfLifeMinutes">The Unease half-life in minutes.</param>
    [Theory]
    [InlineData(-1.0, 15.0)]
    [InlineData(double.NaN, 15.0)]
    [InlineData(double.PositiveInfinity, 15.0)]
    [InlineData(1.0, 0.0)]
    [InlineData(1.0, double.NaN)]
    public void InvalidSettingsThrow(double rate, double halfLifeMinutes)
    {
        NeedRateSettings settings = ZeroRates with { BladderPerHour = rate, UneaseHalfLifeMinutes = halfLifeMinutes };

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => new NeedRates(settings));

        Assert.Equal("settings", exception.ParamName);
    }

    /// <summary>A pulse over fewer than one tick, or a baseline outside [0, 100], is rejected.</summary>
    [Fact]
    public void InvalidPulseOrBaselineThrows()
    {
        NeedSet needs = new(uneaseBaseline: 0.0);

        Assert.Throws<ArgumentOutOfRangeException>(() => needs.AddPulse(Need.Bladder, 15.0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NeedSet(uneaseBaseline: 100.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NeedSet(uneaseBaseline: double.NaN));
    }

    /// <summary>A negative or non-finite multiplier is rejected, naming the parameter.</summary>
    /// <param name="multiplier">The Refreshment multiplier.</param>
    [Theory]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidMultiplierThrows(double multiplier)
    {
        NeedRates rates = new(ZeroRates);
        NeedSet needs = new(uneaseBaseline: 0.0);
        double[] multipliers = Ones();
        multipliers[(int)Need.Refreshment] = multiplier;

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => needs.Tick(rates, multipliers, Awake));

        Assert.Equal("multipliers", exception.ParamName);
    }

    /// <summary>NaN is rejected by <c>Set</c>, <c>Add</c> and <c>AddPulse</c>, and <c>Set</c> rejects a value outside [0, 100].</summary>
    [Fact]
    public void InvalidValueThrows()
    {
        NeedSet needs = new(uneaseBaseline: 0.0);

        Assert.Throws<ArgumentOutOfRangeException>(() => needs.Set(Need.Rest, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => needs.Add(Need.Rest, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => needs.AddPulse(Need.Rest, double.NaN, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => needs.Set(Need.Rest, 100.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => needs.Set(Need.Rest, -0.5));
        Assert.Equal(0.0, needs[Need.Rest], Precision);
    }

    /// <summary>Reading a value past the last defined need is rejected.</summary>
    [Fact]
    public void UndefinedNeedIndexThrows()
    {
        NeedSet needs = new(uneaseBaseline: 0.0);

        Assert.Throws<ArgumentOutOfRangeException>(() => needs[(Need)NeedSet.NeedCount]);
    }

    /// <summary>An immediate push past the top stops at 100.</summary>
    [Fact]
    public void AddClampsAtHundred()
    {
        NeedSet needs = new(uneaseBaseline: 0.0);

        needs.Add(Need.Refreshment, 150.0);

        Assert.Equal(100.0, needs[Need.Refreshment], Precision);
    }

    /// <summary>The pending pulse count falls as each pulse's last tick lands, down to 0.</summary>
    [Fact]
    public void PendingPulseCountFallsToZeroAsPulsesLand()
    {
        NeedRates rates = new(ZeroRates);
        NeedSet needs = new(uneaseBaseline: 0.0);
        Assert.Equal(0, needs.PendingPulseCount);

        needs.AddPulse(Need.Bladder, 10.0, 2);
        needs.AddPulse(Need.Refreshment, 5.0, 4);
        Assert.Equal(2, needs.PendingPulseCount);

        Run(needs, rates, 2, Awake, Ones());
        Assert.Equal(1, needs.PendingPulseCount);

        Run(needs, rates, 2, Awake, Ones());
        Assert.Equal(0, needs.PendingPulseCount);
        Assert.Equal(5.0, needs[Need.Refreshment], ExactTolerance);
    }

    /// <summary>The need count covers every defined need.</summary>
    [Fact]
    public void NeedCountCoversEveryNeed() => Assert.Equal(NeedSet.NeedCount, Enum.GetValues<Need>().Length);

    /// <summary>Under any settings, multipliers, contexts and pulses, every need stays within [0, 100] after every tick.</summary>
    [Fact]
    public void NoNeedLeavesZeroToHundred()
    {
        var settings = Gen.Select(
            Gen.Double[0.0, 200.0],
            Gen.Double[0.0, 200.0],
            Gen.Double[0.0, 200.0],
            Gen.Double[0.0, 200.0],
            Gen.Double[0.0, 200.0],
            Gen.Double[0.5, 120.0],
            (refreshment, bladder, restRise, restFall, boredom, halfLife) =>
                new NeedRateSettings
                {
                    RefreshmentPerHour = refreshment,
                    BladderPerHour = bladder,
                    RestRisePerHour = restRise,
                    RestFallPerHour = restFall,
                    BoredomPerHour = boredom,
                    UneaseHalfLifeMinutes = halfLife,
                }
        );
        var steps = Gen.Select(
            Gen.Double[RateMultiplier.Floor, RateMultiplier.Cap].Array[NeedSet.NeedCount],
            Gen.Bool,
            Gen.Bool,
            Gen.Int[0, NeedSet.NeedCount - 1],
            Gen.Double[-150.0, 150.0],
            Gen.Int[1, 600],
            Gen.Int[1, 3000],
            Gen.Double[-150.0, 150.0],
            (multipliers, asleep, onIfe, need, amount, pulseTicks, ticks, addAmount) =>
                new Step(multipliers, new NeedContext(asleep, onIfe), (Need)need, amount, pulseTicks, ticks, addAmount)
        );

        Gen.Select(settings, Gen.Double[0.0, 100.0], steps.Array[1, 4])
            .Sample((rateSettings, baseline, stepList) => StaysInRange(new NeedRates(rateSettings), new NeedSet(baseline), stepList));
    }

    private static bool StaysInRange(NeedRates rates, NeedSet needs, Step[] steps)
    {
        foreach (Step step in steps)
        {
            needs.AddPulse(step.PulseNeed, step.PulseAmount, step.PulseTicks);
            needs.Add(step.PulseNeed, step.AddAmount);
            if (!AllInRange(needs))
            {
                return false;
            }

            for (int tick = 0; tick < step.Ticks; tick++)
            {
                needs.Tick(rates, step.Multipliers, step.Context);
                if (!AllInRange(needs))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool AllInRange(NeedSet needs)
    {
        foreach (Need need in Enum.GetValues<Need>())
        {
            if (!(needs[need] is >= 0.0 and <= 100.0))
            {
                return false;
            }
        }

        return true;
    }

    private static void Run(NeedSet needs, NeedRates rates, int ticks, NeedContext context, double[] multipliers)
    {
        for (int tick = 0; tick < ticks; tick++)
        {
            needs.Tick(rates, multipliers, context);
        }
    }

    private static double[] Ones() => [1.0, 1.0, 1.0, 1.0, 1.0];

    private sealed record Step(
        double[] Multipliers,
        NeedContext Context,
        Need PulseNeed,
        double PulseAmount,
        int PulseTicks,
        int Ticks,
        double AddAmount
    );
}
