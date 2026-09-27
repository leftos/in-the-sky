using CsCheck;
using Sky.Engine.Cabin;

namespace Sky.Engine.Tests.Cabin;

/// <summary>Pins the occupancy table: capacities, reservations, arrivals, the aisle squeeze and the cart that blocks it.</summary>
public sealed class OccupancyTests
{
    private const int FrontSlot = 0;
    private const int FrontSeatA = 1;
    private const int ReserveOp = 0;
    private const int ArriveOp = 1;

    private static readonly NodeCapacities Capacities = new()
    {
        AisleSlot = 1,
        Seat = 1,
        Door = 2,
        Lav = 1,
        LavQueue = 3,
        Galley = 2,
    };

    /// <summary>A second person asking for a seat already held is refused, and the refusal leaves no trace.</summary>
    [Fact]
    public void ReservationOnFullNodeIsRefused()
    {
        Occupancy occupancy = NewOccupancy();

        Assert.Equal(ReserveResult.Reserved, occupancy.TryReserve(FrontSeatA, 1, OccupantKind.Person));
        Assert.Equal(ReserveResult.Refused, occupancy.TryReserve(FrontSeatA, 2, OccupantKind.Person));

        Assert.Equal(1, occupancy.HolderCount(FrontSeatA));
        Assert.False(occupancy.Holds(FrontSeatA, 2));
    }

    /// <summary>A full aisle slot admits one more person by squeeze, and then no one.</summary>
    [Fact]
    public void PersonSqueezesIntoFullAisleSlotOnce()
    {
        Occupancy occupancy = NewOccupancy();

        Assert.Equal(ReserveResult.Reserved, occupancy.TryReserve(FrontSlot, 1, OccupantKind.Person));
        Assert.Equal(ReserveResult.Squeezed, occupancy.TryReserve(FrontSlot, 2, OccupantKind.Person));
        Assert.Equal(ReserveResult.Refused, occupancy.TryReserve(FrontSlot, 3, OccupantKind.Person));

        Assert.Equal(2, occupancy.HolderCount(FrontSlot));
    }

    /// <summary>A cart fills its aisle slot, and a person cannot squeeze past it.</summary>
    [Fact]
    public void CartBlocksSqueeze()
    {
        Occupancy occupancy = NewOccupancy();

        Assert.Equal(ReserveResult.Reserved, occupancy.TryReserve(FrontSlot, 1, OccupantKind.Cart));
        Assert.Equal(ReserveResult.Refused, occupancy.TryReserve(FrontSlot, 2, OccupantKind.Person));

        Assert.True(occupancy.HasCart(FrontSlot));
        Assert.Equal(1, occupancy.HolderCount(FrontSlot));
    }

    /// <summary>A cart reserves only an empty node, so a slot with a person in it refuses the cart.</summary>
    [Fact]
    public void CartCannotEnterOccupiedSlot()
    {
        Occupancy occupancy = NewOccupancy();

        Assert.Equal(ReserveResult.Reserved, occupancy.TryReserve(FrontSlot, 1, OccupantKind.Person));
        Assert.Equal(ReserveResult.Refused, occupancy.TryReserve(FrontSlot, 2, OccupantKind.Cart));

        Assert.False(occupancy.HasCart(FrontSlot));
    }

    /// <summary>Releasing a node the character does not hold fails, naming the node and the character.</summary>
    [Fact]
    public void ReleaseOfUnheldNodeThrows()
    {
        Occupancy occupancy = NewOccupancy();

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => occupancy.Release(FrontSlot, 7));

        Assert.Contains("node 0", error.Message, StringComparison.Ordinal);
        Assert.Contains("Character 7", error.Message, StringComparison.Ordinal);
    }

    /// <summary>A reservation never turned into an arrival is freed by a release, and the node takes a newcomer.</summary>
    [Fact]
    public void ReleaseFreesReservationNeverArrived()
    {
        Occupancy occupancy = NewOccupancy();
        occupancy.TryReserve(FrontSeatA, 1, OccupantKind.Person);

        occupancy.Release(FrontSeatA, 1);

        Assert.Equal(0, occupancy.HolderCount(FrontSeatA));
        Assert.Equal(ReserveResult.Reserved, occupancy.TryReserve(FrontSeatA, 2, OccupantKind.Person));
    }

    /// <summary>An arrival turns the reservation into presence without changing the node's count.</summary>
    [Fact]
    public void ArriveTurnsReservationIntoArrival()
    {
        Occupancy occupancy = NewOccupancy();
        occupancy.TryReserve(FrontSeatA, 1, OccupantKind.Person);

        occupancy.Arrive(FrontSeatA, 1);

        Assert.True(occupancy.HasArrived(FrontSeatA, 1));
        Assert.Equal(1, occupancy.HolderCount(FrontSeatA));
    }

    /// <summary>Arriving without a reservation fails, and arriving twice fails with its own message.</summary>
    [Fact]
    public void ArriveWithoutReservationThrows()
    {
        Occupancy occupancy = NewOccupancy();

        InvalidOperationException unreserved = Assert.Throws<InvalidOperationException>(() => occupancy.Arrive(FrontSeatA, 1));
        occupancy.TryReserve(FrontSeatA, 1, OccupantKind.Person);
        occupancy.Arrive(FrontSeatA, 1);
        InvalidOperationException twice = Assert.Throws<InvalidOperationException>(() => occupancy.Arrive(FrontSeatA, 1));

        Assert.Contains("holds no reservation on node 1", unreserved.Message, StringComparison.Ordinal);
        Assert.Contains("has already arrived at node 1", twice.Message, StringComparison.Ordinal);
    }

    /// <summary>A reservation not yet turned into an arrival holds the node but has not arrived.</summary>
    [Fact]
    public void HasArrivedIsFalseBeforeArrive()
    {
        Occupancy occupancy = NewOccupancy();
        occupancy.TryReserve(FrontSeatA, 1, OccupantKind.Person);

        Assert.True(occupancy.Holds(FrontSeatA, 1));
        Assert.False(occupancy.HasArrived(FrontSeatA, 1));
    }

    /// <summary>Once a holder leaves a squeezed slot, the slot is back at capacity and admits a squeeze again.</summary>
    [Fact]
    public void SqueezeIsAvailableAgainAfterRelease()
    {
        Occupancy occupancy = NewOccupancy();
        occupancy.TryReserve(FrontSlot, 1, OccupantKind.Person);
        occupancy.TryReserve(FrontSlot, 2, OccupantKind.Person);
        Assert.Equal(ReserveResult.Refused, occupancy.TryReserve(FrontSlot, 3, OccupantKind.Person));

        occupancy.Release(FrontSlot, 1);

        Assert.Equal(ReserveResult.Squeezed, occupancy.TryReserve(FrontSlot, 3, OccupantKind.Person));
    }

    /// <summary>A node lists its holders in the order they reserved it, and a release drops only the one who left.</summary>
    [Fact]
    public void HoldersListsCharactersInReservationOrder()
    {
        Occupancy occupancy = NewOccupancy();
        Assert.Empty(occupancy.Holders(FrontSlot));
        occupancy.TryReserve(FrontSlot, 9, OccupantKind.Person);
        occupancy.TryReserve(FrontSlot, 4, OccupantKind.Person);

        Assert.Equal([9, 4], occupancy.Holders(FrontSlot));
        occupancy.Release(FrontSlot, 9);
        Assert.Equal([4], occupancy.Holders(FrontSlot));
    }

    /// <summary>A cart is refused an empty seat, lav or lav queue, and the refusal leaves no trace.</summary>
    /// <param name="kind">The kind of node the cart asks for.</param>
    [Theory]
    [InlineData(NodeKind.Seat)]
    [InlineData(NodeKind.Lav)]
    [InlineData(NodeKind.LavQueue)]
    public void CartIsRefusedOffTheAisleGalleyAndDoor(NodeKind kind)
    {
        NavGraph graph = NavGraphBuilder.Build(NavGraphBuilderTests.TwoRowLayout(), 10);
        var occupancy = new Occupancy(graph, Capacities);
        int node = graph.Nodes.First(candidate => candidate.Kind == kind).Id;

        Assert.Equal(ReserveResult.Refused, occupancy.TryReserve(node, 1, OccupantKind.Cart));
        Assert.Equal(0, occupancy.HolderCount(node));
    }

    /// <summary>A cart reserves an empty aisle slot, galley or door.</summary>
    /// <param name="kind">The kind of node the cart asks for.</param>
    [Theory]
    [InlineData(NodeKind.AisleSlot)]
    [InlineData(NodeKind.Galley)]
    [InlineData(NodeKind.Door)]
    public void CartReservesEmptyAisleSlotGalleyOrDoor(NodeKind kind)
    {
        NavGraph graph = NavGraphBuilder.Build(NavGraphBuilderTests.TwoRowLayout(), 10);
        var occupancy = new Occupancy(graph, Capacities);
        int node = graph.Nodes.First(candidate => candidate.Kind == kind).Id;

        Assert.Equal(ReserveResult.Reserved, occupancy.TryReserve(node, 1, OccupantKind.Cart));
        Assert.True(occupancy.HasCart(node));
    }

    /// <summary>A node outside the graph, a negative character id or an undefined kind is rejected, naming the parameter.</summary>
    /// <param name="node">The node asked for.</param>
    /// <param name="character">The character asking.</param>
    /// <param name="kind">The occupant kind.</param>
    /// <param name="paramName">The parameter the error names.</param>
    [Theory]
    [InlineData(16, 1, OccupantKind.Person, "node")]
    [InlineData(-1, 1, OccupantKind.Person, "node")]
    [InlineData(0, -1, OccupantKind.Person, "character")]
    [InlineData(0, 1, (OccupantKind)99, "kind")]
    public void InvalidReserveArgumentsThrow(int node, int character, OccupantKind kind, string paramName)
    {
        Occupancy occupancy = NewOccupancy();

        ArgumentOutOfRangeException error = Assert.Throws<ArgumentOutOfRangeException>(() => occupancy.TryReserve(node, character, kind));

        Assert.Equal(paramName, error.ParamName);
    }

    /// <summary>A character reserving a node it already holds fails, naming the node and the character.</summary>
    [Fact]
    public void ReserveOfHeldNodeThrows()
    {
        Occupancy occupancy = NewOccupancy();
        occupancy.TryReserve(FrontSlot, 4, OccupantKind.Person);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => occupancy.TryReserve(FrontSlot, 4, OccupantKind.Person));

        Assert.Contains("Character 4", error.Message, StringComparison.Ordinal);
        Assert.Contains("node 0", error.Message, StringComparison.Ordinal);
    }

    /// <summary>A node's capacity is the one its kind carries in the constructor's capacities.</summary>
    [Fact]
    public void CapacityFollowsNodeKind()
    {
        NavGraph graph = NavGraphBuilder.Build(NavGraphBuilderTests.TwoRowLayout(), 10);
        var occupancy = new Occupancy(graph, Capacities);

        Assert.Equal(3, occupancy.Capacity(graph.LavQueue("lav-fwd")));
        Assert.Equal(2, occupancy.Capacity(graph.FixtureNode("galley-mid")));
        Assert.Equal(1, occupancy.Capacity(FrontSeatA));
    }

    /// <summary>A capacity under one is rejected, naming the node kind.</summary>
    [Fact]
    public void ZeroCapacityIsRejected()
    {
        ArgumentOutOfRangeException error = Assert.Throws<ArgumentOutOfRangeException>(() => Capacities with { Galley = 0 });

        Assert.Equal(nameof(NodeCapacities.Galley), error.ParamName);
    }

    /// <summary>
    /// Random reserves, arrivals and releases by several people and carts never fill a node past its capacity, plus one squeeze
    /// in an aisle slot with no cart, and a node with a cart holds nothing else.
    /// </summary>
    [Fact]
    public void RandomSequencesNeverExceedCapacityPlusSqueeze()
    {
        NavGraph graph = NavGraphBuilder.Build(NavGraphBuilderTests.TwoRowLayout(), 10);
        var op = Gen.Select(
            Gen.Int[0, 2],
            Gen.Int[0, graph.Nodes.Count - 1],
            Gen.Int[0, 4],
            Gen.Int[0, 1],
            (code, node, character, kind) => new OccupancyOp(code, node, character, (OccupantKind)kind)
        );

        op.Array[0, 80].Sample(ops => AllStatesLegal(graph, ops));
    }

    private static Occupancy NewOccupancy() => new(NavGraphBuilder.Build(NavGraphBuilderTests.TwoRowLayout(), 10), Capacities);

    private static bool AllStatesLegal(NavGraph graph, OccupancyOp[] ops)
    {
        var occupancy = new Occupancy(graph, Capacities);
        foreach (OccupancyOp op in ops)
        {
            Apply(occupancy, op);
            if (!Legal(graph, occupancy))
            {
                return false;
            }
        }

        return true;
    }

    private static void Apply(Occupancy occupancy, OccupancyOp op)
    {
        bool holds = occupancy.Holds(op.Node, op.Character);
        if (op.Code == ReserveOp && !holds)
        {
            occupancy.TryReserve(op.Node, op.Character, op.Kind);
        }
        else if (op.Code == ArriveOp && holds && !occupancy.HasArrived(op.Node, op.Character))
        {
            occupancy.Arrive(op.Node, op.Character);
        }
        else if (holds)
        {
            occupancy.Release(op.Node, op.Character);
        }
    }

    private static bool Legal(NavGraph graph, Occupancy occupancy)
    {
        foreach (NavNode node in graph.Nodes)
        {
            int count = occupancy.HolderCount(node.Id);
            bool cart = occupancy.HasCart(node.Id);
            int squeeze = node.Kind == NodeKind.AisleSlot && !cart ? 1 : 0;
            if (count > occupancy.Capacity(node.Id) + squeeze || (cart && count != 1))
            {
                return false;
            }
        }

        return true;
    }

    private readonly record struct OccupancyOp(int Code, int Node, int Character, OccupantKind Kind);
}
