using SpecialPG.Core.Maps.Noise;
using Xunit;

namespace SpecialPG.Core.Tests;

public class SimplexNoiseSamplerTests
{
    [Fact]
    public void SameSeed_ProducesIdenticalSamples()
    {
        var a = new SimplexNoiseSampler(999);
        var b = new SimplexNoiseSampler(999);

        for (var i = 0; i < 10; i++)
        {
            var x = i * 1.414f;
            var y = i * -2.718f;
            Assert.Equal(a.Sample2D(x, y), b.Sample2D(x, y));
        }
    }

    [Fact]
    public void Output_IsRoughlyBounded()
    {
        var n = new SimplexNoiseSampler(42);
        for (var i = 0; i < 100; i++)
        {
            var v = n.Sample2D(i * 0.31f, i * 0.27f);
            Assert.InRange(v, -1.05f, 1.05f);
        }
    }
}
