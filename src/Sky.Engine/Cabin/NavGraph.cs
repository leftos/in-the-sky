using System.Collections.ObjectModel;

namespace Sky.Engine.Cabin;

/// <summary>What a navigation node stands for.</summary>
public enum NodeKind
{
    /// <summary>The spot in an aisle beside one row.</summary>
    AisleSlot,

    /// <summary>A seat.</summary>
    Seat,

    /// <summary>A galley.</summary>
    Galley,

    /// <summary>A lavatory.</summary>
    Lav,

    /// <summary>The spot where passengers wait for a lavatory.</summary>
    LavQueue,

    /// <summary>A cabin door.</summary>
    Door,
}

/// <summary>A place a person can stand or sit in the cabin.</summary>
/// <param name="Id">The node's index in <see cref="NavGraph.Nodes"/>.</param>
/// <param name="Kind">What the node stands for.</param>
/// <param name="RowIndex">The row the node belongs to, 0 at the front.</param>
/// <param name="Label">The seat label, the fixture id, <c>"&lt;lav id&gt;/queue"</c> for a queue,
/// <c>"aisle&lt;a&gt;/row&lt;r&gt;"</c> for a slot.</param>
/// <param name="FixtureId">The owning fixture's id for a door, lav, lav queue or galley node; null for a seat or an aisle slot.</param>
public readonly record struct NavNode(int Id, NodeKind Kind, int RowIndex, string Label, string? FixtureId);

/// <summary>A step from one node to a neighbour.</summary>
/// <param name="To">The neighbour's node id.</param>
/// <param name="Ticks">The ticks the step takes.</param>
public readonly record struct NavEdge(int To, int Ticks);

/// <summary>The cabin's walkable places and the steps between them, with every step's cost already in ticks.</summary>
public sealed class NavGraph
{
    private readonly ReadOnlyCollection<NavNode> nodes;
    private readonly ReadOnlyCollection<NavEdge>[] edges;
    private readonly List<List<int>> slotsByRow = [];
    private readonly Dictionary<string, int> fixtureNodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> lavQueues = new(StringComparer.Ordinal);
    private readonly ReadOnlyCollection<int> seatNodes;

    /// <summary>Builds the graph from its nodes, in id order, and its undirected links.</summary>
    /// <param name="nodes">The nodes; a node's id is its index, and a row's aisle slots appear in aisle order.</param>
    /// <param name="links">The undirected links, each added in both directions; every link takes at least one tick.</param>
    internal NavGraph(IReadOnlyList<NavNode> nodes, IReadOnlyList<(int A, int B, int Ticks)> links)
    {
        this.nodes = new ReadOnlyCollection<NavNode>([.. nodes]);
        edges = BuildEdges(nodes.Count, links);
        List<int> seats = [];
        foreach (NavNode node in nodes)
        {
            IndexNode(node, seats, nameof(nodes));
        }

        seatNodes = seats.AsReadOnly();
    }

    /// <summary>Gets every node, in id order.</summary>
    public IReadOnlyList<NavNode> Nodes => nodes;

    /// <summary>Gets the seat nodes, in id order.</summary>
    public IReadOnlyList<int> SeatNodes => seatNodes;

    /// <summary>Returns the steps out of a node, sorted by neighbour id ascending.</summary>
    /// <param name="nodeId">The node to step from.</param>
    /// <returns>The node's edges.</returns>
    public IReadOnlyList<NavEdge> EdgesFrom(int nodeId) => edges[nodeId];

    /// <summary>Returns the aisle slot beside a row on an aisle.</summary>
    /// <param name="rowIndex">The row, 0 at the front.</param>
    /// <param name="aisleIndex">The aisle, 0 at the left.</param>
    /// <returns>The slot's node id.</returns>
    public int AisleSlot(int rowIndex, int aisleIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rowIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(rowIndex, slotsByRow.Count);
        List<int> slots = slotsByRow[rowIndex];
        ArgumentOutOfRangeException.ThrowIfNegative(aisleIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(aisleIndex, slots.Count);
        return slots[aisleIndex];
    }

    /// <summary>Returns a fixture's node: the door or galley itself, or the lav itself for a lav.</summary>
    /// <param name="fixtureId">The fixture's id.</param>
    /// <returns>The fixture's node id.</returns>
    public int FixtureNode(string fixtureId) => Lookup(fixtureNodes, fixtureId, "fixture", nameof(fixtureId));

    /// <summary>Returns the queue node in front of a lav.</summary>
    /// <param name="lavId">The lav's fixture id.</param>
    /// <returns>The queue's node id.</returns>
    public int LavQueue(string lavId) => Lookup(lavQueues, lavId, "lav", nameof(lavId));

    private static ReadOnlyCollection<NavEdge>[] BuildEdges(int count, IReadOnlyList<(int A, int B, int Ticks)> links)
    {
        var adjacency = new List<NavEdge>[count];
        for (int i = 0; i < count; i++)
        {
            adjacency[i] = [];
        }

        foreach ((int a, int b, int ticks) in links)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(ticks, 1, nameof(links));
            adjacency[a].Add(new NavEdge(b, ticks));
            adjacency[b].Add(new NavEdge(a, ticks));
        }

        var sorted = new ReadOnlyCollection<NavEdge>[count];
        for (int i = 0; i < count; i++)
        {
            sorted[i] = new ReadOnlyCollection<NavEdge>([.. adjacency[i].OrderBy(edge => edge.To)]);
        }

        return sorted;
    }

    private static int Lookup(Dictionary<string, int> index, string id, string what, string paramName) =>
        index.TryGetValue(id, out int node) ? node : throw new ArgumentException($"The graph has no {what} '{id}'.", paramName);

    private static string FixtureIdOf(NavNode node, string paramName) =>
        node.FixtureId ?? throw new ArgumentException($"Node {node.Id} is a {node.Kind} with no fixture id.", paramName);

    private void IndexNode(NavNode node, List<int> seats, string paramName)
    {
        switch (node.Kind)
        {
            case NodeKind.AisleSlot:
                IndexSlot(node);
                break;
            case NodeKind.Seat:
                seats.Add(node.Id);
                break;
            case NodeKind.LavQueue:
                AddUnique(lavQueues, FixtureIdOf(node, paramName), node.Id, paramName);
                break;
            default:
                AddUnique(fixtureNodes, FixtureIdOf(node, paramName), node.Id, paramName);
                break;
        }
    }

    private void IndexSlot(NavNode node)
    {
        while (slotsByRow.Count <= node.RowIndex)
        {
            slotsByRow.Add([]);
        }

        slotsByRow[node.RowIndex].Add(node.Id);
    }

    private static void AddUnique(Dictionary<string, int> index, string id, int node, string paramName)
    {
        if (!index.TryAdd(id, node))
        {
            throw new ArgumentException($"Two fixtures share the id '{id}'.", paramName);
        }
    }
}
