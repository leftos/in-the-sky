namespace Sky.Engine.Flight;

/// <summary>The stages of a flight, entered in this order by <see cref="StageMachine"/>.</summary>
public enum FlightStage
{
    /// <summary>The cabin is being prepared; nobody has boarded.</summary>
    PreBoarding,

    /// <summary>Passengers are boarding and settling into their seats.</summary>
    Boarding,

    /// <summary>The aircraft is taxiing to the runway.</summary>
    TaxiOut,

    /// <summary>The takeoff run and rotation.</summary>
    Takeoff,

    /// <summary>Climbing toward cruise altitude.</summary>
    Climb,

    /// <summary>Level at cruise altitude.</summary>
    Cruise,

    /// <summary>Descending toward the destination.</summary>
    Descent,

    /// <summary>The approach and touchdown.</summary>
    Landing,

    /// <summary>Taxiing from the runway to the gate.</summary>
    TaxiIn,

    /// <summary>Passengers are standing up and leaving the cabin.</summary>
    Deboarding,

    /// <summary>The flight is over; no stage follows it.</summary>
    Done,
}
