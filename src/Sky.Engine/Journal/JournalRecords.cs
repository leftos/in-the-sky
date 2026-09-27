using Sky.Engine.Ports;

namespace Sky.Engine.Journal;

/// <summary>An input record of the journal: something replay consumes to run the flight again exactly (R11, ADR 0004).</summary>
/// <param name="Tick">The tick the record belongs to.</param>
public abstract record JournalInputRecord(long Tick);

/// <summary>One frame: a call that ran the flight <paramref name="TickCount"/> ticks from <paramref name="Tick"/>.</summary>
/// <param name="Tick">The tick the frame started on.</param>
/// <param name="TickCount">How many ticks the frame ran.</param>
public sealed record FrameRecord(long Tick, long TickCount) : JournalInputRecord(Tick);

/// <summary>A feed observation that differed from the one before it, or the first one, with the tick it was read at.</summary>
/// <param name="Tick">The tick the feed was read at.</param>
/// <param name="Observation">What the feed reported.</param>
public sealed record FeedObservationRecord(long Tick, FeedObservation Observation) : JournalInputRecord(Tick);
