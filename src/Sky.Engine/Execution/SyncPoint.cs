namespace Sky.Engine.Execution;

/// <summary>
/// A named meeting point a fixed number of characters must all reach before any of them moves on. It releases on the
/// tick the last member arrives, and every waiting action continues on the tick after, whatever order the characters
/// are ticked in.
/// </summary>
public sealed class SyncPoint
{
    private readonly List<int> arrived = [];
    private readonly int memberCount;

    /// <summary>Creates a sync point no member has reached yet.</summary>
    /// <param name="name">The name error messages give the sync point.</param>
    /// <param name="memberCount">How many distinct characters must arrive for the sync point to release; at least 1.</param>
    public SyncPoint(string name, int memberCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfLessThan(memberCount, 1);
        Name = name;
        this.memberCount = memberCount;
    }

    /// <summary>The sync point's name.</summary>
    public string Name { get; }

    /// <summary>The tick the last member arrived on, or <see langword="null"/> before then.</summary>
    public long? ReleasedTick { get; private set; }

    /// <summary>Records that <paramref name="character"/> has reached the sync point. A second arrival by the same character is ignored.</summary>
    /// <param name="character">The arriving character's id.</param>
    /// <param name="tick">The tick of the arrival.</param>
    /// <exception cref="InvalidOperationException">More distinct characters arrive than the sync point has members.</exception>
    public void Arrive(int character, long tick)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(character);

        int index = arrived.BinarySearch(character);
        if (index >= 0)
        {
            return;
        }

        if (arrived.Count == memberCount)
        {
            throw new InvalidOperationException(
                $"Sync point '{Name}' already has all {memberCount} members; character {character} arrived at tick {tick} as an extra member."
            );
        }

        arrived.Insert(~index, character);
        if (arrived.Count == memberCount)
        {
            ReleasedTick = tick;
        }
    }

    /// <summary>
    /// Takes back <paramref name="character"/>'s arrival, so an interrupted member's cleanup can leave the sync point
    /// waiting for it again. Does nothing once the sync point has released, or for a character that has not arrived.
    /// </summary>
    /// <param name="character">The withdrawing character's id.</param>
    public void Withdraw(int character)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(character);
        if (ReleasedTick is not null)
        {
            return;
        }

        int index = arrived.BinarySearch(character);
        if (index >= 0)
        {
            arrived.RemoveAt(index);
        }
    }

    /// <summary>Whether the sync point released on a tick earlier than <paramref name="tick"/>, so a waiting action may continue on it.</summary>
    /// <param name="tick">The tick a waiting action is running.</param>
    /// <returns><see langword="true"/> when the release tick is before <paramref name="tick"/>.</returns>
    public bool HasReleasedBefore(long tick) => ReleasedTick is { } released && released < tick;
}
