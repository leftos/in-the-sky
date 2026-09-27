using Sky.Engine.Cabin;

namespace Sky.Engine.Tests.Cabin;

/// <summary>Pins the all-pairs path table: its costs, its next hops and how it breaks ties.</summary>
public sealed class PathTableTests
{
    private const double InchesPerTick = 10;

    /// <summary>Walking next hops from any node ends at any target, and the walked ticks add up to the table's cost.</summary>
    [Fact]
    public void FollowingNextHopsReachesEveryTargetAtItsCost()
    {
        NavGraph graph = NavGraphBuilder.Build(NavGraphBuilderTests.TwoRowLayout(), InchesPerTick);
        var table = PathTable.Build(graph);

        Assert.Equal(graph.Nodes.Count, table.NodeCount);
        for (int from = 0; from < table.NodeCount; from++)
        {
            for (int to = 0; to < table.NodeCount; to++)
            {
                Assert.Equal(table.Cost(from, to), WalkedTicks(graph, table, from, to));
            }
        }

        // Row 1's window seat F (centre 135) to the door: F-E 18 in (2) + E-D 18 in (2) + D-aisle 25 in (3)
        // + row 1 to row 0 at pitch 31 (4) + aisle to door 30 in (3) = 14 ticks.
        Assert.Equal(14, table.Cost(SeatNode(graph, 1, "F"), graph.FixtureNode("door-1L")));
    }

    /// <summary>With two equally cheap routes round a middle seat group, the next hop is the lower-id neighbour.</summary>
    [Fact]
    public void EqualCostTiesResolveToTheLowerNodeId()
    {
        NavGraph graph = NavGraphBuilder.Build(TwoAisleLayout(), InchesPerTick);
        var table = PathTable.Build(graph);
        int from = SeatNode(graph, 0, "D");
        int to = SeatNode(graph, 1, "D");
        int left = SeatNode(graph, 0, "C");
        int right = SeatNode(graph, 0, "E");
        int leftSlot = graph.AisleSlot(0, 0);
        int rightSlot = graph.AisleSlot(0, 1);

        // Each end seat links only to the aisle on its own side: the middle group stands between an outer group and the far aisle.
        int[] leftEndNeighbours = [leftSlot, from];
        int[] rightEndNeighbours = [rightSlot, from];
        int[] leftGroupEndNeighbours = [leftSlot, SeatNode(graph, 0, "A")];
        int[] rightGroupEndNeighbours = [rightSlot, SeatNode(graph, 0, "G")];
        Assert.Equal(leftEndNeighbours, NavGraphBuilderTests.Neighbours(graph, left));
        Assert.Equal(rightEndNeighbours, NavGraphBuilderTests.Neighbours(graph, right));
        Assert.Equal(leftGroupEndNeighbours, NavGraphBuilderTests.Neighbours(graph, SeatNode(graph, 0, "B")));
        Assert.Equal(rightGroupEndNeighbours, NavGraphBuilderTests.Neighbours(graph, SeatNode(graph, 0, "F")));
        Assert.Equal(
            table.Cost(left, to) + NavGraphBuilderTests.Ticks(graph, from, left),
            table.Cost(right, to) + NavGraphBuilderTests.Ticks(graph, from, right)
        );
        Assert.True(left < right);
        Assert.Equal(left, table.NextHop(from, to));
    }

    /// <summary>Building the table twice from two builds of one layout gives the same costs and next hops.</summary>
    [Fact]
    public void TwoBuildsGiveIdenticalTables()
    {
        var first = PathTable.Build(NavGraphBuilder.Build(TwoAisleLayout(), InchesPerTick));
        var second = PathTable.Build(NavGraphBuilder.Build(TwoAisleLayout(), InchesPerTick));

        Assert.Equal(first.NodeCount, second.NodeCount);
        for (int from = 0; from < first.NodeCount; from++)
        {
            for (int to = 0; to < first.NodeCount; to++)
            {
                Assert.Equal(first.Cost(from, to), second.Cost(from, to));
                Assert.Equal(first.NextHop(from, to), second.NextHop(from, to));
            }
        }
    }

    /// <summary>A door on an aisle no seat links to the other aisle has no cost and no next hop from that other aisle.</summary>
    [Fact]
    public void UnreachableTargetReportsMinusOne()
    {
        CabinLayout layout = new(
            "two-bare-aisles",
            100,
            [new CabinRow(31, [])],
            [new Aisle(30, 20), new Aisle(70, 20)],
            [new CabinFixture("door-1R", FixtureKind.Door, 0, 1, 30)]
        );
        NavGraph graph = NavGraphBuilder.Build(layout, InchesPerTick);
        var table = PathTable.Build(graph);
        int door = graph.FixtureNode("door-1R");

        Assert.Equal(-1, table.Cost(graph.AisleSlot(0, 0), door));
        Assert.Equal(-1, table.NextHop(graph.AisleSlot(0, 0), door));
        Assert.Equal(3, table.Cost(graph.AisleSlot(0, 1), door));
        Assert.Equal(door, table.NextHop(graph.AisleSlot(0, 1), door));
    }

    /// <summary>A 2-3-2 cabin of two rows whose middle group sits exactly halfway between its two aisles.</summary>
    private static CabinLayout TwoAisleLayout()
    {
        CabinRow row = new(
            31,
            [
                NavGraphBuilderTests.Group(4, 18, "A", "B"),
                NavGraphBuilderTests.Group(73, 18, "C", "D", "E"),
                NavGraphBuilderTests.Group(160, 18, "F", "G"),
            ]
        );
        return new CabinLayout("two-aisle", 200, [row, row], [new Aisle(60, 20), new Aisle(140, 20)], []);
    }

    private static int SeatNode(NavGraph graph, int rowIndex, string label) =>
        graph.Nodes.Single(node => node.Kind == NodeKind.Seat && node.RowIndex == rowIndex && node.Label == label).Id;

    private static int WalkedTicks(NavGraph graph, PathTable table, int from, int to)
    {
        int ticks = 0;
        int node = from;
        for (int steps = 0; node != to; steps++)
        {
            Assert.True(steps < table.NodeCount, $"The walk from {from} to {to} did not arrive.");
            int next = table.NextHop(node, to);
            ticks += NavGraphBuilderTests.Ticks(graph, node, next);
            node = next;
        }

        Assert.Equal(to, table.NextHop(to, to));
        return ticks;
    }
}
