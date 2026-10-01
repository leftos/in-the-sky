using Sky.Content.Schema;
using Sky.Content.Validation;
using Sky.Engine.Cabin;
using Sky.Engine.Manifest;
using Sky.Engine.Randomness;

namespace Sky.Content.Tests;

/// <summary>
/// Pins the shipped content under <c>src/Sky.Content/Data</c> against the docs it was written from: the hybrid tree loads
/// and validates (R10), the reference layout is OD2's 180 seats in two classes, its <see cref="LayoutAscii"/> dump matches
/// the pinned file, the shipped crew carries the Floater trait's one free zone (<c>crew.md</c>), and the shipped manifest
/// rules book no more than the layout's seats over seeds 1 to 20 (<c>passengers.md</c> section 5).
/// </summary>
public sealed class ShippedContentTests
{
    private const string LayoutId = "reference-narrowbody";
    private const string PinnedPath = "Pinned/reference-narrowbody.txt";
    private const int SeatCount = 180;
    private const int BusinessSeatCount = 12;
    private const int EconomySeatCount = 168;
    private const int Seeds = 20;

    /// <summary>The walking pace the test builds the nav graph with; the manifest draw never reads it (balance.md 3.7).</summary>
    private const double InchesPerTick = 7.87;

    /// <summary>The shipped content root beside the test assembly (R9).</summary>
    private static readonly string ShippedData = Path.Combine(AppContext.BaseDirectory, "Data");

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
        /// Fills the file kinds that ship after X1 — crew and scenarios (X2) and activities with their Lua modules (X3) —
        /// from <see cref="ContentTree.MinimalFiles"/>, so the shipped tree loads today and each fill drops by shipping,
        /// with no edit here. The rule is by presence: a fixture file is written only when the shipped tree has no file at
        /// that path, and the crew, the fixture scenario and the fixture layout that scenario names are written only when
        /// the shipped files hold no scenario (X2 ships all three together), since the fill's own writes must not decide
        /// what is still missing. <paramref name="shippedHasScenario"/> is read from the copied shipped files once, before
        /// the first fill.
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

        /// <summary>Whether the file belongs to the batch X2 ships: the crew, the scenarios and the layouts they name.</summary>
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
