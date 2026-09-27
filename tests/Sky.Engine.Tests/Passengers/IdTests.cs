using Sky.Engine.Passengers;

namespace Sky.Engine.Tests.Passengers;

/// <summary>Proves the interned content ids refuse a negative value at construction.</summary>
public sealed class IdTests
{
    /// <summary>An activity id below 0 is refused.</summary>
    [Fact]
    public void NegativeActivityIdIsRefused() => Assert.Throws<ArgumentOutOfRangeException>(() => new ActivityId(-1));

    /// <summary>A trait id below 0 is refused.</summary>
    [Fact]
    public void NegativeTraitIdIsRefused() => Assert.Throws<ArgumentOutOfRangeException>(() => new TraitId(-1));
}
