using Sky.Content.Schema;
using Sky.Engine.Cabin;

namespace Sky.Content.Validation;

/// <summary>
/// Checks every scenario, in ordinal id order: its id is not empty; its layout is loaded; its gate delay lies in
/// <see cref="MinGateDelayMinutes"/> to <see cref="MaxGateDelayMinutes"/>; every locked lav is a lav of the layout; and its
/// crew lever holds together (<c>crew.md</c>: every row belongs to exactly one zone and every zone has at least one crew
/// member), with crew from <c>crew.json</c>'s roster, a count equal to the distinct crew across the zones, stations that are
/// fixtures of the layout and cart spans within the layout.
/// </summary>
public sealed class ScenarioValidator : IContentValidator
{
    /// <summary>The shortest gate delay a scenario may set, in minutes.</summary>
    public const int MinGateDelayMinutes = 0;

    /// <summary>The longest gate delay a scenario may set, in minutes.</summary>
    public const int MaxGateDelayMinutes = 240;

    /// <inheritdoc/>
    public void Validate(ContentSet content)
    {
        ArgumentNullException.ThrowIfNull(content);
        HashSet<string> roster = new(content.Crew.Roster.Select(member => member.Id), StringComparer.Ordinal);
        foreach (string id in content.Scenarios.Keys.Order(StringComparer.Ordinal))
        {
            ValidateScenario($"scenarios/{id}.json", content.Scenarios[id], content.Layouts, roster);
        }
    }

    private static void ValidateScenario(string file, ScenarioFile scenario, IReadOnlyDictionary<string, CabinLayout> layouts, HashSet<string> roster)
    {
        if (scenario.Id.Length == 0)
        {
            throw new ContentLoadException(file, "$.id", "Expected a non-empty id; got \"\", from a file named .json.", null);
        }

        if (!layouts.TryGetValue(scenario.Layout, out CabinLayout? layout))
        {
            throw new ContentLoadException(file, "$.layout", $"Unknown layout \"{scenario.Layout}\"; expected a layout under layouts/.", null);
        }

        if (scenario.GateDelayMinutes is < MinGateDelayMinutes or > MaxGateDelayMinutes)
        {
            string expected = $"Expected a gate delay of {MinGateDelayMinutes} to {MaxGateDelayMinutes} minutes; got {scenario.GateDelayMinutes}.";
            throw new ContentLoadException(file, "$.gate_delay_minutes", expected, null);
        }

        CheckLockedLavs(file, scenario.LockedLavs, layout);
        new CrewCheck(file, layout, roster).Run(scenario.Crew);
    }

    private static void CheckLockedLavs(string file, IReadOnlyList<string> lockedLavs, CabinLayout layout)
    {
        for (int position = 0; position < lockedLavs.Count; position++)
        {
            string id = lockedLavs[position];
            if (!layout.Fixtures.Any(fixture => fixture.Kind == FixtureKind.Lav && string.Equals(fixture.Id, id, StringComparison.Ordinal)))
            {
                throw new ContentLoadException(
                    file,
                    $"$.locked_lavs[{position}]",
                    $"Expected the id of a lav of layout \"{layout.Id}\"; got \"{id}\".",
                    null
                );
            }
        }
    }

    /// <summary>The crew lever's checks against one layout and the roster.</summary>
    private sealed class CrewCheck(string file, CabinLayout layout, HashSet<string> roster)
    {
        private readonly HashSet<string> fixtureIds = new(layout.Fixtures.Select(fixture => fixture.Id), StringComparer.Ordinal);

        public void Run(CrewAssignment crew)
        {
            int[] zoneOfRow = new int[layout.Rows.Count];
            Array.Fill(zoneOfRow, -1);
            for (int zone = 0; zone < crew.Zones.Count; zone++)
            {
                CheckZone(crew.Zones[zone], zone, zoneOfRow);
            }

            int gap = Array.IndexOf(zoneOfRow, -1);
            if (gap >= 0)
            {
                throw new ContentLoadException(file, "$.crew.zones", $"Expected every row in exactly one zone; row {gap} is in none.", null);
            }

            int distinct = crew.Zones.SelectMany(zone => zone.Crew).Distinct(StringComparer.Ordinal).Count();
            if (crew.Count != distinct)
            {
                string expected = $"Expected the crew count to equal the {distinct} distinct crew across the zones; got {crew.Count}.";
                throw new ContentLoadException(file, "$.crew.count", expected, null);
            }

            for (int cart = 0; cart < crew.Carts.Count; cart++)
            {
                string path = $"$.crew.carts[{cart}]";
                CheckRowSpan(path, crew.Carts[cart].FirstRow, crew.Carts[cart].LastRow);
                CheckCrewIds($"{path}.crew", crew.Carts[cart].Crew);
            }
        }

        private void CheckZone(CrewZone zone, int position, int[] zoneOfRow)
        {
            string path = $"$.crew.zones[{position}]";
            CheckRowSpan(path, zone.FirstRow, zone.LastRow);
            for (int row = zone.FirstRow; row <= zone.LastRow; row++)
            {
                if (zoneOfRow[row] >= 0)
                {
                    string expected = $"Expected every row in exactly one zone; row {row} is already in $.crew.zones[{zoneOfRow[row]}].";
                    throw new ContentLoadException(file, path, expected, null);
                }

                zoneOfRow[row] = position;
            }

            if (zone.Crew.Count == 0)
            {
                throw new ContentLoadException(file, $"{path}.crew", "Expected at least one crew member in the zone; got none.", null);
            }

            CheckCrewIds($"{path}.crew", zone.Crew);
            for (int station = 0; station < zone.Stations.Count; station++)
            {
                if (!fixtureIds.Contains(zone.Stations[station]))
                {
                    string expected = $"Expected the id of a fixture of layout \"{layout.Id}\"; got \"{zone.Stations[station]}\".";
                    throw new ContentLoadException(file, $"{path}.stations[{station}]", expected, null);
                }
            }
        }

        private void CheckRowSpan(string path, int firstRow, int lastRow)
        {
            int rows = layout.Rows.Count;
            if (firstRow < 0 || firstRow >= rows)
            {
                string expected = $"Expected a row of layout \"{layout.Id}\", 0 to {rows - 1}; got {firstRow}.";
                throw new ContentLoadException(file, $"{path}.first_row", expected, null);
            }

            if (lastRow < 0 || lastRow >= rows)
            {
                string expected = $"Expected a row of layout \"{layout.Id}\", 0 to {rows - 1}; got {lastRow}.";
                throw new ContentLoadException(file, $"{path}.last_row", expected, null);
            }

            if (firstRow > lastRow)
            {
                throw new ContentLoadException(file, path, $"Expected the first row at or before the last; got {firstRow} to {lastRow}.", null);
            }
        }

        private void CheckCrewIds(string path, IReadOnlyList<string> crewIds)
        {
            for (int position = 0; position < crewIds.Count; position++)
            {
                if (!roster.Contains(crewIds[position]))
                {
                    string expected = $"Unknown crew id \"{crewIds[position]}\"; expected a crew member of crew.json's roster.";
                    throw new ContentLoadException(file, $"{path}[{position}]", expected, null);
                }
            }
        }
    }
}
