namespace Sky.Engine.Cabin;

/// <summary>
/// A cabin measured in real inches: its rows front to back, its aisles left to right and the fixtures along them. A row's index
/// is its position in <see cref="Rows"/>, 0 at the front; lateral positions are inches from the left cabin wall.
/// </summary>
/// <param name="Id">The layout's identifier.</param>
/// <param name="CabinWidthInches">The cabin's width, wall to wall.</param>
/// <param name="Rows">The rows, front to back.</param>
/// <param name="Aisles">The aisles, left to right.</param>
/// <param name="Fixtures">The doors, lavs and galleys beside the aisles.</param>
public sealed record CabinLayout(
    string Id,
    double CabinWidthInches,
    IReadOnlyList<CabinRow> Rows,
    IReadOnlyList<Aisle> Aisles,
    IReadOnlyList<CabinFixture> Fixtures
);

/// <summary>An aisle running the length of the cabin.</summary>
/// <param name="CenterInches">The aisle's centre, in inches from the left cabin wall.</param>
/// <param name="WidthInches">The aisle's width.</param>
public sealed record Aisle(double CenterInches, double WidthInches);

/// <summary>One row of seats.</summary>
/// <param name="PitchInches">The distance from this row to the row behind it.</param>
/// <param name="Groups">The row's seat groups, left to right.</param>
public sealed record CabinRow(double PitchInches, IReadOnlyList<SeatGroup> Groups);

/// <summary>
/// Seats side by side with no aisle between them. A seat's centre is <see cref="LeftInches"/> plus the widths of the seats
/// before it plus half its own width.
/// </summary>
/// <param name="LeftInches">The group's left edge, in inches from the left cabin wall.</param>
/// <param name="Seats">The group's seats, left to right.</param>
public sealed record SeatGroup(double LeftInches, IReadOnlyList<SeatSpec> Seats);

/// <summary>One seat.</summary>
/// <param name="Label">The seat's letter, a label only.</param>
/// <param name="WidthInches">The seat's width.</param>
public sealed record SeatSpec(string Label, double WidthInches);

/// <summary>What a cabin fixture is.</summary>
public enum FixtureKind
{
    /// <summary>A cabin door.</summary>
    Door,

    /// <summary>A lavatory, with a queue spot in front of it.</summary>
    Lav,

    /// <summary>A galley.</summary>
    Galley,
}

/// <summary>A fixture beside the aisle slot of one row on one aisle.</summary>
/// <param name="Id">The fixture's identifier.</param>
/// <param name="Kind">What the fixture is.</param>
/// <param name="RowIndex">The row whose aisle slot the fixture sits beside.</param>
/// <param name="AisleIndex">The aisle the fixture sits beside.</param>
/// <param name="DistanceInches">The distance from that aisle slot to the fixture.</param>
public sealed record CabinFixture(string Id, FixtureKind Kind, int RowIndex, int AisleIndex, double DistanceInches);
