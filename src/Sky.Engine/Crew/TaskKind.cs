namespace Sky.Engine.Crew;

/// <summary>What a crew task is for, one per row of crew.md's task table; a task's priorities are data on the task, not read from its kind.</summary>
public enum TaskKind
{
    /// <summary>Walk a zone's rows until every passenger in it is seated.</summary>
    SecureCheck,

    /// <summary>Reach a passenger with an accident or a panic before its escalation deadline.</summary>
    SevereIncident,

    /// <summary>Carry out the crew work an event choice asks for.</summary>
    EventWork,

    /// <summary>Reach a passenger with one of the minor incident kinds.</summary>
    MinorIncident,

    /// <summary>Ask a passenger out of their seat with the sign on to sit back down.</summary>
    BackToSeat,

    /// <summary>Answer a pressed call button.</summary>
    CallButton,

    /// <summary>Revisit a passenger whose Unease was read as urgent.</summary>
    CheckInFollowUp,

    /// <summary>Bring a drink to a passenger whose Refreshment was read as urgent.</summary>
    CatchUpDrink,

    /// <summary>Help a passenger stowing a bag during boarding.</summary>
    BoardingHelp,

    /// <summary>Help a passenger retrieving a bag during deboarding.</summary>
    DeboardingHelp,

    /// <summary>Stand at the forward crew station while passengers come through the door.</summary>
    DoorStation,

    /// <summary>Rest at the galley to lower strain.</summary>
    GalleyBreak,

    /// <summary>Hand-serve the passengers a round skipped.</summary>
    CatchUpService,

    /// <summary>Serve the business rows by hand.</summary>
    BusinessService,

    /// <summary>Run a drinks round with a cart.</summary>
    DrinksRound,

    /// <summary>Run a meal service with a cart.</summary>
    Meal,

    /// <summary>Check and reset a lavatory.</summary>
    LavCheck,

    /// <summary>Walk a zone and look in on uneasy or distressed passengers.</summary>
    CheckInWalk,
}
