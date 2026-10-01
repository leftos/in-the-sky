using Sky.Content.Schema;

namespace Sky.Content.Validation;

/// <summary>
/// Checks <c>crew.json</c>'s trait factors and roster values, the ranges the Engine leaves to the content: a crew trait's
/// <c>free_zones</c>, the zones it covers free of covering strain (<c>crew.md</c>: the Floater trait), is 0 or more, and a
/// roster entry's <c>competence</c> and <c>empathy</c> lie in [0, 1] and its <c>starting_fatigue</c> in [0, 100]
/// (<c>crew.md</c> "The crew model"). The trait ids, their uniqueness and every roster entry's trait reference are the
/// loader's to refuse (R10).
/// </summary>
public sealed class CrewValidator : IContentValidator
{
    private const string CrewFileName = "crew.json";

    /// <inheritdoc/>
    public void Validate(ContentSet content)
    {
        ArgumentNullException.ThrowIfNull(content);
        CheckTraits(content.Crew.Traits);
        CheckRoster(content.Crew.Roster);
    }

    /// <summary>Checks every trait's free zones.</summary>
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
        }
    }

    /// <summary>Checks every roster entry's competence, empathy and starting fatigue.</summary>
    private static void CheckRoster(IReadOnlyList<CrewMemberSpec> roster)
    {
        for (int position = 0; position < roster.Count; position++)
        {
            CrewMemberSpec member = roster[position];
            CheckRange($"$.roster[{position}].competence", member.Competence, "a competence", 1, member.Id);
            CheckRange($"$.roster[{position}].empathy", member.Empathy, "an empathy", 1, member.Id);
            CheckRange($"$.roster[{position}].starting_fatigue", member.StartingFatigue, "a starting_fatigue", 100, member.Id);
        }
    }

    /// <summary>
    /// Checks one roster value against 0 to <paramref name="highest"/>, both ends accepted, naming the member. The test is
    /// written so that a NaN is refused rather than slipping through both comparisons.
    /// </summary>
    private static void CheckRange(string path, double value, string what, double highest, string member)
    {
        if (!(value >= 0 && value <= highest))
        {
            string expected = $"Expected {what} in [0, {highest}] for crew member \"{member}\"; got {value}.";
            throw new ContentLoadException(CrewFileName, path, expected, null);
        }
    }
}
