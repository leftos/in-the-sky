using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Sky.Content.Schema;
using Sky.Engine.Cabin;
using Sky.Engine.Manifest;
using Sky.Engine.Needs;
using Sky.Engine.Passengers;

namespace Sky.Content;

/// <summary>
/// One loaded content tree: the Engine types built from it where the Engine has them, the schema records where it has not,
/// the interned ids, the activity modules' Lua text, and the hash of every file under the root.
/// </summary>
public sealed class ContentSet
{
    /// <summary>Gets the lowercase hex SHA-256 of every <c>.json</c> and <c>.lua</c> file under the content root, its path and its bytes.</summary>
    public required string Hash { get; init; }

    /// <summary>Gets the layouts, by id.</summary>
    public required IReadOnlyDictionary<string, CabinLayout> Layouts { get; init; }

    /// <summary>Gets the needs content, validated by the Engine.</summary>
    public required NeedsContent Needs { get; init; }

    /// <summary>Gets the manifest rules the generator draws by, validated by the Engine.</summary>
    public required ManifestRules Manifest { get; init; }

    /// <summary>Gets each profession's interned id, by content id.</summary>
    public required IReadOnlyDictionary<string, ProfessionId> ProfessionIds { get; init; }

    /// <summary>Gets the traits file; the trait at index i is <c>new TraitId(i)</c>.</summary>
    public required TraitsFile Traits { get; init; }

    /// <summary>Gets each trait's interned id, by content id.</summary>
    public required IReadOnlyDictionary<string, TraitId> TraitIds { get; init; }

    /// <summary>Gets the activities file; the activity at index i is <c>new ActivityId(i)</c>.</summary>
    public required ActivitiesFile Activities { get; init; }

    /// <summary>Gets each activity's interned id, by content id.</summary>
    public required IReadOnlyDictionary<string, ActivityId> ActivityIds { get; init; }

    /// <summary>Gets the activity modules in interning order: the module at index i belongs to <c>new ActivityId(i)</c>.</summary>
    public required IReadOnlyList<ActivityModuleSource> ActivityModules { get; init; }

    /// <summary>Gets the crew file.</summary>
    public required CrewFile Crew { get; init; }

    /// <summary>Gets the scenarios, by id.</summary>
    public required IReadOnlyDictionary<string, ScenarioFile> Scenarios { get; init; }

    /// <summary>Gets the thought catalogue.</summary>
    public required ThoughtsFile Thoughts { get; init; }
}

/// <summary>The needs file and the Engine objects built from it, each already validated by its Engine constructor.</summary>
/// <param name="File">The needs file as read.</param>
/// <param name="Rates">The per-tick need rates.</param>
/// <param name="Cascades">The cascade rules.</param>
/// <param name="Distress">The distress formula.</param>
/// <param name="Capacities">The nav graph node capacities.</param>
public sealed record NeedsContent(NeedsFile File, NeedRates Rates, Cascades Cascades, Distress Distress, NodeCapacities Capacities);

/// <summary>One activity's Lua module.</summary>
/// <param name="Id">The activity's content id.</param>
/// <param name="Source">The module's Lua source text.</param>
public sealed record ActivityModuleSource(string Id, string Source);

/// <summary>
/// The content hash: SHA-256 over every <c>.json</c> and <c>.lua</c> file under the root (the extension matched ordinal and
/// case-sensitive, so <c>Thumbs.db</c>, <c>.DS_Store</c> or an editor backup never moves it), sorted by its root-relative path
/// with <c>/</c> separators under ordinal comparison; each file feeds its path's UTF-8 bytes, one zero byte, its length as 8
/// bytes little-endian, then its bytes. The same files under another root, or enumerated in another order, give the same hash.
/// </summary>
internal static class ContentHash
{
    private const int BufferSize = 81_920;

    private static readonly byte[] Separator = [0];

    /// <summary>Hashes every file under <paramref name="root"/>.</summary>
    /// <param name="root">The content root, a full path.</param>
    /// <returns>The hash as 64 lowercase hex digits.</returns>
    /// <exception cref="ContentLoadException">A file cannot be read.</exception>
    public static string Compute(string root)
    {
        string[] paths = HashedPaths(root);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[BufferSize];
        foreach (string relative in paths)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(relative));
            hash.AppendData(Separator);
            AppendFile(hash, root, relative, buffer);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static string[] HashedPaths(string root)
    {
        try
        {
            return
            [
                .. Directory
                    .EnumerateFiles(root, "*", SearchOption.AllDirectories)
                    .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
                    .Where(IsContentFile)
                    .Order(StringComparer.Ordinal),
            ];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ContentLoadException(
                ".",
                null,
                $"Expected every directory under the content root to be readable; {exception.Message}",
                exception
            );
        }
    }

    private static bool IsContentFile(string relative) =>
        relative.EndsWith(".json", StringComparison.Ordinal) || relative.EndsWith(".lua", StringComparison.Ordinal);

    private static void AppendFile(IncrementalHash hash, string root, string relative, byte[] buffer)
    {
        try
        {
            using FileStream stream = File.OpenRead(Path.Combine(root, relative));
            byte[] length = new byte[sizeof(long)];
            BinaryPrimitives.WriteInt64LittleEndian(length, stream.Length);
            hash.AppendData(length);
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                hash.AppendData(buffer, 0, read);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ContentLoadException(relative, null, $"Expected a readable file to hash; {exception.Message}", exception);
        }
    }
}
