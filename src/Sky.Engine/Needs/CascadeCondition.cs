namespace Sky.Engine.Needs;

/// <summary>The cabin condition a cascade rule needs, on top of its source need's value.</summary>
public enum CascadeCondition
{
    /// <summary>The source's value alone decides: the rule needs no cabin condition.</summary>
    None,

    /// <summary>
    /// The passenger cannot reach a lav this tick — a seatbelt sign, a queue or a blocked aisle. The caller decides,
    /// from where the passenger is and what the aisle holds.
    /// </summary>
    LavUnreachable,
}
