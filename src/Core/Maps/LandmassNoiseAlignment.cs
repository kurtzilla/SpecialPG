namespace SpecialPG.Core.Maps;

/// <summary>
/// Pans procedural noise sampling so the largest landmass centroid sits near global (0,0).
/// </summary>
public static class LandmassNoiseAlignment
{
    /// <summary>One elevation probe at a map cell center (subsample grid).</summary>
    public readonly record struct ElevationSample(int Gx, int Gy, float Elevation);

    /// <summary>
    /// Offset to add to global tile coordinates before noise sampling:
    /// <c>eval.ToTileCell(gx + dx + 0.5f, gy + dy + 0.5f, …)</c>.
    /// </summary>
    public static (int Dx, int Dy) ComputeOffsetToPlaceLccAtOrigin(
        IReadOnlyList<ElevationSample> samples,
        int minX,
        int minY,
        int stepX,
        int stepY,
        int width,
        int height,
        float waterElevationThreshold)
    {
        if (samples.Count == 0)
            return (0, 0);

        stepX = Math.Max(1, stepX);
        stepY = Math.Max(1, stepY);
        var coarseW = Math.Max(1, (width + stepX - 1) / stepX);
        var coarseH = Math.Max(1, (height + stepY - 1) / stepY);
        var land = new bool[coarseW, coarseH];

        for (var i = 0; i < samples.Count; i++)
        {
            var s = samples[i];
            var cx = (s.Gx - minX) / stepX;
            var cy = (s.Gy - minY) / stepY;
            if ((uint)cx >= (uint)coarseW || (uint)cy >= (uint)coarseH)
                continue;

            land[cx, cy] = s.Elevation >= waterElevationThreshold;
        }

        if (!TryGetLargestLandCentroid(land, minX, minY, stepX, stepY, out var centroidGx, out var centroidGy))
            return (0, 0);

        return (-(int)Math.Round(centroidGx), -(int)Math.Round(centroidGy));
    }

    private static bool TryGetLargestLandCentroid(
        bool[,] land,
        int minX,
        int minY,
        int stepX,
        int stepY,
        out double centroidGx,
        out double centroidGy)
    {
        centroidGx = centroidGy = 0;
        var w = land.GetLength(0);
        var h = land.GetLength(1);
        var comp = new int[w, h];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
                comp[x, y] = -1;
        }

        var compSizes = new List<int>();
        var bestId = -1;
        var bestSize = 0;

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                if (!land[x, y] || comp[x, y] != -1)
                    continue;

                var id = compSizes.Count;
                var size = 0;
                var q = new Queue<(int X, int Y)>();
                q.Enqueue((x, y));
                comp[x, y] = id;

                while (q.Count > 0)
                {
                    var (cx, cy) = q.Dequeue();
                    size++;
                    TryEnqueue(cx - 1, cy);
                    TryEnqueue(cx + 1, cy);
                    TryEnqueue(cx, cy - 1);
                    TryEnqueue(cx, cy + 1);

                    void TryEnqueue(int nx, int ny)
                    {
                        if ((uint)nx >= (uint)w || (uint)ny >= (uint)h)
                            return;
                        if (!land[nx, ny] || comp[nx, ny] != -1)
                            return;
                        comp[nx, ny] = id;
                        q.Enqueue((nx, ny));
                    }
                }

                compSizes.Add(size);
                if (size > bestSize)
                {
                    bestSize = size;
                    bestId = id;
                }
            }
        }

        if (bestId < 0)
            return false;

        double sumGx = 0;
        double sumGy = 0;
        var count = 0;
        for (var cy = 0; cy < h; cy++)
        {
            for (var cx = 0; cx < w; cx++)
            {
                if (comp[cx, cy] != bestId)
                    continue;

                sumGx += minX + cx * stepX + stepX * 0.5;
                sumGy += minY + cy * stepY + stepY * 0.5;
                count++;
            }
        }

        if (count == 0)
            return false;

        centroidGx = sumGx / count;
        centroidGy = sumGy / count;
        return true;
    }
}
