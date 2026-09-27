namespace Sky.Engine.Scoring;

/// <summary>The word the report prints under each outcome (CONCEPT section 6); there is no composite grade.</summary>
public enum Verdict
{
    /// <summary>The outcome went well.</summary>
    Smooth,

    /// <summary>The outcome went neither well nor badly.</summary>
    Rough,

    /// <summary>The outcome went badly.</summary>
    Bad,
}

/// <summary>Where an experience 10th percentile turns Smooth and Bad; both in [0, 100], Bad's line at or below Smooth's.</summary>
public sealed record ExperienceThresholds
{
    /// <summary>Creates the thresholds, rejecting a value outside [0, 100] or a Bad line above the Smooth one.</summary>
    /// <param name="smoothAtLeast">The 10th percentile at or above which the verdict is Smooth.</param>
    /// <param name="badBelow">The 10th percentile below which the verdict is Bad.</param>
    public ExperienceThresholds(double smoothAtLeast, double badBelow)
    {
        RangeGuard.ThrowIfOutside(smoothAtLeast, Experience.Minimum, Experience.Maximum, nameof(smoothAtLeast));
        RangeGuard.ThrowIfOutside(badBelow, Experience.Minimum, smoothAtLeast, nameof(badBelow));
        SmoothAtLeast = smoothAtLeast;
        BadBelow = badBelow;
    }

    /// <summary>The 10th percentile at or above which the verdict is Smooth.</summary>
    public double SmoothAtLeast { get; }

    /// <summary>The 10th percentile below which the verdict is Bad.</summary>
    public double BadBelow { get; }
}

/// <summary>Where the share of incidents handled turns Bad; every incident handled is Smooth.</summary>
public sealed record IncidentThresholds
{
    /// <summary>Creates the thresholds, rejecting a share outside [0, 1].</summary>
    /// <param name="badBelowShare">The handled share below which the verdict is Bad.</param>
    public IncidentThresholds(double badBelowShare)
    {
        RangeGuard.ThrowIfOutside(badBelowShare, 0.0, 1.0, nameof(badBelowShare));
        BadBelowShare = badBelowShare;
    }

    /// <summary>The handled share below which the verdict is Bad.</summary>
    public double BadBelowShare { get; }
}

/// <summary>Where crew strain turns Smooth and Bad: by the peak of the most strained, and by the minutes over the redline.</summary>
public sealed record StrainThresholds
{
    /// <summary>Creates the thresholds, rejecting a negative or non-finite value or a Bad peak below the Smooth one.</summary>
    /// <param name="smoothPeakBelow">The peak below which, with no minutes over the redline, the verdict is Smooth.</param>
    /// <param name="badPeakAtLeast">The peak at or above which the verdict is Bad.</param>
    /// <param name="badMinutesOver">The minutes over the redline above which the verdict is Bad.</param>
    public StrainThresholds(double smoothPeakBelow, double badPeakAtLeast, double badMinutesOver)
    {
        RangeGuard.ThrowIfBelow(smoothPeakBelow, 0.0, nameof(smoothPeakBelow));
        RangeGuard.ThrowIfBelow(badPeakAtLeast, smoothPeakBelow, nameof(badPeakAtLeast));
        RangeGuard.ThrowIfBelow(badMinutesOver, 0.0, nameof(badMinutesOver));
        SmoothPeakBelow = smoothPeakBelow;
        BadPeakAtLeast = badPeakAtLeast;
        BadMinutesOver = badMinutesOver;
    }

    /// <summary>The peak below which, with no minutes over the redline, the verdict is Smooth.</summary>
    public double SmoothPeakBelow { get; }

    /// <summary>The peak at or above which the verdict is Bad.</summary>
    public double BadPeakAtLeast { get; }

    /// <summary>The minutes over the redline above which the verdict is Bad.</summary>
    public double BadMinutesOver { get; }
}

/// <summary>Where the worse of the two door lateness figures turns Smooth and Bad, in sim minutes.</summary>
public sealed record DoorsThresholds
{
    /// <summary>Creates the thresholds, rejecting a negative or non-finite value or a Bad line below the Smooth one.</summary>
    /// <param name="smoothAtMost">The minutes late at or below which the verdict is Smooth.</param>
    /// <param name="badOver">The minutes late above which the verdict is Bad.</param>
    public DoorsThresholds(double smoothAtMost, double badOver)
    {
        RangeGuard.ThrowIfBelow(smoothAtMost, 0.0, nameof(smoothAtMost));
        RangeGuard.ThrowIfBelow(badOver, smoothAtMost, nameof(badOver));
        SmoothAtMost = smoothAtMost;
        BadOver = badOver;
    }

    /// <summary>The minutes late at or below which the verdict is Smooth.</summary>
    public double SmoothAtMost { get; }

    /// <summary>The minutes late above which the verdict is Bad.</summary>
    public double BadOver { get; }
}
