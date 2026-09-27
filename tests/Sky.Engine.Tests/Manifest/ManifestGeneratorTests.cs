using System.Security.Cryptography;
using System.Text;
using CsCheck;
using Sky.Engine.Cabin;
using Sky.Engine.Manifest;
using Sky.Engine.Passengers;
using Sky.Engine.Randomness;
using Sky.Engine.Tests.Cabin;

namespace Sky.Engine.Tests.Manifest;

/// <summary>Pins the manifest generator against passengers.md sections 4 to 6 on the reference narrowbody (OD2).</summary>
public sealed class ManifestGeneratorTests
{
    private const int BusinessRows = 3;
    private const int SeedCount = 40;

    private static readonly TraitId Anxious = new(0);
    private static readonly TraitId NervousFlyer = new(1);
    private static readonly TraitId FrequentFlyer = new(2);
    private static readonly TraitId SmallBladder = new(3);
    private static readonly TraitId BigAppetite = new(4);
    private static readonly TraitId Restless = new(5);
    private static readonly TraitId Patient = new(6);
    private static readonly TraitId Demanding = new(7);
    private static readonly TraitId LightSleeper = new(8);
    private static readonly TraitId HeavySleeper = new(9);
    private static readonly TraitId Sociable = new(10);
    private static readonly TraitId Child = new(11);
    private static readonly TraitId ShortTempered = new(12);
    private static readonly TraitId SleepKit = new(13);
    private static readonly TraitId OwnDevice = new(14);

    private static readonly ProfessionId Retired = new(2);
    private static readonly ProfessionId Student = new(3);

    private static readonly CabinLayout Layout = ReferenceLayout();
    private static readonly NavGraph Graph = NavGraphBuilder.Build(Layout, 10);
    private static readonly ManifestRules Rules = ReferenceRules();
    private static readonly PassengerManifest[] Sampled = [.. Enumerable.Range(0, SeedCount).Select(seed => Generate((ulong)seed))];

    /// <summary>Each seat group's seat nodes, keyed by <see cref="GroupOf"/>, left to right.</summary>
    private static readonly Dictionary<int, int[]> GroupSeatNodes = Graph
        .SeatNodes.GroupBy(GroupOf)
        .ToDictionary(group => group.Key, group => group.ToArray());

    /// <summary>One seed gives one manifest, passenger for passenger and field for field.</summary>
    [Fact]
    public void SameSeedGivesSameManifest() => Assert.Equal(Fingerprint(Generate(7)), Fingerprint(Generate(7)));

    /// <summary>Two seeds give two different manifests.</summary>
    [Fact]
    public void DifferentSeedsGiveDifferentManifests() => Assert.NotEqual(Fingerprint(Generate(7)), Fingerprint(Generate(8)));

    /// <summary>Every passenger sits in a seat node, and no two passengers share one.</summary>
    [Fact]
    public void NoSeatIsBookedTwice()
    {
        foreach (PassengerManifest manifest in Sampled)
        {
            Assert.All(manifest.Passengers, passenger => Assert.Equal(NodeKind.Seat, Graph.Nodes[passenger.SeatNode].Kind));
            Assert.Equal(manifest.Passengers.Count, manifest.Passengers.Select(passenger => passenger.SeatNode).Distinct().Count());
        }
    }

    /// <summary>The load is 0.90 to 1.00 of the 180 seats: 162 to 180 passengers, whatever the seed.</summary>
    [Fact]
    public void LoadFactorIsWithinTheRuleRange() => Gen.ULong.Sample(seed => Generate(seed).Passengers.Count is >= 162 and <= 180);

    /// <summary>10 to 12 of the 12 business seats are booked, whatever the seed.</summary>
    [Fact]
    public void BusinessCountIsWithinTheRuleRange() =>
        Gen.ULong.Sample(seed => Generate(seed).Passengers.Count(passenger => passenger.SeatClass == SeatClass.Business) is >= 10 and <= 12);

    /// <summary>A business passenger sits in the first three rows and an economy passenger behind them, as their booking says.</summary>
    [Fact]
    public void EveryPassengerSitsInTheirBookedClass()
    {
        foreach (PassengerManifest manifest in Sampled)
        {
            foreach (ManifestPassenger passenger in manifest.Passengers)
            {
                Assert.Equal(ClassOf(passenger.SeatNode), passenger.SeatClass);
                Assert.Equal(manifest.Bookings[passenger.BookingId].SeatClass, passenger.SeatClass);
            }
        }
    }

    /// <summary>
    /// Replaying the seating in booking order, a booking of three seated while a seat group of its class still had three free
    /// seats sits wholly in one seat group.
    /// </summary>
    [Fact]
    public void AGroupOfThreeLandsInOneSeatGroupWhenOneIsFree()
    {
        int checkedBookings = 0;
        foreach (PassengerManifest manifest in Sampled)
        {
            HashSet<int> taken = [];
            foreach (Booking booking in manifest.Bookings)
            {
                int[] seats = [.. booking.PassengerIds.Select(id => manifest.Passengers[id].SeatNode)];
                if (seats.Length == 3 && AnyGroupWithFreeSeats(booking.SeatClass, 3, taken))
                {
                    Assert.Single(seats.Select(GroupOf).Distinct());
                    checkedBookings++;
                }

                taken.UnionWith(seats);
            }
        }

        Assert.True(checkedBookings > 0, "No booking of three was checked.");
    }

    /// <summary>
    /// In every seat group holding an adult and a child of one booking, the aisle-most seat that booking holds in the group —
    /// toward the group's edge nearer the aisle's centre — belongs to an adult of that booking. A seat group may hold another
    /// booking's passengers on its aisle side, so the rule is read on the booking's own seats in the group.
    /// </summary>
    [Fact]
    public void AFamilysAdultTakesTheAisleSeat()
    {
        int groupsWithBoth = 0;
        foreach (PassengerManifest manifest in Sampled)
        {
            foreach (Booking booking in manifest.Bookings)
            {
                ManifestPassenger[] members = [.. booking.PassengerIds.Select(id => manifest.Passengers[id])];
                foreach (ManifestPassenger[] group in members.GroupBy(member => GroupOf(member.SeatNode)).Select(seats => seats.ToArray()))
                {
                    if (!group.Any(IsChild) || !group.Any(member => !IsChild(member)))
                    {
                        continue;
                    }

                    int aisleSeat = AisleFirstSeats(group[0].SeatNode).First(seat => group.Any(member => member.SeatNode == seat));
                    Assert.True(
                        group.Any(member => !IsChild(member) && member.SeatNode == aisleSeat),
                        $"Booking {booking.Id} seats a child in {aisleSeat}, the aisle-most seat it holds in its seat group."
                    );
                    groupsWithBoth++;
                }
            }
        }

        Assert.True(groupsWithBoth > 0, "No seat group held an adult and a child of one booking.");
    }

    /// <summary>
    /// A booking with a child never has a seat group holding only children of it while another holds two or more of its seats
    /// and an adult of it but no child of it. Families that spill over seat groups are checked.
    /// </summary>
    [Fact]
    public void AChildSitsBesideAnAdultOfTheirBookingWhenTheSeatGroupHoldsThem()
    {
        int spilledFamilies = 0;
        foreach (PassengerManifest manifest in Sampled)
        {
            foreach (Booking booking in manifest.Bookings)
            {
                ManifestPassenger[] members = [.. booking.PassengerIds.Select(id => manifest.Passengers[id])];
                if (!members.Any(IsChild))
                {
                    continue;
                }

                ManifestPassenger[][] groups = [.. members.GroupBy(member => GroupOf(member.SeatNode)).Select(group => group.ToArray())];
                spilledFamilies += groups.Length > 1 ? 1 : 0;
                AssertChildrenSpreadOverAdults(booking, groups);
            }
        }

        Assert.True(spilledFamilies > 0, "No family spilled over seat groups.");
    }

    /// <summary>At a load of 1.00 every seat is taken, all 12 business seats included.</summary>
    [Fact]
    public void FullLoadSeatsEveryoneAndTwelveInBusiness()
    {
        ManifestRules full = Rules with { LoadFactor = new ShareRange(1, 1) };
        for (ulong seed = 0; seed < SeedCount; seed++)
        {
            PassengerManifest manifest = ManifestGenerator.Generate(Layout, Graph, full, new RngRoot(seed));
            Assert.Equal(180, manifest.Passengers.Count);
            Assert.Equal(12, manifest.Passengers.Count(passenger => passenger.SeatClass == SeatClass.Business));
        }
    }

    /// <summary>
    /// A booking with a child has exactly the family's two adults, every member shares the booking's trip purpose, and the
    /// business bookings come before the economy ones.
    /// </summary>
    [Fact]
    public void FamiliesHaveTheirAdultsAndMembersShareTheirBookingsPurpose()
    {
        int families = 0;
        foreach (PassengerManifest manifest in Sampled)
        {
            foreach (Booking booking in manifest.Bookings)
            {
                ManifestPassenger[] members = [.. booking.PassengerIds.Select(id => manifest.Passengers[id])];
                Assert.All(members, member => Assert.Equal(booking.TripPurpose, member.TripPurpose));
                if (members.Any(IsChild))
                {
                    Assert.Equal(Rules.FamilyAdults, members.Count(member => !IsChild(member)));
                    families++;
                }
            }

            int lastBusiness = manifest.Bookings.Count(booking => booking.SeatClass == SeatClass.Business) - 1;
            Assert.All(manifest.Bookings, booking => Assert.Equal(booking.Id <= lastBusiness, booking.SeatClass == SeatClass.Business));
        }

        Assert.True(families > 0, "No family was drawn.");
    }

    /// <summary>
    /// Pins seed 1's whole manifest by its passenger count and the first 64 bits of its fingerprint's SHA-256. A change here
    /// changes every recorded flight's manifest and so breaks replays: change the pin only with a change that means to.
    /// </summary>
    [Fact]
    public void SeedOneManifestIsPinned()
    {
        PassengerManifest manifest = Generate(1);
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Fingerprint(manifest))))[..16];
        Assert.Equal("180:EACB494936ADC814", $"{manifest.Passengers.Count}:{hash}");
    }

    /// <summary>
    /// Generating refuses rules whose ids clash across fields, naming the field: the child trait or a belonging among the
    /// drawable traits, or an id listed twice.
    /// </summary>
    [Fact]
    public void ChildTraitOrBelongingInTraitsIsRefused()
    {
        TraitRule[] traits = TraitRules();
        AssertGenerateRejects(nameof(ManifestRules.ChildTrait), Rules with { Traits = [.. traits, new TraitRule(Child, 1, 1, false)] });
        AssertGenerateRejects(nameof(ManifestRules.Belongings), Rules with { Traits = [.. traits, new TraitRule(SleepKit, 1, 1, false)] });
        AssertGenerateRejects(nameof(ManifestRules.Belongings), Rules with { Belongings = [.. Rules.Belongings, new BelongingRule(Child, 0, 0, 0)] });
        AssertGenerateRejects(nameof(ManifestRules.Belongings), Rules with { Belongings = [.. Rules.Belongings, Rules.Belongings[0]] });
        AssertGenerateRejects(nameof(ManifestRules.Traits), Rules with { Traits = [.. traits, traits[0]] });
        AssertGenerateRejects(nameof(ManifestRules.Professions), Rules with { Professions = [.. Rules.Professions, Rules.Professions[0]] });
    }

    /// <summary>No passenger carries both traits of a forbidden pair.</summary>
    [Fact]
    public void ForbiddenTraitPairsNeverCoOccur()
    {
        int twoTraitAdults = 0;
        foreach (ManifestPassenger passenger in AllPassengers())
        {
            TraitId[] traits = passenger.Traits.ToArray();
            foreach (TraitPair pair in Rules.ForbiddenPairs)
            {
                Assert.False(traits.Contains(pair.First) && traits.Contains(pair.Second), $"Passenger {passenger.Id} carries {pair}.");
            }

            twoTraitAdults += Personality(traits).Length == 2 ? 1 : 0;
        }

        Assert.True(twoTraitAdults > 0, "No adult drew two traits.");
    }

    /// <summary>A child carries <c>child</c> first, then at most one trait from the child-allowed list, then belongings.</summary>
    [Fact]
    public void ChildrenCarryTheChildTraitAndOnlyAllowedExtras()
    {
        HashSet<TraitId> allowed = [.. Rules.Traits.Where(rule => rule.ChildMayDraw).Select(rule => rule.Trait)];
        int childrenWithExtra = 0;
        foreach (ManifestPassenger child in AllPassengers().Where(passenger => passenger.AgeBand == AgeBand.Child))
        {
            TraitId[] traits = child.Traits.ToArray();
            Assert.Equal(Child, traits[0]);
            TraitId[] extras = Personality(traits[1..]);
            Assert.True(extras.Length <= 1, $"Child {child.Id} drew {extras.Length} extra traits.");
            Assert.All(extras, extra => Assert.Contains(extra, allowed));
            childrenWithExtra += extras.Length;
        }

        Assert.True(childrenWithExtra > 0, "No child drew an extra trait.");
    }

    /// <summary>A child never carries a sleep kit, though adults do.</summary>
    [Fact]
    public void SleepKitIsNeverOnAChild()
    {
        ManifestPassenger[] passengers = [.. AllPassengers()];
        Assert.All(
            passengers.Where(passenger => passenger.AgeBand == AgeBand.Child),
            child => Assert.DoesNotContain(SleepKit, child.Traits.ToArray())
        );
        Assert.Contains(passengers, passenger => passenger.Traits.ToArray().Contains(SleepKit));
    }

    /// <summary>A passenger on a business trip is an adult, and never retired or a student.</summary>
    [Fact]
    public void BusinessTripsHaveNoChildrenAndNoRetiredOrStudent()
    {
        ManifestPassenger[] travellers = [.. AllPassengers().Where(passenger => passenger.TripPurpose == TripPurpose.Business)];
        Assert.NotEmpty(travellers);
        Assert.All(
            travellers,
            traveller =>
            {
                Assert.Equal(AgeBand.Adult, traveller.AgeBand);
                Assert.NotEqual(Retired, traveller.Profession);
                Assert.NotEqual(Student, traveller.Profession);
            }
        );
    }

    /// <summary>A child has no profession and every adult has one.</summary>
    [Fact]
    public void ChildrenHaveNoProfession()
    {
        ManifestPassenger[] passengers = [.. AllPassengers()];
        Assert.Contains(passengers, passenger => passenger.AgeBand == AgeBand.Child);
        Assert.All(passengers, passenger => Assert.Equal(passenger.AgeBand == AgeBand.Adult, passenger.Profession.HasValue));
    }

    /// <summary>A booking's wake time falls in its purpose's range, and each member wakes 0 to 15 minutes after it.</summary>
    [Fact]
    public void WakeTimesFallInTheirPurposeRangeAndWithinFifteenMinutesOfTheBooking()
    {
        foreach (PassengerManifest manifest in Sampled)
        {
            foreach (Booking booking in manifest.Bookings)
            {
                IntRange range = WakeRange(booking.TripPurpose);
                Assert.InRange(booking.WakeMinute, range.Min, range.Max);
                Assert.All(booking.PassengerIds, id => Assert.InRange(manifest.Passengers[id].WakeMinute - booking.WakeMinute, 0, 15));
            }
        }
    }

    /// <summary>A rule out of its range fails with an <see cref="ArgumentException"/> whose parameter is the field's name.</summary>
    [Fact]
    public void InvalidRulesFailNamingTheField()
    {
        AssertRejects(nameof(ManifestRules.LoadFactor), () => Rules with { LoadFactor = new ShareRange(0.95, 0.90) });
        AssertRejects(nameof(ManifestRules.LoadFactor), () => Rules with { LoadFactor = new ShareRange(0.90, 1.10) });
        AssertRejects(nameof(ManifestRules.BusinessBooked), () => Rules with { BusinessBooked = new IntRange(12, 10) });
        AssertRejects(nameof(ManifestRules.BusinessRowCount), () => Rules with { BusinessRowCount = -1 });
        AssertRejects(nameof(ManifestRules.EconomyCabinPurposes), () => Rules with { EconomyCabinPurposes = [new(TripPurpose.Leisure, 0)] });
        AssertRejects(nameof(ManifestRules.AdultTraitCounts), () => Rules with { AdultTraitCounts = [new(1, -5)] });
        AssertRejects(nameof(ManifestRules.ChildExtraTraitShare), () => Rules with { ChildExtraTraitShare = 1.5 });
        AssertRejects(nameof(ManifestRules.Traits), () => Rules with { Traits = [new TraitRule(Anxious, -1, 12, true)] });
        AssertRejects(nameof(ManifestRules.Belongings), () => Rules with { Belongings = [new BelongingRule(SleepKit, 0.15, 0.25, -0.1)] });
        AssertRejects(nameof(ManifestRules.Professions), () => Rules with { Professions = [new ProfessionRule(Retired, double.NaN, false)] });
        AssertRejects(nameof(ManifestRules.Professions), () => Rules with { Professions = [new ProfessionRule(Retired, 12, false)] });
        AssertRejects(nameof(TripPurposeRules.WakeMinutes), () => Rules.LeisureTrip with { WakeMinutes = new IntRange(510, 360) });
        AssertRejects(nameof(TripPurposeRules.GroupSizes), () => Rules.LeisureTrip with { GroupSizes = [new(0, 1)] });
        AssertRejects(nameof(TripPurposeRules.FamilyShare), () => Rules.LeisureTrip with { FamilyShare = -0.1 });
        AssertRejects(nameof(ManifestRules.LoadFactor), () => Rules with { LoadFactor = new ShareRange(double.NaN, 1) });
        AssertRejects(nameof(ManifestRules.FamilyMinimumSize), () => Rules with { FamilyMinimumSize = 0 });
        AssertRejects(nameof(ManifestRules.FamilyAdults), () => Rules with { FamilyAdults = 0 });
        AssertRejects(nameof(ManifestRules.WakeSpreadMinutes), () => Rules with { WakeSpreadMinutes = -1 });
        AssertRejects(nameof(ManifestRules.BusinessCabinPurposes), () => Rules with { BusinessCabinPurposes = [new((TripPurpose)9, 1)] });
        AssertRejects(nameof(ManifestRules.LeisureTrip), () => Rules with { LeisureTrip = null! });
    }

    /// <summary>
    /// Generating refuses a layout with fewer rows than the business rows (naming the layout) and a graph not built from the
    /// layout, by shape or by seat label (naming the graph).
    /// </summary>
    [Fact]
    public void GenerateRefusesALayoutAndGraphThatDoNotMatch()
    {
        CabinLayout relabeled = Layout with
        {
            Rows =
            [
                new CabinRow(38, [NavGraphBuilderTests.Group(6, 26, "A", "B"), NavGraphBuilderTests.Group(90, 26, "D", "F")]),
                .. Layout.Rows.Skip(1),
            ],
        };
        NavGraph twoRowGraph = NavGraphBuilder.Build(NavGraphBuilderTests.TwoRowLayout(), 10);

        AssertRejects("layout", () => ManifestGenerator.Generate(Layout, Graph, Rules with { BusinessRowCount = 40 }, new RngRoot(1)));
        AssertRejects("graph", () => ManifestGenerator.Generate(Layout, twoRowGraph, Rules, new RngRoot(1)));
        AssertRejects("graph", () => ManifestGenerator.Generate(relabeled, Graph, Rules, new RngRoot(1)));
    }

    /// <summary>Generating refuses a layout with no aisle at all, naming the layout.</summary>
    [Fact]
    public void GenerateRefusesALayoutWithNoAisle() =>
        AssertRejects("layout", () => ManifestGenerator.Generate(Layout with { Aisles = [] }, Graph, Rules, new RngRoot(1)));

    /// <summary>
    /// Builds the reference narrowbody of OD2: 3 rows of 2-2 business ahead of 28 rows of 3-3 economy, one aisle, the forward
    /// door and lav at row 0, the aft lav and galley at row 30.
    /// </summary>
    /// <returns>The layout.</returns>
    internal static CabinLayout ReferenceLayout()
    {
        CabinRow business = new(38, [NavGraphBuilderTests.Group(6, 26, "A", "C"), NavGraphBuilderTests.Group(90, 26, "D", "F")]);
        CabinRow economy = new(31, [NavGraphBuilderTests.Group(4, 18, "A", "B", "C"), NavGraphBuilderTests.Group(90, 18, "D", "E", "F")]);
        return new CabinLayout(
            "reference-narrowbody",
            148,
            [.. Enumerable.Repeat(business, BusinessRows), .. Enumerable.Repeat(economy, 28)],
            [new Aisle(74, 20)],
            [
                new CabinFixture("door-1L", FixtureKind.Door, 0, 0, 30),
                new CabinFixture("lav-fwd", FixtureKind.Lav, 0, 0, 4),
                new CabinFixture("lav-aft", FixtureKind.Lav, 30, 0, 4),
                new CabinFixture("galley-aft", FixtureKind.Galley, 30, 0, 20),
            ]
        );
    }

    /// <summary>Builds the manifest rules with the numbers of passengers.md sections 4 to 6, verbatim.</summary>
    /// <returns>The rules.</returns>
    internal static ManifestRules ReferenceRules() =>
        new()
        {
            LoadFactor = new ShareRange(0.90, 1.00),
            BusinessBooked = new IntRange(10, 12),
            BusinessRowCount = BusinessRows,
            BusinessCabinPurposes = [new(TripPurpose.Business, 70), new(TripPurpose.Leisure, 30)],
            EconomyCabinPurposes = [new(TripPurpose.Business, 20), new(TripPurpose.Leisure, 55), new(TripPurpose.Visiting, 25)],
            BusinessTrip = new TripPurposeRules
            {
                GroupSizes = [new(1, 80), new(2, 20)],
                FamilyShare = 0,
                WakeMinutes = new IntRange(300, 390),
            },
            LeisureTrip = new TripPurposeRules
            {
                GroupSizes = [new(1, 20), new(2, 45), new(3, 15), new(4, 15), new(5, 5)],
                FamilyShare = 0.60,
                WakeMinutes = new IntRange(360, 510),
            },
            VisitingTrip = new TripPurposeRules
            {
                GroupSizes = [new(1, 45), new(2, 25), new(3, 15), new(4, 15)],
                FamilyShare = 0.70,
                WakeMinutes = new IntRange(330, 480),
            },
            FamilyMinimumSize = 3,
            FamilyAdults = 2,
            WakeSpreadMinutes = 15,
            AdultTraitCounts = [new(0, 35), new(1, 45), new(2, 20)],
            ChildTrait = Child,
            ChildExtraTraitShare = 0.40,
            Traits = TraitRules(),
            ForbiddenPairs = [new(NervousFlyer, FrequentFlyer), new(LightSleeper, HeavySleeper), new(Patient, Demanding)],
            Belongings = [new BelongingRule(SleepKit, 0.15, 0.25, 0), new BelongingRule(OwnDevice, 0.35, 0.60, 0.40)],
            Professions =
            [
                new ProfessionRule(new ProfessionId(0), 40, true),
                new ProfessionRule(new ProfessionId(1), 15, true),
                new ProfessionRule(Retired, 12, false),
                new ProfessionRule(Student, 10, false),
                new ProfessionRule(new ProfessionId(4), 8, true),
                new ProfessionRule(new ProfessionId(5), 3, true),
                new ProfessionRule(new ProfessionId(6), 2, true),
                new ProfessionRule(new ProfessionId(7), 1, true),
                new ProfessionRule(new ProfessionId(8), 9, true),
            ],
        };

    private static TraitRule[] TraitRules() =>
        [
            new(Anxious, 12, 12, true),
            new(NervousFlyer, 10, 5, false),
            new(FrequentFlyer, 8, 30, false),
            new(SmallBladder, 10, 10, true),
            new(BigAppetite, 10, 10, false),
            new(Restless, 12, 12, true),
            new(Patient, 10, 10, false),
            new(Demanding, 8, 8, false),
            new(LightSleeper, 12, 12, true),
            new(HeavySleeper, 8, 8, true),
            new(Sociable, 15, 15, false),
            new(ShortTempered, 8, 8, false),
        ];

    private static PassengerManifest Generate(ulong seed) => ManifestGenerator.Generate(Layout, Graph, Rules, new RngRoot(seed));

    private static IEnumerable<ManifestPassenger> AllPassengers() => Sampled.SelectMany(manifest => manifest.Passengers);

    private static TraitId[] Personality(TraitId[] traits) =>
        [.. traits.Where(trait => trait != Child && !Rules.Belongings.Any(belonging => belonging.Belonging == trait))];

    private static IntRange WakeRange(TripPurpose purpose) =>
        purpose switch
        {
            TripPurpose.Business => Rules.BusinessTrip.WakeMinutes,
            TripPurpose.Leisure => Rules.LeisureTrip.WakeMinutes,
            _ => Rules.VisitingTrip.WakeMinutes,
        };

    private static SeatClass ClassOf(int seatNode) => Graph.Nodes[seatNode].RowIndex < BusinessRows ? SeatClass.Business : SeatClass.Economy;

    /// <summary>A seat group key: the row times two, plus 1 for the group right of the aisle (seats D to F).</summary>
    private static int GroupOf(int seatNode)
    {
        NavNode node = Graph.Nodes[seatNode];
        return (node.RowIndex * 2) + (node.Label[0] < 'D' ? 0 : 1);
    }

    /// <summary>The seat group's seat nodes from its aisle end: the group's edge nearer the nearest aisle's centre.</summary>
    private static int[] AisleFirstSeats(int seatNode)
    {
        int[] seats = GroupSeatNodes[GroupOf(seatNode)];
        return AisleAtRight(seatNode) ? [.. seats.Reverse()] : seats;
    }

    /// <summary>
    /// Whether the seat group holding <paramref name="seatNode"/> has the aisle to its right: on the reference layout the A
    /// to C groups do and the D to F groups do not, as <see cref="GroupOf"/> encodes.
    /// </summary>
    private static bool AisleAtRight(int seatNode) => Graph.Nodes[seatNode].Label[0] < 'D';

    private static bool AnyGroupWithFreeSeats(SeatClass seatClass, int count, HashSet<int> taken) =>
        Graph.SeatNodes.Where(seat => ClassOf(seat) == seatClass && !taken.Contains(seat)).GroupBy(GroupOf).Any(group => group.Count() >= count);

    private static void AssertRejects(string field, Func<object> build)
    {
        ArgumentException error = Assert.ThrowsAny<ArgumentException>(() => build());
        Assert.Equal(field, error.ParamName);
    }

    private static void AssertGenerateRejects(string field, ManifestRules rules) =>
        AssertRejects(field, () => ManifestGenerator.Generate(Layout, Graph, rules, new RngRoot(1)));

    private static bool IsChild(ManifestPassenger passenger) => passenger.AgeBand == AgeBand.Child;

    /// <summary>
    /// Asserts that when a seat group holds only children of the booking, no seat group holding two or more of its seats and
    /// an adult of it is left without a child of it too.
    /// </summary>
    private static void AssertChildrenSpreadOverAdults(Booking booking, ManifestPassenger[][] groups)
    {
        if (!groups.Any(group => group.All(IsChild)))
        {
            return;
        }

        foreach (ManifestPassenger[] group in groups.Where(group => group.Length >= 2 && group.Any(member => !IsChild(member))))
        {
            Assert.True(
                group.Any(IsChild),
                $"Booking {booking.Id} holds {group.Length} seats in group {GroupOf(group[0].SeatNode)} with an adult and no child."
            );
        }
    }

    private static string Fingerprint(PassengerManifest manifest) =>
        string.Join(
            ';',
            manifest.Passengers.Select(passenger =>
                string.Join(
                    ',',
                    passenger.Id,
                    passenger.BookingId,
                    passenger.TripPurpose,
                    passenger.AgeBand,
                    passenger.SeatClass,
                    passenger.Profession?.Value ?? -1,
                    string.Join('+', passenger.Traits.ToArray().Select(trait => trait.Value)),
                    passenger.SeatNode,
                    passenger.WakeMinute
                )
            )
        );
}
