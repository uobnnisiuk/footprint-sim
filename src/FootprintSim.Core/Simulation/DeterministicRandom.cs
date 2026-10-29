using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace FootprintSim.Simulation;

// SplitMix64 is explicitly specified and stable across .NET versions. It is not cryptographic.
public sealed class StableRandom
{
    private ulong _state;

    public StableRandom(ulong seed)
    {
        _state = seed;
    }

    public StableRandom(int seed)
        : this(unchecked((ulong)(long)seed))
    {
    }

    public ulong NextUInt64()
    {
        _state += 0x9E3779B97F4A7C15UL;
        var value = _state;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }

    public double NextDouble()
    {
        return (NextUInt64() >> 11) * (1.0 / (1UL << 53));
    }

    public int NextInclusive(int minimum, int maximum)
    {
        if (minimum > maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(minimum));
        }

        if (minimum == maximum)
        {
            return minimum;
        }

        var range = (ulong)((long)maximum - minimum + 1L);
        var rejectionThreshold = unchecked(0UL - range) % range;
        ulong value;
        do
        {
            value = NextUInt64();
        }
        while (value < rejectionThreshold);

        return minimum + (int)(value % range);
    }
}

public static class StableSeed
{
    public static ulong Derive(ulong rootSeed, params string[] components)
    {
        ArgumentNullException.ThrowIfNull(components);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> seedBytes = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(seedBytes, rootSeed);
        hash.AppendData(seedBytes);
        Span<byte> lengthBytes = stackalloc byte[sizeof(int)];

        foreach (var component in components)
        {
            ArgumentNullException.ThrowIfNull(component);
            var bytes = Encoding.UTF8.GetBytes(component);
            BinaryPrimitives.WriteInt32LittleEndian(lengthBytes, bytes.Length);
            hash.AppendData(lengthBytes);
            hash.AppendData(bytes);
        }

        var digest = hash.GetHashAndReset();
        return BinaryPrimitives.ReadUInt64LittleEndian(digest);
    }
}
