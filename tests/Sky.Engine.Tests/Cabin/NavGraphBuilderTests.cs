using Sky.Engine.Cabin;

namespace Sky.Engine.Tests.Cabin;

/// <summary>Pins how a cabin layout in inches becomes a navigation graph in ticks.</summary>
public sealed class NavGraphBuilderTests
{
    private const double InchesPerTick = 10;

    /// <summary>Row 0 is a 2-2 business row and row 1 a 3-3 economy row; the ids follow the builder's order.</summary>
    [Fact]
    public void TwoTwoRowInFrontOfThreeThreeRowBuilds()
    {
        NavGraph graph = NavGraphBuilder.Build(TwoRowLayout(), InchesPerTick);

        NodeKind[] expectedKinds =
        [
            NodeKind.AisleSlot,
            NodeKind.Seat,
            NodeKind.Seat,
            NodeKind.Seat,
            NodeKind.Seat,
            NodeKind.AisleSlot,
            NodeKind.Seat,
            NodeKind.Seat,
            NodeKind.Seat,
            NodeKind.Seat,
            NodeKind.Seat,
            NodeKind.Seat,
            NodeKind.Door,
            NodeKind.LavQueue,
            NodeKind.Lav,
            NodeKind.Galley,
        ];
        Assert.Equal(expectedKinds, graph.Nodes.Select(node => node.Kind));
        Assert.Equal(Enumerable.Range(0, expectedKinds.Length), graph.Nodes.Select(node => node.Id));

        string[] expectedLabels = ["aisle0/row0", "A", "C", "D", "F", "aisle0/row1", "A", "B", "C", "D", "E", "F"];
        Assert.Equal(expectedLabels, graph.Nodes.Take(12).Select(node => node.Label));
        int[] expectedSeats = [1, 2, 3, 4, 6, 7, 8, 9, 10, 11];
        Assert.Equal(expectedSeats, graph.SeatNodes);
        Assert.Equal(0, graph.AisleSlot(0, 0));
        Assert.Equal(5, graph.AisleSlot(1, 0));
        Assert.Equal(12, graph.FixtureNode("door-1L"));
        Assert.Equal(13, graph.LavQueue("lav-fwd"));
        Assert.Equal(14, graph.FixtureNode("lav-fwd"));
        Assert.Equal(15, graph.FixtureNode("galley-mid"));
        Assert.Equal("lav-fwd/queue", graph.Nodes[13].Label);
    }

    /// <summary>Every seat can walk to the forward door, the forward lav and the galley.</summary>
    [Fact]
    public void EverySeatReachesForwardDoorLavAndGalley()
    {
        NavGraph graph = NavGraphBuilder.Build(TwoRowLayout(), InchesPerTick);
        int[] targets = [graph.FixtureNode("door-1L"), graph.FixtureNode("lav-fwd"), graph.FixtureNode("galley-mid")];

        foreach (int seat in graph.SeatNodes)
        {
            HashSet<int> reached = Reachable(graph, seat);
            Assert.All(targets, target => Assert.Contains(target, reached));
        }
    }

    /// <summary>The right-hand window seat of the 3-3 row reaches the aisle only through the middle and aisle seats.</summary>
    [Fact]
    public void WindowSeatLateralPathCrossesMiddleAndAisleSeatsInOrder()
    {
        NavGraph graph = NavGraphBuilder.Build(TwoRowLayout(), InchesPerTick);
        const int Aisle = 9;
        const int Middle = 10;
        const int Window = 11;

        int[] windowNeighbours = [Middle];
        int[] middleNeighbours = [Aisle, Window];
        int[] aisleNeighbours = [graph.AisleSlot(1, 0), Middle];
        Assert.Equal(windowNeighbours, Neighbours(graph, Window));
        Assert.Equal(middleNeighbours, Neighbours(graph, Middle));
        Assert.Equal(aisleNeighbours, Neighbours(graph, Aisle));
    }

    /// <summary>Inches become ticks rounded up, and never fewer than one tick.</summary>
    [Fact]
    public void EdgeTicksRoundUpFromInches()
    {
        NavGraph graph = NavGraphBuilder.Build(TwoRowLayout(), InchesPerTick);
        int frontSlot = graph.AisleSlot(0, 0);

        Assert.Equal(4, Ticks(graph, frontSlot, graph.AisleSlot(1, 0)));
        Assert.Equal(1, Ticks(graph, frontSlot, graph.LavQueue("lav-fwd")));
        Assert.Equal(3, Ticks(graph, frontSlot, graph.FixtureNode("door-1L")));

        // Row 1's seat D is the right-hand group's aisle seat: centre 90 + 18 / 2 = 99 in, 99 - 74 = 25 in from the aisle: 3 ticks.
        const int RowOneSeatD = 9;
        Assert.Equal(3, Ticks(graph, RowOneSeatD, graph.AisleSlot(1, 0)));
    }

    /// <summary>A fixture right at its aisle slot still takes one tick to reach.</summary>
    [Fact]
    public void ZeroDistanceFixtureStillCostsOneTick()
    {
        CabinLayout layout = TwoRowLayout() with { Fixtures = [new CabinFixture("door-1L", FixtureKind.Door, 0, 0, 0)] };
        NavGraph graph = NavGraphBuilder.Build(layout, InchesPerTick);

        Assert.Equal(1, Ticks(graph, graph.AisleSlot(0, 0), graph.FixtureNode("door-1L")));
    }

    /// <summary>A distance that is an exact multiple of a tick in decimal inches does not gain a tick from floating-point noise.</summary>
    [Fact]
    public void ExactMultipleDistanceDoesNotGainAPhantomTick()
    {
        CabinLayout layout = new("exact-multiple", 102.2, [new CabinRow(31, [Group(0.1, 16.4, "A", "B", "C")])], [new Aisle(51.1, 20)], []);
        NavGraph graph = NavGraphBuilder.Build(layout, InchesPerTick);
        const int AisleSeat = 3;

        // Seat C's centre is 0.1 + 16.4 + 16.4 + 8.2 = 41.1 in, so it sits 51.1 - 41.1 = 10 in from the aisle: one tick.
        Assert.Equal(1, Ticks(graph, AisleSeat, graph.AisleSlot(0, 0)));
    }

    /// <summary>With two aisles and no group between them, an outer group's end seat links to the near aisle only.</summary>
    [Fact]
    public void EndSeatLinksOnlyToTheNearestAisle()
    {
        CabinLayout layout = new(
            "bare-middle",
            102,
            [new CabinRow(31, [Group(2, 10, "A", "B"), Group(80, 10, "C", "D")])],
            [new Aisle(30, 20), new Aisle(70, 20)],
            []
        );
        NavGraph graph = NavGraphBuilder.Build(layout, InchesPerTick);
        const int SeatA = 2;
        const int SeatB = 3;
        const int SeatC = 4;
        const int SeatD = 5;

        int[] leftGroupEndNeighbours = [graph.AisleSlot(0, 0), SeatA];
        int[] rightGroupEndNeighbours = [graph.AisleSlot(0, 1), SeatD];
        Assert.Equal(leftGroupEndNeighbours, Neighbours(graph, SeatB));
        Assert.Equal(rightGroupEndNeighbours, Neighbours(graph, SeatC));
    }

    /// <summary>A graph refuses a link that takes no time, so every walk costs at least one tick a step.</summary>
    [Fact]
    public void GraphRefusesALinkUnderOneTick()
    {
        NavNode[] nodes = [new(0, NodeKind.AisleSlot, 0, "aisle0/row0", null), new(1, NodeKind.AisleSlot, 1, "aisle0/row1", null)];

        ArgumentOutOfRangeException error = Assert.Throws<ArgumentOutOfRangeException>(() => new NavGraph(nodes, [(0, 1, 0)]));

        Assert.Equal("links", error.ParamName);
    }

    /// <summary>A seat group with no seats fails, naming its row.</summary>
    [Fact]
    public void EmptySeatGroupIsRefused()
    {
        CabinLayout twoRow = TwoRowLayout();
        CabinLayout layout = twoRow with { Rows = [twoRow.Rows[0], new CabinRow(31, [new SeatGroup(4, [])])] };

        ArgumentException error = Assert.Throws<ArgumentException>(() => NavGraphBuilder.Build(layout, InchesPerTick));

        Assert.Contains("Row 1", error.Message, StringComparison.Ordinal);
    }

    /// <summary>A lav's queue is found by the fixture id the queue node carries, whatever its label says.</summary>
    [Fact]
    public void LavQueueIsFoundByFixtureId()
    {
        NavNode[] nodes =
        [
            new(0, NodeKind.AisleSlot, 0, "aisle0/row0", null),
            new(1, NodeKind.LavQueue, 0, "front queue", "lav-fwd"),
            new(2, NodeKind.Lav, 0, "forward lav", "lav-fwd"),
        ];

        var graph = new NavGraph(nodes, [(0, 1, 1), (1, 2, 1)]);

        Assert.Equal(1, graph.LavQueue("lav-fwd"));
        Assert.Equal(2, graph.FixtureNode("lav-fwd"));
    }

    /// <summary>A fixture on a row the layout does not have fails with its id and the bad index.</summary>
    [Fact]
    public void FixtureOnMissingRowFails()
    {
        CabinLayout layout = TwoRowLayout() with { Fixtures = [new CabinFixture("galley-aft", FixtureKind.Galley, 2, 0, 20)] };

        ArgumentException error = Assert.Throws<ArgumentException>(() => NavGraphBuilder.Build(layout, InchesPerTick));

        Assert.Contains("galley-aft", error.Message, StringComparison.Ordinal);
        Assert.Contains("row 2", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Builds the shared test cabin: row 0 a 2-2 business row, row 1 a 3-3 economy row, one aisle at 74 inches, a forward door
    /// and lav at row 0 and a galley at row 1.
    /// </summary>
    /// <returns>The layout.</returns>
    internal static CabinLayout TwoRowLayout() =>
        new(
            "two-row",
            148,
            [
                new CabinRow(31, [Group(6, 26, "A", "C"), Group(90, 26, "D", "F")]),
                new CabinRow(31, [Group(4, 18, "A", "B", "C"), Group(90, 18, "D", "E", "F")]),
            ],
            [new Aisle(74, 20)],
            [
                new CabinFixture("door-1L", FixtureKind.Door, 0, 0, 30),
                new CabinFixture("lav-fwd", FixtureKind.Lav, 0, 0, 4),
                new CabinFixture("galley-mid", FixtureKind.Galley, 1, 0, 20),
            ]
        );

    /// <summary>Builds a seat group of equally wide seats.</summary>
    /// <param name="left">The group's left edge.</param>
    /// <param name="width">Each seat's width.</param>
    /// <param name="labels">The seat labels, left to right.</param>
    /// <returns>The group.</returns>
    internal static SeatGroup Group(double left, double width, params string[] labels) =>
        new(left, [.. labels.Select(label => new SeatSpec(label, width))]);

    /// <summary>Returns the ticks of the edge between two neighbours.</summary>
    /// <param name="graph">The graph.</param>
    /// <param name="from">One end.</param>
    /// <param name="to">The other end.</param>
    /// <returns>The edge's ticks.</returns>
    internal static int Ticks(NavGraph graph, int from, int to) => graph.EdgesFrom(from).Single(edge => edge.To == to).Ticks;

    /// <summary>Returns a node's neighbours, in id order.</summary>
    /// <param name="graph">The graph.</param>
    /// <param name="node">The node.</param>
    /// <returns>The neighbour ids.</returns>
    internal static int[] Neighbours(NavGraph graph, int node) => [.. graph.EdgesFrom(node).Select(edge => edge.To)];

    private static HashSet<int> Reachable(NavGraph graph, int start)
    {
        HashSet<int> reached = [start];
        Queue<int> frontier = new([start]);
        while (frontier.TryDequeue(out int node))
        {
            foreach (NavEdge edge in graph.EdgesFrom(node))
            {
                if (reached.Add(edge.To))
                {
                    frontier.Enqueue(edge.To);
                }
            }
        }

        return reached;
    }
}
