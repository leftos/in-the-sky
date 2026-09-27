using Sky.Engine.Execution;

namespace Sky.Engine.Crew;

/// <summary>
/// The crew's shared list of work. Open tasks are claimed highest claim priority first, then oldest posted, then lowest
/// id; a crew member holding a task is pre-empted only by an open task whose claim is strictly above the held task's
/// hold, and the pre-empted task returns to the board with its original posted tick. The board swaps crew actions
/// through <see cref="SequenceExecutor.Replace"/>, so the arbitration is the board's and the cleanup the executor's.
/// It is zone-agnostic: every claim takes a caller predicate that carries the zone, spill and seated rules. While it
/// starts a task the board refuses every call that changes it, so an <c>actionFor</c> factory and an action's cleanup
/// never call the board.
/// </summary>
public sealed class TaskBoard
{
    private readonly SequenceExecutor executor;
    private readonly List<Posting> open = [];
    private readonly Posting?[] held;
    private long nextSequence;
    private int? arbitratingCrew;

    /// <summary>Creates an empty board for crew ids 0 to <paramref name="crewCount"/> - 1, which are also their executor character ids.</summary>
    /// <param name="executor">The executor that runs the crew's actions.</param>
    /// <param name="crewCount">How many crew members claim from the board.</param>
    public TaskBoard(SequenceExecutor executor, int crewCount)
    {
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentOutOfRangeException.ThrowIfNegative(crewCount);
        this.executor = executor;
        held = new Posting?[crewCount];
    }

    /// <summary>The open tasks, in the order they were first posted.</summary>
    public IReadOnlyList<CrewTask> OpenTasks => open.ConvertAll(posting => posting.Task);

    /// <summary>
    /// The most ticks any open task has spent on the board on ticks the caller reported, through
    /// <see cref="AccrueIdleWait"/>, an able crew member idle for it; cumulative across pre-emptions, and not the task's
    /// age. 0 when no task is open.
    /// </summary>
    public long LongestWaitWhileIdle
    {
        get
        {
            long longest = 0;
            foreach (Posting posting in open)
            {
                longest = Math.Max(longest, posting.IdleWaitTicks);
            }

            return longest;
        }
    }

    /// <summary>Puts <paramref name="task"/> on the board, open.</summary>
    /// <param name="task">The task to post.</param>
    /// <exception cref="ArgumentException">A task with the same id is already open or held.</exception>
    public void Post(CrewTask task)
    {
        RequireNotArbitrating();
        ArgumentNullException.ThrowIfNull(task);
        if (Find(task.Id) is not null)
        {
            throw new ArgumentException($"Task {task.Id} is already on the board; a task id is posted once while it is open or held.", nameof(task));
        }

        open.Add(new Posting(task, nextSequence++));
    }

    /// <summary>Takes an open task off the board unclaimed, as when it times out.</summary>
    /// <param name="taskId">The id of an open task.</param>
    /// <returns>The withdrawn task.</returns>
    /// <exception cref="InvalidOperationException">A crew member holds the task.</exception>
    /// <exception cref="ArgumentException">No open or held task has the id.</exception>
    public CrewTask Withdraw(int taskId)
    {
        RequireNotArbitrating();
        Posting posting = Find(taskId) ?? throw new ArgumentException($"Task {taskId} is neither open nor held on the board.", nameof(taskId));
        if (posting.IsHeld)
        {
            throw new InvalidOperationException($"Task {taskId} is held by a crew member and cannot be withdrawn; complete or pre-empt it first.");
        }

        open.Remove(posting);
        return posting.Task;
    }

    /// <summary>
    /// Gives <paramref name="crew"/>, who holds no task, the best open task <paramref name="mayTake"/> allows, and starts
    /// its action through the executor.
    /// </summary>
    /// <param name="crew">The crew id.</param>
    /// <param name="tick">The tick of the claim, recorded as the task's claimed tick.</param>
    /// <param name="mayTake">Whether this crew member may take a task (zone, spill and seated rules).</param>
    /// <param name="actionFor">Builds the action that carries a task out; it must not call the board.</param>
    /// <returns>The claimed task, or <see langword="null"/> when no open task may be taken.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="crew"/> already holds a task.</exception>
    public CrewTask? TryClaim(int crew, long tick, Func<CrewTask, bool> mayTake, Func<CrewTask, CharacterAction> actionFor)
    {
        RequireNotArbitrating();
        RequireCrew(crew);
        ArgumentNullException.ThrowIfNull(mayTake);
        ArgumentNullException.ThrowIfNull(actionFor);
        if (held[crew] is { } holding)
        {
            throw new InvalidOperationException($"Crew {crew} holds task {holding.Task.Id} and cannot claim another; pre-empt or complete it first.");
        }

        int best = BestOpen(mayTake, claimAbove: null);
        return best < 0 ? null : Start(crew, tick, best, actionFor);
    }

    /// <summary>
    /// Moves <paramref name="crew"/> from its held task to the best open task <paramref name="mayTake"/> allows whose claim
    /// is strictly above the held task's hold. The held task's action is cleaned up by the executor and the task returns
    /// to the board with its original posted tick.
    /// </summary>
    /// <param name="crew">The crew id.</param>
    /// <param name="tick">The tick of the pre-emption, recorded as the new task's claimed tick.</param>
    /// <param name="mayTake">Whether this crew member may take a task (zone, spill and seated rules).</param>
    /// <param name="actionFor">Builds the action that carries a task out; it must not call the board.</param>
    /// <returns>The newly held task, or <see langword="null"/> when no open task out-claims the held one's hold.</returns>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="crew"/> holds no task, or the executor no longer runs the held task's action.
    /// </exception>
    public CrewTask? TryPreempt(int crew, long tick, Func<CrewTask, bool> mayTake, Func<CrewTask, CharacterAction> actionFor)
    {
        RequireNotArbitrating();
        RequireCrew(crew);
        ArgumentNullException.ThrowIfNull(mayTake);
        ArgumentNullException.ThrowIfNull(actionFor);
        Posting holding = held[crew] ?? throw new InvalidOperationException($"Crew {crew} holds no task to be pre-empted; claim one instead.");
        if (!ReferenceEquals(executor.ActionOf(crew), holding.Action))
        {
            throw new InvalidOperationException(
                $"Crew {crew}'s task {holding.Task.Id} is no longer the executor's running action; complete it first."
            );
        }

        int best = BestOpen(mayTake, claimAbove: holding.Task.HoldPriority);
        if (best < 0)
        {
            return null;
        }

        CrewTask started = Start(crew, tick, best, actionFor);
        ReturnToOpen(holding);
        return started;
    }

    /// <summary>Takes <paramref name="crew"/>'s held task off the board; the executor is not touched.</summary>
    /// <param name="crew">The crew id.</param>
    /// <returns>The finished task with its claim tick, so its wait survives it.</returns>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="crew"/> holds no task, or the executor runs an action other than the held task's.
    /// </exception>
    public CompletedTask Complete(int crew)
    {
        RequireNotArbitrating();
        RequireCrew(crew);
        Posting holding = held[crew] ?? throw new InvalidOperationException($"Crew {crew} holds no task to complete.");
        CharacterAction? running = executor.ActionOf(crew);
        if (running is not null && !ReferenceEquals(running, holding.Action))
        {
            throw new InvalidOperationException(
                $"Crew {crew}'s task {holding.Task.Id} cannot complete: the executor runs another action for the crew member."
            );
        }

        held[crew] = null;
        return new CompletedTask(holding.Task, holding.ClaimTick);
    }

    /// <summary>The task <paramref name="crew"/> holds, or <see langword="null"/> when it holds none.</summary>
    /// <param name="crew">The crew id.</param>
    /// <returns>The held task, or <see langword="null"/>.</returns>
    public CrewTask? HeldBy(int crew)
    {
        RequireCrew(crew);
        return held[crew]?.Task;
    }

    /// <summary>How long a task has waited: to <paramref name="tick"/> while open, to its latest claim once held.</summary>
    /// <param name="taskId">The id of an open or held task.</param>
    /// <param name="tick">The current tick, read for an open task.</param>
    /// <returns>The wait in ticks from the task's posted tick.</returns>
    /// <exception cref="ArgumentException">No open or held task has the id; a completed or withdrawn task is off the board.</exception>
    public long WaitTicks(int taskId, long tick)
    {
        Posting posting = Find(taskId) ?? throw new ArgumentException($"Task {taskId} is neither open nor held on the board.", nameof(taskId));
        long end = posting.IsHeld ? posting.ClaimTick : tick;
        return end - posting.Task.PostedTick;
    }

    /// <summary>Adds one idle-wait tick to each open task the predicate reports an able crew member idle for; called once a tick.</summary>
    /// <param name="ableCrewIdle">Whether an able crew member who may take the task is idle this tick.</param>
    public void AccrueIdleWait(Func<CrewTask, bool> ableCrewIdle)
    {
        RequireNotArbitrating();
        ArgumentNullException.ThrowIfNull(ableCrewIdle);
        foreach (Posting posting in open)
        {
            if (ableCrewIdle(posting.Task))
            {
                posting.IdleWaitTicks++;
            }
        }
    }

    private static bool IsBetter(CrewTask candidate, CrewTask best)
    {
        if (candidate.ClaimPriority != best.ClaimPriority)
        {
            return candidate.ClaimPriority > best.ClaimPriority;
        }

        return candidate.PostedTick != best.PostedTick ? candidate.PostedTick < best.PostedTick : candidate.Id < best.Id;
    }

    private int BestOpen(Func<CrewTask, bool> mayTake, int? claimAbove)
    {
        int best = -1;
        for (int index = 0; index < open.Count; index++)
        {
            CrewTask task = open[index].Task;
            bool outClaimed = claimAbove is { } floor && task.ClaimPriority <= floor;
            if (outClaimed || !mayTake(task))
            {
                continue;
            }

            if (best < 0 || IsBetter(task, open[best].Task))
            {
                best = index;
            }
        }

        return best;
    }

    private CrewTask Start(int crew, long tick, int openIndex, Func<CrewTask, CharacterAction> actionFor)
    {
        Posting posting = open[openIndex];
        arbitratingCrew = crew;
        CharacterAction action;
        try
        {
            action = actionFor(posting.Task);
            executor.Replace(crew, action, tick);
        }
        finally
        {
            arbitratingCrew = null;
        }

        open.RemoveAt(openIndex);
        posting.ClaimTick = tick;
        posting.Action = action;
        held[crew] = posting;
        return posting.Task;
    }

    private void ReturnToOpen(Posting posting)
    {
        posting.Action = null;
        int index = open.FindIndex(other => other.Sequence > posting.Sequence);
        open.Insert(index < 0 ? open.Count : index, posting);
    }

    private Posting? Find(int taskId)
    {
        foreach (Posting posting in open)
        {
            if (posting.Task.Id == taskId)
            {
                return posting;
            }
        }

        foreach (Posting? posting in held)
        {
            if (posting?.Task.Id == taskId)
            {
                return posting;
            }
        }

        return null;
    }

    private void RequireNotArbitrating()
    {
        if (arbitratingCrew is { } crew)
        {
            throw new InvalidOperationException(
                $"The board is starting a task for crew {crew}; an actionFor factory or an action's cleanup must not call the board."
            );
        }
    }

    private void RequireCrew(int crew)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(crew);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(crew, held.Length);
    }

    private sealed class Posting(CrewTask task, long sequence)
    {
        public CrewTask Task { get; } = task;

        public long Sequence { get; } = sequence;

        public long IdleWaitTicks { get; set; }

        public long ClaimTick { get; set; }

        public CharacterAction? Action { get; set; }

        public bool IsHeld => Action is not null;
    }
}
