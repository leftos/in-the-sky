using Sky.Content.Schema;

namespace Sky.Content.Validation;

/// <summary>
/// Checks <c>crew.json</c>'s values against the ranges the Engine leaves to the content: a crew trait's <c>free_zones</c>,
/// the zones it covers free of covering strain (<c>crew.md</c>: the Floater trait), is 0 or more; a roster entry's
/// <c>competence</c> and <c>empathy</c> lie in [0, 1] and its <c>starting_fatigue</c> in [0, 100] (<c>crew.md</c> "The crew
/// model"); and every trait factor, the <c>fatigue</c> block and the <c>service</c> block lie in the inclusive ranges of
/// <c>balance.md</c> section 3.12. A NaN or an infinity is refused with the rest. The trait ids, their uniqueness and every
/// roster entry's trait reference are the loader's to refuse (R10).
/// </summary>
public sealed class CrewValidator : IContentValidator
{
    private const string CrewFileName = "crew.json";

    /// <summary>The inclusive range of every bounded value, by its JSON field name, each unique across the file.</summary>
    private static readonly Dictionary<string, (double Min, double Max)> Ranges = new(StringComparer.Ordinal)
    {
        ["competence"] = (0, 1),
        ["empathy"] = (0, 1),
        ["starting_fatigue"] = (0, 100),
        ["dwell_factor"] = (0.25, 2.0),
        ["unease_relief_factor"] = (0.5, 2.0),
        ["service_time_factor"] = (0.5, 1.5),
        ["focus_fatigue_weight_factor"] = (0, 1.6),
        ["focus_factor_over_redline"] = (0.2, 1.0),
        ["preemption_strain_factor"] = (0, 2.0),
        ["on_task_strain_factor"] = (0.5, 1.5),
        ["cart_speed_factor"] = (0.5, 1.0),
        ["drinks_seconds_per_row"] = (20, 120),
        ["meal_seconds_per_row"] = (35, 105),
        ["sign_on_return_minutes"] = (1, 15),
        ["hand_service_lead_minutes"] = (0, 5),
        ["hand_drinks_seconds_per_row"] = (30, 120),
        ["hand_meal_seconds_per_row"] = (60, 240),
        ["rise_per_hour"] = (0, 20),
        ["over_redline_factor"] = (1, 4),
        ["break_fall_per_minute"] = (0, 1),
    };

    /// <inheritdoc/>
    public void Validate(ContentSet content)
    {
        ArgumentNullException.ThrowIfNull(content);
        CheckTraits(content.Crew.Traits);
        CheckRoster(content.Crew.Roster);
        CheckFatigue(content.Crew.Fatigue);
        CheckService(content.Crew.Service);
    }

    /// <summary>Checks every trait's free zones and factors.</summary>
    private static void CheckTraits(IReadOnlyList<CrewTraitSpec> traits)
    {
        for (int position = 0; position < traits.Count; position++)
        {
            CrewTraitSpec trait = traits[position];
            if (trait.FreeZones < 0)
            {
                string expected = $"Expected a free_zones of 0 or more for trait \"{trait.Id}\"; got {trait.FreeZones}.";
                throw new ContentLoadException(CrewFileName, $"$.traits[{position}].free_zones", expected, null);
            }

            string path = $"$.traits[{position}]";
            string owner = $"trait \"{trait.Id}\"";
            CheckRange(path, "focus_fatigue_weight_factor", trait.FocusFatigueWeightFactor, owner);
            CheckRange(path, "focus_factor_over_redline", trait.FocusFactorOverRedline, owner);
            CheckRange(path, "preemption_strain_factor", trait.PreemptionStrainFactor, owner);
            CheckRange(path, "on_task_strain_factor", trait.OnTaskStrainFactor, owner);
            CheckRange(path, "service_time_factor", trait.ServiceTimeFactor, owner);
            CheckRange(path, "unease_relief_factor", trait.UneaseReliefFactor, owner);
            CheckRange(path, "dwell_factor", trait.DwellFactor, owner);
        }
    }

    /// <summary>Checks every roster entry's competence, empathy and starting fatigue.</summary>
    private static void CheckRoster(IReadOnlyList<CrewMemberSpec> roster)
    {
        for (int position = 0; position < roster.Count; position++)
        {
            CrewMemberSpec member = roster[position];
            string path = $"$.roster[{position}]";
            string owner = $"crew member \"{member.Id}\"";
            CheckRange(path, "competence", member.Competence, owner);
            CheckRange(path, "empathy", member.Empathy, owner);
            CheckRange(path, "starting_fatigue", member.StartingFatigue, owner);
        }
    }

    /// <summary>Checks how fatigue rises and falls.</summary>
    private static void CheckFatigue(FatigueSpec fatigue)
    {
        const string path = "$.fatigue";
        const string owner = "the fatigue block";
        CheckRange(path, "rise_per_hour", fatigue.RisePerHour, owner);
        CheckRange(path, "over_redline_factor", fatigue.OverRedlineFactor, owner);
        CheckRange(path, "break_fall_per_minute", fatigue.BreakFallPerMinute, owner);
    }

    /// <summary>Checks the fixed timings of carts and hand service.</summary>
    private static void CheckService(ServiceTimingSpec service)
    {
        const string path = "$.service";
        const string owner = "the service block";
        CheckRange(path, "cart_speed_factor", service.CartSpeedFactor, owner);
        CheckRange(path, "drinks_seconds_per_row", service.DrinksSecondsPerRow, owner);
        CheckRange(path, "meal_seconds_per_row", service.MealSecondsPerRow, owner);
        CheckRange(path, "sign_on_return_minutes", service.SignOnReturnMinutes, owner);
        CheckRange(path, "hand_service_lead_minutes", service.HandServiceLeadMinutes, owner);
        CheckRange(path, "hand_drinks_seconds_per_row", service.HandDrinksSecondsPerRow, owner);
        CheckRange(path, "hand_meal_seconds_per_row", service.HandMealSecondsPerRow, owner);
    }

    /// <summary>
    /// Checks one value against its field's range in <see cref="Ranges"/>, both ends accepted, naming the field and its
    /// owner. The test is written so that a NaN is refused rather than slipping through both comparisons.
    /// </summary>
    private static void CheckRange(string parentPath, string field, double value, string owner)
    {
        (double Min, double Max) range = Ranges[field];
        if (!(value >= range.Min && value <= range.Max))
        {
            string expected = $"Expected {field} in [{range.Min}, {range.Max}] for {owner}; got {value}.";
            throw new ContentLoadException(CrewFileName, $"{parentPath}.{field}", expected, null);
        }
    }
}
