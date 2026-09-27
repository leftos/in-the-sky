using System.Globalization;
using Sky.Content.Validation;
using Sky.Engine.Cabin;

namespace Sky.Content.Tests;

/// <summary>
/// Pins the layout validator: a layout's id is not empty, its aisles and seat groups sit between the walls without
/// overlapping, its fixture ids are unique, it has a door, a lav and a galley, and every seat can walk to every fixture;
/// each refusal names the layout's file and the JSON path of the offending value (R10).
/// </summary>
public sealed class LayoutValidatorTests
{
    private const string LayoutFile = "layouts/tiny.json";

    private const string LastSeat = """{ "label": "C", "width_inches": 18 } ] }""";

    /// <summary>
    /// Two rows and two aisles where every span touches a neighbour: a group at each wall, two groups touching each other,
    /// and groups touching both edges of both aisles; the second row's group bridges the aisles.
    /// </summary>
    private const string TouchingLayout = """
        {
          "id": "tiny",
          "cabin_width_inches": 148,
          "rows": [
            { "pitch_inches": 30, "groups": [
              { "left_inches": 0, "seats": [
                { "label": "A", "width_inches": 18 }, { "label": "B", "width_inches": 18 }, { "label": "C", "width_inches": 18 } ] },
              { "left_inches": 74, "seats": [ { "label": "D", "width_inches": 16 } ] },
              { "left_inches": 90, "seats": [ { "label": "E", "width_inches": 16 } ] },
              { "left_inches": 126, "seats": [ { "label": "F", "width_inches": 22 } ] } ] },
            { "pitch_inches": 30, "groups": [
              { "left_inches": 74, "seats": [ { "label": "D", "width_inches": 16 }, { "label": "E", "width_inches": 16 } ] } ] }
          ],
          "aisles": [ { "center_inches": 64, "width_inches": 20 }, { "center_inches": 116, "width_inches": 20 } ],
          "fixtures": [
            { "id": "door-fwd", "kind": "Door", "row_index": 0, "aisle_index": 0, "distance_inches": 20 },
            { "id": "lav-fwd", "kind": "Lav", "row_index": 0, "aisle_index": 0, "distance_inches": 30 },
            { "id": "galley-aft", "kind": "Galley", "row_index": 1, "aisle_index": 1, "distance_inches": 40 }
          ]
        }
        """;

    /// <summary>
    /// Three 17.1-inch seats from each wall and a 20-inch aisle between them, every span touching the next; summed in floating
    /// point the left group ends at 51.300000000000004 and the right one past the wall, which must still count as touching.
    /// </summary>
    private const string FractionalLayout = """
        {
          "id": "tiny",
          "cabin_width_inches": 122.6,
          "rows": [
            { "pitch_inches": 30, "groups": [
              { "left_inches": 0, "seats": [
                { "label": "A", "width_inches": 17.1 }, { "label": "B", "width_inches": 17.1 }, { "label": "C", "width_inches": 17.1 } ] },
              { "left_inches": 71.3, "seats": [
                { "label": "D", "width_inches": 17.1 }, { "label": "E", "width_inches": 17.1 }, { "label": "F", "width_inches": 17.1 } ] } ] }
          ],
          "aisles": [ { "center_inches": 61.3, "width_inches": 20 } ],
          "fixtures": [
            { "id": "door-fwd", "kind": "Door", "row_index": 0, "aisle_index": 0, "distance_inches": 20 },
            { "id": "lav-fwd", "kind": "Lav", "row_index": 0, "aisle_index": 0, "distance_inches": 30 },
            { "id": "galley-fwd", "kind": "Galley", "row_index": 0, "aisle_index": 0, "distance_inches": 40 }
          ]
        }
        """;

    /// <summary>A layout whose id is empty, from a file named <c>.json</c>, is refused at its id.</summary>
    [Fact]
    public void EmptyIdIsRefused()
    {
        using var tree = ContentTree.Minimal();
        tree.Write("layouts/.json", ContentTree.Layout.Replace("\"id\": \"tiny\"", "\"id\": \"\"", StringComparison.Ordinal));

        ContentLoadException error = Refusal(tree);

        Assert.Equal("layouts/.json", error.File);
        Assert.Equal("$.id", error.JsonPath);
    }

    /// <summary>An aisle whose span reaches past the right wall is refused at the aisle.</summary>
    [Fact]
    public void AisleOutsideTheCabinIsRefused()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace(LayoutFile, "\"center_inches\": 74", "\"center_inches\": 140");

        ContentLoadException error = Refusal(tree);

        Assert.Equal(LayoutFile, error.File);
        Assert.Equal("$.aisles[0]", error.JsonPath);
    }

    /// <summary>A three-seat group that runs past the right wall is refused at its row and group.</summary>
    [Fact]
    public void SeatGroupPastTheWallIsRefused()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace(LayoutFile, "\"left_inches\": 2", "\"left_inches\": 100");

        ContentLoadException error = Refusal(tree);

        Assert.Equal(LayoutFile, error.File);
        Assert.Equal("$.rows[0].groups[0]", error.JsonPath);
    }

    /// <summary>A seat group that runs into the aisle is refused at its row and group.</summary>
    [Fact]
    public void SeatGroupOverlappingAnAisleIsRefused()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace(LayoutFile, "\"left_inches\": 2", "\"left_inches\": 20");

        ContentLoadException error = Refusal(tree);

        Assert.Equal(LayoutFile, error.File);
        Assert.Equal("$.rows[0].groups[0]", error.JsonPath);
    }

    /// <summary>A second group of the row that overlaps the first is refused at the second.</summary>
    [Fact]
    public void OverlappingSeatGroupsAreRefused()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace(LayoutFile, LastSeat, $$"""{{LastSeat}}, { "left_inches": 50, "seats": [ { "label": "D", "width_inches": 10 } ] }""");

        ContentLoadException error = Refusal(tree);

        Assert.Equal(LayoutFile, error.File);
        Assert.Equal("$.rows[0].groups[1]", error.JsonPath);
    }

    /// <summary>Spans that touch a wall, an aisle or another group at an edge, without overlapping, are accepted.</summary>
    [Fact]
    public void TouchingSpansAreAccepted()
    {
        using var tree = ContentTree.Minimal();
        tree.Write(LayoutFile, TouchingLayout);

        new LayoutValidator().Validate(tree.Load());
    }

    /// <summary>Spans that touch an aisle and the right wall are accepted when floating-point noise puts them a hair past it.</summary>
    [Fact]
    public void TouchingSpansWithFractionalWidthsAreAccepted()
    {
        using var tree = ContentTree.Minimal();
        tree.Write(LayoutFile, FractionalLayout);

        new LayoutValidator().Validate(tree.Load());
    }

    /// <summary>A zero or negative cabin width, seat width, aisle width or row pitch is refused at the field.</summary>
    /// <param name="oldText">The field in the minimal layout, ending in its value after one space.</param>
    /// <param name="inches">The value written in its place.</param>
    /// <param name="jsonPath">The field's JSON path.</param>
    [Theory]
    [InlineData("\"cabin_width_inches\": 148", 0, "$.cabin_width_inches")]
    [InlineData("\"cabin_width_inches\": 148", -148, "$.cabin_width_inches")]
    [InlineData("\"label\": \"A\", \"width_inches\": 18", 0, "$.rows[0].groups[0].seats[0].width_inches")]
    [InlineData("\"label\": \"A\", \"width_inches\": 18", -5, "$.rows[0].groups[0].seats[0].width_inches")]
    [InlineData("\"width_inches\": 20", 0, "$.aisles[0].width_inches")]
    [InlineData("\"width_inches\": 20", -4, "$.aisles[0].width_inches")]
    [InlineData("\"pitch_inches\": 30", 0, "$.rows[0].pitch_inches")]
    [InlineData("\"pitch_inches\": 30", -30, "$.rows[0].pitch_inches")]
    public void NonPositiveWidthIsRefused(string oldText, int inches, string jsonPath)
    {
        using var tree = ContentTree.Minimal();
        string newText = string.Concat(oldText.AsSpan(0, oldText.LastIndexOf(' ') + 1), inches.ToString(CultureInfo.InvariantCulture));
        tree.Replace(LayoutFile, oldText, newText);

        ContentLoadException error = Refusal(tree);

        Assert.Equal(LayoutFile, error.File);
        Assert.Equal(jsonPath, error.JsonPath);
    }

    /// <summary>A fixture id used twice is refused at the second use.</summary>
    [Fact]
    public void DuplicateFixtureIdIsRefused()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace(LayoutFile, "\"id\": \"lav-fwd\"", "\"id\": \"door-fwd\"");

        ContentLoadException error = Refusal(tree);

        Assert.Equal(LayoutFile, error.File);
        Assert.Equal("$.fixtures[1].id", error.JsonPath);
    }

    /// <summary>A layout with no fixture of one of the three kinds is refused at its fixtures, naming the kind.</summary>
    /// <param name="missing">The kind the layout lacks.</param>
    [Theory]
    [InlineData(FixtureKind.Door)]
    [InlineData(FixtureKind.Lav)]
    [InlineData(FixtureKind.Galley)]
    public void LayoutWithoutALavIsRefused(FixtureKind missing)
    {
        using var tree = ContentTree.Minimal();
        FixtureKind stand = missing == FixtureKind.Door ? FixtureKind.Lav : FixtureKind.Door;
        tree.Replace(LayoutFile, $"\"kind\": \"{missing}\"", $"\"kind\": \"{stand}\"");

        ContentLoadException error = Refusal(tree);

        Assert.Equal(LayoutFile, error.File);
        Assert.Equal("$.fixtures", error.JsonPath);
        Assert.Contains(missing.ToString(), error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A one-seat group between the three-seat group and the aisle cuts the three seats off: the builder links a group's end
    /// seat to an aisle only with no other group between them, and the three-seat group has no aisle on its wall side. The
    /// refusal names the first seat that cannot reach a fixture.
    /// </summary>
    [Fact]
    public void UnreachableSeatIsRefused()
    {
        using var tree = ContentTree.Minimal();
        tree.Replace(LayoutFile, LastSeat, $$"""{{LastSeat}}, { "left_inches": 56, "seats": [ { "label": "D", "width_inches": 8 } ] }""");

        ContentLoadException error = Refusal(tree);

        Assert.Equal(LayoutFile, error.File);
        Assert.Equal("$.rows[0].groups[0].seats[0]", error.JsonPath);
    }

    private static ContentLoadException Refusal(ContentTree tree)
    {
        ContentSet content = tree.Load();
        return Assert.Throws<ContentLoadException>(() => new LayoutValidator().Validate(content));
    }
}
