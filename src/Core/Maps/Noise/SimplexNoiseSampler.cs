namespace SpecialPG.Core.Maps.Noise;

/// <summary>
/// 2D simplex noise (Stefan Gustavson / Ashima Arts style) with a seeded permutation table.
/// Output is roughly in [-1, 1].
/// </summary>
public sealed class SimplexNoiseSampler : INoiseSampler
{
    private const float F2 = 0.3660254037844386f; // (sqrt(3)-1)/2
    private const float G2 = 0.21132486540518713f; // (3-sqrt(3))/6

    private static readonly int[,] Grad3 =
    {
        { 1, 1, 0 }, { -1, 1, 0 }, { 1, -1, 0 }, { -1, -1, 0 },
        { 1, 0, 1 }, { -1, 0, 1 }, { 1, 0, -1 }, { -1, 0, -1 },
        { 0, 1, 1 }, { 0, -1, 1 }, { 0, 1, -1 }, { 0, -1, -1 }
    };

    private readonly byte[] _perm = new byte[512];

    public SimplexNoiseSampler(int seed)
    {
        Span<byte> source = stackalloc byte[256];
        for (var i = 0; i < 256; i++)
            source[i] = (byte)i;

        for (var i = 255; i > 0; i--)
        {
            var j = (int)((uint)Mix32(seed, i) % (i + 1));
            (source[i], source[j]) = (source[j], source[i]);
        }

        for (var i = 0; i < 512; i++)
            _perm[i] = source[i & 255];
    }

    public float Sample2D(float x, float y)
    {
        var n0 = 0f;
        var n1 = 0f;
        var n2 = 0f;

        var s = (x + y) * F2;
        var i = FastFloor(x + s);
        var j = FastFloor(y + s);
        var t = (i + j) * G2;
        var x0 = x - (i - t);
        var y0 = y - (j - t);

        int i1;
        int j1;
        if (x0 > y0)
        {
            i1 = 1;
            j1 = 0;
        }
        else
        {
            i1 = 0;
            j1 = 1;
        }

        var x1 = x0 - i1 + G2;
        var y1 = y0 - j1 + G2;
        var x2 = x0 - 1 + 2 * G2;
        var y2 = y0 - 1 + 2 * G2;

        var ii = i & 255;
        var jj = j & 255;

        var t0 = 0.5f - x0 * x0 - y0 * y0;
        if (t0 > 0)
        {
            t0 *= t0;
            var gi0 = _perm[ii + _perm[jj]] % 12;
            n0 = t0 * t0 * Dot2(gi0, x0, y0);
        }

        var t1 = 0.5f - x1 * x1 - y1 * y1;
        if (t1 > 0)
        {
            t1 *= t1;
            var gi1 = _perm[ii + i1 + _perm[jj + j1]] % 12;
            n1 = t1 * t1 * Dot2(gi1, x1, y1);
        }

        var t2 = 0.5f - x2 * x2 - y2 * y2;
        if (t2 > 0)
        {
            t2 *= t2;
            var gi2 = _perm[ii + 1 + _perm[jj + 1]] % 12;
            n2 = t2 * t2 * Dot2(gi2, x2, y2);
        }

        return 70f * (n0 + n1 + n2);
    }

    private static float Dot2(int gi, float x, float y) =>
        Grad3[gi, 0] * x + Grad3[gi, 1] * y;

    private static int FastFloor(float x) => (int)Math.Floor(x);

    private static int Mix32(int seed, int i)
    {
        unchecked
        {
            var x = seed ^ (i * unchecked((int)0x9E3779B1));
            x ^= (int)((uint)x >> 16);
            x *= unchecked((int)0x7FEB352D);
            x ^= (int)((uint)x >> 15);
            return x;
        }
    }
}
