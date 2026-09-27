using Sky.Engine.Cabin;
using Sky.Engine.Execution;
using Sky.Engine.Manifest;
using Sky.Engine.Randomness;

namespace Sky.Engine.Flight;

/// <summary>The movement numbers of boarding and deboarding; each field refuses a bad value as it is set, naming itself.</summary>
public sealed record MovementRules
{
    /// <summary>The largest tick count a stow or retrieval range may reach, so a range's width never overflows.</summary>
    private const int MaxBinTicks = int.MaxValue - 1;

    /// <summary>Gets the ticks a boarding passenger stands in the aisle stowing their bag, drawn uniformly, both ends included.</summary>
    public required IntRange StowTicks
    {
        get;
        init => field = RuleChecks.Ordered(value, 0, MaxBinTicks, nameof(StowTicks));
    }

    /// <summary>Gets the ticks a deboarding passenger stands in the aisle retrieving their bag, drawn uniformly, both ends included.</summary>
    public required IntRange RetrievalTicks
    {
        get;
        init => field = RuleChecks.Ordered(value, 0, MaxBinTicks, nameof(RetrievalTicks));
    }

    /// <summary>Gets the extra ticks a hop takes when the walker squeezes past someone in a full aisle slot.</summary>
    public required int SqueezeExtraTicks
    {
        get;
        init => field = RuleChecks.AtLeast(value, 0, nameof(SqueezeExtraTicks));
    }

    /// <summary>Gets the extra ticks a passenger spends crossing each occupied seat between the aisle and their own.</summary>
    public required int OccupiedSeatCrossTicks
    {
        get;
        init => field = RuleChecks.AtLeast(value, 0, nameof(OccupiedSeatCrossTicks));
    }

    /// <summary>Gets how many economy rows board as one zone, counted from the back row.</summary>
    public required int EconomyZoneRows
    {
        get;
        init => field = RuleChecks.AtLeast(value, 1, nameof(EconomyZoneRows));
    }
}

/// <summary>What a character is doing, as far as the mover and the characters around it are concerned.</summary>
internal enum MoverState
{
    /// <summary>Not on the nav graph: not yet boarded.</summary>
    Absent,

    /// <summary>Stopped in place; a walker squeezes past.</summary>
    Standing,

    /// <summary>Walking, or waiting behind someone on the way; a walker going the same way waits behind.</summary>
    Walking,

    /// <summary>Stowing a bag in the aisle while boarding; passengers wait behind.</summary>
    Stowing,

    /// <summary>Retrieving a bag in the aisle while deboarding; passengers wait behind.</summary>
    Retrieving,

    /// <summary>Waiting in the deboarding line; passengers wait behind.</summary>
    Queued,

    /// <summary>Sitting in a seat.</summary>
    Seated,

    /// <summary>Left the aircraft.</summary>
    Off,
}

/// <summary>
/// The mover: every character's node and state, and the actions that move a character one hop at a time along the path table.
/// Before a hop the mover decides whether the walker may enter the next node, then asks <see cref="Occupancy"/>: a passenger
/// never passes a character who is stowing, retrieving or queued (crew do, crew.md R14 rule 2), nor a walker going the same way,
/// and waits behind them; passing anyone else in a full aisle slot is a squeeze, which costs
/// <see cref="MovementRules.SqueezeExtraTicks"/> more on that hop. Every action leaves the character standing, seated or off
/// when it finishes or is interrupted, holding only the node it stands on.
/// A hop reserves the next node, takes the edge's ticks, arrives there and releases the node left.
/// </summary>
/// <remarks>Creates the mover over a flight's graph, path table and occupancy, with every character absent.</remarks>
/// <param name="world">The flight, whose graph, paths, occupancy and passengers are built.</param>
/// <param name="rules">The movement numbers.</param>
/// <param name="characterCount">The characters the executor runs: the crew, then the passengers.</param>
internal sealed class Movement(FlightWorld world, MovementRules rules, int characterCount)
{
    /// <summary>The executor priority of every movement action.</summary>
    internal const int ActionPriority = 10;

    private readonly FlightWorld world = world;
    private readonly MovementRules rules = rules;
    private readonly int?[] nodes = new int?[characterCount];
    private readonly MoverState[] states = new MoverState[characterCount];
    private readonly int[] targets = new int[characterCount];

    /// <summary>Returns a character's state.</summary>
    /// <param name="character">The character id.</param>
    /// <returns>The state.</returns>
    public MoverState StateOf(int character) => states[character];

    /// <summary>Returns the node a character stands on, or null when it is not on the graph.</summary>
    /// <param name="character">The character id.</param>
    /// <returns>The node, or null.</returns>
    public int? NodeOf(int character) => nodes[character];

    /// <summary>Puts an absent character on a node, standing, when the node admits it without a squeeze.</summary>
    /// <param name="character">The character id.</param>
    /// <param name="node">The node entered, a door when boarding.</param>
    /// <returns>True when the character entered; false when the node was full and nothing changed.</returns>
    /// <exception cref="InvalidOperationException">The character is already on the graph or has left it.</exception>
    public bool TryEnter(int character, int node)
    {
        if (states[character] != MoverState.Absent)
        {
            throw new InvalidOperationException($"Character {character} cannot enter node {node}: it is {states[character]}.");
        }

        ReserveResult result = world.Occupancy.TryReserve(node, character, OccupantKind.Person);
        if (result != ReserveResult.Reserved)
        {
            ReleaseIfHeld(node, character, result);
            return false;
        }

        world.Occupancy.Arrive(node, character);
        Place(character, node);
        states[character] = MoverState.Standing;
        return true;
    }

    /// <summary>An action walking a character hop by hop to a node, ending when it arrives there.</summary>
    /// <param name="character">The character id.</param>
    /// <param name="target">The node walked to.</param>
    /// <param name="queueWhenBlocked">Whether a blocked walker stands in the deboarding line, which nobody passes.</param>
    /// <returns>The action.</returns>
    public CharacterAction Walk(int character, int target, bool queueWhenBlocked) => new WalkAction(this, character, target, queueWhenBlocked);

    /// <summary>An action holding a character in place in a bin state for a whole number of ticks drawn from a range.</summary>
    /// <param name="character">The character id.</param>
    /// <param name="state">The state held: stowing or retrieving.</param>
    /// <param name="range">The ticks to draw from, both ends included.</param>
    /// <param name="stream">The stream the ticks are drawn from, on the action's first tick.</param>
    /// <returns>The action.</returns>
    public CharacterAction Hold(int character, MoverState state, IntRange range, SimRandom stream) =>
        new HoldAction(this, character, state, range, stream);

    /// <summary>
    /// An action moving a character between an aisle slot and a seat as one move: the path's ticks plus the crossing ticks for each
    /// occupied seat between. Standing up waits until the target is empty; the character takes <paramref name="arrival"/> on arrival.
    /// </summary>
    /// <param name="character">The character id.</param>
    /// <param name="target">The seat or aisle slot moved to.</param>
    /// <param name="arrival">The state on arrival: seated, or standing in the aisle.</param>
    /// <returns>The action.</returns>
    public CharacterAction Cross(int character, int target, MoverState arrival) => new CrossAction(this, character, target, arrival);

    /// <summary>An action taking a character off the graph from the node it stands on.</summary>
    /// <param name="character">The character id.</param>
    /// <returns>The action.</returns>
    public CharacterAction Exit(int character) => new ExitAction(this, character);

    private static bool IsLine(MoverState state) => state is MoverState.Stowing or MoverState.Retrieving or MoverState.Queued;

    private static int EdgeTicks(NavGraph graph, int from, int to)
    {
        foreach (NavEdge edge in graph.EdgesFrom(from))
        {
            if (edge.To == to)
            {
                return edge.Ticks;
            }
        }

        throw new InvalidOperationException($"Node {to} is not a neighbour of node {from}.");
    }

    private void ReleaseIfHeld(int node, int character, ReserveResult result)
    {
        if (result == ReserveResult.Squeezed)
        {
            world.Occupancy.Release(node, character);
        }
    }

    private int Current(int character) => nodes[character] ?? throw new InvalidOperationException($"Character {character} is not on the nav graph.");

    private void Place(int character, int? node)
    {
        nodes[character] = node;
        int passengerId = character - world.CrewCount;
        if (passengerId >= 0)
        {
            world.Passengers[passengerId].Node = node;
        }
    }

    /// <summary>The sign of the row change from a character's node to its target: +1 aft, -1 forward, 0 along its own row.</summary>
    private int Heading(int character)
    {
        IReadOnlyList<NavNode> graphNodes = world.Graph.Nodes;
        return Math.Sign(graphNodes[targets[character]].RowIndex - graphNodes[Current(character)].RowIndex);
    }

    /// <summary>
    /// Whether a holder of the node a walker wants stops it: someone in the line, unless the walker is crew (crew.md R14 rule 2:
    /// crew pass stowers to give bin help), or a walker not coming the other way.
    /// </summary>
    private bool Blocks(int walker, int holder)
    {
        MoverState state = states[holder];
        bool passenger = walker >= world.CrewCount;
        return (IsLine(state) && passenger) || (state == MoverState.Walking && Heading(walker) * Heading(holder) >= 0);
    }

    /// <summary>Whether any holder of a full aisle slot stops the walker; other nodes are Occupancy's to refuse.</summary>
    private bool IsBlocked(int character, int next)
    {
        Occupancy occupancy = world.Occupancy;
        int holders = occupancy.HolderCount(next);
        if (holders < occupancy.Capacity(next) || world.Graph.Nodes[next].Kind != NodeKind.AisleSlot)
        {
            return false;
        }

        for (int index = 0; index < holders; index++)
        {
            if (Blocks(character, occupancy.HolderAt(next, index)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Reserves the next node of a walk, returning the hop's extra ticks, or null when the walker must wait.</summary>
    private int? TryReserveHop(int character, int next)
    {
        Occupancy occupancy = world.Occupancy;
        if (IsBlocked(character, next))
        {
            return null;
        }

        return occupancy.TryReserve(next, character, OccupantKind.Person) switch
        {
            ReserveResult.Reserved => 0,
            ReserveResult.Squeezed => rules.SqueezeExtraTicks,
            _ => null,
        };
    }

    private void CompleteMove(int character, int to)
    {
        int from = Current(character);
        world.Occupancy.Arrive(to, character);
        world.Occupancy.Release(from, character);
        Place(character, to);
    }

    /// <summary>The seats strictly between two nodes on the cheapest walk that someone holds.</summary>
    private int OccupiedSeatsBetween(int from, int to)
    {
        int crossed = 0;
        for (int node = world.Paths.NextHop(from, to); node != to; node = world.Paths.NextHop(node, to))
        {
            bool seat = world.Graph.Nodes[node].Kind == NodeKind.Seat;
            crossed += seat && world.Occupancy.HolderCount(node) > 0 ? 1 : 0;
        }

        return crossed;
    }

    /// <summary>Walks one hop at a time toward a target, reserving each node before entering it.</summary>
    private sealed class WalkAction(Movement mover, int character, int target, bool queueWhenBlocked) : CharacterAction(ActionPriority)
    {
        private int next = -1;
        private int remaining;

        /// <summary>Done, standing, once at the target; otherwise still walking.</summary>
        private ActionStatus Progress()
        {
            if (mover.Current(character) != target)
            {
                return ActionStatus.Running;
            }

            mover.states[character] = MoverState.Standing;
            return ActionStatus.Done;
        }

        public override ActionStatus Tick(long tick)
        {
            if (next < 0 && !TryStartHop())
            {
                return Progress();
            }

            remaining--;
            if (remaining > 0)
            {
                return ActionStatus.Running;
            }

            mover.CompleteMove(character, next);
            next = -1;
            return Progress();
        }

        public override void Cleanup(long tick)
        {
            if (next >= 0)
            {
                mover.world.Occupancy.Release(next, character);
                next = -1;
            }

            mover.states[character] = MoverState.Standing;
        }

        /// <summary>Reserves the next hop; false when the walker is already at the target or must wait.</summary>
        private bool TryStartHop()
        {
            int node = mover.Current(character);
            mover.targets[character] = target;
            if (node == target)
            {
                return false;
            }

            int hop = mover.world.Paths.NextHop(node, target);
            if (hop < 0)
            {
                throw new InvalidOperationException($"Character {character} cannot reach node {target} from node {node}.");
            }

            int? extra = mover.TryReserveHop(character, hop);
            mover.states[character] = extra is null && queueWhenBlocked ? MoverState.Queued : MoverState.Walking;
            if (extra is not { } extraTicks)
            {
                return false;
            }

            next = hop;
            remaining = EdgeTicks(mover.world.Graph, node, hop) + extraTicks;
            return true;
        }
    }

    /// <summary>
    /// Holds a character in a bin state for a draw of N ticks, drawn on the first tick: the character reads as in that state at
    /// the end of each of the N ticks from the draw tick, and stands again on the tick after the last, so a draw of zero holds
    /// for no tick at all.
    /// </summary>
    private sealed class HoldAction(Movement mover, int character, MoverState state, IntRange range, SimRandom stream)
        : CharacterAction(ActionPriority)
    {
        private int remaining = -1;

        public override ActionStatus Tick(long tick)
        {
            if (remaining < 0)
            {
                remaining = range.Min + stream.NextInt(range.Max - range.Min + 1);
            }
            else
            {
                remaining--;
            }

            if (remaining <= 0)
            {
                mover.states[character] = MoverState.Standing;
                return ActionStatus.Done;
            }

            mover.states[character] = state;
            return ActionStatus.Running;
        }

        public override void Cleanup(long tick) => mover.states[character] = MoverState.Standing;
    }

    /// <summary>Moves between an aisle slot and a seat as one move, waiting for an empty target.</summary>
    private sealed class CrossAction(Movement mover, int character, int target, MoverState arrival) : CharacterAction(ActionPriority)
    {
        private int remaining = -1;

        public override ActionStatus Tick(long tick)
        {
            if (remaining < 0 && !TryReserveTarget())
            {
                return ActionStatus.Running;
            }

            remaining--;
            if (remaining > 0)
            {
                return ActionStatus.Running;
            }

            mover.CompleteMove(character, target);
            mover.states[character] = arrival;
            return ActionStatus.Done;
        }

        public override void Cleanup(long tick)
        {
            if (remaining >= 0)
            {
                mover.world.Occupancy.Release(target, character);
                remaining = -1;
            }

            mover.states[character] = MoverState.Standing;
        }

        private bool TryReserveTarget()
        {
            Occupancy occupancy = mover.world.Occupancy;
            if (occupancy.HolderCount(target) > 0)
            {
                return false;
            }

            ReserveResult result = occupancy.TryReserve(target, character, OccupantKind.Person);
            if (result != ReserveResult.Reserved)
            {
                mover.ReleaseIfHeld(target, character, result);
                return false;
            }

            int from = mover.Current(character);
            mover.targets[character] = target;
            mover.states[character] = MoverState.Walking;
            int crossing = mover.OccupiedSeatsBetween(from, target) * mover.rules.OccupiedSeatCrossTicks;
            remaining = Math.Max(1, mover.world.Paths.Cost(from, target) + crossing);
            return true;
        }
    }

    /// <summary>Takes a character off the graph in one tick.</summary>
    private sealed class ExitAction(Movement mover, int character) : CharacterAction(ActionPriority)
    {
        public override ActionStatus Tick(long tick)
        {
            int node = mover.Current(character);
            mover.world.Occupancy.Release(node, character);
            mover.Place(character, null);
            mover.states[character] = MoverState.Off;
            int passengerId = character - mover.world.CrewCount;
            if (passengerId >= 0)
            {
                mover.world.Passengers[passengerId].IsOff = true;
            }

            return ActionStatus.Done;
        }
    }
}

/// <summary>
/// Runs a list of actions one after another as one executor action. Each step is built when it starts, on the tick the step
/// before it finishes, so no tick passes between two steps; an interruption cleans up the step running.
/// </summary>
/// <param name="steps">The steps, in order; the chain is done when the last one is.</param>
internal sealed class ActionChain(IReadOnlyList<Func<CharacterAction>> steps) : CharacterAction(Movement.ActionPriority)
{
    private int index;
    private CharacterAction? current;

    /// <inheritdoc/>
    public override ActionStatus Tick(long tick)
    {
        while (index < steps.Count)
        {
            current ??= steps[index]();
            if (current.Tick(tick) == ActionStatus.Running)
            {
                return ActionStatus.Running;
            }

            current = null;
            index++;
        }

        return ActionStatus.Done;
    }

    /// <inheritdoc/>
    public override void Cleanup(long tick) => current?.Cleanup(tick);
}
