using Sky.Engine.Cabin;
using Sky.Engine.Manifest;
using Sky.Engine.Needs;
using Sky.Engine.Passengers;
using Sky.Engine.Randomness;
using Sky.Engine.Tests.Cabin;

namespace Sky.Engine.Tests.Passengers;

/// <summary>Proves the starting needs: the draw, the gate conditions on top of it, the group spread and the Unease baseline.</summary>
public sealed class StartingNeedsTests
{
    /// <summary>The boarding start of the reference day departure, 10:30 origin local.</summary>
    internal const int MorningBoardingStart = 630;

    /// <summary>The Unease baseline of a passenger with no Unease trait.</summary>
    internal const double DefaultBaseline = 20;

    private const double Tolerance = 1e-9;

    /// <summary>A trait with a Refreshment factor and no Unease baseline.</summary>
    internal static readonly TraitId Hungry = new(0);

    private static readonly TraitId Nervous = new(1);

    private static readonly TraitId Anxious = new(2);

    /// <summary>The seat nodes of <see cref="NavGraphBuilderTests.TwoRowLayout"/>; the fixture books its passengers into them in id order.</summary>
    private static readonly IReadOnlyList<int> SeatNodes = NavGraphBuilder.Build(NavGraphBuilderTests.TwoRowLayout(), 10).SeatNodes;

    private static readonly Dictionary<string, Func<double, object>> AmountFields = new()
    {
        [nameof(StartingNeedsRules.GroupSpread)] = value => Rules() with { GroupSpread = value },
        [nameof(StartingNeedsRules.ClosedOutletRefreshment)] = value => Rules() with { ClosedOutletRefreshment = value },
        [nameof(StartingNeedsRules.OpenRefreshmentPerMinute)] = value => Rules() with { OpenRefreshmentPerMinute = value },
        [nameof(StartingNeedsRules.ClosedRefreshmentPerMinute)] = value => Rules() with { ClosedRefreshmentPerMinute = value },
        [nameof(StartingNeedsRules.DelayRefreshmentCap)] = value => Rules() with { DelayRefreshmentCap = value },
        [nameof(StartingNeedsRules.BoredomPerMinute)] = value => Rules() with { BoredomPerMinute = value },
        [nameof(StartingNeedsRules.BoredomCap)] = value => Rules() with { BoredomCap = value },
        [nameof(StartingNeedsRules.UneasePerMinute)] = value => Rules() with { UneasePerMinute = value },
        [nameof(StartingNeedsRules.UneaseGraceMinutes)] = value => Rules() with { UneaseGraceMinutes = value },
        [nameof(StartingNeedsRules.UneaseCap)] = value => Rules() with { UneaseCap = value },
        [nameof(BodyClockRules.RestPerHourAwake)] = value => ReferenceBodyClock() with { RestPerHourAwake = value },
        [nameof(BodyClockRules.EarlyWakeRest)] = value => ReferenceBodyClock() with { EarlyWakeRest = value },
    };

    private static readonly Dictionary<string, Func<double, object>> NeedValueFields = new()
    {
        [nameof(StartingNeedsRules.RefreshmentCeiling)] = value => Rules() with { RefreshmentCeiling = value },
        [nameof(BodyClockRules.StartingRestCap)] = value => ReferenceBodyClock() with { StartingRestCap = value },
        [nameof(TraitEffects.DefaultUneaseBaseline)] = value => Traits() with { DefaultUneaseBaseline = value },
        [nameof(TraitEffect.UneaseBaseline)] = value => new TraitEffect { Factors = new Dictionary<Need, double>(), UneaseBaseline = value },
    };

    private static readonly Dictionary<string, Func<int, object>> MinuteFields = new()
    {
        [nameof(StartingNeedsRules.LateAndFedUpMinutes)] = value => Rules() with { LateAndFedUpMinutes = value },
        [nameof(BodyClockRules.EarlyWakeBeforeMinute)] = value => ReferenceBodyClock() with { EarlyWakeBeforeMinute = value },
        [nameof(BoardingConditions.GateDelayMinutes)] = value => Conditions(0, true) with { GateDelayMinutes = value },
        [nameof(BoardingConditions.BoardingStartMinute)] = value => Conditions(0, true) with { BoardingStartMinute = value },
    };

    private static readonly Dictionary<string, Func<NeedRange, object>> RangeFields = new()
    {
        [nameof(StartingNeedsRules.RefreshmentDraw)] = value => Rules() with { RefreshmentDraw = value },
        [nameof(StartingNeedsRules.BladderDraw)] = value => Rules() with { BladderDraw = value },
        [nameof(StartingNeedsRules.BoredomDraw)] = value => Rules() with { BoredomDraw = value },
    };

    private static readonly Dictionary<string, Func<object>> MissingParts = new()
    {
        [nameof(StartingNeedsRules.BodyClock)] = () => Rules() with { BodyClock = null! },
        [nameof(BodyClockRules.RestRiseBands)] = () => ReferenceBodyClock() with { RestRiseBands = null! },
        [nameof(TraitEffects.Traits)] = () => Traits() with { Traits = null! },
        [nameof(TraitEffect.Factors)] = () => new TraitEffect { Factors = null! },
    };

    /// <summary>
    /// On a fixed draw, 60 minutes with the outlets closed adds +25 Refreshment, +12 Boredom and +11.25 Unease, and the
    /// passenger is late and fed up.
    /// </summary>
    [Fact]
    public void SixtyMinutesClosedAddsTwentyFiveRefreshment()
    {
        PassengerManifest manifest = Manifest(1);

        PassengerStart reference = Draw(manifest, Conditions(0, true), Rules(), 7)[0];
        PassengerStart delayed = Draw(manifest, Conditions(60, false), Rules(), 7)[0];

        Assert.Equal(25.0, delayed.Refreshment - reference.Refreshment, Tolerance);
        Assert.Equal(12.0, delayed.Boredom - reference.Boredom, Tolerance);
        Assert.Equal(11.25, delayed.Unease - reference.Unease, Tolerance);
        Assert.Equal(reference.Bladder, delayed.Bladder);
        Assert.Equal(reference.Rest, delayed.Rest);
        Assert.True(delayed.LateAndFedUp);
    }

    /// <summary>With a draw that reaches past the ceiling, no starting Refreshment over 200 seeds at 240 minutes closed is above 70.</summary>
    [Fact]
    public void RefreshmentNeverPassesSeventy()
    {
        PassengerManifest manifest = Manifest(1, 1, 1, 1, 1, 1, 1, 1, 1, 1);
        StartingNeedsRules rules = Rules() with { RefreshmentDraw = new NeedRange(10, 60) };
        List<double> refreshments = [];
        for (ulong seed = 1; seed <= 200; seed++)
        {
            refreshments.AddRange(Draw(manifest, Conditions(240, false), rules, seed).Select(start => start.Refreshment));
        }

        Assert.All(refreshments, refreshment => Assert.True(refreshment <= 70.0, $"Starting Refreshment {refreshment} is above 70."));
        Assert.Contains(70.0, refreshments);
    }

    /// <summary>The reference conditions, no delay with the outlets open, add nothing to the draw and leave nobody late and fed up.</summary>
    [Fact]
    public void ZeroDelayOpenAddsNothing()
    {
        PassengerManifest manifest = Manifest(3, 1);
        StartingNeedsRules noGateNumbers = Rules() with
        {
            ClosedOutletRefreshment = 0,
            OpenRefreshmentPerMinute = 0,
            ClosedRefreshmentPerMinute = 0,
            BoredomPerMinute = 0,
            UneasePerMinute = 0,
        };

        IReadOnlyList<PassengerStart> reference = Draw(manifest, Conditions(0, true), Rules(), 11);

        Assert.Equal(Draw(manifest, Conditions(0, true), noGateNumbers, 11), reference);
        Assert.All(reference, start => Assert.Equal(DefaultBaseline, start.Unease));
        Assert.All(reference, start => Assert.False(start.LateAndFedUp));
    }

    /// <summary>A 29-minute delay is one short of late and fed up.</summary>
    [Fact]
    public void TwentyNineMinutesIsNotLateAndFedUp() =>
        Assert.All(Draw(Manifest(2, 1), Conditions(29, false), Rules(), 3), start => Assert.False(start.LateAndFedUp));

    /// <summary>A 30-minute delay makes every passenger late and fed up.</summary>
    [Fact]
    public void ThirtyMinutesIsLateAndFedUp() =>
        Assert.All(Draw(Manifest(2, 1), Conditions(30, true), Rules(), 3), start => Assert.True(start.LateAndFedUp));

    /// <summary>Every member of a booking of four starts with Refreshment and Boredom within the group spread of the first member's.</summary>
    [Fact]
    public void GroupMembersDrawWithinTheSpread()
    {
        PassengerManifest manifest = Manifest(4);
        for (ulong seed = 1; seed <= 100; seed++)
        {
            IReadOnlyList<PassengerStart> starts = Draw(manifest, Conditions(0, true), Rules(), seed);
            PassengerStart first = starts[0];
            Assert.All(starts, member => Assert.InRange(member.Refreshment - first.Refreshment, -10.0, 10.0));
            Assert.All(starts, member => Assert.InRange(member.Boredom - first.Boredom, -10.0, 10.0));
        }
    }

    /// <summary>The same seed draws the same starting needs.</summary>
    [Fact]
    public void SameSeedSameStartingNeeds()
    {
        PassengerManifest manifest = Manifest(2, 1, 4);

        Assert.Equal(Draw(manifest, Conditions(45, false), Rules(), 42), Draw(manifest, Conditions(45, false), Rules(), 42));
    }

    /// <summary>A passenger with several Unease traits starts at the highest of their baselines.</summary>
    [Fact]
    public void UneaseStartsAtTheHighestTraitBaseline()
    {
        PassengerManifest manifest = Singles([Nervous, Anxious, Hungry]);

        PassengerStart start = Draw(manifest, Conditions(0, true), Rules(), 5)[0];

        Assert.Equal(40.0, start.Unease);
        Assert.Equal(40.0, start.UneaseBaseline);
    }

    /// <summary>A passenger whose traits set no Unease baseline starts at the default.</summary>
    [Fact]
    public void UneaseStartsAtTheDefaultWithNoUneaseTrait()
    {
        PassengerManifest manifest = Singles([Hungry]);

        PassengerStart start = Draw(manifest, Conditions(0, true), Rules(), 5)[0];

        Assert.Equal(DefaultBaseline, start.Unease);
    }

    /// <summary>
    /// Rest at boarding is 6 per hour awake plus 15 for a wake-up before 06:00, capped at 60. A wake-up after boarding counts
    /// no hours awake, and still earns the 15 when it is before 06:00: the short-night test reads the wake time alone.
    /// </summary>
    /// <param name="wakeMinute">The minute the passenger woke.</param>
    /// <param name="boardingStartMinute">The minute boarding starts.</param>
    /// <param name="expected">The expected Rest at boarding.</param>
    [Theory]
    [InlineData(330, 630, 45.0)]
    [InlineData(480, 630, 15.0)]
    [InlineData(660, 630, 0.0)]
    [InlineData(330, 300, 15.0)]
    [InlineData(60, 630, 60.0)]
    public void StartingRestFollowsTheBodyClock(int wakeMinute, int boardingStartMinute, double expected) =>
        Assert.Equal(expected, BodyClock.StartingRest(ReferenceBodyClock(), wakeMinute, boardingStartMinute), Tolerance);

    /// <summary>A passenger carrying a trait the trait effects do not list is refused, naming the trait.</summary>
    [Fact]
    public void ATraitWithNoEffectIsRefused()
    {
        PassengerManifest manifest = Singles([new TraitId(9)]);

        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            StartingNeeds.Draw(manifest, Conditions(0, true), Rules(), Traits(), Streams(1, 1))
        );

        Assert.Equal("trait", error.ParamName);
    }

    /// <summary>A passenger no booking lists is refused, naming the manifest.</summary>
    [Fact]
    public void APassengerInNoBookingIsRefused()
    {
        PassengerManifest full = Manifest(2, 1);
        var missingSecondBooking = new PassengerManifest(full.Passengers, [full.Bookings[0]]);

        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            StartingNeeds.Draw(missingSecondBooking, Conditions(0, true), Rules(), Traits(), Streams(1, 3))
        );

        Assert.Equal("manifest", error.ParamName);
    }

    /// <summary>A stream count that is not the passenger count is refused, naming the streams.</summary>
    [Fact]
    public void AStreamCountMismatchIsRefused()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            StartingNeeds.Draw(Manifest(2, 1), Conditions(0, true), Rules(), Traits(), Streams(1, 2))
        );

        Assert.Equal("streams", error.ParamName);
    }

    /// <summary>An amount, rate or cap that is negative, NaN or infinite is refused, naming the field.</summary>
    /// <param name="field">The field set.</param>
    /// <param name="value">The bad value.</param>
    [Theory]
    [InlineData(nameof(StartingNeedsRules.GroupSpread), -1.0)]
    [InlineData(nameof(StartingNeedsRules.ClosedOutletRefreshment), -1.0)]
    [InlineData(nameof(StartingNeedsRules.OpenRefreshmentPerMinute), -0.05)]
    [InlineData(nameof(StartingNeedsRules.ClosedRefreshmentPerMinute), double.NaN)]
    [InlineData(nameof(StartingNeedsRules.DelayRefreshmentCap), -20.0)]
    [InlineData(nameof(StartingNeedsRules.BoredomPerMinute), double.PositiveInfinity)]
    [InlineData(nameof(StartingNeedsRules.BoredomCap), -1.0)]
    [InlineData(nameof(StartingNeedsRules.UneasePerMinute), -0.25)]
    [InlineData(nameof(StartingNeedsRules.UneaseGraceMinutes), -15.0)]
    [InlineData(nameof(StartingNeedsRules.UneaseCap), double.NaN)]
    [InlineData(nameof(BodyClockRules.RestPerHourAwake), -6.0)]
    [InlineData(nameof(BodyClockRules.EarlyWakeRest), -15.0)]
    public void NegativeAmountIsRefused(string field, double value) => AssertRefused(() => AmountFields[field](value), field);

    /// <summary>A need value outside [0, 100] is refused, naming the field.</summary>
    /// <param name="field">The field set.</param>
    /// <param name="value">The bad value.</param>
    [Theory]
    [InlineData(nameof(StartingNeedsRules.RefreshmentCeiling), 101.0)]
    [InlineData(nameof(BodyClockRules.StartingRestCap), -1.0)]
    [InlineData(nameof(TraitEffects.DefaultUneaseBaseline), double.NaN)]
    [InlineData(nameof(TraitEffect.UneaseBaseline), 100.5)]
    public void NeedValueOutsideTheScaleIsRefused(string field, double value) => AssertRefused(() => NeedValueFields[field](value), field);

    /// <summary>A minute count that is negative, or a time of day outside the day, is refused, naming the field.</summary>
    /// <param name="field">The field set.</param>
    /// <param name="value">The bad value.</param>
    [Theory]
    [InlineData(nameof(StartingNeedsRules.LateAndFedUpMinutes), -1)]
    [InlineData(nameof(BodyClockRules.EarlyWakeBeforeMinute), -1)]
    [InlineData(nameof(BodyClockRules.EarlyWakeBeforeMinute), 1441)]
    [InlineData(nameof(BoardingConditions.GateDelayMinutes), -1)]
    [InlineData(nameof(BoardingConditions.BoardingStartMinute), -1)]
    [InlineData(nameof(BoardingConditions.BoardingStartMinute), 1440)]
    public void MinuteOutOfRangeIsRefused(string field, int value) => AssertRefused(() => MinuteFields[field](value), field);

    /// <summary>A draw range that runs backwards or leaves [0, 100] is refused, naming the field.</summary>
    /// <param name="field">The field set.</param>
    /// <param name="min">The range's low end.</param>
    /// <param name="max">The range's high end.</param>
    [Theory]
    [InlineData(nameof(StartingNeedsRules.RefreshmentDraw), 40.0, 10.0)]
    [InlineData(nameof(StartingNeedsRules.BladderDraw), -1.0, 20.0)]
    [InlineData(nameof(StartingNeedsRules.BoredomDraw), 5.0, 101.0)]
    public void BadDrawRangeIsRefused(string field, double min, double max) =>
        AssertRefused(() => RangeFields[field](new NeedRange(min, max)), field);

    /// <summary>A missing part of the rules is refused, naming the field.</summary>
    /// <param name="field">The field left null.</param>
    [Theory]
    [InlineData(nameof(StartingNeedsRules.BodyClock))]
    [InlineData(nameof(BodyClockRules.RestRiseBands))]
    [InlineData(nameof(TraitEffects.Traits))]
    [InlineData(nameof(TraitEffect.Factors))]
    public void MissingPartIsRefused(string field) => AssertRefused(MissingParts[field], field);

    /// <summary>A Rest rise band that runs backwards, leaves the day, overlaps the one before or has a negative factor is refused.</summary>
    /// <param name="start">The second band's start.</param>
    /// <param name="end">The second band's end.</param>
    /// <param name="factor">The second band's factor.</param>
    [Theory]
    [InlineData(800, 780, 1.5)]
    [InlineData(780, 1441, 1.5)]
    [InlineData(700, 800, 1.5)]
    [InlineData(780, 960, -1.5)]
    public void BadRestRiseBandIsRefused(int start, int end, double factor) =>
        AssertRefused(
            () => ReferenceBodyClock() with { RestRiseBands = [new RestRiseBand(0, 720, 1.0), new RestRiseBand(start, end, factor)] },
            nameof(BodyClockRules.RestRiseBands)
        );

    /// <summary>A trait factor that is negative, or on a need that is not defined, is refused.</summary>
    /// <param name="need">The need the factor is on.</param>
    /// <param name="factor">The factor.</param>
    [Theory]
    [InlineData(Need.Refreshment, -0.5)]
    [InlineData((Need)99, 1.5)]
    public void BadTraitFactorIsRefused(Need need, double factor) =>
        AssertRefused(() => new TraitEffect { Factors = new Dictionary<Need, double> { [need] = factor } }, nameof(TraitEffect.Factors));

    /// <summary>The body clock of passengers.md section 6.</summary>
    /// <returns>The rules.</returns>
    internal static BodyClockRules ReferenceBodyClock() =>
        new()
        {
            RestPerHourAwake = 6,
            EarlyWakeBeforeMinute = 360,
            EarlyWakeRest = 15,
            StartingRestCap = 60,
            RestRiseBands = [new RestRiseBand(0, 720, 1.0), new RestRiseBand(720, 780, 1.2), new RestRiseBand(780, 960, 1.5)],
        };

    /// <summary>The starting-needs numbers of passengers.md section 2.</summary>
    /// <returns>The rules.</returns>
    internal static StartingNeedsRules Rules() =>
        new()
        {
            RefreshmentDraw = new NeedRange(10, 40),
            BladderDraw = new NeedRange(0, 20),
            BoredomDraw = new NeedRange(5, 25),
            GroupSpread = 10,
            ClosedOutletRefreshment = 10,
            OpenRefreshmentPerMinute = 0.05,
            ClosedRefreshmentPerMinute = 0.25,
            DelayRefreshmentCap = 20,
            BoredomPerMinute = 0.2,
            BoredomCap = 20,
            UneasePerMinute = 0.25,
            UneaseGraceMinutes = 15,
            UneaseCap = 15,
            LateAndFedUpMinutes = 30,
            RefreshmentCeiling = 70,
            BodyClock = ReferenceBodyClock(),
        };

    /// <summary>The gate conditions, with boarding starting at 10:30 origin local.</summary>
    /// <param name="delayMinutes">The gate delay.</param>
    /// <param name="concessionsOpen">Whether the outlets were open.</param>
    /// <returns>The conditions.</returns>
    internal static BoardingConditions Conditions(int delayMinutes, bool concessionsOpen) =>
        new()
        {
            GateDelayMinutes = delayMinutes,
            ConcessionsOpen = concessionsOpen,
            BoardingStartMinute = MorningBoardingStart,
        };

    /// <summary>Three traits: one with a Refreshment factor of 1.5 and no baseline, and two with Unease baselines of 25 and 40.</summary>
    /// <returns>The trait effects.</returns>
    internal static TraitEffects Traits() =>
        new()
        {
            DefaultUneaseBaseline = DefaultBaseline,
            Traits = new Dictionary<TraitId, TraitEffect>
            {
                [Hungry] = new() { Factors = new Dictionary<Need, double> { [Need.Refreshment] = 1.5 } },
                [Nervous] = new() { Factors = new Dictionary<Need, double>(), UneaseBaseline = 25 },
                [Anxious] = new() { Factors = new Dictionary<Need, double>(), UneaseBaseline = 40 },
            },
        };

    /// <summary>A manifest of bookings of the given sizes, passengers with no traits who woke at 06:30.</summary>
    /// <param name="bookingSizes">Each booking's member count, in order.</param>
    /// <returns>The manifest.</returns>
    internal static PassengerManifest Manifest(params int[] bookingSizes) =>
        Build([.. bookingSizes.Select(size => Enumerable.Repeat(Array.Empty<TraitId>(), size).ToArray())]);

    /// <summary>A manifest of one-passenger bookings, one for each trait list given.</summary>
    /// <param name="traits">Each passenger's traits.</param>
    /// <returns>The manifest.</returns>
    internal static PassengerManifest Singles(params TraitId[][] traits) => Build([.. traits.Select(passenger => new[] { passenger })]);

    /// <summary>One <c>passenger/&lt;id&gt;</c> stream per passenger, from the root seed.</summary>
    /// <param name="seed">The root seed.</param>
    /// <param name="count">The passenger count.</param>
    /// <returns>The streams, indexed by passenger id.</returns>
    internal static SimRandom[] Streams(ulong seed, int count)
    {
        var root = new RngRoot(seed);
        return [.. Enumerable.Range(0, count).Select(id => root.Stream($"passenger/{id}"))];
    }

    private static IReadOnlyList<PassengerStart> Draw(
        PassengerManifest manifest,
        BoardingConditions conditions,
        StartingNeedsRules rules,
        ulong seed
    ) => StartingNeeds.Draw(manifest, conditions, rules, Traits(), Streams(seed, manifest.Passengers.Count));

    private static PassengerManifest Build(TraitId[][][] bookings)
    {
        List<ManifestPassenger> passengers = [];
        List<Booking> bookingList = [];
        foreach (TraitId[][] members in bookings)
        {
            int[] ids = [.. Enumerable.Range(passengers.Count, members.Length)];
            passengers.AddRange(members.Select((traits, index) => Passenger(ids[index], bookingList.Count, traits)));
            bookingList.Add(new Booking(bookingList.Count, TripPurpose.Leisure, SeatClass.Economy, 390, ids));
        }

        return new PassengerManifest(passengers, bookingList);
    }

    private static ManifestPassenger Passenger(int id, int bookingId, TraitId[] traits) =>
        new()
        {
            Id = id,
            BookingId = bookingId,
            TripPurpose = TripPurpose.Leisure,
            AgeBand = AgeBand.Adult,
            SeatClass = SeatClass.Economy,
            Profession = null,
            Traits = traits,
            SeatNode = SeatNodes[id],
            WakeMinute = 390,
        };

    private static void AssertRefused(Func<object> build, string field)
    {
        ArgumentException error = Assert.ThrowsAny<ArgumentException>(build);
        Assert.Equal(field, error.ParamName);
    }
}
