namespace FootprintSim.Simulation;

// SplitMix64 is small, explicitly specified, and independent of System.Random implementation changes.
internal sealed class DeterministicRandom
{
    private ulong _state;

    public DeterministicRandom(int seed)
    {
        _state = unchecked((ulong)(long)seed);
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
        return minimum + (int)(NextUInt64() % range);
    }

    private ulong NextUInt64()
    {
        _state += 0x9E3779B97F4A7C15UL;
        var value = _state;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}
