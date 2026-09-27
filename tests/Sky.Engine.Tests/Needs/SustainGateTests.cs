using CsCheck;
using Sky.Engine.Needs;

namespace Sky.Engine.Tests.Needs;

/// <summary>
/// Pins the sustain gate: a need counts toward a failure only while it is eligible and at or above the threshold, the
/// failure is raised on exactly the tick the run reaches the window, and the gate re-arms only once the need falls below
/// the threshold minus the reset margin.
/// </summary>
public sealed class SustainGateTests
{
    private const double Threshold = 90.0;

    private const double Margin = 30.0;

    private const long Window = 10;

    /// <summary>The window the property's own gates use, short enough that a drawn sequence raises more than once.</summary>
    private const long PropertyWindow = 3;

    /// <summary>A counting tick's value: at or above the threshold, below 100.</summary>
    private const double Failing = 95.0;

    /// <summary>A value below the threshold but above the re-arming line.</summary>
    private const double NearMiss = 70.0;

    /// <summary>A value below the threshold minus the margin, inside [0, 100).</summary>
    private const double ReArming = 59.9;

    /// <summary>The tick before the window ends raises nothing, and the run stands one short.</summary>
    [Fact]
    public void OneTickShortOfTheWindowRaisesNothing()
    {
        SustainGate gate = Gate();

        for (long tick = 0; tick < Window - 1; tick++)
        {
            Assert.False(gate.Tick(Failing, eligible: true));
        }

        Assert.Equal(Window - 1, gate.RunTicks);
        Assert.True(gate.IsArmed);
    }

    /// <summary>The window's last tick raises the failure once, and the gate disarms with its run reset.</summary>
    [Fact]
    public void FullWindowRaisesOnce()
    {
        SustainGate gate = Gate();
        int raises = 0;

        for (long tick = 0; tick < Window; tick++)
        {
            raises += gate.Tick(Failing, eligible: true) ? 1 : 0;
        }

        Assert.Equal(1, raises);
        Assert.False(gate.IsArmed);
        Assert.Equal(0, gate.RunTicks);
    }

    /// <summary>A tick below the threshold resets the run, so the window starts over.</summary>
    [Fact]
    public void DroppingBelowResetsTheRun()
    {
        SustainGate gate = Gate();
        Run(gate, 5, Failing, eligible: true);

        Assert.False(gate.Tick(Threshold - 0.1, eligible: true));

        Assert.Equal(0, gate.RunTicks);
        Assert.True(gate.IsArmed);
    }

    /// <summary>A need that stays failing after raising does not raise a second failure, because the gate never re-arms.</summary>
    [Fact]
    public void StillFailingAfterRaisingDoesNotRaiseAgain()
    {
        SustainGate gate = Raise();

        for (long tick = 0; tick < 4 * Window; tick++)
        {
            Assert.False(gate.Tick(Failing, eligible: true));
        }

        Assert.False(gate.IsArmed);
    }

    /// <summary>
    /// Only a value strictly below the threshold minus the margin re-arms the gate, eligibility does not matter for
    /// re-arming, and the re-arming tick does not count.
    /// </summary>
    [Fact]
    public void RearmsOnlyBelowThresholdMinusMargin()
    {
        SustainGate gate = Raise();

        Assert.False(gate.Tick(NearMiss, eligible: true));
        Assert.False(gate.IsArmed);

        Assert.False(gate.Tick(Threshold - Margin, eligible: false));
        Assert.False(gate.IsArmed);

        Assert.False(gate.Tick(ReArming, eligible: false));
        Assert.True(gate.IsArmed);
        Assert.Equal(0, gate.RunTicks);

        Run(gate, Window - 1, Failing, eligible: true);
        Assert.True(gate.Tick(Failing, eligible: true));
    }

    /// <summary>An ineligible tick resets the run even when the need is failing.</summary>
    [Fact]
    public void IneligibleTickResetsTheRun()
    {
        SustainGate gate = Gate();
        Run(gate, 5, Failing, eligible: true);

        Assert.False(gate.Tick(Failing, eligible: false));

        Assert.Equal(0, gate.RunTicks);
        Assert.True(gate.IsArmed);
    }

    /// <summary>A need exactly at the threshold counts toward the window.</summary>
    [Fact]
    public void ValueExactlyAtThresholdCounts()
    {
        SustainGate gate = Gate();

        Run(gate, Window - 1, Threshold, eligible: true);

        Assert.True(gate.Tick(Threshold, eligible: true));
    }

    /// <summary>A NaN value is refused, naming it, because per-tick inputs are the Engine's to police.</summary>
    [Fact]
    public void NaNValueIsRefused()
    {
        SustainGate gate = Gate();

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => gate.Tick(double.NaN, eligible: true));

        Assert.Equal("value", exception.ParamName);
        Assert.True(gate.IsArmed);
    }

    /// <summary>A threshold of 100 is refused: a need at its worst cannot hold there by design.</summary>
    [Fact]
    public void ThresholdAtOneHundredIsRefused()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => new SustainGate(100.0, Window, Margin));

        Assert.Equal("threshold", exception.ParamName);
    }

    /// <summary>A window below one tick is refused.</summary>
    [Fact]
    public void ZeroWindowIsRefused()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => new SustainGate(Threshold, 0, Margin));

        Assert.Equal("windowTicks", exception.ParamName);
    }

    /// <summary>A margin equal to the threshold, which would leave no value able to re-arm, is refused.</summary>
    [Fact]
    public void MarginEqualToThresholdIsRefused()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => new SustainGate(Threshold, Window, Threshold));

        Assert.Equal("resetMargin", exception.ParamName);
    }

    /// <summary>A margin below zero, or one above the threshold, is refused.</summary>
    [Fact]
    public void MarginOutsideZeroToThresholdIsRefused()
    {
        ArgumentOutOfRangeException negative = Assert.Throws<ArgumentOutOfRangeException>(() => new SustainGate(Threshold, Window, -0.1));
        ArgumentOutOfRangeException above = Assert.Throws<ArgumentOutOfRangeException>(() => new SustainGate(Threshold, Window, Threshold + 0.1));

        Assert.Equal("resetMargin", negative.ParamName);
        Assert.Equal("resetMargin", above.ParamName);
    }

    /// <summary>
    /// Over sequences drawn from the three levels the gate acts on, every raise after the first is preceded by a value
    /// below threshold minus margin. A raise needs the property's own short window of failing ticks in a row, so most
    /// drawn sequences raise twice and the property is not vacuous.
    /// </summary>
    [Fact]
    public void RaisesAtMostOncePerArming()
    {
        var values = Gen.Frequency((6, Gen.Const(Failing)), (2, Gen.Const(NearMiss)), (2, Gen.Const(ReArming)));
        Gen<Tick[]> ticks = Gen.Select(values, Gen.Int[0, 9].Select(eligible => eligible != 0), (value, eligible) => new Tick(value, eligible)).Array[
            1,
            200
        ];
        int sequencesWithTwoRaises = 0;

        ticks.Sample(sequence =>
        {
            bool spaced = RaisesAreSpaced(sequence, out int raises);
            sequencesWithTwoRaises += raises >= 2 ? 1 : 0;
            return spaced;
        });

        Assert.True(sequencesWithTwoRaises > 0, "no drawn sequence raised twice, so the property proved nothing");
    }

    private static bool RaisesAreSpaced(Tick[] sequence, out int raises)
    {
        SustainGate gate = new(Threshold, PropertyWindow, Margin);
        raises = 0;
        int lastRaise = -1;
        for (int index = 0; index < sequence.Length; index++)
        {
            if (!gate.Tick(sequence[index].Value, sequence[index].Eligible))
            {
                continue;
            }

            raises++;
            if (lastRaise >= 0 && !HasReArmingTick(sequence, lastRaise + 1, index - 1))
            {
                return false;
            }

            lastRaise = index;
        }

        return true;
    }

    private static bool HasReArmingTick(Tick[] sequence, int from, int to)
    {
        for (int index = from; index <= to; index++)
        {
            if (sequence[index].Value < Threshold - Margin)
            {
                return true;
            }
        }

        return false;
    }

    private static SustainGate Gate() => new(Threshold, Window, Margin);

    private static SustainGate Raise()
    {
        SustainGate gate = Gate();
        Run(gate, Window, Failing, eligible: true);
        return gate;
    }

    private static void Run(SustainGate gate, long ticks, double value, bool eligible)
    {
        for (long tick = 0; tick < ticks; tick++)
        {
            gate.Tick(value, eligible);
        }
    }

    private readonly record struct Tick(double Value, bool Eligible);
}
