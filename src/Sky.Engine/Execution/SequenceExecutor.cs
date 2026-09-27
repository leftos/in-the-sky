namespace Sky.Engine.Execution;

/// <summary>
/// Runs at most one <see cref="CharacterAction"/> per character, ticking the characters in id order. A strictly
/// higher-priority action interrupts the current one, whose cleanup runs before the new action's first tick. An action
/// started while <see cref="Tick"/> is running first runs on the next tick, whether its character comes before or after
/// the one that started it, so the character order never decides when a start takes effect.
/// </summary>
public sealed class SequenceExecutor
{
    private readonly CharacterAction?[] current;
    private readonly bool[] startedThisTick;
    private bool ticking;

    /// <summary>Creates an executor whose characters all start with no action.</summary>
    /// <param name="characterCount">How many characters the executor runs, ids 0 to <paramref name="characterCount"/> - 1.</param>
    public SequenceExecutor(int characterCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(characterCount);
        current = new CharacterAction?[characterCount];
        startedThisTick = new bool[characterCount];
    }

    /// <summary>
    /// Starts <paramref name="action"/> for <paramref name="character"/> unless its current action holds an equal or higher
    /// priority. An interrupted action has its <see cref="CharacterAction.Cleanup"/> called once, before the new action is set.
    /// </summary>
    /// <param name="character">The character id.</param>
    /// <param name="action">The action to start.</param>
    /// <param name="tick">The tick on which the start happens, passed to the interrupted action's cleanup.</param>
    /// <returns><see langword="true"/> when the action was started; <see langword="false"/> when the current action kept its place.</returns>
    public bool TryStart(int character, CharacterAction action, long tick)
    {
        RequireCharacter(character);
        ArgumentNullException.ThrowIfNull(action);

        CharacterAction? running = current[character];
        if (running is not null)
        {
            if (running.Priority >= action.Priority)
            {
                return false;
            }

            running.Cleanup(tick);
        }

        current[character] = action;
        startedThisTick[character] = ticking;
        return true;
    }

    /// <summary>
    /// Ticks every character's current action in id order, clearing each one that reports it is done. A character given a
    /// new action during this call is skipped until the next call.
    /// </summary>
    /// <param name="tick">The simulation tick being run.</param>
    public void Tick(long tick)
    {
        ticking = true;
        try
        {
            for (int character = 0; character < current.Length; character++)
            {
                TickCharacter(character, tick);
            }
        }
        finally
        {
            ticking = false;
            Array.Clear(startedThisTick);
        }
    }

    /// <summary>The action <paramref name="character"/> is carrying out, or <see langword="null"/> when it has none.</summary>
    /// <param name="character">The character id.</param>
    /// <returns>The current action, or <see langword="null"/>.</returns>
    public CharacterAction? ActionOf(int character)
    {
        RequireCharacter(character);
        return current[character];
    }

    private void TickCharacter(int character, long tick)
    {
        CharacterAction? action = current[character];
        if (action is null || startedThisTick[character])
        {
            return;
        }

        if (action.Tick(tick) == ActionStatus.Done && ReferenceEquals(current[character], action))
        {
            current[character] = null;
        }
    }

    private void RequireCharacter(int character)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(character);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(character, current.Length);
    }
}
