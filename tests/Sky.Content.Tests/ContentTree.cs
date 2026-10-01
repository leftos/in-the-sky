using System.Text;

namespace Sky.Content.Tests;

/// <summary>
/// A content tree written to its own temporary directory: the smallest set of files <see cref="ContentLoader"/> accepts,
/// which a test then changes one file at a time. Disposing it deletes the directory.
/// </summary>
internal sealed class ContentTree : IDisposable
{
    /// <summary>The layout file, one row of three seats and a door, a lav and a galley at row 0.</summary>
    public const string Layout = """
        {
          "id": "tiny",
          "cabin_width_inches": 148,
          "rows": [
            { "pitch_inches": 30, "groups": [ { "left_inches": 2, "seats": [
              { "label": "A", "width_inches": 18 }, { "label": "B", "width_inches": 18 }, { "label": "C", "width_inches": 18 } ] } ] }
          ],
          "aisles": [ { "center_inches": 74, "width_inches": 20 } ],
          "fixtures": [
            { "id": "door-fwd", "kind": "Door", "row_index": 0, "aisle_index": 0, "distance_inches": 20 },
            { "id": "lav-fwd", "kind": "Lav", "row_index": 0, "aisle_index": 0, "distance_inches": 30 },
            { "id": "galley-fwd", "kind": "Galley", "row_index": 0, "aisle_index": 0, "distance_inches": 40 }
          ]
        }
        """;

    /// <summary>The needs file, with one cascade rule and one incident kind.</summary>
    public const string Needs = """
        {
          "rates": {
            "refreshment_per_hour": 25, "bladder_per_hour": 15, "rest_rise_per_hour": 5, "rest_fall_per_hour": 25,
            "boredom_per_hour": 10, "unease_half_life_minutes": 15
          },
          "cascade_rules": [ { "source": "Bladder", "target": "Unease", "threshold": 70, "factor": 1.5, "condition": "LavUnreachable" } ],
          "distress_terms": [
            { "need": "Refreshment", "threshold": 60, "weight": 1 }, { "need": "Bladder", "threshold": 60, "weight": 1 },
            { "need": "Rest", "threshold": 60, "weight": 1 }, { "need": "Unease", "threshold": 40, "weight": 1 },
            { "need": "Boredom", "threshold": 60, "weight": 1 }
          ],
          "distress_bands": { "uneasy_from": 30, "distressed_from": 60 },
          "starting": { "refreshment": { "min": 10, "max": 40 }, "bladder": { "min": 0, "max": 20 }, "boredom": { "min": 5, "max": 25 },
            "group_spread": 10 },
          "gate_conditions": {
            "closed_refreshment": 10, "refreshment_per_minute_open": 0.05, "refreshment_per_minute_closed": 0.25, "refreshment_cap": 20,
            "boredom_per_minute": 0.2, "boredom_cap": 20, "unease_grace_minutes": 15, "unease_per_minute": 0.25, "unease_cap": 15,
            "late_and_fed_up_from_minutes": 30, "late_and_fed_up_unease_factor": 1.1, "starting_refreshment_cap": 70
          },
          "unease_pushes": {
            "aboard_per_hour": 6, "takeoff_and_landing_per_hour": 30, "climb_and_descent_per_hour": 10, "light_turbulence_per_hour": 30,
            "moderate_turbulence_per_hour": 80, "late_pushback_per_hour": 15, "late_pushback_grace_minutes": 5,
            "unanswered_call_per_hour": 20, "unanswered_call_grace_minutes": 5, "woken_pulse": 5, "pulse_minutes": 2
          },
          "seat_comfort": {
            "business": { "rest": 0.85, "unease": 0.9 }, "economy_middle": { "rest": 1.1, "unease": 1.1 },
            "economy_window_or_aisle": { "rest": 1, "unease": 1 }
          },
          "contagion": {
            "threshold": 40, "beside_weight": 0.25, "across_aisle_weight": 0.15, "calming_threshold": 40, "calming_beside_weight": 0.15,
            "calming_across_aisle_weight": 0.1, "witness_rows": 2, "witness_raised_pulse": 8, "witness_missed_pulse": 15
          },
          "incidents": [ { "id": "accident", "need": "Bladder", "threshold": 90, "window_minutes": 10, "deadline_minutes": 5 } ],
          "incident_reset_margin": 30,
          "node_capacities": { "aisle_slot": 1, "seat": 1, "door": 2, "lav": 1, "lav_queue": 2, "galley": 4 }
        }
        """;

    /// <summary>The traits file: two traits, a belonging and one forbidden pair.</summary>
    public const string Traits = """
        {
          "default_unease_baseline": 10,
          "traits": [
            { "id": "anxious", "modifiers": [ { "need": "Unease", "factor": 1.3 } ], "unease_baseline": 25, "adult_weight": 12,
              "child_optional": true },
            { "id": "calm", "adult_weight": 5 },
            { "id": "child", "given_to_every_child": true },
            { "id": "sleep_kit", "wake_chance_factor": 0.5, "share": { "adults": 0.15, "business_trips": 0.25, "children": 0 } }
          ],
          "forbidden_pairs": [ { "first": "anxious", "second": "calm" } ]
        }
        """;

    /// <summary>The manifest file: every share and range the generator draws by, and two professions.</summary>
    public const string Manifest = """
        {
          "load_factor": { "min": 0.82, "max": 0.95 },
          "business_booked": { "min": 8, "max": 12 },
          "business_row_count": 3,
          "business_cabin_purposes": [
            { "value": "Business", "weight": 70 }, { "value": "Leisure", "weight": 20 }, { "value": "Visiting", "weight": 10 } ],
          "economy_cabin_purposes": [
            { "value": "Business", "weight": 20 }, { "value": "Leisure", "weight": 55 }, { "value": "Visiting", "weight": 25 } ],
          "business_trip": {
            "group_sizes": [ { "value": 1, "weight": 85 }, { "value": 2, "weight": 15 } ],
            "family_share": 0, "wake_minutes": { "min": 300, "max": 420 } },
          "leisure_trip": {
            "group_sizes": [ { "value": 1, "weight": 30 }, { "value": 2, "weight": 40 }, { "value": 3, "weight": 15 },
              { "value": 4, "weight": 15 } ],
            "family_share": 0.5, "wake_minutes": { "min": 360, "max": 540 } },
          "visiting_trip": {
            "group_sizes": [ { "value": 1, "weight": 50 }, { "value": 2, "weight": 30 }, { "value": 3, "weight": 20 } ],
            "family_share": 0.4, "wake_minutes": { "min": 360, "max": 540 } },
          "family_minimum_size": 3,
          "family_adults": 1,
          "wake_spread_minutes": 30,
          "adult_trait_counts": [
            { "value": 0, "weight": 30 }, { "value": 1, "weight": 50 }, { "value": 2, "weight": 20 } ],
          "child_extra_trait_share": 0.5,
          "professions": [
            { "id": "office_worker", "weight": 30, "on_business_trips": true },
            { "id": "retired", "weight": 10, "on_business_trips": false } ]
        }
        """;

    /// <summary>The activities file: <c>idle</c> then <c>sleep</c>.</summary>
    public const string Activities = """
        {
          "activities": [
            { "id": "idle", "site": "Seat", "needs_crew": false },
            { "id": "sleep", "site": "Seat", "needs_crew": false, "duration": { "min": 20, "max": 90 },
              "effects": [ { "need": "Rest", "change": -25, "timing": "PerHour", "lighting": "Dimmed" } ], "pauses": [ "Boredom" ] }
          ],
          "call_reasons": [ { "id": "reassurance", "granted": [ { "need": "Unease", "change": -20 } ], "refused": [] } ],
          "movement": {
            "occupied_seat_cross_ticks": 4, "tray_down_cross_ticks": 12, "tray_down_eater_unease": 3, "crossed_sleeper_wake_chance": 0.6,
            "lav_queue_minutes_per_person": 3, "overflow_give_up_minutes": 8, "overflow_give_up_below_bladder": 80
          }
        }
        """;

    /// <summary>The <c>idle</c> activity's module.</summary>
    public const string IdleModule = "return { utility = function(facts) return 1 end }\n";

    /// <summary>The <c>sleep</c> activity's module.</summary>
    public const string SleepModule = "return { utility = function(facts) return facts.rest end }\n";

    /// <summary>The crew file: one crew trait, two crew.</summary>
    public const string Crew = """
        {
          "traits": [ { "id": "steady", "focus_fatigue_weight_factor": 0.5, "preemption_strain_factor": 0.5 } ],
          "roster": [
            { "id": "purser", "competence": 0.85, "empathy": 0.7, "starting_fatigue": 20, "trait": "steady" },
            { "id": "fa2", "competence": 0.7, "empathy": 0.4, "starting_fatigue": 30, "trait": null }
          ],
          "fatigue": { "rise_per_hour": 6, "over_redline_factor": 2, "break_fall_per_minute": 0.2 },
          "service": {
            "cart_speed_factor": 0.5, "drinks_seconds_per_row": 40, "meal_seconds_per_row": 70, "sign_on_return_minutes": 10,
            "hand_service_lead_minutes": 3, "hand_drinks_seconds_per_row": 60, "hand_meal_seconds_per_row": 120
          }
        }
        """;

    /// <summary>The scenario file, with needs, events and contagion on and thoughts off.</summary>
    public const string Scenario = """
        {
          "id": "ref",
          "layout": "tiny",
          "gate_delay_minutes": 0,
          "concessions_open": true,
          "systems": { "needs": true, "events": true, "contagion": true, "thoughts": false },
          "service_plan": [
            { "kind": "Drinks", "start_minutes": 5, "direction": "FrontToBack" },
            { "kind": "Meal", "start_minutes": 40, "direction": "FrontToBack" }
          ],
          "locked_lavs": [],
          "lighting_plan": [ { "from_minutes": 70, "to_minutes": 100 } ],
          "crew": {
            "count": 2,
            "zones": [ { "id": "F", "first_row": 0, "last_row": 0, "stations": [ "door-fwd", "lav-fwd" ], "crew": [ "purser", "fa2" ] } ],
            "carts": [ { "first_row": 0, "last_row": 0, "crew": [ "purser", "fa2" ] } ]
          },
          "timeline": {
            "boarding_start_local_minutes": 630,
            "entries": [
              { "at_minutes": -20, "stage": "PreBoarding", "seatbelt_sign": true },
              { "at_minutes": 0, "stage": "Boarding", "seatbelt_sign": true }
            ]
          }
        }
        """;

    /// <summary>The thought catalogue, two kinds.</summary>
    public const string Thoughts = """
        {
          "kinds": [
            { "id": "call_ignored", "valence": "Grumble", "hook": "call_light_on", "lever_tag": "crew_staffing", "salience": 3,
              "lasts_minutes": 30, "threshold": 5 },
            { "id": "late_and_fed_up", "valence": "Grumble", "hook": "boarded", "lever_tag": "service_plan", "salience": 2,
              "lasts_minutes": null, "until": "first_served" }
          ]
        }
        """;

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private ContentTree(string root) => Root = root;

    /// <summary>Gets the tree's content root.</summary>
    public string Root { get; }

    /// <summary>The files of the minimal tree, keyed by their path relative to the root, in the order they are written.</summary>
    public static IReadOnlyList<KeyValuePair<string, string>> MinimalFiles { get; } =
    [
        new("layouts/tiny.json", Layout),
        new("manifest.json", Manifest),
        new("needs.json", Needs),
        new("traits.json", Traits),
        new("activities.json", Activities),
        new("activities/idle.lua", IdleModule),
        new("activities/sleep.lua", SleepModule),
        new("crew.json", Crew),
        new("scenarios/ref.json", Scenario),
        new("thoughts.json", Thoughts),
    ];

    /// <summary>Writes the minimal tree into a new temporary directory.</summary>
    /// <returns>The tree.</returns>
    public static ContentTree Minimal() => From(MinimalFiles);

    /// <summary>Writes the given files, in the given order, into a new temporary directory.</summary>
    /// <param name="files">Each file's path relative to the root and its text.</param>
    /// <returns>The tree.</returns>
    public static ContentTree From(IEnumerable<KeyValuePair<string, string>> files)
    {
        string root = Path.Combine(Path.GetTempPath(), "sky-content-tests", Guid.NewGuid().ToString("N"));
        ContentTree tree = new(root);
        foreach (KeyValuePair<string, string> file in files)
        {
            tree.Write(file.Key, file.Value);
        }

        return tree;
    }

    /// <summary>Writes one file as UTF-8 without a byte-order mark, replacing any file already there.</summary>
    /// <param name="relative">The path relative to the root, with forward slashes.</param>
    /// <param name="text">The file's text.</param>
    public void Write(string relative, string text)
    {
        string path = Path.Combine(Root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text, Utf8NoBom);
    }

    /// <summary>Replaces the one occurrence of <paramref name="oldText"/> in a file, failing the test when it is absent.</summary>
    /// <param name="relative">The path relative to the root, with forward slashes.</param>
    /// <param name="oldText">The text to replace, which must occur in the file.</param>
    /// <param name="newText">The replacement.</param>
    public void Replace(string relative, string oldText, string newText)
    {
        string text = File.ReadAllText(Path.Combine(Root, relative));
        Assert.Contains(oldText, text, StringComparison.Ordinal);
        Write(relative, text.Replace(oldText, newText, StringComparison.Ordinal));
    }

    /// <summary>Deletes one file.</summary>
    /// <param name="relative">The path relative to the root, with forward slashes.</param>
    public void Delete(string relative) => File.Delete(Path.Combine(Root, relative));

    /// <summary>Loads the tree.</summary>
    /// <returns>The loaded content.</returns>
    public ContentSet Load() => ContentLoader.Load(Root);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
