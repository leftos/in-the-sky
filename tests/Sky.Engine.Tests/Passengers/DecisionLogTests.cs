using Sky.Engine.Passengers;

namespace Sky.Engine.Tests.Passengers;

/// <summary>Proves the decision log: each passenger keeps its own list, and a dump finds the decision in force at a tick.</summary>
public sealed class DecisionLogTests
{
    private static readonly ActivityId Idle = new(0);

    private static readonly ActivityId LavVisit = new(1);

    private static readonly ActivityId Screen = new(2);

    /// <summary>With decisions at ticks 10, 490 and 970, the one in force at tick 500 is the one taken at 490.</summary>
    [Fact]
    public void DumpReturnsTheDecisionInForceAtTheTick()
    {
        DecisionLog log = ThreeDecisions();

        DecisionRecord? dumped = log.Dump(0, 500);

        Assert.NotNull(dumped);
        Assert.Equal(490, dumped.Value.Tick);
        Assert.Equal(LavVisit, dumped.Value.Chosen);
        Assert.Equal(18.75, dumped.Value.FirstScore);
        Assert.Equal(9.0, dumped.Value.SecondScore);
        Assert.Equal(0.0, dumped.Value.ThirdScore);
    }

    /// <summary>Before a passenger's first decision nothing is in force.</summary>
    [Fact]
    public void DumpBeforeTheFirstDecisionIsNull() => Assert.Null(ThreeDecisions().Dump(0, 9));

    /// <summary>On the tick of a decision, that decision is in force.</summary>
    [Fact]
    public void DumpOnTheExactTickReturnsThatRecord()
    {
        DecisionLog log = ThreeDecisions();

        Assert.Equal(Decision(970, LavVisit, Screen), log.Dump(0, 970));
        Assert.Equal(Decision(10, Idle, Idle), log.Dump(0, 10));
    }

    /// <summary>A decision recorded for one passenger shows in that passenger's list and dump alone.</summary>
    [Fact]
    public void EachPassengerKeepsItsOwnList()
    {
        var log = new DecisionLog(3);
        DecisionRecord first = Decision(5, Idle, Screen);
        DecisionRecord second = Decision(7, Idle, LavVisit);

        log.Record(0, first);
        log.Record(2, second);

        Assert.Equal([first], log.Of(0));
        Assert.Empty(log.Of(1));
        Assert.Equal([second], log.Of(2));
        Assert.Null(log.Dump(1, 100));
        Assert.Equal(second, log.Dump(2, 100));
    }

    /// <summary>A passenger id below 0 or past the last passenger is refused by every member, naming the passenger.</summary>
    [Fact]
    public void AnUnknownPassengerIsRefused()
    {
        var log = new DecisionLog(2);
        DecisionRecord record = Decision(5, Idle, Screen);

        foreach (int passenger in new[] { -1, 2 })
        {
            Assert.Equal("passenger", Assert.Throws<ArgumentOutOfRangeException>(() => log.Record(passenger, record)).ParamName);
            Assert.Equal("passenger", Assert.Throws<ArgumentOutOfRangeException>(() => log.Of(passenger)).ParamName);
            Assert.Equal("passenger", Assert.Throws<ArgumentOutOfRangeException>(() => log.Dump(passenger, 10)).ParamName);
        }
    }

    /// <summary>Passenger 0's decisions at ticks 10 (kept idle), 490 (to lav_visit) and 970 (to screen).</summary>
    private static DecisionLog ThreeDecisions()
    {
        var log = new DecisionLog(2);
        log.Record(0, Decision(10, Idle, Idle));
        log.Record(0, new DecisionRecord(490, Idle, LavVisit, LavVisit, 18.75, Idle, 9.0, Screen, 0.0));
        log.Record(0, Decision(970, LavVisit, Screen));
        return log;
    }

    private static DecisionRecord Decision(long tick, ActivityId current, ActivityId chosen) =>
        new(tick, current, chosen, chosen, 12.0, current, 5.0, Idle, 0.0);
}
