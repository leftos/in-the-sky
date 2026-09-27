namespace Sky.Engine.Flight;

/// <summary>How rough the air is, as the feed reports it.</summary>
public enum Turbulence
{
    /// <summary>Still air.</summary>
    None,

    /// <summary>Light turbulence: it unsettles passengers and can wake a sleeping one.</summary>
    Light,

    /// <summary>Moderate turbulence: the stronger of the two rough levels.</summary>
    Moderate,
}
