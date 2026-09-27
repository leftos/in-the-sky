using Lua;
using Sky.Engine.Needs;
using Sky.Engine.Passengers;
using Sky.Engine.Ports;

namespace Sky.Scripting;

/// <summary>
/// The one adapter a module sees a passenger's facts through: one instance per flight, pointed at the facts of the
/// passenger being scored before each call, so a scoring loop allocates nothing per call and no module can write to the
/// facts. Every member is a read.
/// </summary>
[LuaObject]
internal sealed partial class LuaPassengerFacts
{
    internal static readonly string[] StageNames =
    [
        "pre-boarding",
        "boarding",
        "taxi-out",
        "takeoff",
        "climb",
        "cruise",
        "descent",
        "landing",
        "taxi-in",
        "deboarding",
        "done",
    ];

    internal static readonly string[] TurbulenceNames = ["none", "light", "moderate"];

    private readonly string[] moduleIds;
    private readonly string[] traitNames;
    private PassengerFacts facts;

    /// <summary>Creates the adapter over the flight's activity ids and trait names, which it keeps for itself.</summary>
    /// <param name="moduleIds">One module id per activity, indexed by <see cref="ActivityId.Value"/>; the array is the adapter's.</param>
    /// <param name="traitNames">One trait name per id, indexed by <see cref="TraitId.Value"/>; the array is the adapter's.</param>
    internal LuaPassengerFacts(string[] moduleIds, string[] traitNames)
    {
        this.moduleIds = moduleIds;
        this.traitNames = traitNames;
    }

    /// <summary>Gets the passenger's Refreshment.</summary>
    [LuaMember("refreshment")]
    public double Refreshment => facts.Needs[Need.Refreshment];

    /// <summary>Gets the passenger's Bladder.</summary>
    [LuaMember("bladder")]
    public double Bladder => facts.Needs[Need.Bladder];

    /// <summary>Gets the passenger's Rest.</summary>
    [LuaMember("rest")]
    public double Rest => facts.Needs[Need.Rest];

    /// <summary>Gets the passenger's Unease.</summary>
    [LuaMember("unease")]
    public double Unease => facts.Needs[Need.Unease];

    /// <summary>Gets the passenger's Boredom.</summary>
    [LuaMember("boredom")]
    public double Boredom => facts.Needs[Need.Boredom];

    /// <summary>Gets the stage of the flight as the kebab-case id events name it by.</summary>
    [LuaMember("stage")]
    public string Stage => StageNames[(int)facts.Stage];

    /// <summary>Gets whether the seatbelt sign is lit.</summary>
    [LuaMember("seatbelt_sign")]
    public bool SeatbeltSign => facts.SeatbeltSignOn;

    /// <summary>Gets how rough the air is.</summary>
    [LuaMember("turbulence")]
    public string TurbulenceName => TurbulenceNames[(int)facts.Turbulence];

    /// <summary>Gets the module id of the activity the passenger is on now.</summary>
    [LuaMember("current_activity")]
    public string CurrentActivity => moduleIds[facts.CurrentActivity.Value];

    /// <summary>Gets whether the cabin lights are down.</summary>
    [LuaMember("cabin_dimmed")]
    public bool CabinDimmed => facts.CabinDimmed;

    /// <summary>Gets whether the passenger's call light is on.</summary>
    [LuaMember("call_light")]
    public bool CallLight => facts.CallLightOn;

    /// <summary>Gets whether the passenger's tray table is down.</summary>
    [LuaMember("tray_down")]
    public bool TrayDown => facts.TrayDown;

    /// <summary>Gets whether a neighbour is chatting.</summary>
    [LuaMember("neighbour_chatting")]
    public bool NeighbourChatting => facts.NeighbourChatting;

    /// <summary>Gets whether an awake member of the passenger's group sits in an adjacent seat.</summary>
    [LuaMember("awake_group_member_adjacent")]
    public bool AwakeGroupMemberAdjacent => facts.AwakeGroupMemberAdjacent;

    /// <summary>Gets whether the seat's in-flight entertainment is available.</summary>
    [LuaMember("ife_available")]
    public bool IfeAvailable => facts.IfeAvailable;

    /// <summary>Gets whether the service cart is in the passenger's zone.</summary>
    [LuaMember("cart_in_zone")]
    public bool CartInZone => facts.CartInZone;

    /// <summary>Gets the sim minutes since the passenger was last woken, or <c>math.huge</c> when never woken.</summary>
    [LuaMember("minutes_since_woken")]
    public double MinutesSinceWoken => facts.MinutesSinceWoken;

    /// <summary>Gets the sim minutes since the passenger was last served, or <c>math.huge</c> when never served.</summary>
    [LuaMember("minutes_since_served")]
    public double MinutesSinceServed => facts.MinutesSinceServed;

    /// <summary>Reports whether the passenger carries the trait with this content name.</summary>
    /// <param name="name">The trait's content name.</param>
    /// <returns><see langword="true"/> when one of the passenger's traits has that name.</returns>
    [LuaMember("has_trait")]
    public bool HasTrait(string name)
    {
        foreach (TraitId trait in facts.Traits.Span)
        {
            if (string.Equals(traitNames[trait.Value], name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Points the adapter at the facts of the passenger whose next call this is.</summary>
    /// <param name="facts">The facts the next call reads.</param>
    internal void SetFacts(PassengerFacts facts) => this.facts = facts;
}
