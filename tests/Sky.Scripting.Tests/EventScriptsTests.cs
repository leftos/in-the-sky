using Lua;
using Sky.Engine.Events;
using Sky.Engine.Flight;
using Sky.Engine.Needs;
using Sky.Engine.Passengers;
using Sky.Engine.Ports;
using Sky.Engine.Randomness;

namespace Sky.Scripting.Tests;

/// <summary>
/// Proves event evaluation through the Lua port: a trigger fires only in its phases, the scene, choices and effects come
/// back as the Engine's records, and a module that throws or returns something malformed is disabled for the flight and
/// returns nothing.
/// </summary>
public sealed class EventScriptsTests
{
    private const string ModuleId = "test";

    private const string FiresOnFirstPassenger = "function(ctx) return { subject = ctx:passenger(1) } end";

    private const string PlainScene = "function(facts) return 'The passenger in 12A keeps glancing at the lavatory.' end";

    private const string TwoChoices = """
        function(facts)
          return {
            { id = 'help', label = 'Walk them to the lavatory', needs_crew = true, minutes = 3, quality = 0.8 },
            { id = 'leave', label = 'Leave it for now', needs_crew = false, minutes = 0, quality = 0.2 },
          }
        end
        """;

    private const string OneLine = "function(facts, choice) return { { after_minutes = 0, target = 'subject', line = 'They nod.' } } end";

    private const string HelpChoice = "{ id = 'help', label = 'Help', needs_crew = true, minutes = 3, quality = 0.8 }";

    private const string LeaveChoice = "{ id = 'leave', label = 'Leave it for now', needs_crew = false, minutes = 0, quality = 0.2 }";

    private static readonly string[] TraitNames = ["nervous", "child"];

    private static readonly TraitId[] Nervous = [new TraitId(0)];

    /// <summary>A trigger that fires whenever it is asked returns facts in its phase and nothing in another.</summary>
    [Fact]
    public void TriggerFiresOnlyInItsPhase()
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        LuaBehaviorScripts scripts = Build(host, new EventModule(ModuleId, Source(phases: "{ 'boarding', 'taxi-out' }")));

        EventFacts? boarding = scripts.Trigger(ModuleId, Context(FlightStage.Boarding), Stream(1));
        EventFacts? cruise = scripts.Trigger(ModuleId, Context(FlightStage.Cruise), Stream(1));

        Assert.NotNull(boarding);
        Assert.Equal(ModuleId, boarding.EventId);
        Assert.Equal(11, boarding.Subject);
        Assert.Null(cruise);
        Assert.False(host.IsDisabled(ModuleId));
    }

    /// <summary>
    /// Every consequence kind and target form comes back as its record, its minutes turned into ticks and rounded half
    /// away from zero (half a tick is one, a quarter of a tick none).
    /// </summary>
    [Fact]
    public void EffectsComeBackAsConsequencesAtTheirOffsets()
    {
        const string effects = """
            function(facts, choice)
              if choice ~= 'help' then return {} end
              return {
                { after_minutes = 0, target = 'subject', line = 'They wave to each other over the seat backs.' },
                { after_minutes = 2, target = 'neighbours', need = 'unease', delta = -10 },
                { after_minutes = 5, target = 12, incident = 'noise_complaint' },
                { after_minutes = 1.5, swap = { 'subject', 12 } },
                { after_minutes = 3, target = 'subject', to_seat = '14C' },
                { after_minutes = 30, target = 12, incident = 'fight' },
                { after_minutes = 0.25, target = 12, need = 'refreshment', delta = 4.5 },
                { after_minutes = 1/480, target = 12, need = 'rest', delta = 1 },
                { after_minutes = 0.001, target = 12, need = 'rest', delta = 2 },
              }
            end
            """;
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        LuaBehaviorScripts scripts = Build(host, new EventModule(ModuleId, Source(effects: effects)));
        EventFacts facts = FireAndOffer(scripts);

        IReadOnlyList<DelayedConsequence> help = scripts.Effects(facts, "help", Stream(2));
        IReadOnlyList<DelayedConsequence> leave = scripts.Effects(facts, "leave", Stream(2));

        DelayedConsequence[] expected =
        [
            new LineConsequence(0, EventTarget.Subject, "They wave to each other over the seat backs."),
            new NeedConsequence(480, EventTarget.Neighbours, Need.Unease, -10.0),
            new IncidentConsequence(1_200, EventTarget.Passenger(12), IncidentKind.NoiseComplaint),
            new SeatSwapConsequence(360, EventTarget.Subject, EventTarget.Passenger(12)),
            new SeatMoveConsequence(720, EventTarget.Subject, "14C"),
            new IncidentConsequence(7_200, EventTarget.Passenger(12), IncidentKind.Fight),
            new NeedConsequence(60, EventTarget.Passenger(12), Need.Refreshment, 4.5),
            new NeedConsequence(1, EventTarget.Passenger(12), Need.Rest, 1.0),
            new NeedConsequence(0, EventTarget.Passenger(12), Need.Rest, 2.0),
        ];
        Assert.Equal(expected, help);
        Assert.Empty(leave);
        Assert.False(host.IsDisabled(ModuleId));
    }

    /// <summary>Each choice comes back with its id, label, crew flag, minutes and quality, in the module's order.</summary>
    [Fact]
    public void ChoicesComeBackWithTheirFields()
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        LuaBehaviorScripts scripts = Build(host, new EventModule(ModuleId, Source()));
        EventFacts facts = Fire(scripts);

        IReadOnlyList<EventChoice> choices = scripts.Choices(facts);

        EventChoice[] expected =
        [
            new EventChoice("help", "Walk them to the lavatory", NeedsCrew: true, Minutes: 3.0, Quality: 0.8),
            new EventChoice("leave", "Leave it for now", NeedsCrew: false, Minutes: 0.0, Quality: 0.2),
        ];
        Assert.Equal(expected, choices);
    }

    /// <summary>Fewer than two or more than four choices disables the module, and its trigger no longer fires.</summary>
    /// <param name="count">The number of choices the module returns.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public void ChoiceCountOutsideTwoToFourDisablesTheModule(int count)
    {
        string choices = $$"""
            function(facts)
              local list = {}
              for i = 1, {{count}} do
                list[i] = { id = 'c' .. i, label = 'Choice', needs_crew = false, minutes = 0, quality = 0.5 }
              end
              return list
            end
            """;
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        LuaBehaviorScripts scripts = Build(host, new EventModule(ModuleId, Source(choices: choices)));
        EventFacts facts = Fire(scripts);

        IReadOnlyList<EventChoice> offered = scripts.Choices(facts);

        Assert.Empty(offered);
        Assert.True(host.TryGetDisabledReason(ModuleId, out string? reason));
        Assert.Equal($"'choices' is a list of {count}, not a list of 2 to 4 choices", reason);
        Assert.Null(scripts.Trigger(ModuleId, Context(FlightStage.Cruise), Stream(1)));
    }

    /// <summary>A quality below 0, above 1 or not a number at all disables the module, naming the field.</summary>
    /// <param name="quality">The Lua expression of the first choice's quality.</param>
    /// <param name="expected">The disabled reason.</param>
    [Theory]
    [InlineData("-0.1", "'choices[1].quality' is -0.1, not a number in [0, 1]")]
    [InlineData("1.5", "'choices[1].quality' is 1.5, not a number in [0, 1]")]
    [InlineData("0/0", "'choices[1].quality' is NaN, not a number in [0, 1]")]
    [InlineData("'high'", "'choices[1].quality' is string, not a number")]
    public void QualityOutsideZeroToOneDisablesTheModule(string quality, string expected)
    {
        string choices = $$"""
            function(facts)
              return {
                { id = 'help', label = 'Help', needs_crew = true, minutes = 3, quality = {{quality}} },
                { id = 'leave', label = 'Leave it for now', needs_crew = false, minutes = 0, quality = 0.2 },
              }
            end
            """;
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        LuaBehaviorScripts scripts = Build(host, new EventModule(ModuleId, Source(choices: choices)));
        EventFacts facts = Fire(scripts);

        IReadOnlyList<EventChoice> offered = scripts.Choices(facts);

        Assert.Empty(offered);
        Assert.True(host.TryGetDisabledReason(ModuleId, out string? reason));
        Assert.Equal(expected, reason);
    }

    /// <summary>
    /// An effects function that throws disables its module, returns no consequences and stops its trigger; another
    /// module still fires, and both ids stay listed.
    /// </summary>
    [Fact]
    public void AModuleWhoseEffectsThrowsIsDisabledAndReturnsNone()
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        LuaBehaviorScripts scripts = Build(
            host,
            new EventModule("thrower", Source(id: "thrower", effects: "function(facts, choice) error('effects broke') end")),
            new EventModule("steady", Source(id: "steady"))
        );
        EventFacts facts = FireAndOffer(scripts, "thrower");

        IReadOnlyList<DelayedConsequence> effects = scripts.Effects(facts, "help", Stream(2));

        Assert.Empty(effects);
        Assert.True(host.TryGetDisabledReason("thrower", out string? reason));
        Assert.EndsWith("effects broke", reason, StringComparison.Ordinal);
        Assert.Null(scripts.Trigger("thrower", Context(FlightStage.Cruise), Stream(1)));
        Assert.NotNull(scripts.Trigger("steady", Context(FlightStage.Cruise), Stream(1)));
        Assert.Equal(["thrower", "steady"], scripts.EventIds);
    }

    /// <summary>A module missing a field it needs, or holding one of the wrong shape, is disabled at load naming the field.</summary>
    /// <param name="field">The module field the row replaces.</param>
    /// <param name="value">The Lua expression the field holds instead.</param>
    /// <param name="expected">The disabled reason.</param>
    [Theory]
    [InlineData("id", "nil", "'id' is nil, not a string")]
    [InlineData("id", "'other'", "'id' is 'other', not 'test'")]
    [InlineData("trigger", "5", "'trigger' is number, not a function")]
    [InlineData("describe", "nil", "'describe' is nil, not a function")]
    [InlineData("phases", "'cruise'", "'phases' is string, not a list of stage names")]
    [InlineData("phases", "{}", "'phases' is an empty list, not a list of stage names")]
    [InlineData("phases", "{ 'cruise', 'cruising' }", "'phases[2]' is 'cruising', not a stage name")]
    [InlineData("phases", "{ 'cruise', 3 }", "'phases[2]' is number, not a stage name")]
    public void MalformedModuleAtLoadIsDisabledNamingTheField(string field, string value, string expected)
    {
        string source = field switch
        {
            "id" => Source(id: null, idExpression: value),
            "trigger" => Source(trigger: value),
            "describe" => Source(describe: value),
            _ => Source(phases: value),
        };
        using LuaHost host = new(LuaHost.StandardInstructionBudget);

        LuaBehaviorScripts scripts = Build(host, new EventModule(ModuleId, source));

        Assert.Equal([ModuleId], scripts.EventIds);
        Assert.True(host.TryGetDisabledReason(ModuleId, out string? reason));
        Assert.Equal(expected, reason);
        Assert.Null(scripts.Trigger(ModuleId, Context(FlightStage.Cruise), Stream(1)));
    }

    /// <summary>A need or incident name the host does not know disables the module and returns no consequences.</summary>
    /// <param name="consequence">The one consequence the effects return.</param>
    /// <param name="expected">The disabled reason.</param>
    [Theory]
    [InlineData("{ after_minutes = 1, target = 'subject', need = 'thirst', delta = 5 }", "'effects[1].need' is 'thirst', not a need name")]
    [InlineData("{ after_minutes = 1, target = 'subject', incident = 'riot' }", "'effects[1].incident' is 'riot', not an incident name")]
    [InlineData("{ after_minutes = 1, target = 'subject', need = 7, delta = 5 }", "'effects[1].need' is number, not a need name")]
    public void UnknownNeedOrIncidentNameDisablesTheModule(string consequence, string expected)
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        LuaBehaviorScripts scripts = Build(host, new EventModule(ModuleId, Source(effects: Effects(consequence))));
        EventFacts facts = FireAndOffer(scripts);

        IReadOnlyList<DelayedConsequence> effects = scripts.Effects(facts, "help", Stream(2));

        Assert.Empty(effects);
        Assert.True(host.TryGetDisabledReason(ModuleId, out string? reason));
        Assert.Equal(expected, reason);
    }

    /// <summary>A swap names two single passengers; naming the neighbours on either side disables the module.</summary>
    /// <param name="swap">The swap's two targets.</param>
    /// <param name="expected">The disabled reason.</param>
    [Theory]
    [InlineData("{ 'subject', 'neighbours' }", "'effects[1].swap[2]' is 'neighbours', not a single passenger")]
    [InlineData("{ 'neighbours', 12 }", "'effects[1].swap[1]' is 'neighbours', not a single passenger")]
    public void SwapWithNeighboursIsRefused(string swap, string expected)
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        LuaBehaviorScripts scripts = Build(host, new EventModule(ModuleId, Source(effects: Effects($"{{ after_minutes = 1, swap = {swap} }}"))));
        EventFacts facts = FireAndOffer(scripts);

        IReadOnlyList<DelayedConsequence> effects = scripts.Effects(facts, "help", Stream(2));

        Assert.Empty(effects);
        Assert.True(host.TryGetDisabledReason(ModuleId, out string? reason));
        Assert.Equal(expected, reason);
    }

    /// <summary>
    /// A trigger and an effects call draw only from the stream they are handed: a stream left on the host and a scoring
    /// call in between change neither result, the left stream is not drawn from, and the host holds no stream right
    /// after the trigger returns.
    /// </summary>
    [Fact]
    public void TriggerAndEffectsDrawOnlyFromTheirOwnStream()
    {
        const string trigger = "function(ctx) return { subject = ctx:passenger(1), roll = math.random(1000000) } end";
        const string describe = "function(facts) return tostring(facts.roll) end";
        string effects = Effects("{ after_minutes = 0, target = 'subject', need = 'unease', delta = math.random(100) }");
        string source = Source(trigger: trigger, describe: describe, effects: effects);

        using LuaHost fresh = new(LuaHost.StandardInstructionBudget);
        LuaBehaviorScripts first = Build(fresh, new EventModule(ModuleId, source));
        EventFacts firstFacts = Fire(first, ModuleId, seed: 5);
        string firstRoll = first.Describe(firstFacts);
        Assert.Equal(2, first.Choices(firstFacts).Count);
        IReadOnlyList<DelayedConsequence> firstEffects = first.Effects(firstFacts, "help", Stream(6));

        using LuaHost used = new(LuaHost.StandardInstructionBudget);
        LuaBehaviorScripts second = new(
            used,
            [new ActivityModule("drink", "return { utility = function(facts) return 1 end }")],
            [new EventModule(ModuleId, source)],
            TraitNames
        );
        SimRandom leftOn = Stream(99);
        used.SetRandom(leftOn);
        second.ScoreActivities(ScoringFacts(), [new ActivityId(0)], new double[1]);
        used.SetRandom(leftOn);
        EventFacts secondFacts = Fire(second, ModuleId, seed: 5);
        Assert.True(used.LoadModule("drawer", "return { draw = function() return math.random(10) end }"));
        bool drewAfterTrigger = used.TryCall("drawer", "draw", [], new LuaValue[1]);
        string secondRoll = second.Describe(secondFacts);
        Assert.Equal(2, second.Choices(secondFacts).Count);
        IReadOnlyList<DelayedConsequence> secondEffects = second.Effects(secondFacts, "help", Stream(6));

        Assert.False(drewAfterTrigger);
        Assert.True(used.TryGetDisabledReason("drawer", out string? reason));
        Assert.Contains("no random stream", reason, StringComparison.Ordinal);
        Assert.Equal(firstRoll, secondRoll);
        Assert.Equal(firstEffects, secondEffects);
        Assert.Single(secondEffects);
        Assert.Equal(Stream(99).NextUInt64(), leftOn.NextUInt64());
    }

    /// <summary>Released facts are gone: passing them back is a caller bug that throws, naming the handle.</summary>
    [Fact]
    public void ReleaseDropsTheFactsTable()
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        LuaBehaviorScripts scripts = Build(host, new EventModule(ModuleId, Source()));
        EventFacts facts = Fire(scripts);

        scripts.Release(facts);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => scripts.Describe(facts));
        Assert.Contains($"handle {facts.Handle}", error.Message, StringComparison.Ordinal);
        Assert.False(host.IsDisabled(ModuleId));
    }

    /// <summary>
    /// Describe returns the module's scene, built here from every fact the trigger can read off the context, so each
    /// context member reaches Lua under its name.
    /// </summary>
    [Fact]
    public void DescribeReturnsTheScene()
    {
        const string trigger = """
            function(ctx)
              local id = ctx:passenger(2)
              local parts = {
                ctx.stage, tostring(ctx.seatbelt_sign), ctx.turbulence, tostring(ctx.service_round_running),
                tostring(ctx.longest_task_wait_minutes), tostring(ctx.boarding_seated_share),
                tostring(ctx.passenger_count), tostring(ctx.free_seat_count),
                ctx:free_seat(1), ctx:free_seat_class(1), tostring(ctx:free_seat_has_free_neighbour(1)),
                tostring(ctx:free_seat(2)),
                ctx:seat(id), ctx:age_band(id), tostring(ctx:need(id, 'unease')), tostring(ctx:need(id, 'bladder')),
                tostring(ctx:has_trait(id, 'nervous')), tostring(ctx:has_trait(id, 'child')),
                tostring(ctx:group(id)), tostring(ctx:group_member_count(id)), tostring(ctx:group_member(id, 1)),
                tostring(ctx:neighbour_count(id)), tostring(ctx:neighbour(id, 1)), tostring(ctx:front(id)),
                tostring(ctx:asleep(id)), tostring(ctx:seated(id)), tostring(ctx:seat(99)), tostring(ctx:front(11)),
              }
              return { subject = id, scene = table.concat(parts, ' ') }
            end
            """;
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        LuaBehaviorScripts scripts = Build(
            host,
            new EventModule(ModuleId, Source(phases: "{ 'taxi-out' }", trigger: trigger, describe: "function(facts) return facts.scene end"))
        );
        EventPassenger subject = Passenger(12, "12B") with
        {
            Needs = Needs(unease: 40.0, bladder: 70.0),
            Traits = Nervous,
            GroupId = 3,
            GroupMembers = [11],
            AgeBand = AgeBand.Child,
            Neighbours = [11, 13],
            FrontPassenger = 7,
            Asleep = true,
            Seated = false,
        };
        EventContext context = Context(FlightStage.TaxiOut, Passenger(11, "12A"), subject) with
        {
            SeatbeltOn = true,
            Turbulence = Turbulence.Light,
            ServiceRoundRunning = true,
            LongestTaskWaitMinutes = 4.5,
            BoardingSeatedShare = 0.75,
            FreeSeats = [new FreeSeat("30C", SeatClass.Business, HasFreeNeighbour: true)],
        };
        EventFacts? facts = scripts.Trigger(ModuleId, context, Stream(1));
        Assert.NotNull(facts);

        string scene = scripts.Describe(facts);

        const string expected =
            "taxi-out true light true 4.5 0.75 2 1 30C business true nil 12B child 40 70 true false 3 1 11 2 11 7 true false nil nil";
        Assert.Equal(expected, scene);
        Assert.Equal(12, facts.Subject);
    }

    /// <summary>
    /// A module that keeps <c>ctx</c> past its trigger, in its facts or in a module global, and reads it from another
    /// function raises a Lua error there, which disables it, rather than reading a later check's cabin.
    /// </summary>
    /// <param name="trigger">The trigger, which keeps <c>ctx</c>.</param>
    /// <param name="describe">The describe function, which reads the kept <c>ctx</c>.</param>
    [Theory]
    [InlineData("function(ctx) return { subject = ctx:passenger(1), ctx = ctx } end", "function(facts) return facts.ctx.stage end")]
    [InlineData("function(ctx) kept = ctx return { subject = ctx:passenger(1) } end", "function(facts) return kept:seat(11) end")]
    public void CtxReadOutsideTriggerDisablesTheModule(string trigger, string describe)
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        LuaBehaviorScripts scripts = Build(host, new EventModule(ModuleId, Source(trigger: trigger, describe: describe)));
        EventFacts facts = Fire(scripts);

        string scene = scripts.Describe(facts);

        Assert.Equal(string.Empty, scene);
        Assert.True(host.TryGetDisabledReason(ModuleId, out string? reason));
        Assert.EndsWith("ctx is readable only during trigger", reason, StringComparison.Ordinal);
        Assert.Null(scripts.Trigger(ModuleId, Context(FlightStage.Cruise), Stream(1)));
    }

    /// <summary>
    /// Choices with no crew-free <c>leave</c>, the choice a task nobody starts resolves with, disable the module.
    /// </summary>
    /// <param name="list">The choices the module returns.</param>
    [Theory]
    [InlineData("{ " + HelpChoice + ", { id = 'stay', label = 'Stay', needs_crew = false, minutes = 0, quality = 0.2 } }")]
    [InlineData("{ " + HelpChoice + ", { id = 'leave', label = 'Leave it for now', needs_crew = true, minutes = 2, quality = 0.2 } }")]
    public void ChoicesWithoutALeaveChoiceDisableTheModule(string list)
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        LuaBehaviorScripts scripts = Build(host, new EventModule(ModuleId, Source(choices: Returning(list))));
        EventFacts facts = Fire(scripts);

        IReadOnlyList<EventChoice> offered = scripts.Choices(facts);

        Assert.Empty(offered);
        Assert.True(host.TryGetDisabledReason(ModuleId, out string? reason));
        Assert.Equal("'choices' is a list with no crew-free 'leave' choice, not a list with one", reason);
    }

    /// <summary>
    /// Effects for a choice that was not offered, or before any were, are a caller bug that throws naming the choice,
    /// and leave the module enabled.
    /// </summary>
    [Fact]
    public void EffectsForAChoiceNotOfferedThrows()
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        LuaBehaviorScripts scripts = Build(host, new EventModule(ModuleId, Source()));
        EventFacts facts = Fire(scripts);

        InvalidOperationException beforeChoices = Assert.Throws<InvalidOperationException>(() => scripts.Effects(facts, "help", Stream(2)));
        Assert.Equal(2, scripts.Choices(facts).Count);
        InvalidOperationException notOffered = Assert.Throws<InvalidOperationException>(() => scripts.Effects(facts, "dance", Stream(2)));

        Assert.Contains("'help'", beforeChoices.Message, StringComparison.Ordinal);
        Assert.Contains("'dance'", notOffered.Message, StringComparison.Ordinal);
        Assert.False(host.IsDisabled(ModuleId));
        Assert.Single(scripts.Effects(facts, "leave", Stream(2)));
    }

    /// <summary>A disabled event reports the host's reason through the port; an enabled one reports none.</summary>
    [Fact]
    public void DisabledEventReportsItsReason()
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        LuaBehaviorScripts scripts = Build(
            host,
            new EventModule("broken", Source(id: "broken", trigger: "nil")),
            new EventModule("steady", Source(id: "steady"))
        );

        bool brokenDisabled = scripts.IsEventDisabled("broken", out string? brokenReason);
        bool steadyDisabled = scripts.IsEventDisabled("steady", out string? steadyReason);

        Assert.True(brokenDisabled);
        Assert.Equal("'trigger' is nil, not a function", brokenReason);
        Assert.False(steadyDisabled);
        Assert.Null(steadyReason);
        Assert.Throws<ArgumentException>(() => scripts.IsEventDisabled("missing", out _));
    }

    /// <summary>Each way a choices result can be malformed disables the module, naming the field.</summary>
    /// <param name="list">The Lua expression the choices function returns.</param>
    /// <param name="expected">The disabled reason.</param>
    [Theory]
    [InlineData("{ " + HelpChoice + ", " + HelpChoice + ", " + LeaveChoice + " }", "'choices[2].id' is 'help', not an id unique among the choices")]
    [InlineData(
        "{ { id = 'help', label = 'Help', needs_crew = false, minutes = 3, quality = 0.5 }, " + LeaveChoice + " }",
        "'choices[1].minutes' is 3, not 0 for a choice with no crew"
    )]
    [InlineData(
        "{ { id = 'help', label = 'Help', needs_crew = true, minutes = -1, quality = 0.5 }, " + LeaveChoice + " }",
        "'choices[1].minutes' is -1, not a number of minutes above 0 for a crew choice"
    )]
    [InlineData(
        "{ { id = 'help', label = 'Help', needs_crew = true, minutes = 0, quality = 0.5 }, " + LeaveChoice + " }",
        "'choices[1].minutes' is 0, not a number of minutes above 0 for a crew choice"
    )]
    [InlineData(
        "{ { id = 'help', label = 5, needs_crew = true, minutes = 3, quality = 0.5 }, " + LeaveChoice + " }",
        "'choices[1].label' is number, not a string"
    )]
    [InlineData(
        "{ { id = 'help', label = 'Help', needs_crew = 'yes', minutes = 3, quality = 0.5 }, " + LeaveChoice + " }",
        "'choices[1].needs_crew' is string, not a boolean"
    )]
    [InlineData("{ 'help', " + LeaveChoice + " }", "'choices[1]' is string, not a table")]
    [InlineData("'help'", "choices returned string, not a list of choices")]
    public void MalformedChoicesDisableTheModule(string list, string expected)
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        LuaBehaviorScripts scripts = Build(host, new EventModule(ModuleId, Source(choices: Returning(list))));
        EventFacts facts = Fire(scripts);

        IReadOnlyList<EventChoice> offered = scripts.Choices(facts);

        Assert.Empty(offered);
        Assert.True(host.TryGetDisabledReason(ModuleId, out string? reason));
        Assert.Equal(expected, reason);
    }

    /// <summary>Each way an effects result can be malformed disables the module, naming the field.</summary>
    /// <param name="withSubject">Whether the trigger's facts name a subject.</param>
    /// <param name="list">The Lua expression the effects function returns.</param>
    /// <param name="expected">The disabled reason.</param>
    [Theory]
    [InlineData(
        true,
        "{ { after_minutes = 1, target = 'subject' } }",
        "'effects[1]' is a table with 0 of need, incident, swap, to_seat and line, not a table with exactly one"
    )]
    [InlineData(
        true,
        "{ { after_minutes = 1, target = 'subject', need = 'rest', delta = 1, line = 'x' } }",
        "'effects[1]' is a table with 2 of need, incident, swap, to_seat and line, not a table with exactly one"
    )]
    [InlineData(
        false,
        "{ { after_minutes = 1, target = 'subject', line = 'x' } }",
        "'effects[1].target' is 'subject', not a target these facts can resolve: they name no subject"
    )]
    [InlineData(
        false,
        "{ { after_minutes = 1, target = 'neighbours', need = 'unease', delta = 1 } }",
        "'effects[1].target' is 'neighbours', not a target these facts can resolve: they name no subject"
    )]
    [InlineData(
        true,
        "{ { after_minutes = 1, target = 'neighbours', to_seat = '14C' } }",
        "'effects[1].target' is 'neighbours', not a single passenger"
    )]
    [InlineData(
        true,
        "{ { after_minutes = -1, target = 'subject', line = 'x' } }",
        "'effects[1].after_minutes' is -1, not a number of minutes of 0 or more"
    )]
    [InlineData(true, "{ { after_minutes = 'soon', target = 'subject', line = 'x' } }", "'effects[1].after_minutes' is string, not a number")]
    [InlineData(
        true,
        "{ { after_minutes = 1, target = 'subject', swap = { 'subject', 12 } } }",
        "'effects[1].target' is 'subject', not nil beside a swap, which names its passengers in 'swap'"
    )]
    [InlineData(true, "{ { after_minutes = 1, swap = { 'subject', 12, 11 } } }", "'effects[1].swap[3]' is 11, not nil: a swap names two passengers")]
    [InlineData(
        true,
        "{ { after_minutes = 1, swap = { 'subject', 'subject' } } }",
        "'effects[1].swap[2]' is 'subject', not a passenger other than 'subject': a swap names two passengers"
    )]
    [InlineData(
        true,
        "{ { after_minutes = 1, swap = { 12, 12 } } }",
        "'effects[1].swap[2]' is 12, not a passenger other than 12: a swap names two passengers"
    )]
    [InlineData(
        true,
        "{ { after_minutes = 1, swap = { 'subject', 11 } } }",
        "'effects[1].swap[2]' is 11, not a passenger other than 'subject': a swap names two passengers"
    )]
    [InlineData(
        true,
        "{ { after_minutes = 1, target = 'neighbours', incident = 'fight' } }",
        "'effects[1].target' is 'neighbours', not a single passenger"
    )]
    [InlineData(true, "{ { after_minutes = 1, target = 1.5, line = 'x' } }", "'effects[1].target' is 1.5, not a passenger id")]
    [InlineData(true, "{ { after_minutes = 1, target = 'everyone', line = 'x' } }", "'effects[1].target' is 'everyone', not a target")]
    [InlineData(true, "{ 'x' }", "'effects[1]' is string, not a table")]
    [InlineData(true, "nil", "effects returned nil, not a list of consequences")]
    [InlineData(
        true,
        "(function() local list = {} for i = 1, 33 do list[i] = { after_minutes = 0, target = 'subject', line = 'x' } end return list end)()",
        "'effects' is a list of more than 32, not a list of at most 32 consequences"
    )]
    public void MalformedEffectsDisableTheModule(bool withSubject, string list, string expected)
    {
        string trigger = withSubject ? FiresOnFirstPassenger : "function(ctx) return { other = ctx:passenger(1) } end";
        string effects = $"function(facts, choice) return {list} end";
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        LuaBehaviorScripts scripts = Build(host, new EventModule(ModuleId, Source(trigger: trigger, effects: effects)));
        EventFacts facts = FireAndOffer(scripts);

        IReadOnlyList<DelayedConsequence> consequences = scripts.Effects(facts, "help", Stream(2));

        Assert.Empty(consequences);
        Assert.True(host.TryGetDisabledReason(ModuleId, out string? reason));
        Assert.Equal(expected, reason);
    }

    /// <summary>A describe function that returns anything but a string disables the module and returns no scene.</summary>
    /// <param name="scene">The Lua expression describe returns.</param>
    /// <param name="expected">The disabled reason.</param>
    [Theory]
    [InlineData("5", "describe returned number, not a string")]
    [InlineData("nil", "describe returned nil, not a string")]
    public void DescribeThatReturnsNoSceneDisablesTheModule(string scene, string expected)
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        LuaBehaviorScripts scripts = Build(host, new EventModule(ModuleId, Source(describe: Returning(scene))));
        EventFacts facts = Fire(scripts);

        string described = scripts.Describe(facts);

        Assert.Equal(string.Empty, described);
        Assert.True(host.TryGetDisabledReason(ModuleId, out string? reason));
        Assert.Equal(expected, reason);
    }

    /// <summary>
    /// A trigger that returns something other than nil or a facts table, names a subject who is not aboard, or reads an
    /// unknown need or trait name disables the module and fires nothing.
    /// </summary>
    /// <param name="facts">The Lua expression the trigger returns.</param>
    /// <param name="expected">The end of the disabled reason.</param>
    [Theory]
    [InlineData("true", "trigger returned boolean, not a table or nil")]
    [InlineData("{ subject = 99 }", "'subject' is 99, not the id of a passenger aboard")]
    [InlineData("{ subject = 11.5 }", "'subject' is 11.5, not the id of a passenger aboard")]
    [InlineData("{ subject = 'x' }", "'subject' is 'x', not the id of a passenger aboard")]
    [InlineData(
        "{ subject = ctx:passenger(1), rest = ctx:need(ctx:passenger(1), 'thirst') }",
        "need: 'thirst' is not a need; the needs are refreshment, bladder, rest, unease and boredom"
    )]
    [InlineData(
        "{ subject = ctx:passenger(1), nervous = ctx:has_trait(ctx:passenger(1), 'nervous_flier') }",
        "has_trait: 'nervous_flier' is not a trait of this flight's trait table"
    )]
    public void MalformedTriggerResultDisablesTheModule(string facts, string expected)
    {
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        LuaBehaviorScripts scripts = Build(host, new EventModule(ModuleId, Source(trigger: $"function(ctx) return {facts} end")));

        EventFacts? fired = scripts.Trigger(ModuleId, Context(FlightStage.Cruise), Stream(1));

        Assert.Null(fired);
        Assert.True(host.TryGetDisabledReason(ModuleId, out string? reason));
        Assert.EndsWith(expected, reason, StringComparison.Ordinal);
    }

    /// <summary>A context the adapter cannot render is a caller bug, refused before any Lua runs.</summary>
    /// <param name="fault">Which fault the row's context carries.</param>
    /// <param name="expected">A fragment of the refusal's message.</param>
    [Theory]
    [InlineData("null list", "Passengers or FreeSeats is null")]
    [InlineData("repeated id", "Passenger 1 repeats the id 11")]
    [InlineData("undefined stage", "Stage 99 is not a defined stage")]
    public void CtxThatCannotBeRenderedIsRefused(string fault, string expected)
    {
        EventContext context = fault switch
        {
            "null list" => Context(FlightStage.Cruise) with { Passengers = null! },
            "repeated id" => Context(FlightStage.Cruise, Passenger(11, "12A"), Passenger(11, "12B")),
            _ => Context((FlightStage)99),
        };
        using LuaHost host = new(LuaHost.StandardInstructionBudget);
        LuaBehaviorScripts scripts = Build(host, new EventModule(ModuleId, Source()));

        ArgumentException error = Assert.Throws<ArgumentException>(() => scripts.Trigger(ModuleId, context, Stream(1)));

        Assert.Contains(expected, error.Message, StringComparison.Ordinal);
        Assert.False(host.IsDisabled(ModuleId));
        Assert.NotNull(scripts.Trigger(ModuleId, Context(FlightStage.Cruise), Stream(1)));
    }

    /// <summary>An event id that is blank, repeats another event's, or repeats an activity's is refused, naming the index.</summary>
    /// <param name="first">The first event's id.</param>
    /// <param name="second">The second event's id, or null for one event.</param>
    /// <param name="expected">The start of the refusal's message.</param>
    [Theory]
    [InlineData(" ", null, "Event 0 has a blank module id")]
    [InlineData("a", "a", "Event 1 repeats the module id 'a'")]
    [InlineData("drink", null, "Event 0 repeats the activity module id 'drink'")]
    public void ConstructorRefusesABadEventId(string first, string? second, string expected)
    {
        EventModule[] events = second is null ? [new(first, Source(id: first))] : [new(first, Source(id: first)), new(second, Source(id: second))];
        ActivityModule[] activities = [new("drink", "return { utility = function(facts) return 1 end }")];
        using LuaHost host = new(LuaHost.StandardInstructionBudget);

        ArgumentException error = Assert.Throws<ArgumentException>(() => new LuaBehaviorScripts(host, activities, events, TraitNames));

        Assert.StartsWith(expected, error.Message, StringComparison.Ordinal);
    }

    private static LuaBehaviorScripts Build(LuaHost host, params EventModule[] events) => new(host, [], events, TraitNames);

    private static string Returning(string expression) => $"function(facts) return {expression} end";

    private static EventFacts Fire(LuaBehaviorScripts scripts, string eventId = ModuleId, ulong seed = 1)
    {
        EventFacts? facts = scripts.Trigger(eventId, Context(FlightStage.Cruise), Stream(seed));
        Assert.NotNull(facts);
        return facts;
    }

    private static EventFacts FireAndOffer(LuaBehaviorScripts scripts, string eventId = ModuleId)
    {
        EventFacts facts = Fire(scripts, eventId);
        Assert.Equal(2, scripts.Choices(facts).Count);
        return facts;
    }

    private static SimRandom Stream(ulong seed) => new RngRoot(seed).Stream("event");

    private static string Effects(string consequence) => $"function(facts, choice) return {{ {consequence} }} end";

    private static string Source(
        string? id = ModuleId,
        string? idExpression = null,
        string phases = "{ 'cruise' }",
        string trigger = FiresOnFirstPassenger,
        string describe = PlainScene,
        string choices = TwoChoices,
        string effects = OneLine
    )
    {
        string idValue = idExpression ?? $"'{id}'";
        return $$"""
            return {
              id = {{idValue}},
              phases = {{phases}},
              trigger = {{trigger}},
              describe = {{describe}},
              choices = {{choices}},
              effects = {{effects}},
            }
            """;
    }

    private static EventContext Context(FlightStage stage, params EventPassenger[] passengers) =>
        new()
        {
            Stage = stage,
            SeatbeltOn = false,
            Turbulence = Turbulence.None,
            ServiceRoundRunning = false,
            LongestTaskWaitMinutes = 0.0,
            BoardingSeatedShare = 1.0,
            FreeSeats = [],
            Passengers = passengers.Length > 0 ? passengers : [Passenger(11, "12A"), Passenger(12, "12B")],
        };

    private static EventPassenger Passenger(int id, string seat) =>
        new()
        {
            Id = id,
            Needs = Needs(),
            Traits = ReadOnlyMemory<TraitId>.Empty,
            GroupId = id,
            GroupMembers = [],
            AgeBand = AgeBand.Adult,
            Seat = seat,
            Neighbours = [],
            FrontPassenger = null,
            Asleep = false,
            Seated = true,
        };

    private static NeedSet Needs(double unease = 10.0, double bladder = 0.0)
    {
        NeedSet needs = new(unease);
        needs.Set(Need.Bladder, bladder);
        return needs;
    }

    private static PassengerFacts ScoringFacts() =>
        new()
        {
            Needs = Needs(),
            Traits = ReadOnlyMemory<TraitId>.Empty,
            Stage = FlightStage.Cruise,
            SeatbeltSignOn = false,
            Turbulence = Turbulence.None,
            CurrentActivity = new ActivityId(0),
            CabinDimmed = false,
            CallLightOn = false,
            TrayDown = false,
            MinutesSinceWoken = 0.0,
            NeighbourChatting = false,
            AwakeGroupMemberAdjacent = false,
            IfeAvailable = false,
            MinutesSinceServed = 0.0,
            CartInZone = false,
        };
}
