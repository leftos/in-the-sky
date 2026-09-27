using CsCheck;
using Sky.Engine.Crew;
using Sky.Engine.Time;

namespace Sky.Engine.Tests.Crew;

/// <summary>Pins crew strain's sources, its decay, its clamp and the redline count, on crew.md's first values.</summary>
public sealed class CrewStrainTests
{
    private const int Minute = SimTime.TicksPerSimMinute;
    private const double Tolerance = 1e-9;

    private static readonly StrainSettings FirstValues = new()
    {
        OnTaskPerMinute = 0.45,
        NoBreakMinutes = 60,
        NoBreakFactor = 1.5,
        PreemptionStep = 5,
        RepeatPreemptionStep = 8,
        RepeatWindowMinutes = 10,
        BacklogPerTaskPerMinute = 0.1,
        BacklogCapPerMinute = 0.3,
        SevereIncidentStep = 6,
        IdleDecayPerMinute = 0.2,
        BreakDecayPerMinute = 3,
        Redline = 70,
    };

    /// <summary>A lone pre-emption adds its step, scaled by the trait's pre-emption factor (Steady 0.5, Short fuse 1.5).</summary>
    [Theory]
    [InlineData(1.0, 5.0)]
    [InlineData(0.5, 2.5)]
    [InlineData(1.5, 7.5)]
    public void PreemptionAddsItsStep(double preemptionFactor, double expected)
    {
        CrewStrain strain = new(FirstValues, preemptionFactor, 1.0);

        strain.Preempted(100);

        Assert.Equal(expected, strain.Value, Tolerance);
        Assert.Equal(expected, strain.Peak, Tolerance);
    }

    /// <summary>A second pre-emption at the window's edge adds its step and the repeat step, both scaled by the trait.</summary>
    [Theory]
    [InlineData(1.0, 18.0)]
    [InlineData(1.5, 27.0)]
    public void SecondPreemptionInsideWindowAddsRepeatStep(double preemptionFactor, double expected)
    {
        CrewStrain strain = new(FirstValues, preemptionFactor, 1.0);

        strain.Preempted(100);
        strain.Preempted(100 + (10 * Minute));

        Assert.Equal(expected, strain.Value, Tolerance);
    }

    /// <summary>A second pre-emption one tick past the window adds only its own step.</summary>
    [Fact]
    public void PreemptionOutsideWindowAddsOnlyItsStep()
    {
        CrewStrain strain = new(FirstValues, 1.0, 1.0);

        strain.Preempted(100);
        strain.Preempted(100 + (10 * Minute) + 1);

        Assert.Equal(10.0, strain.Value, Tolerance);
    }

    /// <summary>A minute on break takes 3 off whatever the backlog, a minute idle takes 0.2, and a seated minute changes nothing.</summary>
    [Fact]
    public void BreakLowersStrain()
    {
        Driver driver = new(new CrewStrain(FirstValues, 1.0, 1.0));
        for (int arrival = 0; arrival < 5; arrival++)
        {
            driver.Strain.SevereIncidentArrival();
        }

        Assert.Equal(30.0, driver.Strain.Value, Tolerance);

        driver.Run(CrewActivity.OnBreak, Minute, fatigue: 0, backlog: 10);
        Assert.Equal(27.0, driver.Strain.Value, Tolerance);

        driver.Run(CrewActivity.Idle, Minute, fatigue: 0, backlog: 0);
        Assert.Equal(26.8, driver.Strain.Value, Tolerance);

        driver.Run(CrewActivity.Seated, Minute, fatigue: 0, backlog: 0);
        Assert.Equal(26.8, driver.Strain.Value, Tolerance);
        Assert.Equal(30.0, driver.Strain.Peak, Tolerance);
    }

    /// <summary>The on-task rate is 0.45 a minute for the first hour without a break, 1.5 times that after, and back to 0.45 after a break.</summary>
    [Fact]
    public void OnTaskRateRisesAfterNoBreakThreshold()
    {
        Driver driver = new(new CrewStrain(FirstValues, 1.0, 1.0));

        driver.Run(CrewActivity.OnTask, 60 * Minute, fatigue: 0, backlog: 0);
        Assert.Equal(27.0, driver.Strain.Value, Tolerance);
        Assert.Equal(60L * Minute, driver.Strain.TicksSinceBreak);

        driver.Run(CrewActivity.OnTask, Minute, fatigue: 0, backlog: 0);
        Assert.Equal(27.675, driver.Strain.Value, Tolerance);

        driver.Run(CrewActivity.OnBreak, 1, fatigue: 0, backlog: 0);
        Assert.Equal(0, driver.Strain.TicksSinceBreak);
        driver.Run(CrewActivity.OnTask, Minute, fatigue: 0, backlog: 0);
        Assert.Equal(27.675 - (3.0 / Minute) + 0.45, driver.Strain.Value, Tolerance);
    }

    /// <summary>Fatigue scales the on-task rate by 1 + fatigue / 100, and the Brisk trait's factor scales it too.</summary>
    [Theory]
    [InlineData(0.0, 1.0, 0.45)]
    [InlineData(50.0, 1.0, 0.675)]
    [InlineData(100.0, 1.0, 0.9)]
    [InlineData(0.0, 1.15, 0.5175)]
    public void FatigueScalesOnTaskRate(double fatigue, double onTaskFactor, double expectedPerMinute)
    {
        Driver driver = new(new CrewStrain(FirstValues, 1.0, onTaskFactor));

        driver.Run(CrewActivity.OnTask, Minute, fatigue, backlog: 0);

        Assert.Equal(expectedPerMinute, driver.Strain.Value, Tolerance);
    }

    /// <summary>The backlog adds 0.1 a minute per task, never more than 0.3 a minute.</summary>
    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(1, 0.1)]
    [InlineData(3, 0.3)]
    [InlineData(10, 0.3)]
    public void BacklogIsCapped(int backlog, double expectedPerMinute)
    {
        Driver driver = new(new CrewStrain(FirstValues, 1.0, 1.0));

        driver.Run(CrewActivity.Seated, Minute, fatigue: 0, backlog);

        Assert.Equal(expectedPerMinute, driver.Strain.Value, Tolerance);
    }

    /// <summary>
    /// Strain held exactly at the redline counts no tick over it; above it every tick ending there counts, a pre-emption
    /// crossing it counts no tick by itself, and minutes are the ticks over sixty seconds' worth.
    /// </summary>
    [Fact]
    public void MinutesOverRedlineCountOnlyTicksAbove()
    {
        Driver driver = new(new CrewStrain(FirstValues, 1.0, 1.0));
        for (int preemption = 0; preemption < 14; preemption++)
        {
            driver.Strain.Preempted(preemption * 11L * Minute);
        }

        Assert.Equal(70.0, driver.Strain.Value);
        driver.Run(CrewActivity.Seated, Minute, fatigue: 0, backlog: 0);
        Assert.Equal(0, driver.Strain.TicksOverRedline);
        Assert.Equal(0.0, driver.Strain.MinutesOverRedline);
        Assert.False(driver.Strain.OverRedline);

        driver.Strain.Preempted(1_000L * Minute);
        Assert.True(driver.Strain.OverRedline);
        Assert.Equal(0, driver.Strain.TicksOverRedline);

        driver.Run(CrewActivity.Seated, 2 * Minute, fatigue: 0, backlog: 0);
        Assert.Equal(2 * Minute, driver.Strain.TicksOverRedline);
        Assert.Equal(2.0, driver.Strain.MinutesOverRedline, Tolerance);
    }

    /// <summary>Any mix of activities, fatigue, backlog, pre-emptions and severe incidents keeps strain and its peak within 0 to 100.</summary>
    [Fact]
    public void StrainStaysInZeroToHundred() =>
        Gen.Select(Gen.Int[0, 3], Gen.Double[0.0, 100.0], Gen.Int[0, 10], Gen.Int[0, 9])
            .Array[0, 400]
            .Sample(steps =>
            {
                CrewStrain strain = new(FirstValues, 1.5, 1.15);
                long tick = 0;
                foreach ((int activity, double fatigue, int backlog, int roll) in steps)
                {
                    tick++;
                    if (roll == 0)
                    {
                        strain.Preempted(tick);
                    }

                    if (roll == 9)
                    {
                        strain.SevereIncidentArrival();
                    }

                    strain.Tick(tick, (CrewActivity)activity, fatigue, backlog);
                    if (strain.Value is < 0.0 or > 100.0 || strain.Peak < strain.Value || strain.Peak > 100.0)
                    {
                        return false;
                    }
                }

                return true;
            });

    /// <summary>Settings and trait factors out of range throw, as do fatigue out of 0 to 100, a negative backlog, and ticks that go back.</summary>
    [Fact]
    public void RejectsInvalidInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FirstValues with { Redline = -1 });
        Assert.Throws<ArgumentOutOfRangeException>(() => FirstValues with { OnTaskPerMinute = double.NaN });
        Assert.Throws<ArgumentOutOfRangeException>(() => FirstValues with { BreakDecayPerMinute = double.PositiveInfinity });
        Assert.Throws<ArgumentOutOfRangeException>(() => new CrewStrain(FirstValues, 0.0, 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CrewStrain(FirstValues, 1.0, double.NaN));

        CrewStrain strain = new(FirstValues, 1.0, 1.0);
        Assert.Throws<ArgumentOutOfRangeException>(() => strain.Tick(0, CrewActivity.Idle, 100.5, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => strain.Tick(0, CrewActivity.Idle, 0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => strain.Tick(0, (CrewActivity)9, 0, 0));
        strain.Tick(0, CrewActivity.Idle, 0, 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => strain.Tick(0, CrewActivity.Idle, 0, 0));
        strain.Preempted(5);
        Assert.Throws<ArgumentOutOfRangeException>(() => strain.Preempted(4));
    }

    private sealed class Driver(CrewStrain strain)
    {
        private long nextTick;

        public CrewStrain Strain { get; } = strain;

        public void Run(CrewActivity activity, int ticks, double fatigue, int backlog)
        {
            for (int count = 0; count < ticks; count++)
            {
                Strain.Tick(nextTick++, activity, fatigue, backlog);
            }
        }
    }
}
