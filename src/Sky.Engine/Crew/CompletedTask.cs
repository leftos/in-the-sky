namespace Sky.Engine.Crew;

/// <summary>A task a crew member finished, with the tick it was claimed on, so its wait outlives the board's record of it.</summary>
/// <param name="Task">The finished task.</param>
/// <param name="ClaimTick">The tick of the task's latest claim.</param>
public readonly record struct CompletedTask(CrewTask Task, long ClaimTick)
{
    /// <summary>The task's wait in ticks, from its posted tick to its latest claim.</summary>
    public long WaitTicks => ClaimTick - Task.PostedTick;
}
