namespace Sky.Engine.Cabin;

/// <summary>
/// The cheapest walk in ticks between every pair of nodes of a <see cref="NavGraph"/>, and the first step of it. Among equally
/// cheap first steps the lowest neighbour id wins, so two builds of one graph give identical tables.
/// </summary>
public sealed class PathTable
{
    private readonly int[] costs;
    private readonly int[] nextHops;

    private PathTable(NavGraph graph)
    {
        NodeCount = graph.Nodes.Count;
        costs = new int[NodeCount * NodeCount];
        nextHops = new int[NodeCount * NodeCount];
        for (int target = 0; target < NodeCount; target++)
        {
            FillCosts(graph, target);
        }

        for (int target = 0; target < NodeCount; target++)
        {
            FillNextHops(graph, target);
        }
    }

    /// <summary>Gets the number of nodes the table covers.</summary>
    public int NodeCount { get; }

    /// <summary>Builds the table with one Dijkstra run on ticks per target node.</summary>
    /// <param name="graph">The graph to route over.</param>
    /// <returns>The table.</returns>
    public static PathTable Build(NavGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return new PathTable(graph);
    }

    /// <summary>Returns the first step of the cheapest walk from one node to another.</summary>
    /// <param name="from">The node walked from.</param>
    /// <param name="to">The node walked to.</param>
    /// <returns><paramref name="to"/> when the two are the same node, -1 when <paramref name="to"/> is unreachable, otherwise
    /// the neighbour of <paramref name="from"/> to step to.</returns>
    public int NextHop(int from, int to) => nextHops[Index(from, to)];

    /// <summary>Returns the ticks of the cheapest walk from one node to another.</summary>
    /// <param name="from">The node walked from.</param>
    /// <param name="to">The node walked to.</param>
    /// <returns>The walk's ticks, or -1 when <paramref name="to"/> is unreachable.</returns>
    public int Cost(int from, int to) => costs[Index(from, to)];

    private int Index(int from, int to)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(from);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(from, NodeCount);
        ArgumentOutOfRangeException.ThrowIfNegative(to);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(to, NodeCount);
        return (from * NodeCount) + to;
    }

    /// <summary>Runs Dijkstra from the target; the graph is undirected, so a cost from the target is a cost to it.</summary>
    private void FillCosts(NavGraph graph, int target)
    {
        int[] distance = new int[NodeCount];
        Array.Fill(distance, int.MaxValue);
        distance[target] = 0;
        var queue = new PriorityQueue<int, (int Distance, int Node)>();
        queue.Enqueue(target, (0, target));
        while (queue.TryDequeue(out int node, out (int Distance, int Node) priority))
        {
            if (priority.Distance > distance[node])
            {
                continue;
            }

            foreach (NavEdge edge in graph.EdgesFrom(node))
            {
                int candidate = priority.Distance + edge.Ticks;
                if (candidate < distance[edge.To])
                {
                    distance[edge.To] = candidate;
                    queue.Enqueue(edge.To, (candidate, edge.To));
                }
            }
        }

        for (int node = 0; node < NodeCount; node++)
        {
            costs[(node * NodeCount) + target] = distance[node] == int.MaxValue ? -1 : distance[node];
        }
    }

    private void FillNextHops(NavGraph graph, int target)
    {
        for (int node = 0; node < NodeCount; node++)
        {
            int own = costs[(node * NodeCount) + target];
            int hop = own < 0 ? -1 : target;
            if (own > 0)
            {
                hop = CheapestNeighbour(graph, node, target, own);
            }

            nextHops[(node * NodeCount) + target] = hop;
        }
    }

    /// <summary>Returns the lowest-id neighbour that lies on a cheapest walk to the target.</summary>
    private int CheapestNeighbour(NavGraph graph, int node, int target, int own)
    {
        int best = -1;
        foreach (NavEdge edge in graph.EdgesFrom(node))
        {
            int via = costs[(edge.To * NodeCount) + target];
            bool onCheapestWalk = via >= 0 && via + edge.Ticks == own;
            if (onCheapestWalk && (best < 0 || edge.To < best))
            {
                best = edge.To;
            }
        }

        return best;
    }
}
