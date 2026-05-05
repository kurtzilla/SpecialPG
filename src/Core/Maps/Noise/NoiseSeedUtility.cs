namespace SpecialPG.Core.Maps.Noise;

/// <summary>
/// Stable seed derivation for noise layers (do not use <see cref="HashCode"/> here—its mixing is not
/// guaranteed identical across all runtimes / versions for use as procedural noise identity).
/// </summary>
public static class NoiseSeedUtility
{
    /// <summary>Derive an independent 32-bit seed for a noise channel from world seed and a small salt.</summary>
    public static int DeriveChannelSeed(int worldSeed, int channelSalt)
    {
        unchecked
        {
            var x = worldSeed ^ (channelSalt * unchecked((int)0x85EBCA6B));
            x ^= (int)((uint)x >> 16);
            x *= unchecked((int)0xC2B2AE35);
            x ^= (int)((uint)x >> 13);
            return x;
        }
    }
}
