using System.Globalization;
using System.Text.Json;
using Lua;
using Sky.Engine.Cabin;
using Sky.Engine.Flight;
using Sky.Engine.Needs;
using Sky.Engine.Passengers;
using Sky.Engine.Ports;
using Sky.Engine.Randomness;
using Xunit.Sdk;

namespace Sky.Scripting.Tests;

/// <summary>
/// Proves every event module under the shipped <c>src/Sky.Content/Data/events</c> directory: the host loads it under the
/// id of its own file name, it returns nothing from <c>trigger</c> at any stage its <c>phases</c> leave out even when
/// called directly outside the host's gate, its <c>describe</c> returns a scene, and every choice it offers returns
/// consequences <see cref="ConsequenceReader"/> parses whose passenger targets its own facts recorded
/// (<c>docs/design/events.md</c> sections 1 and 6).
/// </summary>
/// <remarks>
/// The shipped directory holds no module until the event author writes them, so each theory has no data and is skipped
/// rather than failed; every check proves itself on the fixture modules under <c>Fixtures/Events</c>, driven through the
/// same <see cref="Harness"/> the theories use.
/// </remarks>
public sealed class ShippedEventTests
{
    /// <summary>How many seeds the harness offers a trigger at each of its phases before it gives up on it firing.</summary>
    private const int SeedsPerPhase = 2000;

    /// <summary>What a shipped theory reports when no module fired, which the proving run of the modules must not see.</summary>
    private const string SkippedWithoutFacts = "no shipped module fired; X4b's proving run requires 0 skipped";

    /// <summary>The shipped event modules, beside the test assembly where <c>Sky.Content</c> copies the content tree.</summary>
    private static readonly string ShippedEvents = Path.Combine(AppContext.BaseDirectory, "Data", "events");

    /// <summary>The fixture event modules, beside the test assembly where this project copies them.</summary>
    private static readonly string FixtureEvents = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Events");

    /// <summary>The flight's trait names, in the order the shipped <c>Data/traits.json</c> lists them.</summary>
    private static readonly Lazy<string[]> Traits = new(ReadShippedTraitNames);

    /// <summary>
    /// The ids of the shipped event modules, one per <c>.lua</c> file: a module's id is its file name without the
    /// extension. Empty while no module ships, which skips the theory rather than failing it.
    /// </summary>
    /// <returns>The theory data.</returns>
    public static TheoryData<string> ShippedModules()
    {
        TheoryData<string> modules = [];
        if (!Directory.Exists(ShippedEvents))
        {
            return modules;
        }

        foreach (string file in Directory.EnumerateFiles(ShippedEvents, "*.lua").OrderBy(file => file, StringComparer.Ordinal))
        {
            modules.Add(Path.GetFileNameWithoutExtension(file));
        }

        return modules;
    }

    /// <summary>A shipped module loads with no disabled reason and under the id of its own file name.</summary>
    /// <param name="id">The module's id, its file name without the extension.</param>
    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(ShippedModules))]
    public void ShippedModuleLoadsWithoutDisabling(string id)
    {
        using Harness harness = HarnessFor(id, ShippedEvents);

        Assert.Empty(harness.Check().Loads);
    }

    /// <summary>A shipped module's trigger returns nothing at every stage its own <c>phases</c> leave out.</summary>
    /// <param name="id">The module's id, its file name without the extension.</param>
    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(ShippedModules))]
    public void ShippedTriggerChecksItsOwnPhase(string id)
    {
        using Harness harness = HarnessFor(id, ShippedEvents);

        Assert.Empty(harness.Check().Phases);
    }

    /// <summary>Every choice a shipped module offers has effects the reader parses, without disabling the module.</summary>
    /// <param name="id">The module's id, its file name without the extension.</param>
    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(ShippedModules))]
    public void ShippedChoicesEffectsParse(string id)
    {
        using Harness harness = HarnessFor(id, ShippedEvents);

        Report report = harness.Check();

        AssertCheckPassed(report.Branches, report.Fired, Assert.SkipUnless);
    }

    /// <summary>Every passenger a shipped branch targets is a passenger the trigger's own facts recorded.</summary>
    /// <param name="id">The module's id, its file name without the extension.</param>
    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(ShippedModules))]
    public void ShippedEffectTargetsAreAmongFacts(string id)
    {
        using Harness harness = HarnessFor(id, ShippedEvents);

        Report report = harness.Check();

        AssertCheckPassed(report.Targets, report.Fired, Assert.SkipUnless);
    }

    /// <summary>The fixture module that follows the house shape passes all four checks.</summary>
    [Fact]
    public void WellFormedFixturePassesEveryCheck()
    {
        using Harness harness = HarnessFor("well-formed", FixtureEvents);
        Report report = harness.Check();

        Assert.True(report.Fired, "the well-formed fixture's trigger returned no facts on the fixed cabin");
        Assert.Empty(report.Loads);
        Assert.Empty(report.Phases);
        Assert.Empty(report.Branches);
        Assert.Empty(report.Targets);
    }

    /// <summary>A fixture whose trigger skips its own stage check is caught by the phase check.</summary>
    [Fact]
    public void TriggerWithoutOwnStageCheckIsCaught()
    {
        using Harness harness = HarnessFor("trigger-without-stage-check", FixtureEvents);

        Assert.NotEmpty(harness.Check().Phases);
    }

    /// <summary>A fixture whose branch targets a passenger its facts never recorded is caught by the membership check.</summary>
    [Fact]
    public void EffectTargetOutsideFactsIsCaught()
    {
        using Harness harness = HarnessFor("effect-target-outside-facts", FixtureEvents);

        Assert.NotEmpty(harness.Check().Targets);
    }

    /// <summary>A chance-rolling fixture that skips its own stage check is caught, since the check offers it every seed.</summary>
    [Fact]
    public void ChanceTriggerWithoutOwnStageCheckIsCaught()
    {
        using Harness harness = HarnessFor("chance-trigger-without-stage-check", FixtureEvents);

        Assert.NotEmpty(harness.Check().Phases);
    }

    /// <summary>
    /// A fixture whose trigger raises fails both branch theories' assertions, naming the module, rather than skipping
    /// them as a module that merely never fired.
    /// </summary>
    [Fact]
    public void TriggerErrorIsReportedNotSkipped()
    {
        using Harness harness = HarnessFor("trigger-errors", FixtureEvents);
        Report report = harness.Check();

        Assert.False(report.Fired);
        Assert.Contains("'trigger-errors'", Assert.Single(report.Branches), StringComparison.Ordinal);
        Assert.IsType<EmptyException>(Record.Exception(() => AssertCheckPassed(report.Branches, report.Fired, SkipAsFailure)));
        Assert.IsType<EmptyException>(Record.Exception(() => AssertCheckPassed(report.Targets, report.Fired, SkipAsFailure)));
    }

    /// <summary>A fixture whose <c>describe</c> returns a number is caught: the host accepts only a string scene.</summary>
    [Fact]
    public void DescribeReturningANumberIsCaught()
    {
        using Harness harness = HarnessFor("describe-returns-number", FixtureEvents);

        Assert.StartsWith("describe is malformed", Assert.Single(harness.Check().Branches), StringComparison.Ordinal);
    }

    /// <summary>A fixture that does not compile is caught by the load check.</summary>
    [Fact]
    public void ModuleThatDoesNotLoadIsCaught()
    {
        using Harness harness = HarnessFor("does-not-load", FixtureEvents);

        Assert.StartsWith("the module is disabled", Assert.Single(harness.Check().Loads), StringComparison.Ordinal);
    }

    /// <summary>A fixture whose consequence names a need that does not exist is caught by the parse check.</summary>
    [Fact]
    public void EffectsThatDoNotParseAreCaught()
    {
        using Harness harness = HarnessFor("effects-do-not-parse", FixtureEvents);

        Assert.StartsWith("effects('help') is malformed", Assert.Single(harness.Check().Branches), StringComparison.Ordinal);
    }

    /// <summary>
    /// A branch theory's assertions over one check: the check found nothing, and only then is a module that never fired
    /// skipped, so a fault during the drive fails the theory rather than skipping it.
    /// </summary>
    /// <param name="failures">What the check found.</param>
    /// <param name="fired">Whether the trigger returned facts, so the check could run.</param>
    /// <param name="skipUnless">
    /// Skips the theory when its condition is false: <see cref="Assert.SkipUnless"/> in a theory, and
    /// <see cref="SkipAsFailure"/> in a fixture test, since a dynamic skip skips the calling test even when caught.
    /// </param>
    private static void AssertCheckPassed(IReadOnlyList<string> failures, bool fired, Action<bool, string> skipUnless)
    {
        Assert.Empty(failures);
        skipUnless(fired, SkippedWithoutFacts);
    }

    /// <summary>Stands in for a dynamic skip in a fixture test, throwing where the theory would have skipped.</summary>
    /// <param name="condition">The skip's condition; the theory skips when it is false.</param>
    /// <param name="reason">The skip's reason.</param>
    /// <exception cref="InvalidOperationException"><paramref name="condition"/> is false.</exception>
    private static void SkipAsFailure(bool condition, string reason)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"the theory would skip: {reason}");
        }
    }

    /// <summary>Builds the harness over one module, read from <paramref name="directory"/>.</summary>
    /// <param name="id">The module's id, its file name without the extension.</param>
    /// <param name="directory">The directory holding the module's <c>.lua</c> file.</param>
    /// <returns>The harness; the caller disposes it.</returns>
    private static Harness HarnessFor(string id, string directory) =>
        new(id, File.ReadAllText(Path.Combine(directory, id + ".lua")), FixedFlight(Traits.Value), Traits.Value);

    /// <summary>Reads the trait names of the shipped content, in the order the file lists them.</summary>
    /// <returns>One name per trait, indexed by trait id.</returns>
    private static string[] ReadShippedTraitNames()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Data", "traits.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return [.. document.RootElement.GetProperty("traits").EnumerateArray().Select(trait => trait.GetProperty("id").GetString()!)];
    }

    /// <summary>
    /// The cabin every trigger is driven with: one manifest holding each shipped event's hard conditions at once, so a
    /// single context serves every phase. A pair of adults booked together sit rows apart with a lone passenger beside
    /// one of them, two free seats sit side by side, an awake flyer carries <c>nervous_flyer</c> with raised Unease, two
    /// strangers in adjacent seats have raised Unease each, and a child with high Boredom sits beside the adult of its
    /// own group with another passenger in the seat ahead of it. Every id is 100 or more, so a row number a module
    /// records from a seat label can never pass as a passenger id.
    /// </summary>
    /// <param name="traits">The flight's trait names.</param>
    /// <returns>The context, at the stage each trigger is asked with.</returns>
    private static EventContext FixedFlight(string[] traits) =>
        new()
        {
            Stage = FlightStage.Cruise,
            SeatbeltOn = false,
            Turbulence = Turbulence.None,
            ServiceRoundRunning = false,
            LongestTaskWaitMinutes = 0.0,
            BoardingSeatedShare = 1.0,
            FreeSeats = [new FreeSeat("20A", SeatClass.Economy, true), new FreeSeat("20B", SeatClass.Economy, true)],
            Passengers =
            [
                Cabin(100, "21A", 100, CabinNeeds(unease: 80.0), AgeBand.Adult) with
                {
                    GroupMembers = [101],
                },
                Cabin(101, "17C", 100, CabinNeeds(unease: 60.0), AgeBand.Adult) with
                {
                    GroupMembers = [100],
                    Neighbours = [106],
                    FrontPassenger = 103,
                },
                Cabin(102, "16B", 102, CabinNeeds(unease: 80.0), AgeBand.Adult) with
                {
                    Traits = TraitIds(traits, "nervous_flyer"),
                    Neighbours = [103],
                },
                Cabin(103, "16C", 103, CabinNeeds(unease: 75.0), AgeBand.Adult) with
                {
                    Neighbours = [102],
                },
                Cabin(104, "22A", 104, CabinNeeds(boredom: 80.0), AgeBand.Child) with
                {
                    Traits = TraitIds(traits, "child"),
                    GroupMembers = [105],
                    Neighbours = [105],
                    FrontPassenger = 100,
                },
                Cabin(105, "22B", 104, CabinNeeds(unease: 40.0), AgeBand.Adult) with
                {
                    GroupMembers = [104],
                    Neighbours = [104],
                },
                Cabin(106, "17B", 106, CabinNeeds(unease: 30.0), AgeBand.Adult) with
                {
                    Neighbours = [101],
                },
            ],
        };

    /// <summary>One passenger of the fixed cabin: seated and awake, in no group list, with no neighbour and nobody ahead.</summary>
    /// <param name="id">The passenger's id.</param>
    /// <param name="seat">The label of the passenger's seat.</param>
    /// <param name="group">The passenger's group (booking) id.</param>
    /// <param name="needs">The passenger's five needs.</param>
    /// <param name="band">The passenger's age band.</param>
    /// <returns>The passenger.</returns>
    private static EventPassenger Cabin(int id, string seat, int group, NeedSet needs, AgeBand band) =>
        new()
        {
            Id = id,
            Needs = needs,
            Traits = ReadOnlyMemory<TraitId>.Empty,
            GroupId = group,
            GroupMembers = [],
            AgeBand = band,
            Seat = seat,
            Neighbours = [],
            FrontPassenger = null,
            Asleep = false,
            Seated = true,
        };

    /// <summary>One passenger's needs, with the two the fixed cabin varies.</summary>
    /// <param name="unease">The passenger's Unease.</param>
    /// <param name="boredom">The passenger's Boredom.</param>
    /// <returns>The need set.</returns>
    private static NeedSet CabinNeeds(double unease = 10.0, double boredom = 0.0)
    {
        NeedSet needs = new(unease);
        needs.Set(Need.Boredom, boredom);
        return needs;
    }

    /// <summary>The trait ids the named traits carry in the flight's trait table.</summary>
    /// <param name="traits">The flight's trait names.</param>
    /// <param name="names">The traits by their content names.</param>
    /// <returns>One id per name.</returns>
    /// <exception cref="ArgumentException">A name is not a trait of the flight's trait table.</exception>
    private static ReadOnlyMemory<TraitId> TraitIds(string[] traits, params string[] names)
    {
        List<TraitId> ids = [];
        foreach (string name in names)
        {
            int index = Array.IndexOf(traits, name);
            if (index < 0)
            {
                throw new ArgumentException($"'{name}' is not a trait of the flight's trait table.", nameof(names));
            }

            ids.Add(new TraitId(index));
        }

        return ids.ToArray();
    }

    /// <summary>Shows a number the way the reader shows one, in invariant digits.</summary>
    /// <param name="number">The number.</param>
    /// <returns>The text.</returns>
    private static string Show(double number) => number.ToString(CultureInfo.InvariantCulture);

    /// <summary>What the four checks of one module found; every list is empty when the check passed.</summary>
    /// <param name="Loads">Why the module did not load cleanly and under its own file name.</param>
    /// <param name="Phases">Stages outside the module's <c>phases</c> where its trigger still returned something.</param>
    /// <param name="Branches">Choices whose effects the reader could not parse, or that disabled the module.</param>
    /// <param name="Targets">Passenger targets no value the module's own facts recorded.</param>
    /// <param name="Fired">Whether the trigger returned facts on the fixed cabin, so the branch checks could run.</param>
    public sealed record Report(
        IReadOnlyList<string> Loads,
        IReadOnlyList<string> Phases,
        IReadOnlyList<string> Branches,
        IReadOnlyList<string> Targets,
        bool Fired
    );

    /// <summary>
    /// Runs one event module the way the flight does, holding the Lua facts table itself so a check can compare a
    /// branch's targets with what the trigger recorded: the module loads into its own host through
    /// <see cref="LuaEventScripts"/>, its <c>trigger</c> is called directly for the phase check, and its
    /// <c>describe</c>, <c>choices</c> and <c>effects</c> run through the production path, which is what reads a scene,
    /// validates the choice list and reads the effects.
    /// </summary>
    private sealed class Harness : IDisposable
    {
        private readonly LuaHost host;
        private readonly LuaEventContext context;
        private readonly LuaEventScripts scripts;
        private readonly EventContext flight;
        private readonly LuaValue[] contextArgument = new LuaValue[1];
        private readonly LuaValue[] results = new LuaValue[1];
        private readonly string id;
        private Report? report;

        /// <summary>Creates the harness, loading <paramref name="source"/> under <paramref name="id"/> into a fresh host.</summary>
        /// <param name="id">The module's id, which its own <c>id</c> field must carry.</param>
        /// <param name="source">The module's Lua source.</param>
        /// <param name="flight">The cabin every trigger call reads.</param>
        /// <param name="traits">The flight's trait names.</param>
        public Harness(string id, string source, EventContext flight, string[] traits)
        {
            this.id = id;
            this.flight = flight;
            host = new LuaHost(LuaHost.StandardInstructionBudget);
            context = new LuaEventContext(host, traits);
            contextArgument[0] = context;
            scripts = new LuaEventScripts(host, [new EventModule(id, source)], [], traits);
        }

        /// <summary>Runs every check once and gives the same report to every later call.</summary>
        /// <returns>The report.</returns>
        public Report Check()
        {
            report ??= Run();
            return report;
        }

        /// <inheritdoc/>
        public void Dispose() => host.Dispose();

        private Report Run()
        {
            IReadOnlyList<string> loads = LoadFailures();
            if (loads.Count > 0)
            {
                return new Report(loads, [], [], [], false);
            }

            // Read once, before any call: a trigger that faults in the phase check disables the module and drops its table.
            bool[] named = NamedStages();
            IReadOnlyList<string> phases = PhaseFailures(named);
            Fired fired = Drive(named);
            if (fired.Fault is not null)
            {
                return new Report(loads, phases, [fired.Fault], [fired.Fault], false);
            }

            if (fired.Event is null || fired.Table is null)
            {
                return new Report(loads, phases, [], [], false);
            }

            (List<string> branches, List<string> targets) = Branches(fired);
            return new Report(loads, phases, branches, targets, true);
        }

        /// <summary>Reports a module the load disabled, or whose declared id is not the one it is loaded under.</summary>
        /// <returns>Why the load failed; empty when it did not.</returns>
        private IReadOnlyList<string> LoadFailures()
        {
            if (scripts.IsEventDisabled(id, out string? reason))
            {
                return [$"the module is disabled: {reason}"];
            }

            LuaValue declared = host.GetField(id, "id");
            return declared.Type == LuaValueType.String && string.Equals(declared.Read<string>(), id, StringComparison.Ordinal)
                ? []
                : [$"'id' is {ConsequenceReader.Show(declared)}, not '{id}': a module's id is its file name"];
        }

        /// <summary>Reports every stage the module leaves out of <c>phases</c> where its trigger still returns something.</summary>
        /// <param name="named">The module's <c>phases</c>, one flag per stage.</param>
        /// <returns>One message per such stage.</returns>
        private List<string> PhaseFailures(bool[] named)
        {
            List<string> failures = [];
            for (int stage = 0; stage < named.Length; stage++)
            {
                if (named[stage])
                {
                    continue;
                }

                string name = LuaPassengerFacts.StageNames[stage];
                for (ulong seed = 1; seed <= SeedsPerPhase; seed++)
                {
                    if (!CallTrigger(flight with { Stage = (FlightStage)stage }, Draw(seed), out LuaValue returned))
                    {
                        failures.Add($"the trigger of module '{id}' disabled it at stage '{name}': {DisabledReason()}");
                        return failures;
                    }

                    if (returned.Type != LuaValueType.Nil)
                    {
                        failures.Add($"trigger returned {returned.TypeToString()} at stage '{name}', which the module's phases leave out");
                        break;
                    }
                }
            }

            return failures;
        }

        /// <summary>Drives the trigger at each of the module's phases, over the seeds, and keeps the first facts it returns.</summary>
        /// <param name="named">The module's <c>phases</c>, one flag per stage.</param>
        /// <returns>The facts, or why the drive found none.</returns>
        private Fired Drive(bool[] named)
        {
            for (int stage = 0; stage < named.Length; stage++)
            {
                if (!named[stage])
                {
                    continue;
                }

                EventContext at = flight with { Stage = (FlightStage)stage };
                for (ulong seed = 1; seed <= SeedsPerPhase; seed++)
                {
                    if (!CallTrigger(at, Draw(seed), out LuaValue returned))
                    {
                        return new Fired(null, null, $"the trigger of module '{id}' disabled it: {DisabledReason()}");
                    }

                    if (returned.Type != LuaValueType.Nil)
                    {
                        return Record(at, seed, returned);
                    }
                }
            }

            return new Fired(null, null, null);
        }

        /// <summary>Records what the trigger returned through the production path, which keeps the facts for the branches.</summary>
        /// <param name="at">The context the trigger fired in.</param>
        /// <param name="seed">The seed it fired on.</param>
        /// <param name="returned">What the direct call returned; not nil.</param>
        /// <returns>The facts, or why the production path refused them.</returns>
        private Fired Record(EventContext at, ulong seed, LuaValue returned)
        {
            if (returned.Type != LuaValueType.Table)
            {
                return new Fired(null, null, $"the trigger of module '{id}' returned {returned.TypeToString()}, not a table or nil");
            }

            EventFacts? recorded = scripts.Trigger(id, at, Draw(seed));
            if (recorded is not null)
            {
                return new Fired(recorded, returned.Read<LuaTable>(), null);
            }

            return host.IsDisabled(id)
                ? new Fired(null, null, $"the trigger of module '{id}' disabled it: {DisabledReason()}")
                : new Fired(null, null, $"the trigger of module '{id}' returned facts the production path would not take");
        }

        /// <summary>Runs every offered choice's effects, reading each branch through the production path.</summary>
        /// <param name="fired">The facts the drive found.</param>
        /// <returns>The parse failures and the targets the facts do not record.</returns>
        private (List<string> Branches, List<string> Targets) Branches(Fired fired)
        {
            List<string> branches = [];
            List<string> targets = [];
            HashSet<double> recorded = RecordedNumbers(fired.Table!);
            try
            {
                scripts.Describe(fired.Event!);
            }
            catch (InvalidOperationException exception)
            {
                branches.Add($"describe of module '{id}' threw: {exception.Message}");
                return (branches, targets);
            }

            if (host.IsDisabled(id))
            {
                branches.Add($"describe is malformed: {DisabledReason()}");
                return (branches, targets);
            }

            IReadOnlyList<EventChoice> choices = scripts.Choices(fired.Event!);
            if (choices.Count == 0)
            {
                branches.Add($"choices disabled the module: {DisabledReason()}");
                return (branches, targets);
            }

            foreach (EventChoice choice in choices)
            {
                IReadOnlyList<DelayedConsequence> consequences = scripts.Effects(fired.Event!, choice.Id, Draw(1));
                if (host.IsDisabled(id))
                {
                    branches.Add($"effects('{choice.Id}') is malformed: {DisabledReason()}");
                    break;
                }

                foreach (int passenger in PassengersOf(consequences))
                {
                    if (!recorded.Contains(passenger))
                    {
                        targets.Add(
                            $"effects('{choice.Id}') targets passenger {Show(passenger)}, which the facts ({Recorded(recorded)}) do not record"
                        );
                    }
                }
            }

            return (branches, targets);
        }

        /// <summary>The named-event stream a trigger or effects call draws from, on the seed the flight's RNG root hands it.</summary>
        /// <param name="seed">The random root's seed.</param>
        /// <returns>The stream.</returns>
        private static SimRandom Draw(ulong seed) => new RngRoot(seed).Stream("event");

        /// <summary>Calls the module's <c>trigger</c> directly, outside the host's phase gate.</summary>
        /// <param name="at">The context the call reads.</param>
        /// <param name="random">The stream the trigger draws its chance from.</param>
        /// <param name="returned">Receives the call's first result.</param>
        /// <returns><see langword="false"/> when the call disabled the module.</returns>
        private bool CallTrigger(EventContext at, SimRandom random, out LuaValue returned)
        {
            context.SetContext(at);
            context.IsLive = true;
            host.SetRandom(random);
            try
            {
                if (!host.TryCall(id, "trigger", contextArgument, results))
                {
                    returned = LuaValue.Nil;
                    return false;
                }

                returned = results[0];
                return true;
            }
            finally
            {
                context.IsLive = false;
                host.ClearRandom();
            }
        }

        /// <summary>Reads the module's <c>phases</c> as one flag per stage, as the host does.</summary>
        /// <returns>The flags, indexed by stage.</returns>
        private bool[] NamedStages()
        {
            bool[] named = new bool[LuaPassengerFacts.StageNames.Length];
            LuaValue value = host.GetField(id, "phases");
            if (value.Type != LuaValueType.Table)
            {
                return named;
            }

            LuaTable phases = value.Read<LuaTable>();
            for (int index = 1; phases[index].Type != LuaValueType.Nil; index++)
            {
                if (phases[index].Type == LuaValueType.String)
                {
                    int stage = Array.IndexOf(LuaPassengerFacts.StageNames, phases[index].Read<string>());
                    if (stage >= 0)
                    {
                        named[stage] = true;
                    }
                }
            }

            return named;
        }

        /// <summary>Collects every number recorded anywhere in the facts table, however deeply nested.</summary>
        /// <param name="facts">The facts table.</param>
        /// <returns>The numbers.</returns>
        private static HashSet<double> RecordedNumbers(LuaTable facts)
        {
            HashSet<double> numbers = [];
            Collect(facts, numbers, depth: 8);
            return numbers;
        }

        private static void Collect(LuaValue value, HashSet<double> into, int depth)
        {
            if (value.Type == LuaValueType.Number)
            {
                into.Add(value.Read<double>());
                return;
            }

            if (value.Type != LuaValueType.Table || depth == 0)
            {
                return;
            }

            foreach (KeyValuePair<LuaValue, LuaValue> entry in value.Read<LuaTable>())
            {
                Collect(entry.Value, into, depth - 1);
            }
        }

        private static IEnumerable<int> PassengersOf(IReadOnlyList<DelayedConsequence> consequences)
        {
            foreach (DelayedConsequence consequence in consequences)
            {
                foreach (EventTarget target in TargetsOf(consequence))
                {
                    if (target.Kind == EventTargetKind.Passenger)
                    {
                        yield return target.PassengerId;
                    }
                }
            }
        }

        private static IEnumerable<EventTarget> TargetsOf(DelayedConsequence consequence) =>
            consequence switch
            {
                NeedConsequence need => [need.Target],
                IncidentConsequence incident => [incident.Target],
                SeatMoveConsequence move => [move.Target],
                LineConsequence line => [line.Target],
                SeatSwapConsequence swap => [swap.First, swap.Second],
                _ => [],
            };

        private static string Recorded(HashSet<double> numbers) => string.Join(", ", numbers.Order().Select(Show));

        private string DisabledReason()
        {
            host.TryGetDisabledReason(id, out string? reason);
            return reason ?? "no reason";
        }

        /// <summary>The facts a trigger returned, and how the drive got to them.</summary>
        /// <param name="Event">The facts as the production loader recorded them, or null when the trigger never fired.</param>
        /// <param name="Table">The facts table the harness holds itself, or null when the trigger never fired.</param>
        /// <param name="Fault">Why the drive found no facts: a disabled module or a trigger that returned a non-table.</param>
        private sealed record Fired(EventFacts? Event, LuaTable? Table, string? Fault);
    }
}
