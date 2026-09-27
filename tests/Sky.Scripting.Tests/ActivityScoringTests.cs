using Sky.Engine.Flight;
using Sky.Engine.Needs;
using Sky.Engine.Passengers;
using Sky.Engine.Ports;
using Sky.Engine.Randomness;

namespace Sky.Scripting.Tests;

/// <summary>
/// Proves activity scoring through the Lua port: what a module reads off the facts, the score it hands back, and how a
/// module that throws, draws, fails to load or returns something that is not a score of 0 or more is disabled.
/// </summary>
public sealed class ActivityScoringTests
{
    private const string DrinkScoresRefreshment = "return { utility = function(facts) return facts.refreshment end }";

    private const string SleepScoresHalfRest = "return { utility = function(facts) return facts.rest * 0.5 end }";

    private const string SavedFacts = "local saved return { utility = function(facts) saved = saved or facts return saved.refreshment end }";

    private const string DrawsRandom = "return { utility = function(facts) return math.random(10) end }";

    private const string StageIndex = """
        return {
          utility = function(facts)
            local ids = { ["pre-boarding"] = 1, boarding = 2, ["taxi-out"] = 3, takeoff = 4, climb = 5,
                          cruise = 6, descent = 7, landing = 8, ["taxi-in"] = 9, deboarding = 10, done = 11 }
            return ids[facts.stage] or 0
          end
        }
        """;

    private const string TurbulenceIndex = """
        return {
          utility = function(facts)
            local ids = { none = 1, light = 2, moderate = 3 }
            return ids[facts.turbulence] or 0
          end
        }
        """;

    private static readonly string[] TraitNames = ["light_sleeper", "restless"];

    private static readonly TraitId[] LightSleeper = [new TraitId(0)];

    private static readonly ActivityModule[] OneModule = [new("drink", DrinkScoresRefreshment)];

    private static readonly PassengerFacts PlainFacts = Facts();

    /// <summary>The same passenger with every member carrying a value the plain facts do not, so a row tells them apart.</summary>
    private static readonly PassengerFacts EveryFact = Facts(
        refreshment: 80.0,
        bladder: 30.0,
        rest: 40.0,
        unease: 25.0,
        boredom: 15.0,
        stage: FlightStage.TaxiOut,
        turbulence: Turbulence.Moderate,
        seatbeltSignOn: true,
        traits: LightSleeper,
        currentActivity: new ActivityId(1),
        cabinDimmed: true,
        callLightOn: true,
        trayDown: true,
        neighbourChatting: true,
        awakeGroupMemberAdjacent: true,
        ifeAvailable: true,
        cartInZone: true,
        minutesSinceWoken: double.PositiveInfinity,
        minutesSinceServed: 12.5
    );

    /// <summary>A thirsty passenger scores drink above sleep, and each score is the module's own number.</summary>
    [Fact]
    public void ThirstyPassengerScoresDrinkAboveSleep()
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        LuaBehaviorScripts scripts = Build(
            host,
            new ActivityModule("drink", DrinkScoresRefreshment),
            new ActivityModule("sleep", SleepScoresHalfRest)
        );
        PassengerFacts facts = Facts(refreshment: 80.0, rest: 30.0);

        double[] scores = Score(scripts, facts, new ActivityId(0), new ActivityId(1));

        Assert.Equal(80.0, scores[0]);
        Assert.Equal(15.0, scores[1]);
        Assert.True(scores[0] > scores[1]);
    }

    /// <summary>A module that throws scores 0, is disabled, and leaves the other modules scoring; a second call still scores it 0.</summary>
    [Fact]
    public void ThrowingModuleScoresZeroAndIsDisabledWhileOthersScore()
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        LuaBehaviorScripts scripts = Build(
            host,
            new ActivityModule("drink", DrinkScoresRefreshment),
            new ActivityModule("boom", "return { utility = function(facts) error('boom') end }")
        );
        PassengerFacts facts = Facts(refreshment: 80.0);

        double[] first = Score(scripts, facts, new ActivityId(0), new ActivityId(1));

        Assert.Equal(80.0, first[0]);
        Assert.Equal(0.0, first[1]);
        Assert.True(host.IsDisabled("boom"));
        Assert.True(host.TryGetDisabledReason("boom", out string? reason));
        Assert.Contains("boom", reason, StringComparison.Ordinal);

        double[] second = Score(scripts, facts, new ActivityId(0), new ActivityId(1));

        Assert.Equal(80.0, second[0]);
        Assert.Equal(0.0, second[1]);
    }

    /// <summary>
    /// A utility that returns anything but a number, or a number below 0 or above any bound, scores 0, disables its
    /// module, and has the value it returned named in the reason. A numeric string is not a number.
    /// </summary>
    [Theory]
    [InlineData("-1", "returned -1")]
    [InlineData("-1.5", "returned -1.5")]
    [InlineData("'x'", "returned string")]
    [InlineData("'5'", "returned string")]
    [InlineData("' 7 '", "returned string")]
    [InlineData("nil", "returned nil")]
    [InlineData("0/0", "returned NaN")]
    [InlineData("1/0", "returned Infinity")]
    [InlineData("math.huge", "returned Infinity")]
    public void NegativeOrNonNumberResultScoresZeroAndDisables(string result, string named)
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        LuaBehaviorScripts scripts = Build(host, new ActivityModule("odd", $"return {{ utility = function(facts) return {result} end }}"));

        double[] scores = Score(scripts, Facts(), new ActivityId(0));

        Assert.Equal(0.0, scores[0]);
        Assert.True(host.IsDisabled("odd"));
        Assert.True(host.TryGetDisabledReason("odd", out string? reason));
        Assert.Contains(named, reason, StringComparison.Ordinal);
        Assert.Contains("not a score of 0 or more", reason, StringComparison.Ordinal);
    }

    /// <summary>A utility that draws from <c>math.random</c> is refused and disabled, even with a stream set on the host.</summary>
    [Fact]
    public void MathRandomInUtilityDisablesTheModule()
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        host.SetRandom(new RngRoot(7).Stream("passenger/0"));
        LuaBehaviorScripts scripts = Build(host, new ActivityModule("gamble", DrawsRandom));

        double[] scores = Score(scripts, Facts(), new ActivityId(0));

        Assert.Equal(0.0, scores[0]);
        Assert.True(host.IsDisabled("gamble"));
        Assert.True(host.TryGetDisabledReason("gamble", out string? reason));
        Assert.Contains("no random stream is set", reason, StringComparison.Ordinal);
    }

    /// <summary>A utility that keeps a fact between calls still reads the passenger being scored, not the one before.</summary>
    [Fact]
    public void SavedFactsReadTheCurrentPassenger()
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        LuaBehaviorScripts scripts = Build(host, new ActivityModule("keeper", SavedFacts));

        Assert.Equal(80.0, Score(scripts, Facts(refreshment: 80.0), new ActivityId(0))[0]);
        Assert.Equal(20.0, Score(scripts, Facts(refreshment: 20.0), new ActivityId(0))[0]);
    }

    /// <summary>
    /// Every member the adapter exposes reaches Lua, and reading one follows the passenger: each row is true for the
    /// facts that carry the value and false for the plain ones.
    /// </summary>
    [Theory]
    [InlineData("facts.refreshment == 80", true, false)]
    [InlineData("facts.bladder == 30", true, false)]
    [InlineData("facts.rest == 40", true, false)]
    [InlineData("facts.unease == 25", true, false)]
    [InlineData("facts.boredom == 15", true, false)]
    [InlineData("facts.stage == 'taxi-out'", true, false)]
    [InlineData("facts.seatbelt_sign", true, false)]
    [InlineData("facts.turbulence == 'moderate'", true, false)]
    [InlineData("facts.current_activity == 'sleep'", true, false)]
    [InlineData("facts.cabin_dimmed", true, false)]
    [InlineData("facts.call_light", true, false)]
    [InlineData("facts.tray_down", true, false)]
    [InlineData("facts.neighbour_chatting", true, false)]
    [InlineData("facts.awake_group_member_adjacent", true, false)]
    [InlineData("facts.ife_available", true, false)]
    [InlineData("facts.cart_in_zone", true, false)]
    [InlineData("facts.minutes_since_woken == math.huge", true, false)]
    [InlineData("facts.minutes_since_served == 12.5", true, false)]
    [InlineData("facts:has_trait('light_sleeper')", true, false)]
    [InlineData("facts:has_trait('restless')", false, false)]
    public void FactsReachLua(string expression, bool whenCarried, bool whenPlain)
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        LuaBehaviorScripts scripts = Build(
            host,
            new ActivityModule("drink", DrinkScoresRefreshment),
            new ActivityModule("sleep", SleepScoresHalfRest),
            new ActivityModule("probe", $"return {{ utility = function(facts) if {expression} then return 1 end return 0 end }}")
        );

        Assert.Equal(whenCarried ? 1.0 : 0.0, Score(scripts, EveryFact, new ActivityId(2))[0]);
        Assert.Equal(whenPlain ? 1.0 : 0.0, Score(scripts, PlainFacts, new ActivityId(2))[0]);
    }

    /// <summary>Every stage reaches Lua under the kebab-case id events name it by.</summary>
    [Theory]
    [MemberData(nameof(Stages))]
    public void EveryStageHasItsKebabId(FlightStage stage)
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        LuaBehaviorScripts scripts = Build(host, new ActivityModule("stages", StageIndex));

        double[] scores = Score(scripts, Facts(stage: stage), new ActivityId(0));

        Assert.Equal((int)stage + 1, scores[0]);
    }

    /// <summary>Every turbulence level reaches Lua under the id content names it by.</summary>
    [Theory]
    [MemberData(nameof(TurbulenceLevels))]
    public void EveryTurbulenceHasItsId(Turbulence turbulence)
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        LuaBehaviorScripts scripts = Build(host, new ActivityModule("levels", TurbulenceIndex));

        double[] scores = Score(scripts, Facts(turbulence: turbulence), new ActivityId(0));

        Assert.Equal((int)turbulence + 1, scores[0]);
    }

    /// <summary>The stage rows are the enum itself, so a stage added without a kebab id fails the test for that node.</summary>
    public static TheoryData<FlightStage> Stages() => [.. Enum.GetValues<FlightStage>()];

    /// <summary>The turbulence rows are the enum itself, so a level added without an id fails the test for it.</summary>
    public static TheoryData<Turbulence> TurbulenceLevels() => [.. Enum.GetValues<Turbulence>()];

    /// <summary>Facts the adapter cannot render are refused before a single score is written.</summary>
    [Theory]
    [InlineData("stage")]
    [InlineData("turbulence")]
    [InlineData("activity")]
    [InlineData("trait")]
    [InlineData("needs")]
    public void UnrenderableFactsAreRefusedBeforeAnyScore(string field)
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        LuaBehaviorScripts scripts = Build(host, new ActivityModule("drink", DrinkScoresRefreshment));
        TraitId[] unknownTrait = [new TraitId(9)];
        PassengerFacts facts = field switch
        {
            "stage" => Facts() with { Stage = (FlightStage)99 },
            "turbulence" => Facts() with { Turbulence = (Turbulence)7 },
            "activity" => Facts() with { CurrentActivity = new ActivityId(9) },
            "trait" => Facts(traits: unknownTrait),
            _ => Facts() with { Needs = null! },
        };
        double[] scores = [9.0];

        ArgumentException error = Assert.Throws<ArgumentException>(() => scripts.ScoreActivities(facts, [new ActivityId(0)], scores));

        Assert.Equal("facts", error.ParamName);
        Assert.Contains(field, error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(9.0, scores[0]);
    }

    /// <summary>A scores span the length of the candidate span is refused, naming the scores parameter.</summary>
    [Fact]
    public void MismatchedSpanLengthsAreRefused()
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        LuaBehaviorScripts scripts = Build(host, new ActivityModule("drink", DrinkScoresRefreshment));
        PassengerFacts facts = Facts();

        ArgumentException error = Assert.Throws<ArgumentException>(() => scripts.ScoreActivities(facts, [new ActivityId(0)], new double[2]));

        Assert.Equal("scores", error.ParamName);
    }

    /// <summary>A candidate with no module behind it is refused, naming the candidates parameter.</summary>
    [Fact]
    public void UnknownCandidateIsRefused()
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        LuaBehaviorScripts scripts = Build(host, new ActivityModule("drink", DrinkScoresRefreshment));
        PassengerFacts facts = Facts();

        ArgumentOutOfRangeException error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            scripts.ScoreActivities(facts, [new ActivityId(1)], new double[1])
        );

        Assert.Equal("candidates", error.ParamName);
    }

    /// <summary>Two activities with the same id are refused at construction, naming the repeated id.</summary>
    [Fact]
    public void RepeatedActivityIdIsRefused()
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        ActivityModule[] activities = [new("drink", DrinkScoresRefreshment), new("drink", SleepScoresHalfRest)];

        ArgumentException error = Assert.Throws<ArgumentException>(() => new LuaBehaviorScripts(host, activities, [], TraitNames));

        Assert.Contains("repeats", error.Message, StringComparison.Ordinal);
    }

    /// <summary>A blank activity id is refused at construction, naming its index.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void BlankActivityIdIsRefused(string id)
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        ActivityModule[] activities = [new("drink", DrinkScoresRefreshment), new(id, SleepScoresHalfRest)];

        ArgumentException error = Assert.Throws<ArgumentException>(() => new LuaBehaviorScripts(host, activities, [], TraitNames));

        Assert.Contains("Activity 1", error.Message, StringComparison.Ordinal);
    }

    /// <summary>A blank trait name is refused at construction, naming its index.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void BlankTraitNameIsRefused(string name)
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        string[] traitNames = ["light_sleeper", name];

        ArgumentException error = Assert.Throws<ArgumentException>(() => new LuaBehaviorScripts(host, OneModule, [], traitNames));

        Assert.Contains("Trait 1", error.Message, StringComparison.Ordinal);
    }

    /// <summary>None of the four arguments may be null.</summary>
    [Fact]
    public void NullArgumentsAreRefused()
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);

        ArgumentNullException noHost = Assert.Throws<ArgumentNullException>(() => new LuaBehaviorScripts(null!, OneModule, [], TraitNames));
        ArgumentNullException noActivities = Assert.Throws<ArgumentNullException>(() => new LuaBehaviorScripts(host, null!, [], TraitNames));
        ArgumentNullException noEvents = Assert.Throws<ArgumentNullException>(() => new LuaBehaviorScripts(host, OneModule, null!, TraitNames));
        ArgumentNullException noTraitNames = Assert.Throws<ArgumentNullException>(() => new LuaBehaviorScripts(host, OneModule, [], null!));

        Assert.Equal("host", noHost.ParamName);
        Assert.Equal("activities", noActivities.ParamName);
        Assert.Equal("events", noEvents.ParamName);
        Assert.Equal("traitNames", noTraitNames.ParamName);
    }

    private static LuaBehaviorScripts Build(LuaHost host, params ActivityModule[] activities) => new(host, activities, [], TraitNames);

    private static double[] Score(LuaBehaviorScripts scripts, PassengerFacts facts, params ActivityId[] candidates)
    {
        double[] scores = new double[candidates.Length];
        scripts.ScoreActivities(facts, candidates, scores);
        return scores;
    }

    private static PassengerFacts Facts(
        double refreshment = 50.0,
        double bladder = 0.0,
        double rest = 50.0,
        double unease = 10.0,
        double boredom = 0.0,
        FlightStage stage = FlightStage.Cruise,
        Turbulence turbulence = Turbulence.None,
        bool seatbeltSignOn = false,
        TraitId[]? traits = null,
        ActivityId currentActivity = default,
        bool cabinDimmed = false,
        bool callLightOn = false,
        bool trayDown = false,
        bool neighbourChatting = false,
        bool awakeGroupMemberAdjacent = false,
        bool ifeAvailable = false,
        bool cartInZone = false,
        double minutesSinceWoken = 0.0,
        double minutesSinceServed = double.PositiveInfinity
    )
    {
        NeedSet needs = new(unease);
        needs.Set(Need.Refreshment, refreshment);
        needs.Set(Need.Bladder, bladder);
        needs.Set(Need.Rest, rest);
        needs.Set(Need.Boredom, boredom);
        return new PassengerFacts
        {
            Needs = needs,
            Traits = traits is null ? ReadOnlyMemory<TraitId>.Empty : traits,
            Stage = stage,
            SeatbeltSignOn = seatbeltSignOn,
            Turbulence = turbulence,
            CurrentActivity = currentActivity,
            CabinDimmed = cabinDimmed,
            CallLightOn = callLightOn,
            TrayDown = trayDown,
            MinutesSinceWoken = minutesSinceWoken,
            NeighbourChatting = neighbourChatting,
            AwakeGroupMemberAdjacent = awakeGroupMemberAdjacent,
            IfeAvailable = ifeAvailable,
            MinutesSinceServed = minutesSinceServed,
            CartInZone = cartInZone,
        };
    }
}
