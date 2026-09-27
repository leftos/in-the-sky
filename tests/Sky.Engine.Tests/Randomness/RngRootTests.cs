using CsCheck;
using Sky.Engine.Randomness;

namespace Sky.Engine.Tests.Randomness;

/// <summary>Pins the RNG root, its named streams and the generators behind them to reference outputs and to each other.</summary>
public sealed class RngRootTests
{
    private const int DrawsPerSeed = 100;

    /// <summary>SplitMix64 reproduces the reference outputs for seed 1477776061723855037.</summary>
    /// <remarks>
    /// Vectors from the rand_xoshiro crate, produced with the reference splitmix64.c (https://prng.di.unimi.it/splitmix64.c):
    /// https://github.com/rust-random/rngs/blob/master/rand_xoshiro/src/splitmix64.rs (test <c>reference</c>).
    /// </remarks>
    [Fact]
    public void SplitMix64MatchesReferenceVector()
    {
        ulong[] expected = [1985237415132408290UL, 2979275885539914483UL, 13511426838097143398UL, 8488337342461049707UL, 15141737807933549159UL];
        ulong state = 1477776061723855037UL;

        ulong[] actual = [.. expected.Select(_ => SimRandom.SplitMix64(ref state))];

        Assert.Equal(expected, actual);
    }

    /// <summary>xoshiro256** reproduces the reference outputs from the state {1, 2, 3, 4}.</summary>
    /// <remarks>
    /// Vectors from the rand_xoshiro crate, produced with the reference xoshiro256starstar.c (https://prng.di.unimi.it/xoshiro256starstar.c):
    /// https://github.com/rust-random/rngs/blob/master/rand_xoshiro/src/xoshiro256starstar.rs (test <c>reference</c>).
    /// </remarks>
    [Fact]
    public void XoshiroMatchesReferenceVector()
    {
        ulong[] expected =
        [
            11520UL,
            0UL,
            1509978240UL,
            1215971899390074240UL,
            1216172134540287360UL,
            607988272756665600UL,
            16172922978634559625UL,
            8476171486693032832UL,
            10595114339597558777UL,
            2904607092377533576UL,
        ];
        var random = new SimRandom(1, 2, 3, 4);

        ulong[] actual = [.. expected.Select(_ => random.NextUInt64())];

        Assert.Equal(expected, actual);
    }

    /// <summary>FNV-1a-64 gives the published offset basis for no input and the published hash of "a".</summary>
    /// <remarks>Vectors from draft-eastlake-fnv-21, Appendix C, Table 3: https://www.ietf.org/archive/id/draft-eastlake-fnv-21.txt.</remarks>
    [Fact]
    public void Fnv1a64MatchesReferenceVector()
    {
        Assert.Equal(0xcbf29ce484222325UL, RngRoot.Fnv1a64([]));
        Assert.Equal(0xaf63dc4c8601ec8cUL, RngRoot.Fnv1a64("a"u8));
    }

    /// <summary>The first five draws of stream <c>passenger/0</c> under seed 1 never change.</summary>
    [Fact]
    public void FirstFiveDrawsOfPassengerZeroUnderSeedOneArePinned()
    {
        ulong[] expected = [10143435324271051406UL, 11112465200013441795UL, 9098188495144178674UL, 17596748601454043121UL, 14493206531987128365UL];
        SimRandom random = new RngRoot(1).Stream("passenger/0");

        ulong[] actual = [.. expected.Select(_ => random.NextUInt64())];

        Assert.Equal(expected, actual);
    }

    /// <summary>Drawing from <c>crew/0</c> leaves the sequence of <c>passenger/0</c> untouched.</summary>
    [Fact]
    public void DrawingFromCrewZeroDoesNotShiftPassengerZero()
    {
        var root = new RngRoot(1);
        ulong[] untouched = Draw(root.Stream("passenger/0"), 5);

        SimRandom passenger = root.Stream("passenger/0");
        SimRandom crew = root.Stream("crew/0");
        Draw(crew, 7);

        Assert.Equal(untouched, Draw(passenger, 5));
    }

    /// <summary>Two roots built from one seed hand out streams with one sequence.</summary>
    [Fact]
    public void TwoRootsWithOneSeedAgree()
    {
        var first = new RngRoot(42);
        var second = new RngRoot(42);

        Assert.Equal(first.Seed, second.Seed);
        Assert.Equal(Draw(first.Stream("manifest"), 10), Draw(second.Stream("manifest"), 10));
    }

    /// <summary>Every <see cref="SimRandom.NextInt"/> draw lies in [0, maxExclusive), whatever the seed and bound.</summary>
    [Fact]
    public void NextIntStaysInRange()
    {
        Gen.Select(Gen.ULong, Gen.Int[1, int.MaxValue])
            .Sample(
                (seed, maxExclusive) =>
                {
                    SimRandom random = new RngRoot(seed).Stream("events");
                    for (int draw = 0; draw < DrawsPerSeed; draw++)
                    {
                        Assert.InRange(random.NextInt(maxExclusive), 0, maxExclusive - 1);
                    }
                }
            );
    }

    /// <summary>Every <see cref="SimRandom.NextDouble"/> draw lies in [0, 1), whatever the seed.</summary>
    [Fact]
    public void NextDoubleStaysInUnitInterval()
    {
        Gen.ULong.Sample(seed =>
        {
            SimRandom random = new RngRoot(seed).Stream("feed");
            for (int draw = 0; draw < DrawsPerSeed; draw++)
            {
                double value = random.NextDouble();
                Assert.True(value is >= 0.0 and < 1.0, $"NextDouble gave {value} under seed {seed}");
            }
        });
    }

    /// <summary>
    /// A root seed and a stream name cannot trade bits: seed 1's <c>passenger/0</c> is not seed 1099511628274's <c>passenger/1</c>.
    /// </summary>
    /// <remarks>The pair collides when the stream seed is the root seed XOR the name's hash, as the first derivation did.</remarks>
    [Fact]
    public void SeedAndNameAreNotInterchangeable()
    {
        ulong[] first = Draw(new RngRoot(1).Stream("passenger/0"), 5);
        ulong[] second = Draw(new RngRoot(1099511628274).Stream("passenger/1"), 5);

        Assert.NotEqual(first, second);
    }

    /// <summary>A null or empty stream name is rejected.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void StreamRejectsNullOrEmptyName(string? name)
    {
        var root = new RngRoot(1);

        Assert.ThrowsAny<ArgumentException>(() => root.Stream(name!));
    }

    /// <summary>A zero or negative bound for <see cref="SimRandom.NextInt"/> is rejected.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NextIntRejectsNonPositiveBound(int maxExclusive)
    {
        SimRandom random = new RngRoot(1).Stream("events");

        Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(maxExclusive));
    }

    /// <summary><see cref="SimRandom.Chance"/> with probability 0 never succeeds and with probability 1 always does, whatever the seed.</summary>
    [Fact]
    public void ChanceZeroNeverAndOneAlways()
    {
        Gen.ULong.Sample(seed =>
        {
            SimRandom random = new RngRoot(seed).Stream("events");
            for (int draw = 0; draw < DrawsPerSeed; draw++)
            {
                Assert.False(random.Chance(0.0), $"Chance(0) succeeded under seed {seed}");
                Assert.True(random.Chance(1.0), $"Chance(1) failed under seed {seed}");
            }
        });
    }

    /// <summary>Each <see cref="SimRandom.Chance"/> call consumes exactly one draw, as a twin stream drawing directly shows.</summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void ChanceConsumesOneDrawPerCall(double p)
    {
        var root = new RngRoot(1);
        SimRandom chance = root.Stream("events");
        SimRandom twin = root.Stream("events");

        chance.Chance(p);
        twin.NextUInt64();

        Assert.Equal(twin.NextUInt64(), chance.NextUInt64());
    }

    /// <summary>A NaN probability is rejected rather than read as a probability of 0.</summary>
    [Fact]
    public void ChanceRejectsNaN()
    {
        SimRandom random = new RngRoot(1).Stream("events");

        Assert.Throws<ArgumentOutOfRangeException>(() => random.Chance(double.NaN));
    }

    private static ulong[] Draw(SimRandom random, int count) => [.. Enumerable.Range(0, count).Select(_ => random.NextUInt64())];
}
