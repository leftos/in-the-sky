using Sky.Engine.Execution;

namespace Sky.Engine.Tests.Execution;

/// <summary>Pins the sequence executor's priority, interrupt and cleanup rules, and the sync point's release tick.</summary>
public sealed class SequenceExecutorTests
{
    /// <summary>An interrupted action's cleanup runs once, before the interrupting action's first tick.</summary>
    [Fact]
    public void InterruptRunsCleanupOnceBeforeTheNewActionsFirstTick()
    {
        List<string> log = [];
        SequenceExecutor executor = new(1);
        RecordingAction low = new("low", 1, log);
        RecordingAction high = new("high", 2, log);

        Assert.True(executor.TryStart(0, low, 0));
        executor.Tick(0);
        executor.Tick(1);
        Assert.True(executor.TryStart(0, high, 2));
        executor.Tick(2);
        executor.Tick(3);

        Assert.Equal(["low tick 0", "low tick 1", "low cleanup 2", "high tick 2", "high tick 3"], log);
        Assert.Equal(1, low.CleanupCount);
        Assert.Equal(0, high.CleanupCount);
        Assert.Same(high, executor.ActionOf(0));
    }

    /// <summary>Interrupting one character's action leaves another character's action running and uncleaned.</summary>
    [Fact]
    public void InterruptLeavesOtherCharactersUntouched()
    {
        List<string> log = [];
        SequenceExecutor executor = new(2);
        RecordingAction interrupted = new("interrupted", 1, log);
        RecordingAction other = new("other", 1, log);
        Assert.True(executor.TryStart(0, interrupted, 0));
        Assert.True(executor.TryStart(1, other, 0));

        Assert.True(executor.TryStart(0, new RecordingAction("high", 2, log), 1));
        executor.Tick(1);

        Assert.Same(other, executor.ActionOf(1));
        Assert.Equal(0, other.CleanupCount);
        Assert.Equal(["interrupted cleanup 1", "high tick 1", "other tick 1"], log);
    }

    /// <summary>An action of the same priority as the current one is refused and changes nothing.</summary>
    [Fact]
    public void EqualPriorityDoesNotInterrupt() => AssertRefused(currentPriority: 3, newPriority: 3);

    /// <summary>An action of a lower priority than the current one is refused and changes nothing.</summary>
    [Fact]
    public void LowerPriorityDoesNotInterrupt() => AssertRefused(currentPriority: 3, newPriority: 2);

    /// <summary>An action that returns done is cleared, and its cleanup never runs, then or when another action starts.</summary>
    [Fact]
    public void FinishedActionIsClearedWithoutCleanup()
    {
        List<string> log = [];
        SequenceExecutor executor = new(1);
        RecordingAction finishing = new("finishing", 5, log, ticksToFinish: 2);

        Assert.True(executor.TryStart(0, finishing, 0));
        executor.Tick(0);
        Assert.Same(finishing, executor.ActionOf(0));
        executor.Tick(1);
        Assert.Null(executor.ActionOf(0));

        Assert.True(executor.TryStart(0, new RecordingAction("next", 1, log), 2));
        Assert.Equal(0, finishing.CleanupCount);
        Assert.Equal(["finishing tick 0", "finishing tick 1"], log);
    }

    /// <summary>An action that starts a successor for its own character and then finishes leaves the successor in place.</summary>
    [Fact]
    public void ActionStartedDuringTickSurvivesTheStarterFinishing()
    {
        List<string> log = [];
        SequenceExecutor executor = new(1);
        RecordingAction successor = new("successor", 1, log);
        Assert.True(executor.TryStart(0, new StarterAction(executor, 0, successor), 0));

        executor.Tick(0);

        Assert.Same(successor, executor.ActionOf(0));
        executor.Tick(1);
        Assert.Equal(["successor tick 1"], log);
    }

    /// <summary>An action started during a tick first runs on the next tick, whether its character comes before or after the starter.</summary>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public void ActionStartedDuringTickFirstRunsNextTickWhateverTheCharacterOrder(int starter, int target)
    {
        List<string> log = [];
        SequenceExecutor executor = new(2);
        RecordingAction started = new("started", 1, log);
        Assert.True(executor.TryStart(starter, new StarterAction(executor, target, started), 0));

        executor.Tick(0);

        Assert.Same(started, executor.ActionOf(target));
        Assert.Empty(log);
        executor.Tick(1);
        Assert.Equal(["started tick 1"], log);
    }

    /// <summary>
    /// Two members release the sync point on the tick the later one arrives, and both move on the tick after, whichever
    /// character arrives first in id order and when both arrive on the same tick.
    /// </summary>
    [Theory]
    [InlineData(3L, 7L)]
    [InlineData(7L, 3L)]
    [InlineData(5L, 5L)]
    public void TwoMemberSyncPointReleasesBothOnTheTickTheSecondArrives(long firstArrival, long secondArrival)
    {
        long releaseTick = Math.Max(firstArrival, secondArrival);
        SyncPoint doorClosed = new("door-closed", 2);
        SequenceExecutor executor = new(2);
        SyncAction first = new(doorClosed, 0, firstArrival);
        SyncAction second = new(doorClosed, 1, secondArrival);
        Assert.True(executor.TryStart(0, first, 0));
        Assert.True(executor.TryStart(1, second, 0));

        for (long tick = 0; tick <= 10; tick++)
        {
            executor.Tick(tick);
        }

        Assert.Equal(releaseTick, doorClosed.ReleasedTick);
        Assert.Equal(releaseTick + 1, first.PostSyncTick);
        Assert.Equal(releaseTick + 1, second.PostSyncTick);
        Assert.Null(executor.ActionOf(0));
        Assert.Null(executor.ActionOf(1));
    }

    /// <summary>A repeat arrival is ignored, but a distinct character arriving at a full sync point throws, naming it.</summary>
    [Fact]
    public void ExtraArrivalAtAFullSyncPointThrows()
    {
        SyncPoint galleyStowed = new("galley-stowed", 2);
        galleyStowed.Arrive(0, 1);
        galleyStowed.Arrive(1, 2);
        galleyStowed.Arrive(1, 3);
        Assert.Equal(2L, galleyStowed.ReleasedTick);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => galleyStowed.Arrive(2, 4));

        Assert.Contains("galley-stowed", error.Message, StringComparison.Ordinal);
        Assert.Equal(2L, galleyStowed.ReleasedTick);
    }

    /// <summary>
    /// A withdrawn member holds the sync point until it arrives again; withdrawing a character that never arrived, or
    /// withdrawing after the release, changes nothing.
    /// </summary>
    [Fact]
    public void WithdrawnMemberHoldsTheSyncPointUntilItArrivesAgain()
    {
        SyncPoint cartReady = new("cart-ready", 2);
        cartReady.Arrive(0, 1);
        cartReady.Withdraw(0);
        cartReady.Withdraw(1);
        cartReady.Arrive(1, 3);
        Assert.Null(cartReady.ReleasedTick);
        Assert.False(cartReady.HasReleasedBefore(4));

        cartReady.Arrive(0, 5);
        Assert.Equal(5L, cartReady.ReleasedTick);

        cartReady.Withdraw(0);
        Assert.Equal(5L, cartReady.ReleasedTick);
        Assert.True(cartReady.HasReleasedBefore(6));
    }

    /// <summary>The executor rejects a negative character count, an out-of-range character and a null action.</summary>
    [Fact]
    public void ExecutorRejectsInvalidArguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SequenceExecutor(-1));

        SequenceExecutor executor = new(2);
        RecordingAction action = new("action", 1, []);
        Assert.Throws<ArgumentOutOfRangeException>(() => executor.TryStart(-1, action, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => executor.TryStart(2, action, 0));
        Assert.Throws<ArgumentNullException>(() => executor.TryStart(0, null!, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => executor.ActionOf(2));
        Assert.Null(executor.ActionOf(0));
    }

    /// <summary>A sync point rejects an empty name, fewer than one member and a negative character id.</summary>
    [Fact]
    public void SyncPointRejectsInvalidArguments()
    {
        Assert.Throws<ArgumentException>(() => new SyncPoint("", 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SyncPoint("x", 0));

        SyncPoint syncPoint = new("x", 2);
        Assert.Throws<ArgumentOutOfRangeException>(() => syncPoint.Arrive(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => syncPoint.Withdraw(-1));
    }

    private static void AssertRefused(int currentPriority, int newPriority)
    {
        List<string> log = [];
        SequenceExecutor executor = new(1);
        RecordingAction running = new("running", currentPriority, log);
        RecordingAction challenger = new("challenger", newPriority, log);
        Assert.True(executor.TryStart(0, running, 0));

        Assert.False(executor.TryStart(0, challenger, 1));
        executor.Tick(1);

        Assert.Same(running, executor.ActionOf(0));
        Assert.Equal(0, running.CleanupCount);
        Assert.Equal(["running tick 1"], log);
    }

    private sealed class RecordingAction(string name, int priority, List<string> log, int ticksToFinish = int.MaxValue) : CharacterAction(priority)
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

    private sealed class StarterAction(SequenceExecutor executor, int target, CharacterAction started) : CharacterAction(0)
    {
        public override ActionStatus Tick(long tick)
        {
            Assert.True(executor.TryStart(target, started, tick));
            return ActionStatus.Done;
        }
    }

    private sealed class SyncAction(SyncPoint syncPoint, int character, long arrivalTick) : CharacterAction(0)
    {
        private bool arrived;

        public long? PostSyncTick { get; private set; }

        public override ActionStatus Tick(long tick)
        {
            if (!arrived)
            {
                if (tick < arrivalTick)
                {
                    return ActionStatus.Running;
                }

                syncPoint.Arrive(character, tick);
                arrived = true;
            }

            if (!syncPoint.HasReleasedBefore(tick))
            {
                return ActionStatus.Running;
            }

            PostSyncTick = tick;
            return ActionStatus.Done;
        }
    }
}
