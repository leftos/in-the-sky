using Sky.Engine.Cabin;
using Sky.Engine.Passengers;
using Sky.Engine.Randomness;

namespace Sky.Engine.Manifest;

/// <summary>
/// Draws a flight's passengers from the <c>manifest</c> stream and seats them (passengers.md sections 1, 5 and 6). The draws
/// run in one fixed order: the load factor, the business count, then booking by booking (business class first, then economy)
/// the trip purpose, the group size, the family roll (drawn only when the trip is not <see cref="TripPurpose.Business"/> and
/// the drawn size reaches <see cref="ManifestRules.FamilyMinimumSize"/>), the booking's wake time, and member by member in
/// drafted order (adult, child, adult, child, then whoever is left) the wake offset, the traits, the belongings and the
/// profession. Seating draws nothing.
/// </summary>
public static class ManifestGenerator
{
    /// <summary>The name of the stream every manifest draw comes from (R5).</summary>
    public const string StreamName = "manifest";

    /// <summary>Draws and seats a manifest.</summary>
    /// <param name="layout">The cabin; its first <see cref="ManifestRules.BusinessRowCount"/> rows are business class.</param>
    /// <param name="graph">The navigation graph built from <paramref name="layout"/>; passengers are seated on its seat nodes.</param>
    /// <param name="rules">The shares, ranges and weights to draw by.</param>
    /// <param name="rng">The flight's randomness root.</param>
    /// <returns>The passengers in seating order and their bookings in draw order.</returns>
    /// <exception cref="ArgumentException">The layout has fewer rows than the business row count, or the graph's seats are not
    /// the layout's.</exception>
    public static PassengerManifest Generate(CabinLayout layout, NavGraph graph, ManifestRules rules, RngRoot rng)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(rng);
        rules.CheckAcrossFields();

        var plan = SeatPlan.From(layout, graph, rules.BusinessRowCount);
        SimRandom random = rng.Stream(StreamName);
        (int business, int economy) = DrawTargets(rules, plan, random);
        BookingDraw draw = new(rules, random);
        List<BookingDraft> drafts = [];
        draw.Fill(SeatClass.Business, business, drafts);
        draw.Fill(SeatClass.Economy, economy, drafts);
        return plan.Seat(drafts);
    }

    /// <summary>
    /// Draws the load and splits it into class targets. The business count is raised when economy cannot hold the rest of the
    /// load, then capped at the business seats and the load; economy takes the rest, capped at its seats.
    /// </summary>
    private static (int Business, int Economy) DrawTargets(ManifestRules rules, SeatPlan plan, SimRandom random)
    {
        double loadFactor = rules.LoadFactor.Min + (random.NextDouble() * (rules.LoadFactor.Max - rules.LoadFactor.Min));
        int total = (int)Math.Round(loadFactor * plan.SeatCount(), MidpointRounding.AwayFromZero);
        int business = UniformIn(rules.BusinessBooked, random);
        int economySeats = plan.SeatCount(SeatClass.Economy);
        business = Math.Max(business, total - economySeats);
        business = Math.Min(Math.Min(business, plan.SeatCount(SeatClass.Business)), total);
        return (business, Math.Min(total - business, economySeats));
    }

    private static int UniformIn(IntRange range, SimRandom random) => range.Min + random.NextInt(range.Max - range.Min + 1);

    /// <summary>Orders members adult, child, adult, child, and so on, with whoever is left over last.</summary>
    private static AgeBand[] SeatingOrder(int adults, int children)
    {
        var order = new AgeBand[adults + children];
        int adultsLeft = adults;
        for (int i = 0; i < order.Length; i++)
        {
            bool adultTurn = adultsLeft > 0 && (i % 2 == 0 || i - (adults - adultsLeft) >= children);
            order[i] = adultTurn ? AgeBand.Adult : AgeBand.Child;
            adultsLeft -= adultTurn ? 1 : 0;
        }

        return order;
    }

    /// <summary>A booking drawn but not yet seated; its members are in draw order.</summary>
    private sealed record BookingDraft(SeatClass SeatClass, TripPurpose TripPurpose, int WakeMinute, MemberDraft[] Members);

    /// <summary>A passenger drawn but not yet seated.</summary>
    private sealed record MemberDraft(AgeBand AgeBand, TraitId[] Traits, ProfessionId? Profession, int WakeMinute);

    /// <summary>Draws bookings and their members from the manifest stream.</summary>
    private sealed class BookingDraw(ManifestRules rules, SimRandom random)
    {
        /// <summary>Draws bookings of one class until <paramref name="target"/> passengers are booked in it.</summary>
        public void Fill(SeatClass seatClass, int target, List<BookingDraft> drafts)
        {
            IReadOnlyList<WeightedOption<TripPurpose>> purposes =
                seatClass == SeatClass.Business ? rules.BusinessCabinPurposes : rules.EconomyCabinPurposes;
            int remaining = target;
            while (remaining > 0)
            {
                BookingDraft draft = DrawBooking(seatClass, Pick(purposes), remaining);
                drafts.Add(draft);
                remaining -= draft.Members.Length;
            }
        }

        /// <summary>
        /// Draws one booking. A family is decided on the drawn size; a booking larger than <paramref name="remaining"/> is then
        /// cut to it, children first.
        /// </summary>
        private BookingDraft DrawBooking(SeatClass seatClass, TripPurpose purpose, int remaining)
        {
            TripPurposeRules trip = rules.For(purpose);
            int size = Pick(trip.GroupSizes);
            bool family = purpose != TripPurpose.Business && size >= rules.FamilyMinimumSize && random.Chance(trip.FamilyShare);
            int kept = Math.Min(size, remaining);
            int adults = Math.Min(family ? rules.FamilyAdults : size, kept);
            int wake = UniformIn(trip.WakeMinutes, random);
            AgeBand[] order = SeatingOrder(adults, kept - adults);
            var members = new MemberDraft[order.Length];
            for (int i = 0; i < order.Length; i++)
            {
                members[i] = DrawMember(order[i], purpose, wake);
            }

            return new BookingDraft(seatClass, purpose, wake, members);
        }

        private MemberDraft DrawMember(AgeBand ageBand, TripPurpose purpose, int bookingWake)
        {
            int wake = bookingWake + random.NextInt(rules.WakeSpreadMinutes + 1);
            bool businessTrip = purpose == TripPurpose.Business;
            List<TraitId> traits = [];
            if (ageBand == AgeBand.Child)
            {
                DrawChildTraits(traits);
            }
            else
            {
                DrawAdultTraits(traits, businessTrip);
            }

            DrawBelongings(traits, ageBand, businessTrip);
            ProfessionId? profession = ageBand == AgeBand.Adult ? DrawProfession(businessTrip) : null;
            return new MemberDraft(ageBand, [.. traits], profession, wake);
        }

        private void DrawAdultTraits(List<TraitId> traits, bool businessTrip)
        {
            int count = Pick(rules.AdultTraitCounts);
            for (int i = 0; i < count && TryPickTrait(traits, businessTrip, childDraw: false, out TraitId trait); i++)
            {
                traits.Add(trait);
            }
        }

        private void DrawChildTraits(List<TraitId> traits)
        {
            traits.Add(rules.ChildTrait);
            if (random.Chance(rules.ChildExtraTraitShare) && TryPickTrait(traits, businessTrip: false, childDraw: true, out TraitId trait))
            {
                traits.Add(trait);
            }
        }

        /// <summary>
        /// Draws one trait by weight among those not yet drawn and forming no forbidden pair with one drawn; reports false,
        /// drawing nothing, when no candidate carries weight.
        /// </summary>
        private bool TryPickTrait(List<TraitId> drawn, bool businessTrip, bool childDraw, out TraitId trait)
        {
            IReadOnlyList<TraitRule> candidates = rules.Traits;
            double[] weights = new double[candidates.Count];
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = CandidateWeight(candidates[i], drawn, businessTrip, childDraw);
            }

            int index = PickIndex(weights);
            trait = index < 0 ? default : candidates[index].Trait;
            return index >= 0;
        }

        private double CandidateWeight(TraitRule candidate, List<TraitId> drawn, bool businessTrip, bool childDraw)
        {
            if ((childDraw && !candidate.ChildMayDraw) || drawn.Contains(candidate.Trait) || IsForbiddenWith(candidate.Trait, drawn))
            {
                return 0;
            }

            return businessTrip ? candidate.BusinessTripWeight : candidate.AdultWeight;
        }

        /// <summary>Whether <paramref name="trait"/> forms a forbidden pair with any trait already drawn.</summary>
        private bool IsForbiddenWith(TraitId trait, List<TraitId> drawn)
        {
            foreach (TraitPair pair in rules.ForbiddenPairs)
            {
                if ((pair.First == trait && drawn.Contains(pair.Second)) || (pair.Second == trait && drawn.Contains(pair.First)))
                {
                    return true;
                }
            }

            return false;
        }

        private void DrawBelongings(List<TraitId> traits, AgeBand ageBand, bool businessTrip)
        {
            foreach (BelongingRule belonging in rules.Belongings)
            {
                double share =
                    ageBand == AgeBand.Child ? belonging.ChildShare
                    : businessTrip ? belonging.BusinessTripShare
                    : belonging.AdultShare;
                if (random.Chance(share))
                {
                    traits.Add(belonging.Belonging);
                }
            }
        }

        private ProfessionId DrawProfession(bool businessTrip)
        {
            IReadOnlyList<ProfessionRule> professions = rules.Professions;
            double[] weights = new double[professions.Count];
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = !businessTrip || professions[i].OnBusinessTrips ? professions[i].Weight : 0;
            }

            return professions[PickIndex(weights)].Profession;
        }

        private T Pick<T>(IReadOnlyList<WeightedOption<T>> options)
        {
            double[] weights = new double[options.Count];
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = options[i].Weight;
            }

            return options[PickIndex(weights)].Value;
        }

        /// <summary>
        /// Draws an index with probability proportional to its weight, with one draw; returns -1, drawing nothing, when the
        /// weights sum to 0.
        /// </summary>
        private int PickIndex(double[] weights)
        {
            double total = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                total += weights[i];
            }

            if (total <= 0)
            {
                return -1;
            }

            double pick = random.NextDouble() * total;
            int last = -1;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] > 0)
                {
                    last = i;
                    pick -= weights[i];
                    if (pick < 0)
                    {
                        return i;
                    }
                }
            }

            return last;
        }
    }

    /// <summary>
    /// The layout's seat groups by class, each a list of seat node ids running from the group's aisle end, row by row from
    /// the front.
    /// </summary>
    private sealed class SeatPlan
    {
        private readonly List<int[]> businessGroups = [];
        private readonly List<int[]> economyGroups = [];

        /// <summary>Pairs the layout's seat groups with the graph's seat nodes, which the builder numbers in the same order.</summary>
        public static SeatPlan From(CabinLayout layout, NavGraph graph, int businessRowCount)
        {
            if (layout.Aisles.Count == 0)
            {
                throw new ArgumentException(
                    $"The layout '{layout.Id}' has no aisle; a layout needs at least one aisle to seat passengers.",
                    nameof(layout)
                );
            }

            if (businessRowCount > layout.Rows.Count)
            {
                throw new ArgumentException(
                    $"The rules make {businessRowCount} rows business class, but the layout '{layout.Id}' has {layout.Rows.Count}.",
                    nameof(layout)
                );
            }

            SeatPlan plan = new();
            int next = 0;
            for (int row = 0; row < layout.Rows.Count; row++)
            {
                List<int[]> groups = row < businessRowCount ? plan.businessGroups : plan.economyGroups;
                foreach (SeatGroup group in layout.Rows[row].Groups)
                {
                    groups.Add(AisleFirst(layout, group, GroupSeats(graph, row, group, ref next)));
                }
            }

            if (next != graph.SeatNodes.Count)
            {
                throw new ArgumentException($"The graph has more seats than the layout '{layout.Id}'; build it from this layout.", nameof(graph));
            }

            return plan;
        }

        public int SeatCount() => SeatCount(SeatClass.Business) + SeatCount(SeatClass.Economy);

        public int SeatCount(SeatClass seatClass) => GroupsOf(seatClass).Sum(group => group.Length);

        /// <summary>Seats every booking in draw order and numbers the passengers in the order they sit down.</summary>
        public PassengerManifest Seat(List<BookingDraft> drafts)
        {
            HashSet<int> taken = [];
            List<ManifestPassenger> passengers = [];
            List<Booking> bookings = [];
            foreach (BookingDraft draft in drafts)
            {
                (MemberDraft Member, int Seat)[] placed = Arrange(draft.Members, SeatsFor(draft.SeatClass, draft.Members.Length, taken));
                int[] ids = new int[placed.Length];
                for (int i = 0; i < placed.Length; i++)
                {
                    taken.Add(placed[i].Seat);
                    ids[i] = passengers.Count;
                    passengers.Add(ToPassenger(passengers.Count, bookings.Count, draft, placed[i].Member, placed[i].Seat));
                }

                bookings.Add(new Booking(bookings.Count, draft.TripPurpose, draft.SeatClass, draft.WakeMinute, Array.AsReadOnly(ids)));
            }

            return new PassengerManifest(passengers, bookings);
        }

        private static int[] GroupSeats(NavGraph graph, int row, SeatGroup group, ref int next)
        {
            int[] seats = new int[group.Seats.Count];
            for (int i = 0; i < seats.Length; i++, next++)
            {
                if (next >= graph.SeatNodes.Count || !IsSeat(graph.Nodes[graph.SeatNodes[next]], row, group.Seats[i]))
                {
                    throw new ArgumentException($"The graph's seats run out of step with the layout at row {row}.", nameof(graph));
                }

                seats[i] = graph.SeatNodes[next];
            }

            return seats;
        }

        private static bool IsSeat(NavNode node, int row, SeatSpec seat) => node.RowIndex == row && node.Label == seat.Label;

        /// <summary>
        /// Turns a group's seats to run from its aisle end: its edge nearer the nearest aisle's centre — unless the group has
        /// an aisle on each side, an aisle centre at or left of its left edge and another at or right of its right edge, when
        /// it uses the left edge's.
        /// </summary>
        private static int[] AisleFirst(CabinLayout layout, SeatGroup group, int[] seats)
        {
            double leftEdge = group.LeftInches;
            double rightEdge = leftEdge + group.Seats.Sum(seat => seat.WidthInches);
            bool aisleOnEachSide =
                layout.Aisles.Any(aisle => aisle.CenterInches <= leftEdge) && layout.Aisles.Any(aisle => aisle.CenterInches >= rightEdge);
            double fromLeft = layout.Aisles.Min(aisle => Math.Abs(aisle.CenterInches - leftEdge));
            if (!aisleOnEachSide && layout.Aisles.Min(aisle => Math.Abs(aisle.CenterInches - rightEdge)) < fromLeft)
            {
                Array.Reverse(seats);
            }

            return seats;
        }

        /// <summary>
        /// Assigns a booking's members to its seat runs, one run to each seat group it sits in. The first runs of two or more
        /// seats, one to each adult, keep a seat for an adult and fill the rest with children; children left over take the
        /// other runs in order, and adults fill every seat left. Within a run the order runs from the group's aisle end: an
        /// adult first, then the run's children, then its other adults.
        /// </summary>
        private static (MemberDraft Member, int Seat)[] Arrange(MemberDraft[] members, List<int[]> runs)
        {
            Queue<MemberDraft> adults = new(members.Where(member => member.AgeBand == AgeBand.Adult));
            Queue<MemberDraft> children = new(members.Where(member => member.AgeBand == AgeBand.Child));
            int[] childrenPerRun = ChildrenPerRun(runs, adults.Count, children.Count);
            List<(MemberDraft Member, int Seat)> placed = [];
            for (int r = 0; r < runs.Count; r++)
            {
                AgeBand[] order = RunOrder(runs[r].Length - childrenPerRun[r], childrenPerRun[r]);
                for (int i = 0; i < order.Length; i++)
                {
                    placed.Add((order[i] == AgeBand.Adult ? adults.Dequeue() : children.Dequeue(), runs[r][i]));
                }
            }

            return [.. placed];
        }

        /// <summary>
        /// Orders one run's members from the seat group's aisle end: one adult first, then the run's children, then its
        /// remaining adults, so the last adult takes the window end. A run of children alone is left as it is.
        /// </summary>
        private static AgeBand[] RunOrder(int adults, int children)
        {
            int childrenStart = adults > 0 && children > 0 ? 1 : 0;
            var order = new AgeBand[adults + children];
            for (int i = 0; i < order.Length; i++)
            {
                order[i] = i >= childrenStart && i < childrenStart + children ? AgeBand.Child : AgeBand.Adult;
            }

            return order;
        }

        private static int[] ChildrenPerRun(List<int[]> runs, int adults, int children)
        {
            bool[] hosted = new bool[runs.Count];
            int hosts = adults;
            for (int r = 0; r < runs.Count && hosts > 0; r++)
            {
                hosted[r] = runs[r].Length >= 2;
                hosts -= hosted[r] ? 1 : 0;
            }

            int[] counts = new int[runs.Count];
            int left = FillChildren(runs, hosted, counts, children, hostedRuns: true);
            FillChildren(runs, hosted, counts, left, hostedRuns: false);
            return counts;
        }

        /// <summary>Puts children into the hosted runs (one seat kept for the adult) or the others, in order; returns those left.</summary>
        private static int FillChildren(List<int[]> runs, bool[] hosted, int[] counts, int children, bool hostedRuns)
        {
            int left = children;
            for (int r = 0; r < runs.Count && left > 0; r++)
            {
                if (hosted[r] == hostedRuns)
                {
                    counts[r] = Math.Min(left, runs[r].Length - (hostedRuns ? 1 : 0));
                    left -= counts[r];
                }
            }

            return left;
        }

        private static ManifestPassenger ToPassenger(int id, int bookingId, BookingDraft booking, MemberDraft member, int seat) =>
            new()
            {
                Id = id,
                BookingId = bookingId,
                TripPurpose = booking.TripPurpose,
                AgeBand = member.AgeBand,
                SeatClass = booking.SeatClass,
                Profession = member.Profession,
                Traits = member.Traits,
                SeatNode = seat,
                WakeMinute = member.WakeMinute,
            };

        /// <summary>
        /// Returns a booking's seats as runs, one to each seat group: the first seat group of the class with enough free seats
        /// takes the whole booking; failing that, the booking takes the class's first free seats in order.
        /// </summary>
        private List<int[]> SeatsFor(SeatClass seatClass, int count, HashSet<int> taken)
        {
            List<int[]> groups = GroupsOf(seatClass);
            foreach (int[] group in groups)
            {
                if (group.Count(seat => !taken.Contains(seat)) >= count)
                {
                    return [FreeSeats(group, count, taken)];
                }
            }

            List<int[]> runs = [];
            int left = count;
            foreach (int[] group in groups)
            {
                if (left == 0)
                {
                    break;
                }

                int[] run = FreeSeats(group, left, taken);
                if (run.Length > 0)
                {
                    runs.Add(run);
                    left -= run.Length;
                }
            }

            return left == 0 ? runs : throw new InvalidOperationException($"A booking of {count} found only {count - left} free seats in its class.");
        }

        private static int[] FreeSeats(int[] group, int count, HashSet<int> taken) => [.. group.Where(seat => !taken.Contains(seat)).Take(count)];

        private List<int[]> GroupsOf(SeatClass seatClass) => seatClass == SeatClass.Business ? businessGroups : economyGroups;
    }
}
