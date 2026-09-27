using Sky.Engine.Cabin;

namespace Sky.Engine.Tests.Cabin;

/// <summary>Pins the one-line-per-row text dump of a cabin layout.</summary>
public sealed class LayoutAsciiTests
{
    /// <summary>The shared two-row cabin renders its seat groups around the aisle, then each row's fixtures in list order.</summary>
    [Fact]
    public void TwoRowLayoutRendersPinnedLiteral() =>
        Assert.Equal("00 AC|DF door-1L lav-fwd\n01 ABC|DEF galley-mid", LayoutAscii.Render(NavGraphBuilderTests.TwoRowLayout()));

    /// <summary>A 2-3-2 row interleaves its three groups with both aisles by lateral position.</summary>
    [Fact]
    public void RowWithTwoAislesRendersBothInOrder()
    {
        CabinLayout layout = new(
            "two-three-two",
            200,
            [
                new CabinRow(
                    32,
                    [
                        NavGraphBuilderTests.Group(4, 18, "A", "B"),
                        NavGraphBuilderTests.Group(73, 18, "C", "D", "E"),
                        NavGraphBuilderTests.Group(160, 18, "F", "G"),
                    ]
                ),
            ],
            [new Aisle(55, 20), new Aisle(142, 20)],
            []
        );

        Assert.Equal("00 AB|CDE|FG", LayoutAscii.Render(layout));
    }

    /// <summary>A fixture on a row the layout does not have fails as the graph builder does, with its id and the bad index.</summary>
    [Fact]
    public void FixtureOnMissingRowFails()
    {
        CabinLayout layout = NavGraphBuilderTests.TwoRowLayout() with { Fixtures = [new CabinFixture("galley-aft", FixtureKind.Galley, 2, 0, 20)] };

        ArgumentException error = Assert.Throws<ArgumentException>(() => LayoutAscii.Render(layout));

        Assert.Equal("layout", error.ParamName);
        Assert.Contains("galley-aft", error.Message, StringComparison.Ordinal);
        Assert.Contains("row 2", error.Message, StringComparison.Ordinal);
    }
}
