namespace Sky.Engine.Flight;

/// <summary>What a stage does: <see cref="Start"/> runs once when the flight enters the stage.</summary>
public interface IStageHandler
{
    /// <summary>Runs once, as the flight enters the stage this handler belongs to.</summary>
    /// <param name="tick">The tick the stage was entered on.</param>
    void Start(long tick);
}
