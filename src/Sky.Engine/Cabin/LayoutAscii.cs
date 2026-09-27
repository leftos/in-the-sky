using System.Globalization;
using System.Text;

namespace Sky.Engine.Cabin;

/// <summary>
/// Renders a cabin layout as text, one line per row: the two-digit row index, a space, the row's seat groups and the cabin's
/// aisles left to right (a group as its seat labels run together, an aisle as <c>|</c>), then a space and the id of each fixture
/// in that row, in layout order. Lines are joined by LF with no trailing newline.
/// </summary>
public static class LayoutAscii
{
    private const string AisleMark = "|";

    /// <summary>Renders a layout, one line per row.</summary>
    /// <param name="layout">The layout.</param>
    /// <returns>The rows' lines, LF-joined.</returns>
    /// <exception cref="ArgumentException">A fixture names a row or an aisle the layout does not have.</exception>
    public static string Render(CabinLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        NavGraphBuilder.ValidateFixtures(layout);
        string[] lines = new string[layout.Rows.Count];
        for (int row = 0; row < lines.Length; row++)
        {
            lines[row] = RenderRow(layout, row);
        }

        return string.Join('\n', lines);
    }

    private static string RenderRow(CabinLayout layout, int rowIndex)
    {
        StringBuilder line = new StringBuilder(rowIndex.ToString("D2", CultureInfo.InvariantCulture)).Append(' ');
        foreach (string part in LateralParts(layout.Rows[rowIndex], layout.Aisles))
        {
            line.Append(part);
        }

        foreach (CabinFixture fixture in layout.Fixtures)
        {
            if (fixture.RowIndex == rowIndex)
            {
                line.Append(' ').Append(fixture.Id);
            }
        }

        return line.ToString();
    }

    private static IEnumerable<string> LateralParts(CabinRow row, IReadOnlyList<Aisle> aisles) =>
        row
            .Groups.Select(group => (Position: group.LeftInches, Text: string.Concat(group.Seats.Select(seat => seat.Label))))
            .Concat(aisles.Select(aisle => (Position: aisle.CenterInches, Text: AisleMark)))
            .OrderBy(part => part.Position)
            .Select(part => part.Text);
}
