using Sky.Engine.Time;

namespace Sky.Engine.Crew;

/// <summary>
/// One crew member's strain, 0 to 100: it rises with time on task (faster with fatigue and once too long has passed
/// since a break), pre-emptions, a zone backlog and severe incidents, and decays when idle, less the zone backlog, and faster on a galley break.
/// It tracks its peak and counts the ticks it ends strictly above the redline. Per-minute settings are turned into
/// per-tick constants once, at construction.
/// </summary>
public sealed class CrewStrain
{
    private const double Maximum = 100.0;

    private readonly double onTaskPerTick;
    private readonly double noBreakTicks;
    private readonly double noBreakFactor;
    private readonly double preemptionStep;
    private readonly double repeatPreemptionStep;
    private readonly double repeatWindowTicks;
    private readonly double backlogPerTaskPerTick;
    private readonly double backlogCapPerTick;
    private readonly double severeIncidentStep;
    private readonly double idleDecayPerTick;
    private readonly double breakDecayPerTick;
    private readonly double redline;
    private long? lastTick;
    private long? lastPreemptionTick;

    /// <summary>Creates a crew member's strain at 0.</summary>
    /// <param name="settings">The strain numbers.</param>
    /// <param name="preemptionFactor">The crew trait's scale on pre-emption steps, above 0 (Steady 0.5, Short fuse 1.5).</param>
    /// <param name="onTaskFactor">The crew trait's scale on the on-task rate, above 0 (Brisk 1.15).</param>
    public CrewStrain(StrainSettings settings, double preemptionFactor, double onTaskFactor)
    {
        ArgumentNullException.ThrowIfNull(settings);
        RequirePositiveFactor(preemptionFactor, nameof(preemptionFactor));
        RequirePositiveFactor(onTaskFactor, nameof(onTaskFactor));

        onTaskPerTick = settings.OnTaskPerMinute * onTaskFactor / SimTime.TicksPerSimMinute;
        noBreakTicks = settings.NoBreakMinutes * SimTime.TicksPerSimMinute;
        noBreakFactor = settings.NoBreakFactor;
        preemptionStep = settings.PreemptionStep * preemptionFactor;
        repeatPreemptionStep = settings.RepeatPreemptionStep * preemptionFactor;
        repeatWindowTicks = settings.RepeatWindowMinutes * SimTime.TicksPerSimMinute;
        backlogPerTaskPerTick = settings.BacklogPerTaskPerMinute / SimTime.TicksPerSimMinute;
        backlogCapPerTick = settings.BacklogCapPerMinute / SimTime.TicksPerSimMinute;
        severeIncidentStep = settings.SevereIncidentStep;
        idleDecayPerTick = settings.IdleDecayPerMinute / SimTime.TicksPerSimMinute;
        breakDecayPerTick = settings.BreakDecayPerMinute / SimTime.TicksPerSimMinute;
        redline = settings.Redline;
    }

    /// <summary>The current strain, 0 to 100.</summary>
    public double Value { get; private set; }

    /// <summary>The highest strain reached so far.</summary>
    public double Peak { get; private set; }

    /// <summary>
    /// Ticks since the last tick spent on break (or since construction): 0 after a break tick, one more after every
    /// other tick.
    /// </summary>
    public long TicksSinceBreak { get; private set; }

    /// <summary>Whether the current strain is strictly above the redline.</summary>
    public bool OverRedline => Value > redline;

    /// <summary>How many ticks ended with strain strictly above the redline.</summary>
    public long TicksOverRedline { get; private set; }

    /// <summary><see cref="TicksOverRedline"/> in sim minutes.</summary>
    public double MinutesOverRedline => (double)TicksOverRedline / SimTime.TicksPerSimMinute;

    /// <summary>
    /// Runs one tick: the activity's change, plus the backlog's in every activity but a break, clamped to 0 to 100. A
    /// break restarts the time since the last break; every other tick advances it.
    /// </summary>
    /// <param name="tick">The tick being run, later than the previous call's.</param>
    /// <param name="activity">What the crew member is doing this tick.</param>
    /// <param name="fatigue">The crew member's fatigue this tick, 0 to 100; it scales the on-task rate by 1 + fatigue / 100.</param>
    /// <param name="backlogTasks">How many tasks count as backlog for this crew member this tick.</param>
    public void Tick(long tick, CrewActivity activity, double fatigue, int backlogTasks)
    {
        RequireTickInputs(tick, fatigue, backlogTasks);
        double change = ActivityChange(activity, fatigue);
        if (activity != CrewActivity.OnBreak)
        {
            change += Math.Min(backlogPerTaskPerTick * backlogTasks, backlogCapPerTick);
        }

        TicksSinceBreak = activity == CrewActivity.OnBreak ? 0 : TicksSinceBreak + 1;
        lastTick = tick;
        Add(change);
        if (Value > redline)
        {
            TicksOverRedline++;
        }
    }

    /// <summary>Adds a pre-emption's step, and the repeat step too when the previous pre-emption was within the repeat window.</summary>
    /// <param name="tick">The tick of the pre-emption, no earlier than the previous one's.</param>
    public void Preempted(long tick)
    {
        double step = preemptionStep;
        if (lastPreemptionTick is { } previous)
        {
            if (tick < previous)
            {
                throw new ArgumentOutOfRangeException(nameof(tick), tick, $"Pre-emptions must not go back in time; tick {tick} follows {previous}.");
            }

            if (tick - previous <= repeatWindowTicks)
            {
                step += repeatPreemptionStep;
            }
        }

        lastPreemptionTick = tick;
        Add(step);
    }

    /// <summary>Adds the step for arriving at a severe incident.</summary>
    public void SevereIncidentArrival() => Add(severeIncidentStep);

    private static void RequirePositiveFactor(double factor, string name)
    {
        if (!double.IsFinite(factor) || factor <= 0.0)
        {
            throw new ArgumentOutOfRangeException(name, factor, $"Trait factor {name} must be finite and above 0; got {factor}.");
        }
    }

    private void RequireTickInputs(long tick, double fatigue, int backlogTasks)
    {
        if (lastTick is { } previous && tick <= previous)
        {
            throw new ArgumentOutOfRangeException(nameof(tick), tick, $"Strain ticks must increase; tick {tick} follows {previous}.");
        }

        if (!double.IsFinite(fatigue) || fatigue < 0.0 || fatigue > Maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(fatigue), fatigue, $"Fatigue must be between 0 and 100; got {fatigue}.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(backlogTasks);
    }

    private double ActivityChange(CrewActivity activity, double fatigue) =>
        activity switch
        {
            CrewActivity.OnTask => onTaskPerTick * (1.0 + (fatigue / Maximum)) * (TicksSinceBreak >= noBreakTicks ? noBreakFactor : 1.0),
            CrewActivity.Idle => -idleDecayPerTick,
            CrewActivity.OnBreak => -breakDecayPerTick,
            CrewActivity.Seated => 0.0,
            _ => throw new ArgumentOutOfRangeException(nameof(activity), activity, $"Crew activity {activity} is not defined."),
        };

    private void Add(double change)
    {
        Value = Math.Clamp(Value + change, 0.0, Maximum);
        Peak = Math.Max(Peak, Value);
    }
}
