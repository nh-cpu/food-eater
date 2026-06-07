using System;

public static partial class TerrainHeightData
{
    public static bool HasGround(float x, float z) => SampleGroundCoverage(x, z) >= 0.5f;

    public static bool TrySampleHeight(float x, float z, out float height)
    {
        height = SampleHeight(x, z);
        return HasGround(x, z);
    }

    public static float SampleHeight(float x, float z)
    {
        var sample = SampleGrid(x, z);

        var h00 = Heights[IndexOf(sample.X0, sample.Z0)];
        var h10 = Heights[IndexOf(sample.X1, sample.Z0)];
        var h01 = Heights[IndexOf(sample.X0, sample.Z1)];
        var h11 = Heights[IndexOf(sample.X1, sample.Z1)];

        var h0 = Lerp(h00, h10, sample.Tx);
        var h1 = Lerp(h01, h11, sample.Tx);
        return Lerp(h0, h1, sample.Tz);
    }

    public static float GridStepX => (MaxX - MinX) / (Resolution - 1);
    public static float GridStepZ => (MaxZ - MinZ) / (Resolution - 1);
    public static float CenterX => (MinX + MaxX) * 0.5f;
    public static float CenterZ => (MinZ + MaxZ) * 0.5f;

    public static byte[] CreateFullGroundMask()
    {
        var mask = new byte[Heights.Length];
        Array.Fill(mask, (byte)1);
        return mask;
    }

    private static int IndexOf(int x, int z) => z * Resolution + x;

    private static float SampleGroundCoverage(float x, float z)
    {
        var sample = SampleGrid(x, z);

        var m00 = GroundMask[IndexOf(sample.X0, sample.Z0)];
        var m10 = GroundMask[IndexOf(sample.X1, sample.Z0)];
        var m01 = GroundMask[IndexOf(sample.X0, sample.Z1)];
        var m11 = GroundMask[IndexOf(sample.X1, sample.Z1)];

        var m0 = Lerp(m00, m10, sample.Tx);
        var m1 = Lerp(m01, m11, sample.Tx);
        return Lerp(m0, m1, sample.Tz);
    }

    private static GridSample SampleGrid(float x, float z)
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

        return new GridSample(x0, z0, x1, z1, gridX - x0, gridZ - z0);
    }

    private static float Lerp(float from, float to, float t) => from + (to - from) * t;

    private readonly record struct GridSample(int X0, int Z0, int X1, int Z1, float Tx, float Tz);
}
