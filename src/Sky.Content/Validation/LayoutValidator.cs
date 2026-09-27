using Sky.Engine.Cabin;

namespace Sky.Content.Validation;

/// <summary>
/// Checks every layout, in ordinal id order: its id is not empty; its cabin width, aisle widths, row pitches and seat widths are
/// positive and finite; every aisle and every seat group lies between the walls; no
/// seat group overlaps another group of its row or an aisle (spans that touch at an edge are fine); fixture ids are non-empty
/// and unique; there is at least one door, one lav and one galley; and every seat can walk to every fixture on the nav graph.
/// </summary>
public sealed class LayoutValidator : IContentValidator
{
    /// <summary>The walking pace the reachability check builds the nav graph with; whether a walk exists does not depend on it.</summary>
    private const double ReachabilityInchesPerTick = 1.0;

    private static readonly FixtureKind[] RequiredKinds = [FixtureKind.Door, FixtureKind.Lav, FixtureKind.Galley];

    /// <inheritdoc/>
    public void Validate(ContentSet content)
    {
        ArgumentNullException.ThrowIfNull(content);
        foreach (string id in content.Layouts.Keys.Order(StringComparer.Ordinal))
        {
            ValidateLayout($"layouts/{id}.json", content.Layouts[id]);
        }
    }

    private static void ValidateLayout(string file, CabinLayout layout)
    {
        if (layout.Id.Length == 0)
        {
            throw new ContentLoadException(file, "$.id", "Expected a non-empty id; got \"\", from a file named .json.", null);
        }

        CheckDimensions(file, layout);
        CheckAisles(file, layout);
        for (int row = 0; row < layout.Rows.Count; row++)
        {
            CheckRow(file, layout, row);
        }

        CheckFixtureIds(file, layout.Fixtures);
        CheckFixtureKinds(file, layout.Fixtures);
        CheckReachability(file, layout);
    }

    /// <summary>Checks that the cabin width, every aisle width, every row pitch and every seat width is positive and finite.</summary>
    private static void CheckDimensions(string file, CabinLayout layout)
    {
        RequirePositive(file, "$.cabin_width_inches", layout.CabinWidthInches, "cabin width");
        for (int aisle = 0; aisle < layout.Aisles.Count; aisle++)
        {
            RequirePositive(file, $"$.aisles[{aisle}].width_inches", layout.Aisles[aisle].WidthInches, "aisle width");
        }

        for (int row = 0; row < layout.Rows.Count; row++)
        {
            RequirePositive(file, $"$.rows[{row}].pitch_inches", layout.Rows[row].PitchInches, "row pitch");
            IReadOnlyList<SeatGroup> groups = layout.Rows[row].Groups;
            for (int group = 0; group < groups.Count; group++)
            {
                for (int seat = 0; seat < groups[group].Seats.Count; seat++)
                {
                    string path = $"$.rows[{row}].groups[{group}].seats[{seat}].width_inches";
                    RequirePositive(file, path, groups[group].Seats[seat].WidthInches, "seat width");
                }
            }
        }
    }

    private static void RequirePositive(string file, string path, double inches, string what)
    {
        if (inches is not > 0 || !double.IsFinite(inches))
        {
            throw new ContentLoadException(file, path, $"Expected a positive, finite {what} in inches; got {inches}.", null);
        }
    }

    private static void CheckAisles(string file, CabinLayout layout)
    {
        for (int aisle = 0; aisle < layout.Aisles.Count; aisle++)
        {
            var span = Span.Of(layout.Aisles[aisle]);
            if (!span.LiesWithin(layout.CabinWidthInches))
            {
                throw new ContentLoadException(
                    file,
                    $"$.aisles[{aisle}]",
                    $"Expected an aisle between the walls, 0 to {layout.CabinWidthInches} inches; {span}.",
                    null
                );
            }
        }
    }

    private static void CheckRow(string file, CabinLayout layout, int row)
    {
        IReadOnlyList<SeatGroup> groups = layout.Rows[row].Groups;
        for (int group = 0; group < groups.Count; group++)
        {
            string path = $"$.rows[{row}].groups[{group}]";
            var span = Span.Of(groups[group]);
            if (!span.LiesWithin(layout.CabinWidthInches))
            {
                throw new ContentLoadException(
                    file,
                    path,
                    $"Expected a seat group between the walls, 0 to {layout.CabinWidthInches} inches; {span}.",
                    null
                );
            }

            for (int other = 0; other < group; other++)
            {
                if (span.Overlaps(Span.Of(groups[other])))
                {
                    throw new ContentLoadException(file, path, $"Expected a seat group clear of group {other} of its row; {span} overlaps it.", null);
                }
            }

            for (int aisle = 0; aisle < layout.Aisles.Count; aisle++)
            {
                if (span.Overlaps(Span.Of(layout.Aisles[aisle])))
                {
                    throw new ContentLoadException(file, path, $"Expected a seat group clear of aisle {aisle}; {span} overlaps it.", null);
                }
            }
        }
    }

    private static void CheckFixtureIds(string file, IReadOnlyList<CabinFixture> fixtures)
    {
        Dictionary<string, int> seen = new(StringComparer.Ordinal);
        for (int position = 0; position < fixtures.Count; position++)
        {
            string path = $"$.fixtures[{position}].id";
            string id = fixtures[position].Id;
            if (id.Length == 0)
            {
                throw new ContentLoadException(file, path, "Expected a non-empty fixture id; got \"\".", null);
            }

            if (!seen.TryAdd(id, position))
            {
                throw new ContentLoadException(
                    file,
                    path,
                    $"Expected a unique fixture id; \"{id}\" is already declared at $.fixtures[{seen[id]}].",
                    null
                );
            }
        }
    }

    private static void CheckFixtureKinds(string file, IReadOnlyList<CabinFixture> fixtures)
    {
        foreach (FixtureKind kind in RequiredKinds)
        {
            if (!fixtures.Any(fixture => fixture.Kind == kind))
            {
                throw new ContentLoadException(file, "$.fixtures", $"Expected at least one {kind} fixture; the layout has none.", null);
            }
        }
    }

    private static void CheckReachability(string file, CabinLayout layout)
    {
        NavGraph graph;
        try
        {
            graph = NavGraphBuilder.Build(layout, ReachabilityInchesPerTick);
        }
        catch (ArgumentException exception)
        {
            throw new ContentLoadException(file, "$", $"Expected a layout the Engine can build a nav graph from; {exception.Message}", exception);
        }

        var table = PathTable.Build(graph);
        int[] fixtureNodes = [.. layout.Fixtures.Select(fixture => graph.FixtureNode(fixture.Id))];
        int seat = 0;
        foreach (string path in SeatPaths(layout))
        {
            int node = graph.SeatNodes[seat++];
            for (int fixture = 0; fixture < fixtureNodes.Length; fixture++)
            {
                if (table.Cost(node, fixtureNodes[fixture]) == -1)
                {
                    string id = layout.Fixtures[fixture].Id;
                    throw new ContentLoadException(
                        file,
                        path,
                        $"Expected every seat to reach every fixture; this seat has no walk to \"{id}\".",
                        null
                    );
                }
            }
        }
    }

    /// <summary>Each seat's JSON path, in the order the nav graph builder numbers the seats: row by row, group by group, left to right.</summary>
    private static IEnumerable<string> SeatPaths(CabinLayout layout)
    {
        for (int row = 0; row < layout.Rows.Count; row++)
        {
            IReadOnlyList<SeatGroup> groups = layout.Rows[row].Groups;
            for (int group = 0; group < groups.Count; group++)
            {
                for (int seat = 0; seat < groups[group].Seats.Count; seat++)
                {
                    yield return $"$.rows[{row}].groups[{group}].seats[{seat}]";
                }
            }
        }
    }

    /// <summary>
    /// A lateral span in inches from the left wall. Its ends, and the wall it is compared with, are rounded to 9 decimals as the
    /// nav graph builder rounds distances, so floating-point noise on a sum of widths never turns a touching edge into an overlap.
    /// </summary>
    private readonly record struct Span(double Low, double High)
    {
        private const int Decimals = 9;

        public static Span Of(Aisle aisle) => Rounded(aisle.CenterInches - (aisle.WidthInches / 2), aisle.CenterInches + (aisle.WidthInches / 2));

        public static Span Of(SeatGroup group) => Rounded(group.LeftInches, group.LeftInches + group.Seats.Sum(seat => seat.WidthInches));

        public bool LiesWithin(double cabinWidth) => Low >= 0 && High <= Round(cabinWidth);

        private static Span Rounded(double low, double high) => new(Round(low), Round(high));

        private static double Round(double inches) => Math.Round(inches, Decimals);

        /// <summary>Whether the two spans share more than an edge.</summary>
        public bool Overlaps(Span other) => Low < other.High && other.Low < High;

        public override string ToString() => $"it spans {Low} to {High} inches";
    }
}
