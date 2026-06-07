using System;

public readonly record struct ToppingShapeProfile(
    string Name,
    float HalfWidth,
    float HalfDepth,
    float MinY,
    float MaxY,
    float Mass,
    float Friction,
    float EdgeGrip
)
{
    public float Thickness => MaxY - MinY;
    public float CenterY => (MinY + MaxY) * 0.5f;
    public float SlideBoundary => MathF.Max(HalfWidth, HalfDepth) * EdgeGrip;
}

public static partial class ToppingShapeData
{
    public static ToppingShapeProfile Default => new(
        "Default",
        0.9f,
        0.9f,
        -0.1f,
        0.1f,
        1f,
        0.15f,
        2.5f
    );
}
