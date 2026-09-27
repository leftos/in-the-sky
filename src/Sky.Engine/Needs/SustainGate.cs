namespace Sky.Engine.Needs;

/// <summary>
/// One passenger's one need, watched for failure: the failure is raised on the tick the need has held at or above the
/// threshold for a whole window, and the same failure cannot be raised again until the need falls below the threshold
/// minus the reset margin. One gate per passenger per need.
/// </summary>
public sealed class SustainGate
{
    private readonly double threshold;
    private readonly long windowTicks;
    private readonly double reArmLevel;

    /// <summary>Creates a gate, armed with an empty run.</summary>
    /// <param name="threshold">The value at or above which an eligible tick counts, in [0, 100).</param>
    /// <param name="windowTicks">The counting ticks in a row that raise the failure, at least 1.</param>
    /// <param name="resetMargin">
    /// How far below the threshold the need must fall to re-arm, in [0, <paramref name="threshold"/>).
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">A parameter is outside its range.</exception>
    public SustainGate(double threshold, long windowTicks, double resetMargin)
    {
        if (!(threshold >= 0.0 && threshold < 100.0))
        {
            throw new ArgumentOutOfRangeException(nameof(threshold), threshold, $"The threshold must be in [0, 100); got {threshold}.");
        }

        if (windowTicks < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(windowTicks), windowTicks, $"The window must be at least 1 tick; got {windowTicks}.");
        }

        if (!(resetMargin >= 0.0 && resetMargin < threshold))
        {
            throw new ArgumentOutOfRangeException(
                nameof(resetMargin),
                resetMargin,
                $"The reset margin must be in [0, {threshold}); got {resetMargin}."
            );
        }

        this.threshold = threshold;
        this.windowTicks = windowTicks;
        reArmLevel = threshold - resetMargin;
    }

    /// <summary>Whether the gate is counting toward a failure; a raised gate stays disarmed until the need falls far enough.</summary>
    public bool IsArmed { get; private set; } = true;

    /// <summary>The counting ticks in the current run, 0 while disarmed.</summary>
    public long RunTicks { get; private set; }

    /// <summary>
    /// Runs one tick. While armed, an eligible tick at or above the threshold adds to the run and any other tick resets
    /// it; the tick the run reaches the window raises the failure, disarms the gate and resets the run. While disarmed
    /// nothing raises, and the first tick below the threshold minus the reset margin re-arms the gate without counting.
    /// </summary>
    /// <param name="value">The need's value this tick.</param>
    /// <param name="eligible">
    /// Whether this tick may count at all, such as a <c>noise_complaint</c> counting only while the passenger is awake.
    /// </param>
    /// <returns>Whether this tick raises the failure, which is exactly the tick the run reaches the window.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is NaN; per-tick inputs are the caller's to keep finite.</exception>
    public bool Tick(double value, bool eligible)
    {
        if (double.IsNaN(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"The need's value must not be NaN; got {value}.");
        }

        if (!IsArmed)
        {
            if (value < reArmLevel)
            {
                IsArmed = true;
            }

            return false;
        }

        if (eligible && value >= threshold)
        {
            RunTicks++;
            if (RunTicks < windowTicks)
            {
                return false;
            }

            RunTicks = 0;
            IsArmed = false;
            return true;
        }

        RunTicks = 0;
        return false;
    }
}
