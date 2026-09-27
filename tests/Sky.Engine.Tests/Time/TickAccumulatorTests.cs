using CsCheck;
using Sky.Engine.Time;

namespace Sky.Engine.Tests.Time;

/// <summary>Proves the tick accumulator turns elapsed milliseconds into whole ticks and carries the rest.</summary>
public sealed class TickAccumulatorTests
{
    /// <summary>A hundred milliseconds are no tick yet; four hundred more make two, with nothing left over.</summary>
    [Fact]
    public void HundredThenFourHundredMillisecondsGiveZeroThenTwoTicks()
    {
        TickAccumulator accumulator = new();

        Assert.Equal(0, accumulator.Add(100));
        Assert.Equal(2, accumulator.Add(400));
        Assert.Equal(0, accumulator.RemainderMilliseconds);
    }

    /// <summary>A part of a tick left over by one call counts toward the tick of the next.</summary>
    [Fact]
    public void RemainderCarriesAcrossCalls()
    {
        TickAccumulator accumulator = new();

        Assert.Equal(0, accumulator.Add(249));
        Assert.Equal(1, accumulator.Add(1));
    }

    /// <summary>Negative elapsed time is refused, naming the argument and carrying the value.</summary>
    [Fact]
    public void NegativeElapsedThrows()
    {
        TickAccumulator accumulator = new();

        ArgumentOutOfRangeException thrown = Assert.Throws<ArgumentOutOfRangeException>(() => accumulator.Add(-1));
        Assert.Equal("elapsedMilliseconds", thrown.ParamName);
        Assert.Equal(-1L, Assert.IsType<long>(thrown.ActualValue));
    }

    /// <summary>However the elapsed time is split across calls, the ticks and the remainder match those of the whole.</summary>
    [Fact]
    public void TicksOverAnySplitEqualTicksOfTheWhole()
    {
        Gen.Long[0, 10_000]
            .List.Sample(parts =>
            {
                TickAccumulator accumulator = new();
                long ticks = parts.Sum(part => (long)accumulator.Add(part));
                long total = parts.Sum();

                Assert.Equal(total / SimTime.TickMilliseconds, ticks);
                Assert.Equal(total % SimTime.TickMilliseconds, accumulator.RemainderMilliseconds);
            });
    }
}
