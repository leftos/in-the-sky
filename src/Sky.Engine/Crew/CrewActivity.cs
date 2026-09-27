namespace Sky.Engine.Crew;

/// <summary>What a crew member is doing on a tick, as far as strain is concerned.</summary>
public enum CrewActivity
{
    /// <summary>Holding no task, not on break and not seated: strain decays, less the zone backlog.</summary>
    Idle,

    /// <summary>Working a claimed task other than a break: strain rises.</summary>
    OnTask,

    /// <summary>On a galley break: strain falls fastest and the time since the last break restarts.</summary>
    OnBreak,

    /// <summary>In a jumpseat: strain neither rises nor decays from activity.</summary>
    Seated,
}
