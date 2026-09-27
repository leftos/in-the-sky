using Sky.Engine.Flight;
using Sky.Engine.Ports;

namespace Sky.Engine.Tests.Flight;

/// <summary>Proves the stage machine checks its handler map at construction and starts every stage the feed crosses, in order.</summary>
public sealed class StageMachineTests
{
    private static readonly FlightStage[] Stages = Enum.GetValues<FlightStage>();

    /// <summary>A feed that jumps from taxi-out to cruise runs takeoff, climb and cruise in that order on the one tick.</summary>
    [Fact]
    public void JumpFromTaxiOutToCruiseStartsTakeoffClimbAndCruiseInOrderInOneTick()
    {
        (StageMachine machine, List<Started> records) = BuildMachine();

        machine.Advance(At(FlightStage.TaxiOut), 10);
        records.Clear();
        machine.Advance(At(FlightStage.Cruise), 11);

        Assert.Equal([new Started(FlightStage.Takeoff, 11), new Started(FlightStage.Climb, 11), new Started(FlightStage.Cruise, 11)], records);
    }

    /// <summary>The first advance enters pre-boarding before the stage the feed reports.</summary>
    [Fact]
    public void FirstAdvanceEntersEveryStageFromPreBoarding()
    {
        (StageMachine machine, List<Started> records) = BuildMachine();

        machine.Advance(At(FlightStage.Boarding), 0);

        Assert.Equal([new Started(FlightStage.PreBoarding, 0), new Started(FlightStage.Boarding, 0)], records);
    }

    /// <summary>A flight walked through every stage starts each stage once, in this order; repeating the last stage starts nothing.</summary>
    [Fact]
    public void FullFlightEntersEachStageExactlyOnce()
    {
        (StageMachine machine, List<Started> records) = BuildMachine();

        FlightStage[] order =
        [
            FlightStage.PreBoarding,
            FlightStage.Boarding,
            FlightStage.TaxiOut,
            FlightStage.Takeoff,
            FlightStage.Climb,
            FlightStage.Cruise,
            FlightStage.Descent,
            FlightStage.Landing,
            FlightStage.TaxiIn,
            FlightStage.Deboarding,
            FlightStage.Done,
        ];

        List<Started> expected = [];
        long tick = 0;
        foreach (FlightStage stage in order)
        {
            expected.Add(new Started(stage, tick));
            machine.Advance(At(stage), tick++);
        }

        machine.Advance(At(FlightStage.Done), tick);

        Assert.Equal(expected, records);
    }

    /// <summary>The stages' values are 0 to 10 in flight order, so a stage's value is its place in the order.</summary>
    [Fact]
    public void StageValuesAreContiguousFromZero()
    {
        int[] values = [.. Enum.GetValues<FlightStage>().Select(stage => (int)stage)];

        Assert.Equal([0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10], values);
    }

    /// <summary>A handler map missing a stage is refused at construction, naming the argument and the stage.</summary>
    [Fact]
    public void MissingHandlerFailsAtConstructionNamingTheStage()
    {
        Dictionary<FlightStage, IStageHandler> handlers = FullHandlerMap();
        handlers.Remove(FlightStage.Descent);

        ArgumentException thrown = Assert.Throws<ArgumentException>(() => new StageMachine(handlers));

        Assert.Equal("handlers", thrown.ParamName);
        Assert.Contains("Descent", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>A null handler is refused at construction, naming the argument and the stage.</summary>
    [Fact]
    public void NullHandlerFailsAtConstructionNamingTheStage()
    {
        Dictionary<FlightStage, IStageHandler> handlers = FullHandlerMap();
        handlers[FlightStage.Landing] = null!;

        ArgumentException thrown = Assert.Throws<ArgumentException>(() => new StageMachine(handlers));

        Assert.Equal("handlers", thrown.ParamName);
        Assert.Contains("Landing", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>A stage behind the current one starts nothing and leaves the stage alone; the next one after it still starts.</summary>
    [Fact]
    public void EarlierStageIsIgnored()
    {
        (StageMachine machine, List<Started> records) = BuildMachine();

        machine.Advance(At(FlightStage.Cruise), 0);
        records.Clear();
        machine.Advance(At(FlightStage.Climb), 1);

        Assert.Empty(records);
        Assert.Equal(FlightStage.Cruise, machine.Current);

        machine.Advance(At(FlightStage.Descent), 2);

        Assert.Equal([new Started(FlightStage.Descent, 2)], records);
        Assert.Equal(FlightStage.Descent, machine.Current);
    }

    /// <summary>A stage value outside the enum is refused before any handler runs, leaving the stage where it was.</summary>
    [Fact]
    public void UndefinedStageIsRefusedBeforeAnyHandlerRuns()
    {
        (StageMachine machine, List<Started> records) = BuildMachine();

        machine.Advance(At(FlightStage.Cruise), 0);
        records.Clear();

        ArgumentOutOfRangeException above = Assert.Throws<ArgumentOutOfRangeException>(() => machine.Advance(At((FlightStage)42), 1));
        ArgumentOutOfRangeException below = Assert.Throws<ArgumentOutOfRangeException>(() => machine.Advance(At((FlightStage)(-1)), 2));

        Assert.Equal("observation", above.ParamName);
        Assert.Equal("observation", below.ParamName);
        Assert.Empty(records);
        Assert.Equal(FlightStage.Cruise, machine.Current);
    }

    /// <summary>A handler that advances the machine takes the remaining steps; no stage starts twice and the stage never goes back.</summary>
    [Fact]
    public void ReentrantAdvanceFromStartNeitherRegressesNorDoubleStarts()
    {
        List<Started> records = [];
        StageMachine? machine = null;
        Dictionary<FlightStage, IStageHandler> handlers = FullHandlerMap(records);
        handlers[FlightStage.Takeoff] = new RecordingHandler(FlightStage.Takeoff, records, tick => machine!.Advance(At(FlightStage.Landing), tick));

        machine = new StageMachine(handlers);
        machine.Advance(At(FlightStage.TaxiOut), 10);
        records.Clear();
        machine.Advance(At(FlightStage.Cruise), 11);

        Assert.Equal(
            [
                new Started(FlightStage.Takeoff, 11),
                new Started(FlightStage.Climb, 11),
                new Started(FlightStage.Cruise, 11),
                new Started(FlightStage.Descent, 11),
                new Started(FlightStage.Landing, 11),
            ],
            records
        );
        Assert.Equal(FlightStage.Landing, machine.Current);
    }

    /// <summary>A throwing handler leaves its stage entered; the next advance continues from the following stage and does not retry it.</summary>
    [Fact]
    public void ThrowingHandlerLeavesItsStageEnteredAndIsNotRetried()
    {
        List<Started> records = [];
        Dictionary<FlightStage, IStageHandler> handlers = FullHandlerMap(records);
        handlers[FlightStage.Takeoff] = new RecordingHandler(
            FlightStage.Takeoff,
            records,
            _ => throw new InvalidOperationException("The takeoff handler failed.")
        );
        StageMachine machine = new(handlers);

        machine.Advance(At(FlightStage.TaxiOut), 10);
        Assert.Throws<InvalidOperationException>(() => machine.Advance(At(FlightStage.Cruise), 11));
        Assert.Equal(FlightStage.Takeoff, machine.Current);

        records.Clear();
        machine.Advance(At(FlightStage.Cruise), 12);

        Assert.Equal([new Started(FlightStage.Climb, 12), new Started(FlightStage.Cruise, 12)], records);
    }

    /// <summary>A feed reporting the current stage again starts nothing.</summary>
    [Fact]
    public void SameStageStartsNothing()
    {
        (StageMachine machine, List<Started> records) = BuildMachine();

        machine.Advance(At(FlightStage.Cruise), 0);
        List<Started> entered = [.. records];
        machine.Advance(At(FlightStage.Cruise), 5);

        Assert.Equal(entered, records);
        Assert.Single(records, record => record.Stage == FlightStage.Cruise);
    }

    /// <summary>The machine has no current stage until the first advance.</summary>
    [Fact]
    public void CurrentIsNullBeforeTheFirstAdvance()
    {
        (StageMachine machine, _) = BuildMachine();

        Assert.Null(machine.Current);
    }

    private static (StageMachine Machine, List<Started> Records) BuildMachine()
    {
        List<Started> records = [];
        return (new StageMachine(FullHandlerMap(records)), records);
    }

    private static Dictionary<FlightStage, IStageHandler> FullHandlerMap() => FullHandlerMap([]);

    private static Dictionary<FlightStage, IStageHandler> FullHandlerMap(List<Started> records)
    {
        Dictionary<FlightStage, IStageHandler> handlers = [];
        foreach (FlightStage stage in Stages)
        {
            handlers[stage] = new RecordingHandler(stage, records);
        }

        return handlers;
    }

    private static FeedObservation At(FlightStage stage) => new(stage, SeatbeltSignOn: false, Turbulence: Turbulence.None);

    private readonly record struct Started(FlightStage Stage, long Tick);

    private sealed class RecordingHandler(FlightStage stage, List<Started> records, Action<long>? onStart = null) : IStageHandler
    {
        public void Start(long tick)
        {
            records.Add(new Started(stage, tick));
            onStart?.Invoke(tick);
        }
    }
}
