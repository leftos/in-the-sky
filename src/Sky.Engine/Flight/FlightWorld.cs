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
}

/// <summary>
/// The flight world: the cabin, its passengers, the executor, the task board and the stage machine, run one tick at a time.
/// Each tick reads the feed and hands a changed observation to the stage machine, ticks the executor, ticks every boarded
/// passenger's needs, then advances <see cref="Tick"/>. The journal's input records are appended as they happen.
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

    private FeedObservation? lastObservation;

    /// <summary>Builds the flight: the nav graph, path table and occupancy from the layout, and each passenger with their starting needs.</summary>
    /// <param name="setup">Everything the flight is built from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="setup"/> or one of its reference inputs is null, naming the input.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The aboard Unease push is negative, NaN or infinite, naming it.</exception>
    public FlightWorld(FlightSetup setup)
    {
        RequireInputs(setup);
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
        Passengers = [.. manifest.Select(passenger => BuildPassenger(passenger, starts[passenger.Id], setup.CrewCount))];
        traitFactors = [.. manifest.Select(passenger => TraitFactors(passenger, setup.Traits))];

        Executor = new SequenceExecutor(setup.CrewCount + Passengers.Count);
        Board = new TaskBoard(Executor, setup.CrewCount);
        Stages = new StageMachine(NoOpHandlers());
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

    /// <summary>Gets the next tick to run: the number of ticks run so far.</summary>
    public long Tick { get; private set; }

    /// <summary>Gets the first tick at which every boarded passenger was seated, or null until then.</summary>
    public long? AllSeatedTick { get; internal set; }

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
            Executor.Tick(Tick);
            TickNeeds();
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
        RequireFiniteNonNegative(setup.AboardUneasePushPerHour, nameof(setup.AboardUneasePushPerHour));
    }

    private static void RequireFiniteNonNegative(double value, string paramName)
    {
        if (!double.IsFinite(value) || value < 0.0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"{paramName} must be finite and at least 0; it is {value}.");
        }
    }

    private static Passenger BuildPassenger(ManifestPassenger passenger, PassengerStart start, int crewCount) =>
        new(passenger, start.ToNeedSet(), start.LateAndFedUp, crewCount);

    private static Dictionary<FlightStage, IStageHandler> NoOpHandlers() =>
        Enum.GetValues<FlightStage>().ToDictionary(stage => stage, IStageHandler (_) => new NoOpStageHandler());

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
