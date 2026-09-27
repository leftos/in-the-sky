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

    private static readonly NeedContext Awake = new(IsAsleep: false, OnIfe: false, UneasePushPerHour: 0.0);

    private static readonly NeedContext Asleep = new(IsAsleep: true, OnIfe: false, UneasePushPerHour: 0.0);

    private static readonly NeedContext OnIfe = new(IsAsleep: false, OnIfe: true, UneasePushPerHour: 0.0);

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

    /// <summary>The Rest multiplier scales its rise while awake, and the Unease multiplier leaves the pull to baseline alone.</summary>
    [Fact]
    public void MultiplierScalesRestRiseAndLeavesUneasePull()
    {
        NeedRates rates = new(ZeroRates with { RestRisePerHour = 20.0, UneaseHalfLifeMinutes = 15.0 });
        NeedSet needs = new(uneaseBaseline: 20.0);
        needs.Set(Need.Rest, 80.0);
        needs.Set(Need.Unease, 60.0);
        double[] multipliers = Ones();
        multipliers[(int)Need.Rest] = 2.0;
        multipliers[(int)Need.Unease] = 2.5;

        Run(needs, rates, 15 * SimTime.TicksPerSimMinute, Awake, multipliers);

        Assert.Equal(90.0, needs[Need.Rest], Precision);
        Assert.Equal(40.0, needs[Need.Unease], Precision);
    }

    /// <summary>
    /// A steady push P with Unease multiplier m holds Unease at baseline + m × P ÷ k, where k is the pull rate ln 2 ÷ half-life
    /// (CONCEPT's worked example: baseline 20, P = 60 an hour, m = 2.5, half-life 15 minutes). The per-tick update's fixed
    /// point sits about m × P_t ÷ 2 under the continuous one, P_t being the push per tick: about 0.005 here.
    /// </summary>
    [Fact]
    public void SteadyPushHoldsUneaseAtMultiplierTimesPushOverPullRate()
    {
        NeedRates rates = new(ZeroRates with { UneaseHalfLifeMinutes = 15.0 });
        NeedSet needs = new(uneaseBaseline: 20.0);
        double[] multipliers = Ones();
        multipliers[(int)Need.Unease] = 2.5;
        NeedContext pushed = Awake with { UneasePushPerHour = 60.0 };

        Run(needs, rates, 4 * SimTime.TicksPerSimHour, pushed, multipliers);

        double pullRatePerHour = Math.Log(2.0) / 0.25;
        Assert.Equal(20.0 + (2.5 * 60.0 / pullRatePerHour), needs[Need.Unease], 0.05);
    }

    /// <summary>A −20 relief lowers Unease by exactly 20, and the pull after it runs the same, whatever the Unease multiplier.</summary>
    /// <param name="multiplier">The Unease multiplier in the ticks after the relief.</param>
    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(2.5)]
    public void ReliefIsUnscaled(double multiplier)
    {
        NeedRates rates = new(ZeroRates with { UneaseHalfLifeMinutes = 15.0 });
        NeedSet needs = new(uneaseBaseline: 20.0);
        double[] multipliers = Ones();
        multipliers[(int)Need.Unease] = multiplier;
        needs.Set(Need.Unease, 60.0);
        Run(needs, rates, 1, Awake, multipliers);
        double beforeRelief = needs[Need.Unease];

        needs.Add(Need.Unease, -20.0);
        Assert.Equal(beforeRelief - 20.0, needs[Need.Unease], ExactTolerance);

        needs.Set(Need.Unease, 60.0);
        needs.Add(Need.Unease, -20.0);
        Assert.Equal(40.0, needs[Need.Unease], ExactTolerance);

        Run(needs, rates, 15 * SimTime.TicksPerSimMinute, Awake, multipliers);
        Assert.Equal(30.0, needs[Need.Unease], Precision);
    }

    /// <summary>A sleeper's Rest falls at the flight's rate exactly, the same at a Rest multiplier of 0.5 and of 2.0.</summary>
    [Fact]
    public void RestFallIsUnscaled()
    {
        NeedRates rates = new(ZeroRates with { RestFallPerHour = 20.0 });

        Assert.Equal(60.0, RestAfterOneHourAsleep(rates, 0.5), Precision);
        Assert.Equal(60.0, RestAfterOneHourAsleep(rates, 2.0), Precision);
    }

    /// <summary>A one-off push on Unease lands as its amount times the multiplier, and stops at 100.</summary>
    [Fact]
    public void PushUneaseScalesByMultiplier()
    {
        NeedSet needs = new(uneaseBaseline: 20.0);

        needs.PushUnease(10.0, 2.5);
        Assert.Equal(45.0, needs[Need.Unease], ExactTolerance);

        needs.PushUnease(10.0, 0.0);
        Assert.Equal(45.0, needs[Need.Unease], ExactTolerance);

        needs.PushUnease(40.0, 2.0);
        Assert.Equal(100.0, needs[Need.Unease], ExactTolerance);
    }

    /// <summary>A positive <c>Add</c> on Unease is rejected, pointing at <c>PushUnease</c>, and leaves Unease alone.</summary>
    [Fact]
    public void PositiveAddOnUneaseThrows()
    {
        NeedSet needs = new(uneaseBaseline: 20.0);

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => needs.Add(Need.Unease, 5.0));

        Assert.Equal("delta", exception.ParamName);
        Assert.Contains(nameof(NeedSet.PushUnease), exception.Message, StringComparison.Ordinal);
        Assert.Equal(20.0, needs[Need.Unease], ExactTolerance);
    }

    /// <summary>
    /// With the Unease multiplier at 2.5, a −20 pulse on Unease lands as −20 and a +10 pulse lands as +25, while a Bladder pulse ignores
    /// the Bladder multiplier. The half-life is long enough that the pull does not move Unease within the window.
    /// </summary>
    [Fact]
    public void NegativePulseSliceOnUneaseIsUnscaled()
    {
        NeedRates rates = new(ZeroRates with { UneaseHalfLifeMinutes = 1e12 });
        NeedSet needs = new(uneaseBaseline: 60.0);
        double[] multipliers = Ones();
        multipliers[(int)Need.Unease] = 2.5;
        multipliers[(int)Need.Bladder] = 2.0;
        needs.AddPulse(Need.Unease, -20.0, 10);
        needs.AddPulse(Need.Unease, 10.0, 10);
        needs.AddPulse(Need.Bladder, 15.0, 10);

        Run(needs, rates, 10, Awake, multipliers);

        Assert.Equal(65.0, needs[Need.Unease], Precision);
        Assert.Equal(15.0, needs[Need.Bladder], ExactTolerance);
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

    /// <summary>
    /// NaN is rejected by <c>Set</c>, <c>Add</c> and <c>AddPulse</c>, <c>Set</c> rejects a value outside [0, 100], and a
    /// negative or non-finite Unease push is rejected by <c>PushUnease</c> and by <c>Tick</c>'s context.
    /// </summary>
    [Fact]
    public void InvalidValueThrows()
    {
        NeedRates rates = new(ZeroRates);
        NeedSet needs = new(uneaseBaseline: 0.0);

        Assert.Throws<ArgumentOutOfRangeException>(() => needs.Set(Need.Rest, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => needs.Add(Need.Rest, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => needs.AddPulse(Need.Rest, double.NaN, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => needs.Set(Need.Rest, 100.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => needs.Set(Need.Rest, -0.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => needs.PushUnease(-1.0, 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => needs.PushUnease(double.PositiveInfinity, 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => needs.PushUnease(1.0, -0.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => needs.PushUnease(1.0, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => needs.Tick(rates, Ones(), Awake with { UneasePushPerHour = -1.0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => needs.Tick(rates, Ones(), Awake with { UneasePushPerHour = double.NaN }));
        Assert.Equal(0.0, needs[Need.Rest], Precision);
        Assert.Equal(0.0, needs[Need.Unease], Precision);
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
        var contexts = Gen.Select(Gen.Bool, Gen.Bool, Gen.Double[0.0, 200.0], (asleep, onIfe, push) => new NeedContext(asleep, onIfe, push));
        var steps = Gen.Select(
            Gen.Double[RateMultiplier.Floor, RateMultiplier.Cap].Array[NeedSet.NeedCount],
            contexts,
            Gen.Int[0, NeedSet.NeedCount - 1],
            Gen.Double[-150.0, 150.0],
            Gen.Int[1, 600],
            Gen.Int[1, 3000],
            Gen.Double[-150.0, 150.0],
            (multipliers, context, need, amount, pulseTicks, ticks, addAmount) =>
                new Step(multipliers, context, (Need)need, amount, pulseTicks, ticks, addAmount)
        );

        Gen.Select(settings, Gen.Double[0.0, 100.0], steps.Array[1, 4])
            .Sample((rateSettings, baseline, stepList) => StaysInRange(new NeedRates(rateSettings), new NeedSet(baseline), stepList));
    }

    private static bool StaysInRange(NeedRates rates, NeedSet needs, Step[] steps)
    {
        foreach (Step step in steps)
        {
            needs.AddPulse(step.PulseNeed, step.PulseAmount, step.PulseTicks);
            if (step.PulseNeed == Need.Unease && step.AddAmount > 0.0)
            {
                needs.PushUnease(step.AddAmount, step.Multipliers[(int)Need.Unease]);
            }
            else
            {
                needs.Add(step.PulseNeed, step.AddAmount);
            }

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

    private static double RestAfterOneHourAsleep(NeedRates rates, double restMultiplier)
    {
        NeedSet needs = new(uneaseBaseline: 0.0);
        needs.Set(Need.Rest, 80.0);
        double[] multipliers = Ones();
        multipliers[(int)Need.Rest] = restMultiplier;
        Run(needs, rates, SimTime.TicksPerSimHour, Asleep, multipliers);
        return needs[Need.Rest];
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
