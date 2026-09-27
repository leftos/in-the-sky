namespace Sky.Engine.Needs;

/// <summary>A passenger need, each on a scale from 0 (fine) to 100 (at its worst).</summary>
public enum Need
{
    /// <summary>Hunger and thirst as one; rises with time and falls with drinks and meals.</summary>
    Refreshment,

    /// <summary>Rises with time and with drinks; falls with a lav visit.</summary>
    Bladder,

    /// <summary>Tiredness; rises while awake and falls only while asleep.</summary>
    Rest,

    /// <summary>Anxiety; pulled toward the passenger's baseline and pushed up by what happens in the cabin.</summary>
    Unease,

    /// <summary>Rises with time; paused while asleep or watching IFE.</summary>
    Boredom,
}
