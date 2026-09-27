namespace Sky.Engine.Cabin;

/// <summary>Derives a <see cref="NavGraph"/> from a <see cref="CabinLayout"/>, turning inches into ticks once.</summary>
public static class NavGraphBuilder
{
    /// <summary>
    /// Builds the graph. Node ids run row by row, front to back, each row's aisle slots in aisle order then its seats left to
    /// right; then the fixtures in list order, a lav giving its queue node then its lav node. A group's end seat links to the
    /// nearest aisle on its side when no other group of the row lies between them. The builder does not check that the graph
    /// is connected; the content validator does.
    /// </summary>
    /// <param name="layout">The cabin to build from.</param>
    /// <param name="inchesPerTick">How many inches a person walks in one tick; a distance of d inches takes
    /// <c>max(1, ceil(d / inchesPerTick))</c> ticks, the quotient rounded to 9 decimals first so floating-point noise on an
    /// exact multiple adds no tick.</param>
    /// <returns>The navigation graph.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="inchesPerTick"/> is not positive.</exception>
    /// <exception cref="ArgumentException">A row has a seat group with no seats, or a fixture names a row or an aisle the
    /// layout does not have.</exception>
    public static NavGraph Build(CabinLayout layout, double inchesPerTick)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (inchesPerTick is not > 0)
        {
            throw new ArgumentOutOfRangeException(nameof(inchesPerTick), inchesPerTick, "Inches per tick must be positive.");
        }

        ValidateRows(layout);
        ValidateFixtures(layout);
        GraphDraft draft = new(inchesPerTick);
        for (int rowIndex = 0; rowIndex < layout.Rows.Count; rowIndex++)
        {
            AddRow(draft, layout, rowIndex);
        }

        for (int rowIndex = 0; rowIndex + 1 < layout.Rows.Count; rowIndex++)
        {
            for (int aisle = 0; aisle < layout.Aisles.Count; aisle++)
            {
                draft.Link(draft.Slots[rowIndex][aisle], draft.Slots[rowIndex + 1][aisle], layout.Rows[rowIndex].PitchInches);
            }
        }

        foreach (CabinFixture fixture in layout.Fixtures)
        {
            AddFixture(draft, fixture);
        }

        return new NavGraph(draft.Nodes, draft.Links);
    }

    private static void ValidateRows(CabinLayout layout)
    {
        for (int rowIndex = 0; rowIndex < layout.Rows.Count; rowIndex++)
        {
            if (layout.Rows[rowIndex].Groups.Any(group => group.Seats.Count == 0))
            {
                throw new ArgumentException($"Row {rowIndex} has a seat group with no seats.", nameof(layout));
            }
        }
    }

    private static void ValidateFixtures(CabinLayout layout)
    {
        foreach (CabinFixture fixture in layout.Fixtures)
        {
            if (fixture.RowIndex < 0 || fixture.RowIndex >= layout.Rows.Count)
            {
                throw new ArgumentException(
                    $"Fixture '{fixture.Id}' names row {fixture.RowIndex}, but the layout has rows 0 to {layout.Rows.Count - 1}.",
                    nameof(layout)
                );
            }

            if (fixture.AisleIndex < 0 || fixture.AisleIndex >= layout.Aisles.Count)
            {
                throw new ArgumentException(
                    $"Fixture '{fixture.Id}' names aisle {fixture.AisleIndex}, but the layout has aisles 0 to {layout.Aisles.Count - 1}.",
                    nameof(layout)
                );
            }
        }
    }

    private static void AddRow(GraphDraft draft, CabinLayout layout, int rowIndex)
    {
        int[] slots = new int[layout.Aisles.Count];
        for (int aisle = 0; aisle < slots.Length; aisle++)
        {
            slots[aisle] = draft.AddNode(NodeKind.AisleSlot, rowIndex, $"aisle{aisle}/row{rowIndex}", null);
        }

        draft.Slots.Add(slots);
        foreach (SeatGroup group in layout.Rows[rowIndex].Groups)
        {
            LinkEndsToAisles(draft, layout, rowIndex, AddGroup(draft, rowIndex, group));
        }
    }

    private static GroupSeats AddGroup(GraphDraft draft, int rowIndex, SeatGroup group)
    {
        int[] seats = new int[group.Seats.Count];
        double[] centres = new double[group.Seats.Count];
        double left = group.LeftInches;
        for (int i = 0; i < seats.Length; i++)
        {
            SeatSpec seat = group.Seats[i];
            seats[i] = draft.AddNode(NodeKind.Seat, rowIndex, seat.Label, null);
            centres[i] = left + (seat.WidthInches / 2);
            left += seat.WidthInches;
            if (i > 0)
            {
                draft.Link(seats[i - 1], seats[i], centres[i] - centres[i - 1]);
            }
        }

        return new GroupSeats(seats, centres, group.LeftInches, left);
    }

    private static void LinkEndsToAisles(GraphDraft draft, CabinLayout layout, int rowIndex, GroupSeats seats)
    {
        IReadOnlyList<SeatGroup> rowGroups = layout.Rows[rowIndex].Groups;
        int right = NearestAisleRightOf(layout.Aisles, seats.RightEdge);
        if (right >= 0 && !AnyGroupBetween(rowGroups, seats.RightEdge, layout.Aisles[right].CenterInches))
        {
            draft.Link(seats.Ids[^1], draft.Slots[rowIndex][right], layout.Aisles[right].CenterInches - seats.Centres[^1]);
        }

        int left = NearestAisleLeftOf(layout.Aisles, seats.LeftEdge);
        if (left >= 0 && !AnyGroupBetween(rowGroups, layout.Aisles[left].CenterInches, seats.LeftEdge))
        {
            draft.Link(seats.Ids[0], draft.Slots[rowIndex][left], seats.Centres[0] - layout.Aisles[left].CenterInches);
        }
    }

    private static int NearestAisleRightOf(IReadOnlyList<Aisle> aisles, double edge)
    {
        int nearest = -1;
        for (int aisle = 0; aisle < aisles.Count; aisle++)
        {
            double centre = aisles[aisle].CenterInches;
            if (centre > edge && (nearest < 0 || centre < aisles[nearest].CenterInches))
            {
                nearest = aisle;
            }
        }

        return nearest;
    }

    private static int NearestAisleLeftOf(IReadOnlyList<Aisle> aisles, double edge)
    {
        int nearest = -1;
        for (int aisle = 0; aisle < aisles.Count; aisle++)
        {
            double centre = aisles[aisle].CenterInches;
            if (centre < edge && (nearest < 0 || centre > aisles[nearest].CenterInches))
            {
                nearest = aisle;
            }
        }

        return nearest;
    }

    /// <summary>Whether any group of the row overlaps the open span between two lateral positions.</summary>
    private static bool AnyGroupBetween(IReadOnlyList<SeatGroup> rowGroups, double low, double high) =>
        rowGroups.Any(group => group.LeftInches < high && group.LeftInches + group.Seats.Sum(seat => seat.WidthInches) > low);

    private static void AddFixture(GraphDraft draft, CabinFixture fixture)
    {
        int slot = draft.Slots[fixture.RowIndex][fixture.AisleIndex];
        NodeKind kind = NodeKindOf(fixture.Kind);
        if (kind == NodeKind.Lav)
        {
            int queue = draft.AddNode(NodeKind.LavQueue, fixture.RowIndex, $"{fixture.Id}/queue", fixture.Id);
            int lav = draft.AddNode(NodeKind.Lav, fixture.RowIndex, fixture.Id, fixture.Id);
            draft.Link(slot, queue, fixture.DistanceInches / 2);
            draft.Link(queue, lav, fixture.DistanceInches / 2);
            return;
        }

        draft.Link(slot, draft.AddNode(kind, fixture.RowIndex, fixture.Id, fixture.Id), fixture.DistanceInches);
    }

    // No default arm, so a new FixtureKind member fails the build here (CS8509). CS8524 only reports casts of unnamed
    // values, which no layout carries.
#pragma warning disable CS8524
    private static NodeKind NodeKindOf(FixtureKind kind) =>
        kind switch
        {
            FixtureKind.Door => NodeKind.Door,
            FixtureKind.Lav => NodeKind.Lav,
            FixtureKind.Galley => NodeKind.Galley,
        };
#pragma warning restore CS8524

    /// <summary>A group's seat node ids and seat centres, left to right, and the group's two edges.</summary>
    private readonly record struct GroupSeats(int[] Ids, double[] Centres, double LeftEdge, double RightEdge);

    /// <summary>The nodes and links gathered so far, and the one place inches become ticks.</summary>
    private sealed class GraphDraft(double inchesPerTick)
    {
        public List<NavNode> Nodes { get; } = [];

        public List<(int A, int B, int Ticks)> Links { get; } = [];

        public List<int[]> Slots { get; } = [];

        public int AddNode(NodeKind kind, int rowIndex, string label, string? fixtureId)
        {
            int id = Nodes.Count;
            Nodes.Add(new NavNode(id, kind, rowIndex, label, fixtureId));
            return id;
        }

        public void Link(int a, int b, double inches) => Links.Add((a, b, Math.Max(1, (int)Math.Ceiling(Math.Round(inches / inchesPerTick, 9)))));
    }
}
