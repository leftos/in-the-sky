namespace Sky.Engine.Crew;

/// <summary>
/// The numbers <see cref="CrewStrain"/> runs on, in strain points and sim minutes. Every value must be finite and
/// non-negative; the record rejects any other value as it is set.
/// </summary>
public sealed record StrainSettings
{
    /// <summary>Strain gained per sim minute on a task, before the fatigue, trait and no-break factors.</summary>
    public required double OnTaskPerMinute
    {
        get;
        init => field = NonNegative(value, nameof(OnTaskPerMinute));
    }

    /// <summary>Sim minutes since the last break after which the on-task rate is multiplied by <see cref="NoBreakFactor"/>.</summary>
    public required double NoBreakMinutes
    {
        get;
        init => field = NonNegative(value, nameof(NoBreakMinutes));
    }

    /// <summary>The on-task rate's multiplier once <see cref="NoBreakMinutes"/> have passed since the last break.</summary>
    public required double NoBreakFactor
    {
        get;
        init => field = NonNegative(value, nameof(NoBreakFactor));
    }

    /// <summary>Strain added by a pre-emption, before the trait's pre-emption factor.</summary>
    public required double PreemptionStep
    {
        get;
        init => field = NonNegative(value, nameof(PreemptionStep));
    }

    /// <summary>
    /// Strain added on top of <see cref="PreemptionStep"/> by a pre-emption within <see cref="RepeatWindowMinutes"/> of
    /// the previous one.
    /// </summary>
    public required double RepeatPreemptionStep
    {
        get;
        init => field = NonNegative(value, nameof(RepeatPreemptionStep));
    }

    /// <summary>Sim minutes within which a second pre-emption adds <see cref="RepeatPreemptionStep"/>.</summary>
    public required double RepeatWindowMinutes
    {
        get;
        init => field = NonNegative(value, nameof(RepeatWindowMinutes));
    }

    /// <summary>Strain gained per sim minute for each backlog task, up to <see cref="BacklogCapPerMinute"/>.</summary>
    public required double BacklogPerTaskPerMinute
    {
        get;
        init => field = NonNegative(value, nameof(BacklogPerTaskPerMinute));
    }

    /// <summary>The most strain the backlog adds per sim minute, however many tasks wait.</summary>
    public required double BacklogCapPerMinute
    {
        get;
        init => field = NonNegative(value, nameof(BacklogCapPerMinute));
    }

    /// <summary>Strain added on arriving at a severe incident.</summary>
    public required double SevereIncidentStep
    {
        get;
        init => field = NonNegative(value, nameof(SevereIncidentStep));
    }

    /// <summary>Strain lost per sim minute idle and not on break.</summary>
    public required double IdleDecayPerMinute
    {
        get;
        init => field = NonNegative(value, nameof(IdleDecayPerMinute));
    }

    /// <summary>Strain lost per sim minute on a galley break.</summary>
    public required double BreakDecayPerMinute
    {
        get;
        init => field = NonNegative(value, nameof(BreakDecayPerMinute));
    }

    /// <summary>
    /// Sim minutes a break must run, in one unbroken stretch, before it restarts the time since the last break; a shorter
    /// break still lowers strain for the ticks it ran.
    /// </summary>
    public required double MinimumBreakMinutes
    {
        get;
        init => field = NonNegative(value, nameof(MinimumBreakMinutes));
    }

    /// <summary>The strain a crew member is over only when strictly above it.</summary>
    public required double Redline
    {
        get;
        init => field = NonNegative(value, nameof(Redline));
    }

    private static double NonNegative(double value, string name) =>
        double.IsFinite(value) && value >= 0.0
            ? value
            : throw new ArgumentOutOfRangeException(name, value, $"Strain setting {name} must be finite and non-negative; got {value}.");
}
