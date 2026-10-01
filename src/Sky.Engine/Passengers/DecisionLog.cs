namespace Sky.Engine.Passengers;

/// <summary>
/// One passenger decision: the tick it was taken on, the activity the passenger was on, the one chosen, and the three
/// highest-ranked candidates with their final scores (the keep-current bias included), in the order the choice ranked them.
/// A slot no candidate fills holds the rules' initial activity with a score of 0.
/// </summary>
/// <param name="Tick">The tick the decision was taken on.</param>
/// <param name="Current">The activity the passenger was on when it decided.</param>
/// <param name="Chosen">The activity the passenger chose.</param>
/// <param name="First">The highest-ranked candidate.</param>
/// <param name="FirstScore">The first candidate's final score.</param>
/// <param name="Second">The second-ranked candidate.</param>
/// <param name="SecondScore">The second candidate's final score.</param>
/// <param name="Third">The third-ranked candidate.</param>
/// <param name="ThirdScore">The third candidate's final score.</param>
public readonly record struct DecisionRecord(
    long Tick,
    ActivityId Current,
    ActivityId Chosen,
    ActivityId First,
    double FirstScore,
    ActivityId Second,
    double SecondScore,
    ActivityId Third,
    double ThirdScore
);

/// <summary>Every decision of every passenger, kept whole: one list per passenger, indexed by manifest id, in tick order.</summary>
public sealed class DecisionLog
{
    private readonly List<DecisionRecord>[] records;

    /// <summary>Creates an empty log for a flight's passengers.</summary>
    /// <param name="passengerCount">How many passengers the flight carries, at least 0.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="passengerCount"/> is negative.</exception>
    public DecisionLog(int passengerCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(passengerCount);
        records = new List<DecisionRecord>[passengerCount];
        for (int passenger = 0; passenger < passengerCount; passenger++)
        {
            records[passenger] = [];
        }
    }

    /// <summary>Appends a passenger's decision to their list.</summary>
    /// <param name="passenger">The passenger's manifest id.</param>
    /// <param name="record">The decision.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="passenger"/> is not a passenger of the log.</exception>
    /// <exception cref="ArgumentException">The decision's tick is before the passenger's last recorded one.</exception>
    public void Record(int passenger, in DecisionRecord record)
    {
        RequirePassenger(passenger);
        List<DecisionRecord> list = records[passenger];
        if (list.Count > 0 && record.Tick < list[^1].Tick)
        {
            throw new ArgumentException(
                $"Passenger {passenger}'s decision at tick {record.Tick} is before their last one, at tick {list[^1].Tick}.",
                nameof(record)
            );
        }

        list.Add(record);
    }

    /// <summary>Returns a passenger's decisions, in the order they were taken.</summary>
    /// <param name="passenger">The passenger's manifest id.</param>
    /// <returns>The decisions.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="passenger"/> is not a passenger of the log.</exception>
    public IReadOnlyList<DecisionRecord> Of(int passenger)
    {
        RequirePassenger(passenger);
        return records[passenger];
    }

    /// <summary>Returns the decision in force for a passenger at a tick: their latest one taken at or before it.</summary>
    /// <param name="passenger">The passenger's manifest id.</param>
    /// <param name="tick">The tick.</param>
    /// <returns>The decision, or null when the passenger had taken none by then.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="passenger"/> is not a passenger of the log.</exception>
    public DecisionRecord? Dump(int passenger, long tick)
    {
        RequirePassenger(passenger);
        List<DecisionRecord> list = records[passenger];
        int low = 0;
        int high = list.Count - 1;
        int found = -1;
        while (low <= high)
        {
            int middle = low + ((high - low) / 2);
            if (list[middle].Tick <= tick)
            {
                found = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return found < 0 ? null : list[found];
    }

    private void RequirePassenger(int passenger)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(passenger);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(passenger, records.Length);
    }
}
