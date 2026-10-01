using Sky.Content.Schema;
using Sky.Content.Validation;
using Sky.Engine.Cabin;
using Sky.Engine.Manifest;
using Sky.Engine.Randomness;

namespace Sky.Content.Tests;

/// <summary>
/// Pins the shipped content under <c>src/Sky.Content/Data</c> against the docs it was written from: the hybrid tree loads
/// and validates (R10), the reference layout is OD2's 180 seats in two classes, its <see cref="LayoutAscii"/> dump matches
/// the pinned file, the shipped crew carries the Floater trait's one free zone (<c>crew.md</c>), the shipped manifest
/// rules book no more than the layout's seats over seeds 1 to 20 (<c>passengers.md</c> section 5), the seven shipped
/// scenarios load and validate, each variant moves exactly one lever of the reference (OD4) and keeps every crew member's
/// home zone, and the reference dims the cabin in <c>crew.md</c>'s two cruise windows.
/// </summary>
public sealed class ShippedContentTests
{
    private const string LayoutId = "reference-narrowbody";
    private const string PinnedPath = "Pinned/reference-narrowbody.txt";
    private const int SeatCount = 180;
    private const int BusinessSeatCount = 12;
    private const int EconomySeatCount = 168;
    private const int Seeds = 20;
    private const string ReferenceScenarioId = "reference";

    /// <summary>The walking pace the test builds the nav graph with; the manifest draw never reads it (balance.md 3.7).</summary>
    private const double InchesPerTick = 7.87;

    /// <summary>The shipped content root beside the test assembly (R9).</summary>
    private static readonly string ShippedData = Path.Combine(AppContext.BaseDirectory, "Data");

    /// <summary>The ids of the seven shipped scenarios: the reference and its six one-lever variants.</summary>
    private static readonly string[] ShippedScenarioIds =
    [
        ReferenceScenarioId,
        "service-back-to-front",
        "one-lav-locked",
        "lights-up",
        "four-crew",
        "covering",
        "gate-delay-closed",
    ];

    /// <summary>The lever groups a variant may move (OD4, plus the gate conditions), each with the test of sameness.</summary>
    private static readonly (string Name, Func<ScenarioFile, ScenarioFile, bool> Same)[] LeverGroups =
    [
        ("service plan", (a, b) => a.ServicePlan.SequenceEqual(b.ServicePlan)),
        ("locked lavs", (a, b) => a.LockedLavs.ToHashSet(StringComparer.Ordinal).SetEquals(b.LockedLavs)),
        ("lighting plan", (a, b) => a.LightingPlan.SequenceEqual(b.LightingPlan)),
        ("crew assignment", (a, b) => SameCrew(a.Crew, b.Crew)),
        ("gate conditions", (a, b) => a.GateDelayMinutes == b.GateDelayMinutes && a.ConcessionsOpen == b.ConcessionsOpen),
    ];

    /// <summary>The shipped files, with the kinds that ship later filled from the fixtures, load and validate.</summary>
    [Fact]
    public void ShippedContentLoadsAndValidates()
    {
        using var tree = HybridTree.Create();

        ContentSet content = tree.Load();
        ContentValidator.Validate(content);

        Assert.NotEmpty(content.Layouts);
        Assert.Contains(LayoutId, content.Layouts.Keys);
    }

    /// <summary>The reference layout is OD2's 180 seats: 12 business in the front rows, 168 economy behind them.</summary>
    [Fact]
    public void ReferenceLayoutHasOneHundredEightySeats()
    {
        using var tree = HybridTree.Create();
        ContentSet content = tree.Load();
        CabinLayout layout = content.Layouts[LayoutId];
        int businessRows = content.Manifest.BusinessRowCount;

        Assert.Equal(SeatCount, SeatsIn(layout.Rows));
        Assert.Equal(BusinessSeatCount, SeatsIn(layout.Rows.Take(businessRows)));
        Assert.Equal(EconomySeatCount, SeatsIn(layout.Rows.Skip(businessRows)));
    }

    /// <summary>The reference layout renders exactly the pinned dump of <see cref="LayoutAscii.Render"/>.</summary>
    [Fact]
    public void ReferenceLayoutMatchesItsPinnedDump()
    {
        using var tree = HybridTree.Create();
        CabinLayout layout = tree.Load().Layouts[LayoutId];

        Assert.Equal(PinnedText(), LayoutAscii.Render(layout));
    }

    /// <summary>Every shipped manifest books at least the load factor's floor and at most the layout's seats.</summary>
    [Fact]
    public void ShippedManifestDrawsOneHundredEightyOrFewer()
    {
        using var tree = HybridTree.Create();
        ContentSet content = tree.Load();
        CabinLayout layout = content.Layouts[LayoutId];
        NavGraph graph = NavGraphBuilder.Build(layout, InchesPerTick);
        int least = (int)Math.Floor(content.Manifest.LoadFactor.Min * SeatCount);

        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            PassengerManifest manifest = ManifestGenerator.Generate(layout, graph, content.Manifest, new RngRoot(seed));
            Assert.InRange(manifest.Passengers.Count, least, SeatCount);
        }
    }

    private static int SeatsIn(IEnumerable<CabinRow> rows) => rows.Sum(row => row.Groups.Sum(group => group.Seats.Count));

    /// <summary>The shipped crew's <c>floater</c> trait carries one zone free of covering strain (<c>crew.md</c> line 82).</summary>
    [Fact]
    public void ShippedFloaterTraitCarriesOneFreeZone()
    {
        using var tree = HybridTree.Create();

        CrewFile crew = tree.Load().Crew;

        Assert.Equal(1, crew.Traits.Single(trait => trait.Id == "floater").FreeZones);
    }

    /// <summary>The seven shipped scenarios load and validate, and they are the only scenarios in the tree.</summary>
    [Fact]
    public void EveryShippedScenarioLoadsAndValidates()
    {
        using var tree = HybridTree.Create();

        ContentSet content = tree.Load();
        ContentValidator.Validate(content);

        Assert.Equal(ShippedScenarioIds.Order(StringComparer.Ordinal), content.Scenarios.Keys.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// Each variant moves exactly its own lever group of the reference, and its layout, system switches and timeline are
    /// the reference's.
    /// </summary>
    /// <param name="variantId">The variant's scenario id.</param>
    /// <param name="lever">The one lever group the variant moves.</param>
    [Theory]
    [InlineData("service-back-to-front", "service plan")]
    [InlineData("one-lav-locked", "locked lavs")]
    [InlineData("lights-up", "lighting plan")]
    [InlineData("four-crew", "crew assignment")]
    [InlineData("covering", "crew assignment")]
    [InlineData("gate-delay-closed", "gate conditions")]
    public void EachVariantDiffersFromReferenceInExactlyOneLever(string variantId, string lever)
    {
        using var tree = HybridTree.Create();
        IReadOnlyDictionary<string, ScenarioFile> scenarios = tree.Load().Scenarios;
        ScenarioFile reference = scenarios[ReferenceScenarioId];
        ScenarioFile variant = scenarios[variantId];

        string[] moved = [.. LeverGroups.Where(group => !group.Same(reference, variant)).Select(group => group.Name)];

        Assert.Equal(new[] { lever }, moved);
        Assert.Equal(reference.Layout, variant.Layout);
        Assert.Equal(reference.Systems, variant.Systems);
        Assert.Equal(reference.Timeline.BoardingStartLocalMinutes, variant.Timeline.BoardingStartLocalMinutes);
        Assert.Equal(reference.Timeline.Entries, variant.Timeline.Entries);
    }

    /// <summary>
    /// Every variant that keeps the reference's crew keeps each member's home zone, the first zone listing them, so a
    /// variant moves its lever and not where a crew member sits, waits and is filed (<c>crew.md</c>, "The home zone").
    /// </summary>
    /// <param name="variantId">The variant's scenario id.</param>
    [Theory]
    [InlineData("service-back-to-front")]
    [InlineData("one-lav-locked")]
    [InlineData("lights-up")]
    [InlineData("covering")]
    [InlineData("gate-delay-closed")]
    public void EachVariantKeepsEveryMembersHomeZone(string variantId)
    {
        using var tree = HybridTree.Create();
        IReadOnlyDictionary<string, ScenarioFile> scenarios = tree.Load().Scenarios;

        Assert.Equal(HomeZones(scenarios[ReferenceScenarioId].Crew), HomeZones(scenarios[variantId].Crew));
    }

    /// <summary>
    /// The reference dims the cabin in two fixed windows counted from the seatbelt sign first going off after takeoff: 19 to
    /// 39 and 62 to 69 (<c>crew.md</c>, "The reference lighting plan").
    /// </summary>
    [Fact]
    public void ReferenceLightingPlanIsTheTwoCruiseWindows()
    {
        using var tree = HybridTree.Create();

        ScenarioFile reference = tree.Load().Scenarios[ReferenceScenarioId];

        DimmedPeriod[] expected = [new DimmedPeriod(19, 39), new DimmedPeriod(62, 69)];
        Assert.Equal(expected, reference.LightingPlan);
    }

    /// <summary>
    /// The crew's home zones, one <c>"member in zone"</c> line per crew member in ordinal member order: a member's home
    /// zone is the first zone that lists them (<c>crew.md</c>, "The home zone").
    /// </summary>
    private static string[] HomeZones(CrewAssignment crew) =>
        [
            .. crew
                .Zones.SelectMany(zone => zone.Crew.Select(member => (Member: member, Zone: zone.Id)))
                .DistinctBy(home => home.Member, StringComparer.Ordinal)
                .OrderBy(home => home.Member, StringComparer.Ordinal)
                .Select(home => $"{home.Member} in {home.Zone}"),
        ];

    /// <summary>
    /// Whether two crew levers set the same count, the same zones matched by id (each with its rows, stations and crew in
    /// order, lead first) and the same cart spans. Zone ids are unique within a scenario (the validator checks it).
    /// </summary>
    private static bool SameCrew(CrewAssignment a, CrewAssignment b) =>
        a.Count == b.Count
        && a.Zones.Count == b.Zones.Count
        && a.Zones.All(zone => b.Zones.Any(other => SameZone(zone, other)))
        && a.Carts.Count == b.Carts.Count
        && a.Carts.Zip(b.Carts).All(pair => SameCart(pair.First, pair.Second));

    private static bool SameZone(CrewZone a, CrewZone b) =>
        string.Equals(a.Id, b.Id, StringComparison.Ordinal)
        && a.FirstRow == b.FirstRow
        && a.LastRow == b.LastRow
        && a.Stations.SequenceEqual(b.Stations, StringComparer.Ordinal)
        && a.Crew.SequenceEqual(b.Crew, StringComparer.Ordinal);

    private static bool SameCart(CartSpan a, CartSpan b) =>
        a.FirstRow == b.FirstRow && a.LastRow == b.LastRow && a.Crew.SequenceEqual(b.Crew, StringComparer.Ordinal);

    /// <summary>Reads the pinned dump, its line endings normalised to LF and its closing newline dropped.</summary>
    /// <returns>The dump's text, as <see cref="LayoutAscii.Render"/> writes it.</returns>
    private static string PinnedText() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, PinnedPath)).Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');

    /// <summary>
    /// A content root built at test time: every file of the shipped <c>Data</c> directory, then the fixture files of the
    /// kinds that ship later. Disposing it deletes the directory.
    /// </summary>
    private sealed class HybridTree : IDisposable
    {
        private HybridTree(string root) => Root = root;

        /// <summary>Gets the hybrid content root.</summary>
        public string Root { get; }

        /// <summary>Builds the hybrid tree in a new temporary directory.</summary>
        /// <returns>The tree.</returns>
        public static HybridTree Create()
        {
            string root = Path.Combine(Path.GetTempPath(), "sky-content-tests", Guid.NewGuid().ToString("N"));
            CopyShipped(root);
            FillUnshippedKinds(root, HasScenario(root));
            return new HybridTree(root);
        }

        /// <summary>Loads the hybrid tree.</summary>
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

        /// <summary>Copies every file under the shipped <c>Data</c> directory, keeping each one's relative path.</summary>
        private static void CopyShipped(string root)
        {
            if (!Directory.Exists(ShippedData))
            {
                return;
            }

            foreach (string file in Directory.EnumerateFiles(ShippedData, "*", SearchOption.AllDirectories))
            {
                string destination = Path.Combine(root, Path.GetRelativePath(ShippedData, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination, overwrite: true);
            }
        }

        /// <summary>
        /// Fills the file kinds the shipped tree does not carry, the activities and their Lua modules, from
        /// <see cref="ContentTree.MinimalFiles"/>, so the shipped tree loads; a kind's fill drops once it ships, with no
        /// edit here. The rule is by presence: a fixture file is written only when the shipped tree has no file at that
        /// path, and the fixture crew, scenario and layout are written only when the shipped files hold no scenario.
        /// <paramref name="shippedHasScenario"/> is read from the copied shipped files once, before the first fill.
        /// </summary>
        /// <param name="root">The content root the shipped files were copied into.</param>
        /// <param name="shippedHasScenario">Whether the shipped files alone carry a scenario.</param>
        private static void FillUnshippedKinds(string root, bool shippedHasScenario)
        {
            foreach (KeyValuePair<string, string> file in ContentTree.MinimalFiles)
            {
                if (ShouldFill(file.Key, root, shippedHasScenario))
                {
                    string path = Path.Combine(root, file.Key);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllText(path, file.Value);
                }
            }
        }

        private static bool ShouldFill(string relative, string root, bool shippedHasScenario) =>
            !File.Exists(Path.Combine(root, relative)) && (!ShipsWithScenarios(relative) || !shippedHasScenario);

        /// <summary>Whether the file ships with the scenarios: the crew, the scenarios and the layouts they name.</summary>
        private static bool ShipsWithScenarios(string relative) =>
            relative is "crew.json"
            || relative.StartsWith("scenarios/", StringComparison.Ordinal)
            || relative.StartsWith("layouts/", StringComparison.Ordinal);

        /// <summary>Whether the content root holds a scenario the loader would read, <c>scenarios/*.json</c>.</summary>
        /// <param name="root">The content root.</param>
        /// <returns>Whether one scenario file is there.</returns>
        private static bool HasScenario(string root)
        {
            string directory = Path.Combine(root, "scenarios");
            return Directory.Exists(directory) && Directory.EnumerateFiles(directory, "*.json").Any();
        }
    }
}
