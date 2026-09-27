namespace Sky.Engine.Passengers;

/// <summary>A passenger's age band, fixed by the manifest; a child also carries the <c>child</c> trait.</summary>
public enum AgeBand
{
    /// <summary>An adult, who may travel alone and may be a child's companion.</summary>
    Adult,

    /// <summary>A child, who always travels in a booking with at least one adult.</summary>
    Child,
}
