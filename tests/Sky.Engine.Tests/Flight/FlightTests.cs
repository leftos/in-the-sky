using Sky.Engine.Cabin;
using Sky.Engine.Flight;
using Sky.Engine.Journal;
using Sky.Engine.Manifest;
using Sky.Engine.Needs;
using Sky.Engine.Passengers;
using Sky.Engine.Ports;
using Sky.Engine.Tests.Cabin;
using Sky.Engine.Tests.Fakes;
using Sky.Engine.Tests.Passengers;

namespace Sky.Engine.Tests.Flight;

/// <summary>Proves the flight's tick loop: the tick count, the journal's input records, the stage machine and the needs.</summary>
public sealed class FlightTests
{
    private const double Tolerance = 1e-9;

    /// <summary>Ten sim minutes of ticks.</summary>
    private const long TenMinutes = 2400;

    /// <summary>The boarding start of an afternoon departure, 13:30 origin local, inside the post-lunch dip.</summary>
    private const int AfternoonBoardingStart = 810;

    private static readonly NodeCapacities Capacities = new()
    {
        AisleSlot = 1,
        Seat = 1,
        Door = 2,
        Lav = 1,
        LavQueue = 3,
        Galley = 2,
    };

    private static readonly NeedRates Rates = new(
        new NeedRateSettings
        {
            RefreshmentPerHour = 25,
            BladderPerHour = 20,
            RestRisePerHour = 5,
            RestFallPerHour = 10,
            BoredomPerHour = 15,
            UneaseHalfLifeMinutes = 15,
        }
    );

    private static readonly Dictionary<string, Func<FlightSetup, FlightSetup>> WithoutInput = new()
    {
        [nameof(FlightSetup.Layout)] = setup => setup with { Layout = null! },
        [nameof(FlightSetup.Capacities)] = setup => setup with { Capacities = null! },
        [nameof(FlightSetup.Manifest)] = setup => setup with { Manifest = null! },
        [nameof(FlightSetup.Feed)] = setup => setup with { Feed = null! },
        [nameof(FlightSetup.Scripts)] = setup => setup with { Scripts = null! },
        [nameof(FlightSetup.NeedRates)] = setup => setup with { NeedRates = null! },
        [nameof(FlightSetup.StartingNeeds)] = setup => setup with { StartingNeeds = null! },
        [nameof(FlightSetup.Conditions)] = setup => setup with { Conditions = null! },
        [nameof(FlightSetup.Traits)] = setup => setup with { Traits = null! },
    };

    /// <summary>Each step advances the tick by the count it was given.</summary>
    [Fact]
    public void StepAdvancesTheTickByTheCountGiven()
    {
        var flight = new FlightWorld(Setup(StartingNeedsTests.Manifest(2)));

        flight.Step(7);
        Assert.Equal(7, flight.Tick);

        flight.Step(5);
        Assert.Equal(12, flight.Tick);
    }

    /// <summary>Two steps journal two frame records, each with the tick it started on and its tick count.</summary>
    [Fact]
    public void StepJournalsOneFrameRecordPerCall()
    {
        var flight = new FlightWorld(Setup(StartingNeedsTests.Manifest(1)));

        flight.Step(3);
        flight.Step(4);

        Assert.Equal([new FrameRecord(0, 3), new FrameRecord(3, 4)], flight.Journal.OfType<FrameRecord>());
    }

    /// <summary>A feed that moves to boarding at tick 10 is journalled once at tick 10, and moves the stage machine.</summary>
    [Fact]
    public void AChangedFeedObservationIsJournalledOnce()
    {
        var flight = new FlightWorld(Setup(StartingNeedsTests.Manifest(1)) with { Feed = new StageFeed(10) });

        flight.Step(20);

        FeedObservationRecord[] observations = [.. flight.Journal.OfType<FeedObservationRecord>()];
        Assert.Equal([0L, 10L], observations.Select(record => record.Tick));
        Assert.Equal(FlightStage.Boarding, observations[1].Observation.Stage);
        Assert.Equal(FlightStage.Boarding, flight.Stages.Current);
    }

    /// <summary>With equal needs and a steady Unease push, a late and fed up passenger's Unease rises ×1.1 the other's.</summary>
    [Fact]
    public void LateAndFedUpAmplifiesUneaseRise()
    {
        PassengerManifest manifest = StartingNeedsTests.Manifest(1);
        FlightWorld late = Boarded(Setup(manifest) with { Conditions = StartingNeedsTests.Conditions(30, false) });
        FlightWorld calm = Boarded(Setup(manifest) with { Conditions = StartingNeedsTests.Conditions(0, true) });
        Assert.True(late.Passengers[0].LateAndFedUp);
        Assert.False(calm.Passengers[0].LateAndFedUp);
        late.Passengers[0].Needs.Set(Need.Unease, StartingNeedsTests.DefaultBaseline);
        calm.Passengers[0].Needs.Set(Need.Unease, StartingNeedsTests.DefaultBaseline);

        late.Step(TenMinutes);
        calm.Step(TenMinutes);

        double lateRise = late.Passengers[0].Needs[Need.Unease] - StartingNeedsTests.DefaultBaseline;
        double calmRise = calm.Passengers[0].Needs[Need.Unease] - StartingNeedsTests.DefaultBaseline;
        Assert.True(calmRise > 1.0, $"The Unease push barely moved Unease: {calmRise}.");
        Assert.Equal(1.1 * calmRise, lateRise, Tolerance);
    }

    /// <summary>A trait with a Refreshment factor of 1.5 makes its passenger's Refreshment rise 1.5 times another's.</summary>
    [Fact]
    public void TraitMultipliersApplyEachTick()
    {
        FlightWorld flight = Boarded(Setup(StartingNeedsTests.Singles([StartingNeedsTests.Hungry], [])));
        double hungryBefore = flight.Passengers[0].Needs[Need.Refreshment];
        double otherBefore = flight.Passengers[1].Needs[Need.Refreshment];

        flight.Step(TenMinutes);

        double hungryRise = flight.Passengers[0].Needs[Need.Refreshment] - hungryBefore;
        double otherRise = flight.Passengers[1].Needs[Need.Refreshment] - otherBefore;
        Assert.True(otherRise > 1.0, $"Refreshment barely rose: {otherRise}.");
        Assert.Equal(1.5 * otherRise, hungryRise, Tolerance);
    }

    /// <summary>Rest rises 1.5 times as fast inside the 13:00 to 16:00 band as in the morning.</summary>
    [Fact]
    public void RestRisesFasterInTheAfternoonBand()
    {
        PassengerManifest manifest = StartingNeedsTests.Manifest(1);
        FlightWorld morning = Boarded(Setup(manifest));
        BoardingConditions afternoonStart = StartingNeedsTests.Conditions(0, true) with { BoardingStartMinute = AfternoonBoardingStart };
        FlightWorld afternoon = Boarded(Setup(manifest) with { Conditions = afternoonStart });
        double morningBefore = morning.Passengers[0].Needs[Need.Rest];
        double afternoonBefore = afternoon.Passengers[0].Needs[Need.Rest];

        morning.Step(TenMinutes);
        afternoon.Step(TenMinutes);

        double morningRise = morning.Passengers[0].Needs[Need.Rest] - morningBefore;
        double afternoonRise = afternoon.Passengers[0].Needs[Need.Rest] - afternoonBefore;
        Assert.True(morningRise > 0.5, $"Rest barely rose: {morningRise}.");
        Assert.Equal(1.5 * morningRise, afternoonRise, Tolerance);
    }

    /// <summary>A passenger who has not boarded keeps their starting needs while the flight ticks.</summary>
    [Fact]
    public void NoPassengerNeedsTickBeforeBoarding()
    {
        var flight = new FlightWorld(Setup(StartingNeedsTests.Manifest(2)));
        double[] before = NeedValues(flight.Passengers[1]);

        flight.Step(TenMinutes);

        Assert.False(flight.Passengers[1].IsBoarded);
        Assert.Equal(before, NeedValues(flight.Passengers[1]));
    }

    /// <summary>Crew take the executor's first character ids, so every passenger's character id comes after the crew's.</summary>
    [Fact]
    public void PassengerCharacterIdsFollowTheCrew()
    {
        var flight = new FlightWorld(Setup(StartingNeedsTests.Manifest(3)) with { CrewCount = 6 });

        Assert.Equal(6, flight.Passengers[0].CharacterId);
        Assert.All(flight.Passengers, passenger => Assert.True(passenger.CharacterId >= 6, $"Character id {passenger.CharacterId} is a crew id."));
    }

    /// <summary>A setup missing a required input is refused, naming the input.</summary>
    /// <param name="input">The input left null.</param>
    [Theory]
    [InlineData(nameof(FlightSetup.Layout))]
    [InlineData(nameof(FlightSetup.Capacities))]
    [InlineData(nameof(FlightSetup.Manifest))]
    [InlineData(nameof(FlightSetup.Feed))]
    [InlineData(nameof(FlightSetup.Scripts))]
    [InlineData(nameof(FlightSetup.NeedRates))]
    [InlineData(nameof(FlightSetup.StartingNeeds))]
    [InlineData(nameof(FlightSetup.Conditions))]
    [InlineData(nameof(FlightSetup.Traits))]
    public void ConstructionRefusesAMissingInput(string input)
    {
        FlightSetup setup = WithoutInput[input](Setup(StartingNeedsTests.Manifest(1)));

        ArgumentNullException error = Assert.Throws<ArgumentNullException>(() => new FlightWorld(setup));

        Assert.Equal(input, error.ParamName);
    }

    /// <summary>An aboard Unease push that is negative, NaN or infinite is refused, naming the input.</summary>
    /// <param name="push">The bad push per hour.</param>
    [Theory]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void ConstructionRefusesABadAboardPush(double push)
    {
        FlightSetup setup = Setup(StartingNeedsTests.Manifest(1)) with { AboardUneasePushPerHour = push };

        ArgumentOutOfRangeException error = Assert.Throws<ArgumentOutOfRangeException>(() => new FlightWorld(setup));

        Assert.Equal(nameof(FlightSetup.AboardUneasePushPerHour), error.ParamName);
    }

    private static FlightSetup Setup(PassengerManifest manifest) =>
        new()
        {
            Layout = NavGraphBuilderTests.TwoRowLayout(),
            InchesPerTick = 10,
            Capacities = Capacities,
            Manifest = manifest,
            CrewCount = 2,
            Seed = 9,
            Feed = new StageFeed(long.MaxValue),
            Scripts = new FakeBehaviorScripts(),
            NeedRates = Rates,
            AboardUneasePushPerHour = 30,
            StartingNeeds = StartingNeedsTests.Rules(),
            Conditions = StartingNeedsTests.Conditions(0, true),
            Traits = StartingNeedsTests.Traits(),
        };

    /// <summary>Builds a flight and seats every passenger in their booked seat, as boarding would.</summary>
    private static FlightWorld Boarded(FlightSetup setup)
    {
        var flight = new FlightWorld(setup);
        foreach (Passenger passenger in flight.Passengers)
        {
            passenger.Node = passenger.Manifest.SeatNode;
        }

        return flight;
    }

    private static double[] NeedValues(Passenger passenger) => [.. Enum.GetValues<Need>().Select(need => passenger.Needs[need])];

    /// <summary>A feed in pre-boarding until <paramref name="boardingTick"/>, and boarding from then on.</summary>
    private sealed class StageFeed(long boardingTick) : ISimFeed
    {
        public FeedObservation Observe(long tick) =>
            new(tick < boardingTick ? FlightStage.PreBoarding : FlightStage.Boarding, false, Turbulence.None);
    }
}
