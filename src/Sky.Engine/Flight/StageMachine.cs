using Sky.Engine.Ports;

namespace Sky.Engine.Flight;

/// <summary>
/// The flight's stage machine. A feed observation moves it forward through <see cref="FlightStage"/>, calling the entered
/// stage's handler once as it is entered; the handler map is checked complete at construction, and a feed that jumps
/// several stages in one tick calls every crossed stage's handler in order on that tick. A handler that throws leaves its
/// stage entered; a later <see cref="Advance"/> continues from the next stage.
/// </summary>
public sealed class StageMachine
{
    private readonly IStageHandler[] handlers;

    /// <summary>Creates a machine from one handler for every stage.</summary>
    /// <param name="handlers">The handler of each <see cref="FlightStage"/> value; every value must be present and non-null.</param>
    /// <exception cref="ArgumentException">A stage has no handler.</exception>
    public StageMachine(IReadOnlyDictionary<FlightStage, IStageHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        FlightStage[] stages = Enum.GetValues<FlightStage>();
        this.handlers = new IStageHandler[stages.Length];
        foreach (FlightStage stage in stages)
        {
            if (!handlers.TryGetValue(stage, out IStageHandler? handler) || handler is null)
            {
                throw new ArgumentException($"No handler for stage {stage}.", nameof(handlers));
            }

            this.handlers[(int)stage] = handler;
        }
    }

    /// <summary>The stage the flight is in, or <see langword="null"/> until the first <see cref="Advance"/>.</summary>
    public FlightStage? Current { get; private set; }

    /// <summary>
    /// Moves the flight to the stage <paramref name="observation"/> reports, entering pre-boarding first when no stage has
    /// been entered, then each stage in enum order up to and including the target, calling its handler as it is entered.
    /// The stage only advances: a target equal to the current stage, or earlier than it, does nothing. Every step is read
    /// from <see cref="Current"/>, so a handler that advances the machine itself takes the remaining steps over.
    /// </summary>
    /// <param name="observation">The feed's reading for this tick; its stage must be a defined <see cref="FlightStage"/>.</param>
    /// <param name="tick">The tick the crossed stages are entered on.</param>
    /// <exception cref="ArgumentOutOfRangeException">The observation's stage is not a defined <see cref="FlightStage"/>.</exception>
    public void Advance(FeedObservation observation, long tick)
    {
        if (!Enum.IsDefined(observation.Stage))
        {
            throw new ArgumentOutOfRangeException(
                nameof(observation),
                observation.Stage,
                $"Feed stage {observation.Stage} is not a defined FlightStage."
            );
        }

        int target = (int)observation.Stage;
        while (NextStage() is { } stage && (int)stage <= target)
        {
            Current = stage;
            handlers[(int)stage].Start(tick);
        }
    }

    /// <summary>The stage after <see cref="Current"/>: pre-boarding while no stage is entered, none after the last stage.</summary>
    /// <returns>The next stage, or <see langword="null"/> once the flight is past its last stage.</returns>
    private FlightStage? NextStage()
    {
        if (Current is not { } current)
        {
            return FlightStage.PreBoarding;
        }

        int next = (int)current + 1;
        return next < handlers.Length ? (FlightStage)next : null;
    }
}
