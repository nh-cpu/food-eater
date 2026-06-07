using System;

public static partial class TerrainHeightData
{
    public static float SampleHeight(float x, float z)
    {
        var normalizedX = (x - MinX) / (MaxX - MinX);
        var normalizedZ = (z - MinZ) / (MaxZ - MinZ);

        normalizedX = Math.Clamp(normalizedX, 0f, 1f);
        normalizedZ = Math.Clamp(normalizedZ, 0f, 1f);

        var gridX = normalizedX * (Resolution - 1);
        var gridZ = normalizedZ * (Resolution - 1);

        var x0 = Math.Clamp((int)MathF.Floor(gridX), 0, Resolution - 1);
        var z0 = Math.Clamp((int)MathF.Floor(gridZ), 0, Resolution - 1);
        var x1 = Math.Clamp(x0 + 1, 0, Resolution - 1);
        var z1 = Math.Clamp(z0 + 1, 0, Resolution - 1);

        var tx = gridX - x0;
        var tz = gridZ - z0;

        var h00 = Heights[IndexOf(x0, z0)];
        var h10 = Heights[IndexOf(x1, z0)];
        var h01 = Heights[IndexOf(x0, z1)];
        var h11 = Heights[IndexOf(x1, z1)];

        var h0 = Lerp(h00, h10, tx);
        var h1 = Lerp(h01, h11, tx);
        return Lerp(h0, h1, tz);
    }

    public static float GridStepX => (MaxX - MinX) / (Resolution - 1);
    public static float GridStepZ => (MaxZ - MinZ) / (Resolution - 1);
    public static float CenterX => (MinX + MaxX) * 0.5f;
    public static float CenterZ => (MinZ + MaxZ) * 0.5f;

    private static int IndexOf(int x, int z) => z * Resolution + x;

    private static float Lerp(float from, float to, float t) => from + (to - from) * t;
}
