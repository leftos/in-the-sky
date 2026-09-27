using System.Buffers.Binary;
using System.Text;

namespace Sky.Engine.Randomness;

/// <summary>The root of a run's randomness: one seed from which every named stream derives its own generator.</summary>
/// <param name="seed">The run's root seed.</param>
public sealed class RngRoot(ulong seed)
{
    private const ulong FnvOffsetBasis = 0xCBF29CE484222325UL;
    private const ulong FnvPrime = 0x100000001B3UL;

    /// <summary>Gets the run's root seed.</summary>
    public ulong Seed { get; } = seed;

    /// <summary>
    /// Creates a new generator for the stream <paramref name="name"/>, seeded with the FNV-1a-64 hash of the root seed's eight
    /// little-endian bytes followed by the name's UTF-8 bytes, so no other seed and name pair shares its sequence by construction.
    /// </summary>
    /// <param name="name">The stream name, such as <c>passenger/0</c>; each call with one name starts the same sequence afresh.</param>
    /// <returns>A generator independent of every other stream's.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or empty.</exception>
    public SimRandom Stream(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        byte[] key = new byte[sizeof(ulong) + Encoding.UTF8.GetByteCount(name)];
        BinaryPrimitives.WriteUInt64LittleEndian(key, Seed);
        Encoding.UTF8.GetBytes(name, 0, name.Length, key, sizeof(ulong));
        return new SimRandom(Fnv1a64(key));
    }

    /// <summary>Hashes <paramref name="bytes"/> with 64-bit FNV-1a.</summary>
    internal static ulong Fnv1a64(ReadOnlySpan<byte> bytes)
    {
        ulong hash = FnvOffsetBasis;
        foreach (byte value in bytes)
        {
            hash ^= value;
            hash *= FnvPrime;
        }

        return hash;
    }
}
