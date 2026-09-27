namespace Sky.Engine.Cabin;

/// <summary>Who a holder of a node is: a person or a service cart.</summary>
public enum OccupantKind
{
    /// <summary>A passenger or a crew member.</summary>
    Person,

    /// <summary>A service cart, which fills its node alone.</summary>
    Cart,
}

/// <summary>The answer to a reservation.</summary>
public enum ReserveResult
{
    /// <summary>The node is full; nothing was recorded.</summary>
    Refused,

    /// <summary>The node had room and now holds the reservation.</summary>
    Reserved,

    /// <summary>The node was a full aisle slot with no cart, and the person squeezed in one over its capacity.</summary>
    Squeezed,
}

/// <summary>
/// The cabin's capacity and legality table: which characters hold each node, by reservation or arrival, and whether one more
/// may reserve it. A reservation and an arrival both count against a node's capacity. A person reserving a full aisle slot with
/// no cart squeezes in, one over capacity; any other full node refuses. A cart reserves only an empty aisle slot, galley or
/// door, and a node holding a cart admits nobody. Carts take their ids from the same <c>int</c> space as characters: the caller
/// allocates them, and a cart's id must not collide with any character's. A move is the caller's: reserve the next node, arrive
/// there, release the current one.
/// </summary>
public sealed class Occupancy
{
    private readonly NavGraph graph;
    private readonly int[] capacities;
    private readonly List<Holder>[] holders;

    /// <summary>Builds an empty table over a graph.</summary>
    /// <param name="graph">The cabin's navigation graph.</param>
    /// <param name="capacities">The capacity of each node kind.</param>
    public Occupancy(NavGraph graph, NodeCapacities capacities)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(capacities);
        this.graph = graph;
        int count = graph.Nodes.Count;
        this.capacities = new int[count];
        holders = new List<Holder>[count];
        for (int node = 0; node < count; node++)
        {
            this.capacities[node] = capacities.For(graph.Nodes[node].Kind);
            holders[node] = [];
        }
    }

    /// <summary>Asks for a node on behalf of a character; a refusal records nothing.</summary>
    /// <param name="node">The node's id.</param>
    /// <param name="character">The character's id.</param>
    /// <param name="kind">Whether the character is a person or a cart.</param>
    /// <returns>Whether the node refused, admitted, or admitted by squeeze.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The node is not in the graph, the character id is negative, or the kind is
    /// not defined.</exception>
    /// <exception cref="InvalidOperationException">The character already holds the node.</exception>
    public ReserveResult TryReserve(int node, int character, OccupantKind kind)
    {
        List<Holder> list = HoldersOf(node, character);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "The occupant kind is not defined.");
        }

        if (IndexOf(list, character) >= 0)
        {
            throw new InvalidOperationException($"Character {character} already holds node {node}.");
        }

        ReserveResult result = Admit(node, list, kind);
        if (result != ReserveResult.Refused)
        {
            list.Add(new Holder(character, kind, Arrived: false));
        }

        return result;
    }

    /// <summary>Turns a character's reservation on a node into an arrival.</summary>
    /// <param name="node">The node's id.</param>
    /// <param name="character">The character's id.</param>
    /// <exception cref="InvalidOperationException">The character holds nothing on the node, or has already arrived there.</exception>
    public void Arrive(int node, int character)
    {
        List<Holder> list = HoldersOf(node, character);
        int index = IndexOf(list, character);
        if (index < 0)
        {
            throw new InvalidOperationException($"Character {character} holds no reservation on node {node}.");
        }

        if (list[index].Arrived)
        {
            throw new InvalidOperationException($"Character {character} has already arrived at node {node}.");
        }

        list[index] = list[index] with { Arrived = true };
    }

    /// <summary>Frees whatever a character holds on a node, a reservation or an arrival.</summary>
    /// <param name="node">The node's id.</param>
    /// <param name="character">The character's id.</param>
    /// <exception cref="InvalidOperationException">The character holds nothing on the node.</exception>
    public void Release(int node, int character)
    {
        List<Holder> list = HoldersOf(node, character);
        int index = IndexOf(list, character);
        if (index < 0)
        {
            throw new InvalidOperationException($"Character {character} holds nothing on node {node}.");
        }

        list.RemoveAt(index);
    }

    /// <summary>Returns how many characters hold a node, reserved or arrived.</summary>
    /// <param name="node">The node's id.</param>
    /// <returns>The holder count.</returns>
    public int HolderCount(int node) => HoldersOf(node).Count;

    /// <summary>Returns the ids of the characters holding a node, reserved or arrived, in the order they reserved it.</summary>
    /// <param name="node">The node's id.</param>
    /// <returns>The holders' ids, earliest reservation first.</returns>
    public IReadOnlyList<int> Holders(int node) => [.. HoldersOf(node).Select(holder => holder.Character)];

    /// <summary>Returns whether a character holds a node, reserved or arrived.</summary>
    /// <param name="node">The node's id.</param>
    /// <param name="character">The character's id.</param>
    /// <returns>True when the character holds the node.</returns>
    public bool Holds(int node, int character) => IndexOf(HoldersOf(node, character), character) >= 0;

    /// <summary>Returns whether a character has arrived at a node.</summary>
    /// <param name="node">The node's id.</param>
    /// <param name="character">The character's id.</param>
    /// <returns>True when the character holds the node and has arrived there.</returns>
    public bool HasArrived(int node, int character)
    {
        List<Holder> list = HoldersOf(node, character);
        int index = IndexOf(list, character);
        return index >= 0 && list[index].Arrived;
    }

    /// <summary>Returns whether a cart holds a node, reserved or arrived.</summary>
    /// <param name="node">The node's id.</param>
    /// <returns>True when a cart holds the node.</returns>
    public bool HasCart(int node) => ContainsCart(HoldersOf(node));

    /// <summary>Returns a node's capacity, before any squeeze.</summary>
    /// <param name="node">The node's id.</param>
    /// <returns>The holders the node admits.</returns>
    public int Capacity(int node)
    {
        ValidateNode(node);
        return capacities[node];
    }

    private static int IndexOf(List<Holder> list, int character) => list.FindIndex(holder => holder.Character == character);

    private static bool ContainsCart(List<Holder> list) => list.Exists(holder => holder.Kind == OccupantKind.Cart);

    private ReserveResult Admit(int node, List<Holder> list, OccupantKind kind)
    {
        if (ContainsCart(list))
        {
            return ReserveResult.Refused;
        }

        NodeKind nodeKind = graph.Nodes[node].Kind;
        if (kind == OccupantKind.Cart)
        {
            return AdmitCart(nodeKind, list.Count);
        }

        if (list.Count < capacities[node])
        {
            return ReserveResult.Reserved;
        }

        bool atCapacity = list.Count == capacities[node];
        return nodeKind == NodeKind.AisleSlot && atCapacity ? ReserveResult.Squeezed : ReserveResult.Refused;
    }

    private static ReserveResult AdmitCart(NodeKind nodeKind, int holderCount)
    {
        bool cartKind = nodeKind is NodeKind.AisleSlot or NodeKind.Galley or NodeKind.Door;
        return cartKind && holderCount == 0 ? ReserveResult.Reserved : ReserveResult.Refused;
    }

    private List<Holder> HoldersOf(int node, int character)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(character);
        return HoldersOf(node);
    }

    private List<Holder> HoldersOf(int node)
    {
        ValidateNode(node);
        return holders[node];
    }

    private void ValidateNode(int node)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(node);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(node, holders.Length);
    }

    private readonly record struct Holder(int Character, OccupantKind Kind, bool Arrived);
}
