namespace Sky.Engine.Execution;

/// <summary>What a character action reports after one tick of work.</summary>
public enum ActionStatus
{
    /// <summary>The action has more work to do on a later tick.</summary>
    Running,

    /// <summary>The action has finished; the executor clears it without calling its cleanup.</summary>
    Done,
}

/// <summary>One unit of work a character carries out over one or more ticks, run by a <see cref="SequenceExecutor"/>.</summary>
/// <param name="priority">A higher value interrupts a lower one; an equal value does not interrupt.</param>
public abstract class CharacterAction(int priority)
{
    /// <summary>The priority this action holds; only a strictly higher priority interrupts it.</summary>
    public int Priority { get; } = priority;

    /// <summary>Does one tick of work.</summary>
    /// <param name="tick">The simulation tick being run.</param>
    /// <returns><see cref="ActionStatus.Done"/> once the action has finished, otherwise <see cref="ActionStatus.Running"/>.</returns>
    public abstract ActionStatus Tick(long tick);

    /// <summary>
    /// Undoes or settles whatever the action leaves half-done. The executor calls it exactly once, when a higher-priority
    /// action interrupts this one, and never when the action finishes by returning <see cref="ActionStatus.Done"/>.
    /// </summary>
    /// <param name="tick">The tick on which the interruption happens.</param>
    public virtual void Cleanup(long tick) { }
}
