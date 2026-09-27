using Sky.Engine.Crew;
using Sky.Engine.Execution;

namespace Sky.Engine.Tests.Crew;

/// <summary>Pins the task board's claim order, its claim-against-hold pre-emption through the executor, and its wait read-outs.</summary>
public sealed class TaskBoardTests
{
    private static readonly Func<CrewTask, bool> Anything = _ => true;

    /// <summary>An idle crew member claims the open task with the highest claim priority, whatever the posting order.</summary>
    [Fact]
    public void ClaimsHighestPriorityFirst()
    {
        Fixture fixture = new(crewCount: 2);
        fixture.Board.Post(Posted(1, TaskKind.LavCheck, 30, 30, postedTick: 0));
        fixture.Board.Post(Posted(2, TaskKind.CallButton, 60, 60, postedTick: 5));
        fixture.Board.Post(Posted(3, TaskKind.CheckInWalk, 20, 20, postedTick: 1));

        Assert.Equal(2, fixture.Claim(0, tick: 6)?.Id);
        Assert.Equal(1, fixture.Claim(1, tick: 6)?.Id);
        Assert.Equal([3], fixture.Board.OpenTasks.Select(task => task.Id));
        Assert.Equal(2, fixture.Board.HeldBy(0)?.Id);
        Assert.NotNull(fixture.Executor.ActionOf(0));
    }

    /// <summary>Within one claim priority the task posted first is claimed first, even when it was put on the board later.</summary>
    [Fact]
    public void ClaimsOldestFirstWithinPriority()
    {
        Fixture fixture = new(crewCount: 1);
        fixture.Board.Post(Posted(1, TaskKind.CallButton, 60, 60, postedTick: 10));
        fixture.Board.Post(Posted(2, TaskKind.CallButton, 60, 60, postedTick: 4));

        Assert.Equal(2, fixture.Claim(0, tick: 12)?.Id);
    }

    /// <summary>With equal claim and posted tick the lower task id is claimed, whatever the posting order.</summary>
    [Fact]
    public void EqualClaimAndAgeGoesToLowerId()
    {
        Fixture fixture = new(crewCount: 1);
        fixture.Board.Post(Posted(7, TaskKind.CallButton, 60, 60, postedTick: 3));
        fixture.Board.Post(Posted(3, TaskKind.CallButton, 60, 60, postedTick: 3));

        Assert.Equal(3, fixture.Claim(0, tick: 4)?.Id);
    }

    /// <summary>The caller's predicate hides tasks from a claim; with nothing allowed the claim returns null and changes nothing.</summary>
    [Fact]
    public void MayTakeFiltersTasks()
    {
        Fixture fixture = new(crewCount: 1);
        fixture.Board.Post(Posted(1, TaskKind.CallButton, 60, 60, postedTick: 0, zone: "A"));
        fixture.Board.Post(Posted(2, TaskKind.LavCheck, 30, 30, postedTick: 0, zone: "B"));

        Assert.Null(fixture.Board.TryClaim(0, 1, _ => false, fixture.ActionFor));
        Assert.Null(fixture.Board.HeldBy(0));
        Assert.Null(fixture.Executor.ActionOf(0));
        Assert.Equal([1, 2], fixture.Board.OpenTasks.Select(task => task.Id));

        Assert.Equal(2, fixture.Board.TryClaim(0, 1, task => task.Zone == "B", fixture.ActionFor)?.Id);
    }

    /// <summary>A call button (claim 60) does not pre-empt a running cart (hold 75): nothing changes and the cart is not cleaned up.</summary>
    [Fact]
    public void CallButtonDoesNotPreemptCart()
    {
        Fixture fixture = new(crewCount: 1);
        fixture.Board.Post(Cart(id: 1, postedTick: 0));
        fixture.Claim(0, tick: 0);
        fixture.Board.Post(Posted(2, TaskKind.CallButton, 60, 60, postedTick: 1));

        Assert.Null(fixture.Board.TryPreempt(0, 1, Anything, fixture.ActionFor));

        Assert.Equal(1, fixture.Board.HeldBy(0)?.Id);
        Assert.Same(fixture.Actions[1], fixture.Executor.ActionOf(0));
        Assert.Equal(0, fixture.Actions[1].CleanupCount);
        Assert.Equal([2], fixture.Board.OpenTasks.Select(task => task.Id));
    }

    /// <summary>
    /// A secure check (claim 90) pre-empts a cart (hold 75): the cart's cleanup runs once, before the check's first tick,
    /// and the cart returns to the board in its posting place with its original posted tick.
    /// </summary>
    [Fact]
    public void SecureCheckPreemptsCartAndCartReturnsToBoard()
    {
        Fixture fixture = new(crewCount: 1);
        CrewTask cart = Cart(id: 1, postedTick: 2);
        fixture.Board.Post(cart);
        fixture.Claim(0, tick: 3);
        fixture.Executor.Tick(3);
        fixture.Executor.Tick(4);
        fixture.Board.Post(Posted(2, TaskKind.LavCheck, 30, 30, postedTick: 4));
        fixture.Board.Post(Posted(3, TaskKind.SecureCheck, 90, 90, postedTick: 5));

        Assert.Equal(3, fixture.Board.TryPreempt(0, 5, Anything, fixture.ActionFor)?.Id);
        fixture.Executor.Tick(5);

        Assert.Equal(["task 1 tick 3", "task 1 tick 4", "task 1 cleanup 5", "task 3 tick 5"], fixture.Log);
        Assert.Equal(1, fixture.Actions[1].CleanupCount);
        Assert.Equal(3, fixture.Board.HeldBy(0)?.Id);
        Assert.Equal([1, 2], fixture.Board.OpenTasks.Select(task => task.Id));
        Assert.Same(cart, fixture.Board.OpenTasks[0]);
        Assert.Equal(2, fixture.Board.OpenTasks[0].PostedTick);
    }

    /// <summary>A call button (claim 60) pre-empts a galley break (claim 10, hold 35), which returns to the board.</summary>
    [Fact]
    public void CallButtonPreemptsGalleyBreak()
    {
        Fixture fixture = new(crewCount: 1);
        fixture.Board.Post(Posted(1, TaskKind.GalleyBreak, 10, 35, postedTick: 0));
        fixture.Claim(0, tick: 0);
        fixture.Board.Post(Posted(2, TaskKind.CallButton, 60, 60, postedTick: 1));

        Assert.Equal(2, fixture.Board.TryPreempt(0, 1, Anything, fixture.ActionFor)?.Id);

        Assert.Equal(1, fixture.Actions[1].CleanupCount);
        Assert.Same(fixture.Actions[2], fixture.Executor.ActionOf(0));
        Assert.Equal([1], fixture.Board.OpenTasks.Select(task => task.Id));
    }

    /// <summary>
    /// Idle wait grows only on ticks the caller reports an able crew member idle for the task, not while the task is
    /// held, and a pre-empted task keeps what it had gathered.
    /// </summary>
    [Fact]
    public void LongestWaitWhileIdleCountsOnlyIdleTicks()
    {
        Fixture fixture = new(crewCount: 1);
        Assert.Equal(0, fixture.Board.LongestWaitWhileIdle);
        fixture.Board.Post(Posted(1, TaskKind.LavCheck, 30, 30, postedTick: 0));
        fixture.Board.Post(Posted(2, TaskKind.CheckInWalk, 20, 20, postedTick: 0));

        bool[] idle = [true, false, true, false, false, true];
        foreach (bool crewIdle in idle)
        {
            fixture.Board.AccrueIdleWait(task => crewIdle && task.Id == 1);
        }

        Assert.Equal(3, fixture.Board.LongestWaitWhileIdle);

        fixture.Claim(0, tick: 6);
        fixture.Board.AccrueIdleWait(_ => true);
        Assert.Equal(1, fixture.Board.LongestWaitWhileIdle);

        fixture.Board.Post(Posted(3, TaskKind.SecureCheck, 90, 90, postedTick: 7));
        fixture.Board.TryPreempt(0, 7, Anything, fixture.ActionFor);
        Assert.Equal(3, fixture.Board.LongestWaitWhileIdle);
        fixture.Board.AccrueIdleWait(task => task.Id == 1);
        Assert.Equal(4, fixture.Board.LongestWaitWhileIdle);
    }

    /// <summary>A task's wait runs from its posted tick to now while open and stops at its claim; an unknown id throws.</summary>
    [Fact]
    public void WaitTicksMeasuresPostedToClaimed()
    {
        Fixture fixture = new(crewCount: 1);
        fixture.Board.Post(Posted(1, TaskKind.CallButton, 60, 60, postedTick: 10));

        Assert.Equal(15, fixture.Board.WaitTicks(1, 25));
        fixture.Claim(0, tick: 30);
        Assert.Equal(20, fixture.Board.WaitTicks(1, 100));

        CompletedTask completed = fixture.Board.Complete(0);
        Assert.Equal(1, completed.Task.Id);
        Assert.Equal(30, completed.ClaimTick);
        Assert.Equal(20, completed.WaitTicks);
        Assert.Throws<ArgumentException>(() => fixture.Board.WaitTicks(1, 100));
        Assert.Throws<ArgumentException>(() => fixture.Board.WaitTicks(99, 100));
    }

    /// <summary>Posting an id that is open or held throws; once completed the id may be posted again.</summary>
    [Fact]
    public void DuplicatePostThrows()
    {
        Fixture fixture = new(crewCount: 1);
        fixture.Board.Post(Posted(1, TaskKind.CallButton, 60, 60, postedTick: 0));
        Assert.Throws<ArgumentException>(() => fixture.Board.Post(Posted(1, TaskKind.LavCheck, 30, 30, postedTick: 1)));

        fixture.Claim(0, tick: 1);
        Assert.Throws<ArgumentException>(() => fixture.Board.Post(Posted(1, TaskKind.LavCheck, 30, 30, postedTick: 2)));

        fixture.Board.Complete(0);
        fixture.Board.Post(Posted(1, TaskKind.LavCheck, 30, 30, postedTick: 3));
        Assert.Equal([1], fixture.Board.OpenTasks.Select(task => task.Id));
    }

    /// <summary>A crew member holding a task cannot claim another; the held task and the open one are left as they were.</summary>
    [Fact]
    public void ClaimWhileHoldingThrows()
    {
        Fixture fixture = new(crewCount: 1);
        fixture.Board.Post(Posted(1, TaskKind.CallButton, 60, 60, postedTick: 0));
        fixture.Board.Post(Posted(2, TaskKind.LavCheck, 30, 30, postedTick: 0));
        fixture.Claim(0, tick: 0);

        Assert.Throws<InvalidOperationException>(() => fixture.Board.TryClaim(0, 1, Anything, fixture.ActionFor));

        Assert.Equal(1, fixture.Board.HeldBy(0)?.Id);
        Assert.Equal([2], fixture.Board.OpenTasks.Select(task => task.Id));
    }

    /// <summary>Pre-empting or completing for a crew member who holds nothing throws; completing frees the crew member to claim.</summary>
    [Fact]
    public void PreemptOrCompleteWithoutATaskThrows()
    {
        Fixture fixture = new(crewCount: 1);
        fixture.Board.Post(Posted(1, TaskKind.CallButton, 60, 60, postedTick: 0));

        Assert.Throws<InvalidOperationException>(() => fixture.Board.TryPreempt(0, 0, Anything, fixture.ActionFor));
        Assert.Throws<InvalidOperationException>(() => fixture.Board.Complete(0));

        fixture.Claim(0, tick: 0);
        fixture.Board.Complete(0);
        Assert.Null(fixture.Board.HeldBy(0));
        Assert.Empty(fixture.Board.OpenTasks);
    }

    /// <summary>A task needs a zone and at least one crew member; the board rejects out-of-range crew ids.</summary>
    [Fact]
    public void RejectsInvalidArguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Posted(1, TaskKind.CallButton, 60, 60, postedTick: 0) with { CrewNeeded = 0 });
        Assert.Throws<ArgumentException>(() => Posted(1, TaskKind.CallButton, 60, 60, postedTick: 0, zone: " "));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TaskBoard(new SequenceExecutor(1), -1));

        Fixture fixture = new(crewCount: 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Board.HeldBy(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Board.TryClaim(-1, 0, Anything, fixture.ActionFor));
    }

    /// <summary>
    /// Business hand service (claim 42, hold 60) is not pre-empted by a call button (claim 60): the claim must be strictly above the hold.
    /// </summary>
    [Fact]
    public void CallButtonDoesNotPreemptBusinessService()
    {
        Fixture fixture = new(crewCount: 1);
        fixture.Board.Post(Posted(1, TaskKind.BusinessService, 42, 60, postedTick: 0));
        fixture.Claim(0, tick: 0);
        fixture.Board.Post(Posted(2, TaskKind.CallButton, 60, 60, postedTick: 1));

        Assert.Null(fixture.Board.TryPreempt(0, 1, Anything, fixture.ActionFor));

        Assert.Equal(1, fixture.Board.HeldBy(0)?.Id);
        Assert.Equal(0, fixture.Actions[1].CleanupCount);
    }

    /// <summary>
    /// A due galley break (claim 45, hold 35) is claimed ahead of an older cart (claim 40); once running, a lav check
    /// (30) does not break it, but the cart, claiming above its hold, does.
    /// </summary>
    [Fact]
    public void DueGalleyBreakClaimsAheadOfACartAndYieldsToIt()
    {
        Fixture fixture = new(crewCount: 1);
        fixture.Board.Post(Cart(id: 1, postedTick: 0));
        fixture.Board.Post(Posted(2, TaskKind.GalleyBreak, 45, 35, postedTick: 1));
        fixture.Board.Post(Posted(3, TaskKind.LavCheck, 30, 30, postedTick: 0));

        Assert.Equal(2, fixture.Claim(0, tick: 1)?.Id);
        Assert.Null(fixture.Board.TryPreempt(0, 2, task => task.Kind == TaskKind.LavCheck, fixture.ActionFor));
        Assert.Equal(1, fixture.Board.TryPreempt(0, 2, Anything, fixture.ActionFor)?.Id);

        Assert.Equal(1, fixture.Actions[2].CleanupCount);
        Assert.Equal([2, 3], fixture.Board.OpenTasks.Select(task => task.Id));
    }

    /// <summary>With two crew and one open task the second claim finds nothing and leaves that crew member's running action alone.</summary>
    [Fact]
    public void SecondClaimOnOneTaskFindsNothingAndLeavesTheExecutorAlone()
    {
        Fixture fixture = new(crewCount: 2);
        RecordingAction bystander = new("bystander", fixture.Log);
        Assert.True(fixture.Executor.TryStart(1, bystander, 0));
        fixture.Board.Post(Posted(1, TaskKind.CallButton, 60, 60, postedTick: 0));

        Assert.Equal(1, fixture.Claim(0, tick: 0)?.Id);
        Assert.Null(fixture.Claim(1, tick: 0));

        Assert.Same(bystander, fixture.Executor.ActionOf(1));
        Assert.Equal(0, bystander.CleanupCount);
        Assert.Null(fixture.Board.HeldBy(1));
    }

    /// <summary>Withdrawing an open task takes it off the board, after which its wait is unknown and its id may be posted again.</summary>
    [Fact]
    public void WithdrawTakesAnOpenTaskOffTheBoard()
    {
        Fixture fixture = new(crewCount: 1);
        CrewTask call = Posted(1, TaskKind.CallButton, 60, 60, postedTick: 0);
        fixture.Board.Post(call);
        fixture.Board.Post(Posted(2, TaskKind.LavCheck, 30, 30, postedTick: 0));

        Assert.Same(call, fixture.Board.Withdraw(1));
        Assert.Equal([2], fixture.Board.OpenTasks.Select(task => task.Id));
        Assert.Throws<ArgumentException>(() => fixture.Board.WaitTicks(1, 5));

        fixture.Board.Post(Posted(1, TaskKind.CallButton, 60, 60, postedTick: 5));
        Assert.Equal([2, 1], fixture.Board.OpenTasks.Select(task => task.Id));
        Assert.Equal(1, fixture.Claim(0, tick: 6)?.Id);
        Assert.Throws<InvalidOperationException>(() => fixture.Board.Withdraw(1));
        Assert.Throws<ArgumentException>(() => fixture.Board.Withdraw(99));
        Assert.Equal(1, fixture.Board.HeldBy(0)?.Id);
    }

    /// <summary>Once the held task's action has returned done, pre-empting throws naming the crew and task; completing still works.</summary>
    [Fact]
    public void PreemptAfterTheHeldActionFinishedThrows()
    {
        Fixture fixture = new(crewCount: 1);
        fixture.Board.Post(Posted(1, TaskKind.CallButton, 60, 60, postedTick: 0));
        fixture.Board.TryClaim(0, 0, Anything, _ => new RecordingAction("quick", fixture.Log, ticksToFinish: 1));
        fixture.Executor.Tick(0);
        Assert.Null(fixture.Executor.ActionOf(0));
        fixture.Board.Post(Posted(2, TaskKind.SecureCheck, 90, 90, postedTick: 1));

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => fixture.Board.TryPreempt(0, 1, Anything, fixture.ActionFor));

        AssertNames(error, "Crew 0", "task 1");
        Assert.Equal([2], fixture.Board.OpenTasks.Select(task => task.Id));
        Assert.Equal(1, fixture.Board.Complete(0).Task.Id);
    }

    /// <summary>Once another start has swapped the held task's action out, pre-empting and completing both throw naming the crew and task.</summary>
    [Fact]
    public void PreemptAfterTheHeldActionWasSwappedOutThrows()
    {
        Fixture fixture = new(crewCount: 1);
        fixture.Board.Post(Posted(1, TaskKind.CallButton, 60, 60, postedTick: 0));
        fixture.Claim(0, tick: 0);
        Assert.True(fixture.Executor.TryStart(0, new RecordingAction("elsewhere", fixture.Log, priority: 5), 1));
        fixture.Board.Post(Posted(2, TaskKind.SecureCheck, 90, 90, postedTick: 1));

        AssertNames(Assert.Throws<InvalidOperationException>(() => fixture.Board.TryPreempt(0, 1, Anything, fixture.ActionFor)), "Crew 0", "task 1");
        AssertNames(Assert.Throws<InvalidOperationException>(() => fixture.Board.Complete(0)), "Crew 0", "task 1");

        Assert.Equal(1, fixture.Actions[1].CleanupCount);
        Assert.Equal(1, fixture.Board.HeldBy(0)?.Id);
    }

    /// <summary>
    /// A board call from inside an <c>actionFor</c> factory or an action's cleanup throws naming the crew and leaves the
    /// board unchanged; the board works again afterwards.
    /// </summary>
    [Fact]
    public void BoardCallFromActionForOrCleanupThrows()
    {
        Fixture fixture = new(crewCount: 1);
        fixture.Board.Post(Posted(1, TaskKind.LavCheck, 30, 30, postedTick: 0));

        InvalidOperationException fromFactory = Assert.Throws<InvalidOperationException>(() =>
            fixture.Board.TryClaim(
                0,
                0,
                Anything,
                task =>
                {
                    fixture.Board.Post(Posted(9, TaskKind.CallButton, 60, 60, postedTick: 0));
                    return fixture.ActionFor(task);
                }
            )
        );
        AssertNames(fromFactory, "crew 0");
        Assert.Equal([1], fixture.Board.OpenTasks.Select(task => task.Id));
        Assert.Null(fixture.Board.HeldBy(0));
        Assert.Null(fixture.Executor.ActionOf(0));

        Assert.Equal(1, fixture.Board.TryClaim(0, 0, Anything, _ => new BoardCallingAction(fixture.Board))?.Id);
        fixture.Board.Post(Posted(2, TaskKind.SecureCheck, 90, 90, postedTick: 1));

        AssertNames(Assert.Throws<InvalidOperationException>(() => fixture.Board.TryPreempt(0, 1, Anything, fixture.ActionFor)), "crew 0");
        Assert.Equal(1, fixture.Board.HeldBy(0)?.Id);
        Assert.Equal([2], fixture.Board.OpenTasks.Select(task => task.Id));
    }

    private static void AssertNames(InvalidOperationException error, params string[] fragments)
    {
        foreach (string fragment in fragments)
        {
            Assert.Contains(fragment, error.Message, StringComparison.Ordinal);
        }
    }

    private static CrewTask Cart(int id, long postedTick) =>
        new()
        {
            Id = id,
            Kind = TaskKind.DrinksRound,
            ClaimPriority = 40,
            HoldPriority = 75,
            Zone = "cart",
            CrewNeeded = 2,
            PostedTick = postedTick,
        };

    private static CrewTask Posted(int id, TaskKind kind, int claim, int hold, long postedTick, string zone = "A") =>
        new()
        {
            Id = id,
            Kind = kind,
            ClaimPriority = claim,
            HoldPriority = hold,
            Zone = zone,
            CrewNeeded = 1,
            PostedTick = postedTick,
        };

    private sealed class Fixture
    {
        public Fixture(int crewCount)
        {
            Executor = new SequenceExecutor(crewCount);
            Board = new TaskBoard(Executor, crewCount);
        }

        public SequenceExecutor Executor { get; }

        public TaskBoard Board { get; }

        public List<string> Log { get; } = [];

        public SortedList<int, RecordingAction> Actions { get; } = [];

        public RecordingAction ActionFor(CrewTask task)
        {
            RecordingAction action = new($"task {task.Id}", Log);
            Actions[task.Id] = action;
            return action;
        }

        public CrewTask? Claim(int crew, long tick) => Board.TryClaim(crew, tick, Anything, ActionFor);
    }

    private sealed class BoardCallingAction(TaskBoard board) : CharacterAction(0)
    {
        public override ActionStatus Tick(long tick) => ActionStatus.Running;

        public override void Cleanup(long tick) => board.Complete(0);
    }

    private sealed class RecordingAction(string name, List<string> log, int priority = 0, int ticksToFinish = int.MaxValue)
        : CharacterAction(priority)
    {
        private int ticksRun;

        public int CleanupCount { get; private set; }

        public override ActionStatus Tick(long tick)
        {
            log.Add($"{name} tick {tick}");
            ticksRun++;
            return ticksRun >= ticksToFinish ? ActionStatus.Done : ActionStatus.Running;
        }

        public override void Cleanup(long tick)
        {
            log.Add($"{name} cleanup {tick}");
            CleanupCount++;
        }
    }
}
