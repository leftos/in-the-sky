using Sky.Engine.Cabin;
using Sky.Engine.Crew;
using Sky.Engine.Execution;
using Sky.Engine.Journal;
using Sky.Engine.Manifest;
using Sky.Engine.Needs;
using Sky.Engine.Passengers;
using Sky.Engine.Ports;
using Sky.Engine.Randomness;
using Sky.Engine.Time;

namespace Sky.Engine.Flight;

/// <summary>Everything a flight is built from. <see cref="FlightWorld"/> refuses a null reference input and a bad push.</summary>
public sealed record FlightSetup
{
    /// <summary>Gets the cabin the flight runs in.</summary>
    public required CabinLayout Layout { get; init; }

    /// <summary>Gets the inches a character walks in one tick, for the nav graph's edge ticks.</summary>
    public required double InchesPerTick { get; init; }

    /// <summary>Gets how many occupants each kind of node holds.</summary>
    public required NodeCapacities Capacities { get; init; }

    /// <summary>Gets the passengers and their bookings.</summary>
    public required PassengerManifest Manifest { get; init; }

    /// <summary>Gets how many crew members claim from the task board.</summary>
    public required int CrewCount { get; init; }

    /// <summary>Gets the run's root seed.</summary>
    public required ulong Seed { get; init; }

    /// <summary>Gets the simulator feed the flight reads once a tick.</summary>
    public required ISimFeed Feed { get; init; }

    /// <summary>Gets the behaviour and event scripts.</summary>
    public required IBehaviorScripts Scripts { get; init; }

    /// <summary>Gets the flight's per-tick need rates.</summary>
    public required NeedRates NeedRates { get; init; }

    /// <summary>Gets the Unease push, per sim hour, of being aboard, on every boarded passenger (passengers.md section 2).</summary>
    public required double AboardUneasePushPerHour { get; init; }

    /// <summary>Gets the starting-needs and body-clock numbers.</summary>
    public required StartingNeedsRules StartingNeeds { get; init; }

    /// <summary>Gets the gate conditions and the boarding start, the time of day at tick 0.</summary>
    public required BoardingConditions Conditions { get; init; }

    /// <summary>Gets the effect of every trait the passengers carry.</summary>
    public required TraitEffects Traits { get; init; }

    /// <summary>Gets the boarding and deboarding movement numbers.</summary>
    public required MovementRules Movement { get; init; }

    /// <summary>Gets the passengers' decision cadence, keep-current bias, candidate activities and initial activity.</summary>
    public required DecisionRules Decisions { get; init; }
}

/// <summary>
/// The flight world: the cabin, its passengers, the executor, the task board and the stage machine, run one tick at a time.
/// Each tick reads the feed and hands a changed observation to the stage machine, lets the boarding flow admit and stand
/// passengers, ticks the executor, records <see cref="AllSeatedTick"/> and <see cref="AllOffTick"/>, ticks every boarded
/// passenger's needs, lets every passenger due a decision choose an activity, then advances <see cref="Tick"/>. The journal's
/// input records are appended as they happen.
/// </summary>
public sealed class FlightWorld
{
    /// <summary>The Context-class factor on Unease while a passenger is late and fed up (passengers.md section 2).</summary>
    public const double LateAndFedUpUneaseFactor = 1.1;

    private readonly List<JournalInputRecord> journal = [];

    private readonly SimRandom[] passengerStreams;

    private readonly double[][] traitFactors;

    private readonly NeedRates needRates;

    private readonly BodyClockRules bodyClock;

    private readonly int boardingStartMinute;

    private readonly double aboardUneasePushPerHour;

    private readonly BoardingFlow boardingFlow;

    private readonly DecisionRules decisionRules;

    /// <summary>The scores of the decision being taken, one per candidate; the world owns it so a decision allocates nothing.</summary>
    private readonly double[] decisionScores;

    private FeedObservation? lastObservation;

    /// <summary>Builds the flight: the nav graph, path table and occupancy from the layout, and each passenger with their starting needs.</summary>
    /// <param name="setup">Everything the flight is built from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="setup"/> or one of its reference inputs is null, naming the input.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The crew count is negative, the aboard Unease push is negative, NaN or
    /// infinite, the decision cadence is under 1 tick, or the keep-current bias is negative, NaN or infinite, naming the input
    /// or field.</exception>
    /// <exception cref="ArgumentException">The layout has no door, or the decision candidates are empty or list an activity twice, or
    /// the initial activity is not a candidate, naming the field.</exception>
    public FlightWorld(FlightSetup setup)
    {
        RequireInputs(setup);
        decisionRules = setup.Decisions;
        decisionScores = new double[decisionRules.Candidates.Length];
        CrewCount = setup.CrewCount;
        Layout = setup.Layout;
        Graph = NavGraphBuilder.Build(setup.Layout, setup.InchesPerTick);
        Paths = PathTable.Build(Graph);
        Occupancy = new Occupancy(Graph, setup.Capacities);
        Feed = setup.Feed;
        Scripts = setup.Scripts;
        Rng = new RngRoot(setup.Seed);
        needRates = setup.NeedRates;
        bodyClock = setup.StartingNeeds.BodyClock;
        boardingStartMinute = setup.Conditions.BoardingStartMinute;
        aboardUneasePushPerHour = setup.AboardUneasePushPerHour;

        IReadOnlyList<ManifestPassenger> manifest = setup.Manifest.Passengers;
        passengerStreams = [.. manifest.Select(passenger => Rng.Stream($"passenger/{passenger.Id}"))];
        IReadOnlyList<PassengerStart> starts = StartingNeeds.Draw(
            setup.Manifest,
            setup.Conditions,
            setup.StartingNeeds,
            setup.Traits,
            passengerStreams
        );
        Passengers = [.. manifest.Select(passenger => BuildPassenger(passenger, starts[passenger.Id], setup.CrewCount, decisionRules.Initial))];
        Decisions = new DecisionLog(Passengers.Count);
        traitFactors = [.. manifest.Select(passenger => TraitFactors(passenger, setup.Traits))];

        Executor = new SequenceExecutor(setup.CrewCount + Passengers.Count);
        Board = new TaskBoard(Executor, setup.CrewCount);
        Movement = new Movement(this, setup.Movement, setup.CrewCount + Passengers.Count);
        boardingFlow = new BoardingFlow(this, setup.Manifest, setup.Movement);
        Stages = new StageMachine(StageHandlers(boardingFlow));
        Journal = journal.AsReadOnly();
    }

    /// <summary>Gets the cabin the flight runs in.</summary>
    public CabinLayout Layout { get; }

    /// <summary>Gets the cabin's nav graph.</summary>
    public NavGraph Graph { get; }

    /// <summary>Gets the shortest paths over the nav graph.</summary>
    public PathTable Paths { get; }

    /// <summary>Gets who holds or has reserved each node.</summary>
    public Occupancy Occupancy { get; }

    /// <summary>Gets the passengers, indexed by passenger id.</summary>
    public IReadOnlyList<Passenger> Passengers { get; }

    /// <summary>Gets the executor that runs every character's actions.</summary>
    public SequenceExecutor Executor { get; }

    /// <summary>Gets the crew's task board.</summary>
    public TaskBoard Board { get; }

    /// <summary>Gets the flight's stage machine.</summary>
    public StageMachine Stages { get; }

    /// <summary>Gets the root of the run's randomness.</summary>
    public RngRoot Rng { get; }

    /// <summary>Gets the simulator feed the flight reads once a tick.</summary>
    public ISimFeed Feed { get; }

    /// <summary>Gets the behaviour and event scripts.</summary>
    public IBehaviorScripts Scripts { get; }

    /// <summary>Gets the journal's input records, in the order they happened.</summary>
    public IReadOnlyList<JournalInputRecord> Journal { get; }

    /// <summary>Gets every passenger decision taken so far, kept whole, one list per passenger.</summary>
    public DecisionLog Decisions { get; }

    /// <summary>Gets the next tick to run: the number of ticks run so far.</summary>
    public long Tick { get; private set; }

    /// <summary>
    /// Gets the first tick, after boarding starts and before deboarding does, at which every manifest passenger was seated, or null
    /// until then; it stays null when deboarding starts first.
    /// </summary>
    public long? AllSeatedTick { get; private set; }

    /// <summary>Gets the first tick, once deboarding has started, at which every passenger who boarded was off, or null until then.</summary>
    public long? AllOffTick { get; private set; }

    /// <summary>Gets how many crew the flight carries; they hold the executor's first character ids.</summary>
    internal int CrewCount { get; }

    /// <summary>Gets the mover: every character's node and movement state.</summary>
    internal Movement Movement { get; }

    /// <summary>Runs <paramref name="ticks"/> ticks as one frame, journalling the frame first; zero ticks run and journal nothing.</summary>
    /// <param name="ticks">How many ticks to run, at least 0.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="ticks"/> is negative.</exception>
    public void Step(long ticks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ticks);
        if (ticks == 0)
        {
            return;
        }

        journal.Add(new FrameRecord(Tick, ticks));
        for (long run = 0; run < ticks; run++)
        {
            ReadFeed();
            boardingFlow.Tick(Tick);
            Executor.Tick(Tick);
            RecordMilestones();
            TickNeeds();
            TickDecisions();
            Tick++;
        }
    }

    /// <summary>Gets a passenger's <c>passenger/&lt;id&gt;</c> stream, the one their starting needs were drawn from, to draw on from.</summary>
    /// <param name="passengerId">The passenger's id.</param>
    /// <returns>The passenger's stream, shared by every per-passenger draw of the flight.</returns>
    internal SimRandom PassengerStream(int passengerId) => passengerStreams[passengerId];

    private static void RequireInputs(FlightSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(setup.Layout, nameof(setup.Layout));
        ArgumentNullException.ThrowIfNull(setup.Capacities, nameof(setup.Capacities));
        ArgumentNullException.ThrowIfNull(setup.Manifest, nameof(setup.Manifest));
        ArgumentNullException.ThrowIfNull(setup.Feed, nameof(setup.Feed));
        ArgumentNullException.ThrowIfNull(setup.Scripts, nameof(setup.Scripts));
        ArgumentNullException.ThrowIfNull(setup.NeedRates, nameof(setup.NeedRates));
        ArgumentNullException.ThrowIfNull(setup.StartingNeeds, nameof(setup.StartingNeeds));
        ArgumentNullException.ThrowIfNull(setup.Conditions, nameof(setup.Conditions));
        ArgumentNullException.ThrowIfNull(setup.Traits, nameof(setup.Traits));
        ArgumentNullException.ThrowIfNull(setup.Movement, nameof(setup.Movement));
        ArgumentNullException.ThrowIfNull(setup.Decisions, nameof(setup.Decisions));
        ArgumentOutOfRangeException.ThrowIfNegative(setup.CrewCount, nameof(setup.CrewCount));
        RequireFiniteNonNegative(setup.AboardUneasePushPerHour, nameof(setup.AboardUneasePushPerHour));
        RequireDecisionRules(setup.Decisions);
    }

    private static void RequireDecisionRules(DecisionRules rules)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rules.CadenceTicks, 1, nameof(rules.CadenceTicks));
        RequireFiniteNonNegative(rules.KeepCurrentBias, nameof(rules.KeepCurrentBias));
        if (rules.Candidates.IsDefaultOrEmpty)
        {
            throw DecisionRuleRefusal($"{nameof(rules.Candidates)} must list at least one activity.", nameof(rules.Candidates));
        }

        if (FirstRepeated(rules.Candidates.AsSpan()) is { } repeated)
        {
            throw DecisionRuleRefusal($"{nameof(rules.Candidates)} list activity {repeated.Value} more than once.", nameof(rules.Candidates));
        }

        if (!rules.Candidates.Contains(rules.Initial))
        {
            throw DecisionRuleRefusal(
                $"{nameof(rules.Initial)} activity {rules.Initial.Value} is not one of the {nameof(rules.Candidates)}.",
                nameof(rules.Initial)
            );
        }
    }

    /// <summary>A refusal naming the <see cref="DecisionRules"/> field at fault rather than a parameter of the method that checks it.</summary>
    private static ArgumentException DecisionRuleRefusal(string message, string field) => new(message, field);

    /// <summary>The first activity listed more than once, or null when every activity is listed once.</summary>
    private static ActivityId? FirstRepeated(ReadOnlySpan<ActivityId> activities)
    {
        for (int index = 0; index < activities.Length; index++)
        {
            for (int later = index + 1; later < activities.Length; later++)
            {
                if (activities[later] == activities[index])
                {
                    return activities[index];
                }
            }
        }

        return null;
    }

    private static void RequireFiniteNonNegative(double value, string paramName)
    {
        if (!double.IsFinite(value) || value < 0.0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"{paramName} must be finite and at least 0; it is {value}.");
        }
    }

    private static Passenger BuildPassenger(ManifestPassenger passenger, PassengerStart start, int crewCount, ActivityId initialActivity) =>
        new(passenger, start.ToNeedSet(), start.LateAndFedUp, crewCount) { CurrentActivity = initialActivity };

    /// <summary>
    /// A passenger's facts for a decision: what the world and the passenger hold, and every member nothing in the Engine holds
    /// yet at its nothing-present value, one line each.
    /// </summary>
    private static PassengerFacts DecisionFacts(Passenger passenger, FlightStage stage, FeedObservation observation) =>
        new()
        {
            Needs = passenger.Needs,
            Traits = passenger.Manifest.Traits,
            Stage = stage,
            SeatbeltSignOn = observation.SeatbeltSignOn,
            Turbulence = observation.Turbulence,
            CurrentActivity = passenger.CurrentActivity,
            CabinDimmed = false,
            CallLightOn = false,
            TrayDown = false,
            MinutesSinceWoken = double.PositiveInfinity,
            NeighbourChatting = false,
            AwakeGroupMemberAdjacent = false,
            IfeAvailable = false,
            MinutesSinceServed = double.PositiveInfinity,
            CartInZone = false,
        };

    /// <summary>A handler for every stage: boarding and deboarding start their flows, every other stage does nothing on entry.</summary>
    private static Dictionary<FlightStage, IStageHandler> StageHandlers(BoardingFlow flow)
    {
        Dictionary<FlightStage, IStageHandler> handlers = Enum.GetValues<FlightStage>()
            .ToDictionary(stage => stage, IStageHandler (_) => new NoOpStageHandler());
        handlers[FlightStage.Boarding] = new StageHandler(_ => flow.StartBoarding());
        handlers[FlightStage.Deboarding] = new StageHandler(_ => flow.StartDeboarding());
        return handlers;
    }

    /// <summary>The product of a passenger's trait factors on each need, indexed by <see cref="Need"/>.</summary>
    private static double[] TraitFactors(ManifestPassenger passenger, TraitEffects traits)
    {
        double[] factors = new double[NeedSet.NeedCount];
        Array.Fill(factors, 1.0);
        foreach (TraitId trait in passenger.Traits.Span)
        {
            IReadOnlyDictionary<Need, double> traitFactors = traits.EffectOf(trait).Factors;
            for (int need = 0; need < NeedSet.NeedCount; need++)
            {
                if (traitFactors.TryGetValue((Need)need, out double factor))
                {
                    factors[need] *= factor;
                }
            }
        }

        return factors;
    }

    /// <summary>Records the tick every passenger is first seated after boarding starts, and first off after deboarding starts.</summary>
    private void RecordMilestones()
    {
        bool boarding = boardingFlow.BoardingStarted && !boardingFlow.DeboardingStarted;
        if (AllSeatedTick is null && boarding && boardingFlow.AllSeated())
        {
            AllSeatedTick = Tick;
        }

        if (AllOffTick is null && boardingFlow.DeboardingStarted && boardingFlow.AllOff())
        {
            AllOffTick = Tick;
        }
    }

    private void ReadFeed()
    {
        FeedObservation observation = Feed.Observe(Tick);
        if (lastObservation == observation)
        {
            return;
        }

        lastObservation = observation;
        journal.Add(new FeedObservationRecord(Tick, observation));
        Stages.Advance(observation, Tick);
    }

    private void TickNeeds()
    {
        double minuteOfDay = (boardingStartMinute + ((double)Tick / SimTime.TicksPerSimMinute)) % BodyClockRules.MinutesPerDay;
        double restBandFactor = BodyClock.RestRiseFactor(bodyClock, minuteOfDay);
        var context = new NeedContext(IsAsleep: false, OnIfe: false, UneasePushPerHour: aboardUneasePushPerHour);
        Span<double> multipliers = stackalloc double[NeedSet.NeedCount];
        foreach (Passenger passenger in Passengers)
        {
            if (!passenger.IsBoarded)
            {
                continue;
            }

            ComposeMultipliers(passenger, restBandFactor, multipliers);
            passenger.Needs.Tick(needRates, multipliers, context);
        }
    }

    /// <summary>Lets every passenger due a decision on this tick choose an activity, in id order.</summary>
    private void TickDecisions()
    {
        FeedObservation observation = lastObservation ?? throw new InvalidOperationException("The feed was not read before the decisions.");
        FlightStage stage = Stages.Current ?? throw new InvalidOperationException("No stage was entered before the decisions.");
        foreach (Passenger passenger in Passengers)
        {
            if (DecidesNow(passenger))
            {
                Decide(passenger, DecisionFacts(passenger, stage, observation));
            }
        }
    }

    /// <summary>
    /// Whether a passenger decides on this tick: it is their staggered cadence tick, they are aboard, and they are standing or
    /// seated; a move under way, or any other mover state, is never interrupted by a decision.
    /// </summary>
    private bool DecidesNow(Passenger passenger)
    {
        int cadence = decisionRules.CadenceTicks;
        return Tick % cadence == passenger.Id % cadence
            && passenger.IsBoarded
            && Movement.StateOf(passenger.CharacterId) is MoverState.Standing or MoverState.Seated;
    }

    /// <summary>Scores the candidates, adds the keep-current bias, chooses, sets the passenger's activity and records the decision.</summary>
    private void Decide(Passenger passenger, in PassengerFacts facts)
    {
        ActivityId current = passenger.CurrentActivity;
        Scripts.ScoreActivities(in facts, decisionRules.Candidates.AsSpan(), decisionScores);
        RequireValidScores(passenger.Id);
        AddKeepCurrentBias(current);
        DecisionRecord record = RankAndChoose(current);
        passenger.CurrentActivity = record.Chosen;
        Decisions.Record(passenger.Id, record);
    }

    /// <summary>Refuses a score the behaviour port wrote that is not finite or is below 0, which the port's contract rules out.</summary>
    /// <exception cref="InvalidOperationException">A score is NaN, infinite or negative, naming the passenger and the activity.</exception>
    private void RequireValidScores(int passengerId)
    {
        for (int index = 0; index < decisionScores.Length; index++)
        {
            double score = decisionScores[index];
            if (!double.IsFinite(score) || score < 0.0)
            {
                throw new InvalidOperationException(
                    $"Passenger {passengerId}'s score for activity {decisionRules.Candidates[index].Value} is {score}; "
                        + "a score must be finite and at least 0."
                );
            }
        }
    }

    /// <summary>Adds the keep-current bias to the current activity's score when it is a candidate scoring above 0.</summary>
    private void AddKeepCurrentBias(ActivityId current)
    {
        int index = decisionRules.Candidates.IndexOf(current);
        if (index >= 0 && decisionScores[index] > 0.0)
        {
            decisionScores[index] += decisionRules.KeepCurrentBias;
        }
    }

    /// <summary>
    /// Ranks the top three candidates by final score and chooses the first; when every score is 0 the passenger keeps the
    /// current activity.
    /// </summary>
    private DecisionRecord RankAndChoose(ActivityId current)
    {
        int first = BestRanked(current, -1, -1);
        int second = BestRanked(current, first, -1);
        int third = BestRanked(current, first, second);
        ActivityId chosen = decisionScores[first] > 0.0 ? decisionRules.Candidates[first] : current;
        return new DecisionRecord(
            Tick,
            current,
            chosen,
            SlotActivity(first),
            SlotScore(first),
            SlotActivity(second),
            SlotScore(second),
            SlotActivity(third),
            SlotScore(third)
        );
    }

    /// <summary>The index of the best-ranked candidate other than the two taken ones, or -1 when none is left.</summary>
    private int BestRanked(ActivityId current, int taken, int alsoTaken)
    {
        int best = -1;
        for (int index = 0; index < decisionScores.Length; index++)
        {
            if (index != taken && index != alsoTaken && (best < 0 || Outranks(index, best, current)))
            {
                best = index;
            }
        }

        return best;
    }

    /// <summary>
    /// Whether one candidate ranks above another: the higher score; on a tie the current activity; on a tie between two others
    /// the lower <see cref="ActivityId"/> (balance.md section 3.9).
    /// </summary>
    private bool Outranks(int index, int other, ActivityId current)
    {
        double score = decisionScores[index];
        double otherScore = decisionScores[other];
        if (score > otherScore || score < otherScore)
        {
            return score > otherScore;
        }

        ActivityId activity = decisionRules.Candidates[index];
        ActivityId otherActivity = decisionRules.Candidates[other];
        if (activity == current || otherActivity == current)
        {
            return activity == current;
        }

        return activity.Value < otherActivity.Value;
    }

    private ActivityId SlotActivity(int index) => index < 0 ? decisionRules.Initial : decisionRules.Candidates[index];

    private double SlotScore(int index) => index < 0 ? 0.0 : decisionScores[index];

    /// <summary>
    /// The one place a passenger's per-tick need multipliers are composed: the Trait class holds the passenger's trait
    /// factors and, on Rest, the body-clock band's factor; the Context class holds late-and-fed-up on Unease.
    /// </summary>
    private void ComposeMultipliers(Passenger passenger, double restBandFactor, Span<double> multipliers)
    {
        double[] traits = traitFactors[passenger.Id];
        var lateAndFedUp = new RateModifier(SourceClass.Context, passenger.LateAndFedUp ? LateAndFedUpUneaseFactor : 1.0);
        var noContext = new RateModifier(SourceClass.Context, 1.0);
        Span<RateModifier> modifiers = stackalloc RateModifier[2];
        for (int need = 0; need < NeedSet.NeedCount; need++)
        {
            double trait = need == (int)Need.Rest ? traits[need] * restBandFactor : traits[need];
            modifiers[0] = new RateModifier(SourceClass.Trait, trait);
            modifiers[1] = need == (int)Need.Unease ? lateAndFedUp : noContext;
            multipliers[need] = RateMultiplier.Compose(modifiers);
        }
    }

    /// <summary>A stage handler that does nothing on entry.</summary>
    private sealed class NoOpStageHandler : IStageHandler
    {
        public void Start(long tick) { }
    }
}
