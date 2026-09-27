using Sky.Content.Schema;

namespace Sky.Content.Validation;

/// <summary>
/// Checks <c>thoughts.json</c>: every kind's lever tag, and its neighbour lever tag when it has one, is one of
/// <see cref="LeverTags"/>, and its hook is one of <see cref="Hooks"/>.
/// </summary>
public sealed class ThoughtCatalogueValidator : IContentValidator
{
    private const string ThoughtsFile = "thoughts.json";

    /// <summary>
    /// Gets the lever tags a thought kind may carry (<c>passengers.md</c> section 10): the four M1 levers (<c>service_plan</c>,
    /// <c>lavs</c>, <c>lighting_plan</c>, <c>crew_staffing</c>), the two crew policies (<c>check_in_cadence</c>,
    /// <c>announcement_policy</c>), and <c>moment</c> for what no lever changes.
    /// </summary>
    public static IReadOnlyList<string> LeverTags { get; } =
    ["service_plan", "lavs", "lighting_plan", "crew_staffing", "check_in_cadence", "announcement_policy", "moment"];

    /// <summary>
    /// Gets the engine hook ids that give birth to thoughts, each with the kinds of <c>passengers.md</c> section 10's catalogue
    /// it serves: <c>boarded</c> (<c>late_and_fed_up</c>); <c>decision_point</c> (<c>still_waiting_for_drinks</c>,
    /// <c>too_bright_to_sleep</c>, <c>scared_of_bumps</c>, <c>bored_no_screen</c>); <c>lav_queue_joined</c>
    /// (<c>lav_queue_long</c>); <c>aisle_wait</c> (<c>stuck_behind_cart</c>); <c>served</c> (<c>served_late</c>,
    /// <c>drink_welcome</c>); <c>call_light_on</c> (<c>call_ignored</c>); <c>call_answered</c> (<c>call_answered_fast</c>);
    /// <c>lav_visit_finished</c> (<c>lav_untidy</c>); <c>witness_pulse</c> (<c>saw_incident</c>); <c>woken</c>
    /// (<c>woken_up</c>); <c>woke_naturally</c> (<c>good_nap</c>); <c>unease_lowered_by_crew</c> (<c>reassured</c>).
    /// </summary>
    public static IReadOnlyList<string> Hooks { get; } =
    [
        "boarded",
        "decision_point",
        "lav_queue_joined",
        "aisle_wait",
        "served",
        "call_light_on",
        "call_answered",
        "lav_visit_finished",
        "witness_pulse",
        "woken",
        "woke_naturally",
        "unease_lowered_by_crew",
    ];

    /// <inheritdoc/>
    public void Validate(ContentSet content)
    {
        ArgumentNullException.ThrowIfNull(content);
        IReadOnlyList<ThoughtKindSpec> kinds = content.Thoughts.Kinds;
        for (int position = 0; position < kinds.Count; position++)
        {
            string path = $"$.kinds[{position}]";
            ThoughtKindSpec kind = kinds[position];
            RequireListed($"{path}.lever_tag", kind.LeverTag, LeverTags, "a lever tag");
            if (kind.NeighbourLeverTag is { } neighbour)
            {
                RequireListed($"{path}.neighbour_lever_tag", neighbour, LeverTags, "a lever tag");
            }

            RequireListed($"{path}.hook", kind.Hook, Hooks, "an engine hook");
        }
    }

    private static void RequireListed(string path, string value, IReadOnlyList<string> known, string expected)
    {
        if (!known.Contains(value, StringComparer.Ordinal))
        {
            throw new ContentLoadException(ThoughtsFile, path, $"Expected {expected}, one of {string.Join(", ", known)}; got \"{value}\".", null);
        }
    }
}
