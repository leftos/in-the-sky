namespace Sky.Engine.Ports;

/// <summary>The source of sim time the engine advances by; the host implements it.</summary>
public interface IClockSource
{
    /// <summary>Returns the sim milliseconds elapsed since the previous call, never negative.</summary>
    /// <returns>The sim milliseconds elapsed since the previous call.</returns>
    long TakeElapsedMilliseconds();
}
