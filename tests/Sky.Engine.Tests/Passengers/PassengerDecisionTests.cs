using Sky.Engine.Cabin;
using Sky.Engine.Flight;
using Sky.Engine.Needs;
using Sky.Engine.Passengers;
using Sky.Engine.Ports;
using Sky.Engine.Tests.Fakes;
using Sky.Engine.Tests.Flight;

namespace Sky.Engine.Tests.Passengers;

/// <summary>
/// Proves the passenger decision core on flights with real boarding: who decides and when, how the keep-current bias and the
/// tie rules choose, that every decision is logged, and that bad decision rules are refused.
/// </summary>
public sealed class PassengerDecisionTests
{
    private const ulong Seed = 9;

    /// <summary>The ticks between two decisions of one passenger in <see cref="TestFlights.Decisions"/>.</summary>
    private const int Cadence = 480;

    private static readonly ActivityId Idle = TestFlights.Idle;

    private static readonly ActivityId LavVisit = TestFlights.LavVisit;

    private static readonly ActivityId Screen = TestFlights.Screen;

    /// <summary>
    /// With idle scoring 5 and lav_visit by balance.md section 3.9, a seated passenger at Bladder 60 decides on lav_visit at
    /// their first cadence tick after sitting down, and it becomes their current activity.
    /// </summary>
    [Fact]
    public void BladderAboveThresholdChoosesLavVisitWithinTheCadence()
    {
        FlightWorld flight = SoloFlight(BladderScorer, TestFlights.Decisions());
        Passenger passenger = flight.Passengers[0];
        passenger.Needs.Set(Need.Bladder, 60);

        TestFlights.RunUntil(flight, world => world.Movement.StateOf(passenger.CharacterId) == MoverState.Seated);
        long seatedTick = flight.Tick - 1;
        TestFlights.RunUntil(flight, world => world.Tick >= seatedTick + Cadence);

        DecisionRecord decision = Assert.Single(flight.Decisions.Of(0), record => record.Tick >= seatedTick && record.Tick < seatedTick + Cadence);
        Assert.Equal(LavVisit, decision.First);
        Assert.Equal(LavVisit, decision.Chosen);
        Assert.Equal(LavVisit, passenger.CurrentActivity);
    }

    /// <summary>Over a whole flight, every passenger decides only on ticks congruent to their id modulo the cadence.</summary>
    [Fact]
    public void APassengerDecidesOnlyOnItsCadenceTick()
    {
        var flight = new FlightWorld(TestFlights.Setup(TestFlights.FullManifest(), Seed));

        TestFlights.RunUntil(flight, world => world.AllOffTick is not null);

        Assert.All(
            flight.Passengers,
            passenger =>
            {
                Assert.NotEmpty(flight.Decisions.Of(passenger.Id));
                Assert.All(flight.Decisions.Of(passenger.Id), record => Assert.Equal(passenger.Id % Cadence, record.Tick % Cadence));
            }
        );
    }

    /// <summary>Idle at 5 plus the bias of 4 holds against screen at 8, a lead of 3.</summary>
    [Fact]
    public void KeepCurrentBiasHoldsAgainstASmallerLead()
    {
        FlightWorld flight = SoloFlight(Scores(5, 0, 8), TestFlights.Decisions());

        DecisionRecord decision = FirstDecision(flight);

        Assert.Equal(Idle, decision.Chosen);
        Assert.Equal((Idle, 9.0), (decision.First, decision.FirstScore));
        Assert.Equal((Screen, 8.0), (decision.Second, decision.SecondScore));
        Assert.Equal(Idle, flight.Passengers[0].CurrentActivity);
    }

    /// <summary>Idle at 5 plus the bias of 4 yields to screen at 10, a lead of 5.</summary>
    [Fact]
    public void KeepCurrentBiasYieldsToALargerLead()
    {
        FlightWorld flight = SoloFlight(Scores(5, 0, 10), TestFlights.Decisions());

        DecisionRecord decision = FirstDecision(flight);

        Assert.Equal(Screen, decision.Chosen);
        Assert.Equal((Screen, 10.0), (decision.First, decision.FirstScore));
        Assert.Equal((Idle, 9.0), (decision.Second, decision.SecondScore));
        Assert.Equal(Screen, flight.Passengers[0].CurrentActivity);
    }

    /// <summary>Lav_visit and screen tied at 7, both above the current idle, go to lav_visit, the lower id, though screen is scored first.</summary>
    [Fact]
    public void ATieBetweenTwoOthersGoesToTheLowerId()
    {
        DecisionRules screenFirst = TestFlights.Decisions() with { Candidates = [Screen, LavVisit, Idle] };
        FlightWorld flight = SoloFlight(Scores(1, 7, 7), screenFirst);

        DecisionRecord decision = FirstDecision(flight);

        Assert.Equal(LavVisit, decision.Chosen);
        Assert.Equal([LavVisit, Screen, Idle], [decision.First, decision.Second, decision.Third]);
        Assert.Equal([7.0, 7.0, 5.0], [decision.FirstScore, decision.SecondScore, decision.ThirdScore]);
    }

    /// <summary>A current activity scoring 0 gets no bias, so screen at 1 takes the decision from idle at 0.</summary>
    [Fact]
    public void ACurrentActivityScoringZeroGetsNoBias()
    {
        FlightWorld flight = SoloFlight(Scores(0, 0, 1), TestFlights.Decisions());

        DecisionRecord decision = FirstDecision(flight);

        Assert.Equal(Screen, decision.Chosen);
        Assert.Equal(0.0, decision.SecondScore);
        Assert.Equal(Screen, flight.Passengers[0].CurrentActivity);
    }

    /// <summary>When every candidate scores 0 the passenger keeps the activity they are on, here screen, not the lowest id.</summary>
    [Fact]
    public void AllScoresZeroKeepsTheCurrentActivity()
    {
        FlightWorld flight = SoloFlight(Scores(0, 0, 0), TestFlights.Decisions() with { Initial = Screen });

        DecisionRecord decision = FirstDecision(flight);

        Assert.Equal(Screen, decision.Current);
        Assert.Equal(Screen, decision.First);
        Assert.Equal(Screen, decision.Chosen);
        Assert.Equal(Screen, flight.Passengers[0].CurrentActivity);
    }

    /// <summary>
    /// With a decision due every tick, no passenger decides on a tick it ends in any mover state but standing or seated across a
    /// whole boarding run (prior art: OpenPax releases a bladder only for a seated passenger), while every passenger still decides.
    /// </summary>
    [Fact]
    public void APassengerWalkingToTheirSeatTakesNoDecision()
    {
        DecisionRules everyTick = TestFlights.Decisions() with { CadenceTicks = 1 };
        var flight = new FlightWorld(TestFlights.Setup(TestFlights.FullManifest(), Seed) with { Decisions = everyTick });
        int walkingTicks = 0;

        while (flight.AllSeatedTick is null)
        {
            Assert.True(flight.Tick < TestFlights.TickLimit, "Boarding never finished.");
            flight.Step(1);
            walkingTicks += AssertNoDecisionOnTheMove(flight, flight.Tick - 1);
        }

        Assert.True(walkingTicks > 0, "No passenger was ever walking at the end of a tick.");
        Assert.All(flight.Passengers, passenger => Assert.NotEmpty(flight.Decisions.Of(passenger.Id)));
    }

    /// <summary>After deboarding, no passenger has a decision on or after the tick they went off, a full cadence later.</summary>
    [Fact]
    public void AnOffPassengerTakesNoDecision()
    {
        var flight = new FlightWorld(TestFlights.Setup(TestFlights.FullManifest(), Seed));
        long[] offTicks = new long[flight.Passengers.Count];
        Array.Fill(offTicks, -1);

        while (flight.AllOffTick is null || flight.Tick <= flight.AllOffTick + Cadence)
        {
            Assert.True(flight.Tick < TestFlights.TickLimit, "The flight never emptied.");
            flight.Step(1);
            RecordOffTicks(flight, offTicks, flight.Tick - 1);
        }

        Assert.All(
            flight.Passengers,
            passenger =>
            {
                long offTick = offTicks[passenger.Id];
                Assert.True(offTick >= 0, $"Passenger {passenger.Id} never went off.");
                Assert.NotEmpty(flight.Decisions.Of(passenger.Id));
                Assert.All(
                    flight.Decisions.Of(passenger.Id),
                    record => Assert.True(record.Tick < offTick, $"A decision at {record.Tick}, off at {offTick}.")
                );
            }
        );
    }

    /// <summary>Two flights on the same seed and manifest, scored from their needs, keep identical decision logs.</summary>
    [Fact]
    public void TheSameSeedGivesTheSameDecisionLog()
    {
        FlightWorld first = NeedScoredFlight();
        FlightWorld second = NeedScoredFlight();

        TestFlights.RunUntil(first, world => world.AllOffTick is not null);
        TestFlights.RunUntil(second, world => world.Tick >= first.Tick);

        Assert.Contains(first.Passengers, passenger => first.Decisions.Of(passenger.Id).Any(record => record.Chosen == Screen));
        Assert.All(first.Passengers, passenger => Assert.Equal(first.Decisions.Of(passenger.Id), second.Decisions.Of(passenger.Id)));
    }

    /// <summary>
    /// A cadence under 1 tick, a negative bias, no candidates, a candidate listed twice or an initial activity outside them is
    /// refused, naming the field.
    /// </summary>
    /// <param name="bad">The bad rule.</param>
    /// <param name="field">The field the refusal names.</param>
    [Theory]
    [InlineData("CadenceZero", nameof(DecisionRules.CadenceTicks))]
    [InlineData("BiasNegative", nameof(DecisionRules.KeepCurrentBias))]
    [InlineData("NoCandidates", nameof(DecisionRules.Candidates))]
    [InlineData("RepeatedCandidate", nameof(DecisionRules.Candidates))]
    [InlineData("InitialNotACandidate", nameof(DecisionRules.Initial))]
    public void InvalidDecisionRulesAreRefused(string bad, string field)
    {
        DecisionRules rules = bad switch
        {
            "CadenceZero" => TestFlights.Decisions() with { CadenceTicks = 0 },
            "BiasNegative" => TestFlights.Decisions() with { KeepCurrentBias = -1 },
            "NoCandidates" => TestFlights.Decisions() with { Candidates = [] },
            "RepeatedCandidate" => TestFlights.Decisions() with { Candidates = [Idle, LavVisit, Screen, LavVisit] },
            "InitialNotACandidate" => TestFlights.Decisions() with { Initial = new ActivityId(7) },
            _ => throw new ArgumentOutOfRangeException(nameof(bad), bad, "Not a bad decision rule."),
        };
        FlightSetup setup = TestFlights.Setup(TestFlights.FullManifest(), Seed) with { Decisions = rules };

        ArgumentException error = Assert.ThrowsAny<ArgumentException>(() => new FlightWorld(setup));

        Assert.Equal(field, error.ParamName);
    }

    /// <summary>A score the behaviour port writes that is NaN, infinite or negative stops the flight, naming passenger and activity.</summary>
    /// <param name="score">The bad score, given to screen.</param>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-1.0)]
    public void ANonFiniteOrNegativeScoreIsRefused(double score)
    {
        FlightWorld flight = SoloFlight(Scores(5, 0, score), TestFlights.Decisions());

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => FirstDecision(flight));

        Assert.Contains("Passenger 0's", error.Message, StringComparison.Ordinal);
        Assert.Contains($"activity {Screen.Value} ", error.Message, StringComparison.Ordinal);
        Assert.Empty(flight.Decisions.Of(0));
    }

    /// <summary>Idle at 5, lav_visit 0 below Bladder 55 and 10 + 1.75 per point above, screen 0 (balance.md section 3.9).</summary>
    private static double BladderScorer(PassengerFacts facts, ActivityId activity)
    {
        double bladder = facts.Needs[Need.Bladder];
        if (activity == LavVisit)
        {
            return bladder < 55 ? 0.0 : 10 + (1.75 * (bladder - 55));
        }

        return activity == Idle ? 5.0 : 0.0;
    }

    /// <summary>Idle at 5, lav_visit by Bladder and screen by Boredom, both as balance.md section 3.9 scores them.</summary>
    private static double NeedScorer(PassengerFacts facts, ActivityId activity)
    {
        double boredom = facts.Needs[Need.Boredom];
        if (activity == Screen)
        {
            return boredom < 20 ? 0.0 : Math.Min(40, 12 + (0.5 * (boredom - 20)));
        }

        return BladderScorer(facts, activity);
    }

    /// <summary>A scorer giving each candidate a fixed score.</summary>
    private static Func<PassengerFacts, ActivityId, double> Scores(double idle, double lavVisit, double screen) =>
        (_, activity) =>
            activity == Idle ? idle
            : activity == LavVisit ? lavVisit
            : screen;

    /// <summary>
    /// A one-passenger flight, its passenger in row 0's window seat, scored by <paramref name="scorer"/> under <paramref name="rules"/>.
    /// </summary>
    private static FlightWorld SoloFlight(Func<PassengerFacts, ActivityId, double> scorer, DecisionRules rules)
    {
        NavGraph graph = TestFlights.Graph();
        FlightSetup setup = TestFlights.Setup(
            TestFlights.ManifestOf(
                graph,
                [
                    [graph.SeatNodes[0]],
                ]
            ),
            Seed
        ) with
        {
            Scripts = new FakeBehaviorScripts { Scorer = scorer },
            Decisions = rules,
        };
        return new FlightWorld(setup);
    }

    private static FlightWorld NeedScoredFlight() =>
        new(TestFlights.Setup(TestFlights.FullManifest(), Seed) with { Scripts = new FakeBehaviorScripts { Scorer = NeedScorer } });

    /// <summary>Runs a solo flight to its passenger's first decision and returns it.</summary>
    private static DecisionRecord FirstDecision(FlightWorld flight)
    {
        TestFlights.RunUntil(flight, world => world.Decisions.Of(0).Count > 0);
        return flight.Decisions.Of(0)[0];
    }

    /// <summary>Asserts no passenger in a mover state other than standing or seated decided on the tick; returns how many were walking.</summary>
    private static int AssertNoDecisionOnTheMove(FlightWorld flight, long tick)
    {
        int walking = 0;
        foreach (Passenger passenger in flight.Passengers)
        {
            MoverState state = flight.Movement.StateOf(passenger.CharacterId);
            if (state is not (MoverState.Standing or MoverState.Seated))
            {
                walking += state == MoverState.Walking ? 1 : 0;
                IReadOnlyList<DecisionRecord> records = flight.Decisions.Of(passenger.Id);
                Assert.False(records.Count > 0 && records[^1].Tick == tick, $"Passenger {passenger.Id} decided at tick {tick} while {state}.");
            }
        }

        return walking;
    }

    private static void RecordOffTicks(FlightWorld flight, long[] offTicks, long tick)
    {
        foreach (Passenger passenger in flight.Passengers)
        {
            if (passenger.IsOff && offTicks[passenger.Id] < 0)
            {
                offTicks[passenger.Id] = tick;
            }
        }
    }
}
