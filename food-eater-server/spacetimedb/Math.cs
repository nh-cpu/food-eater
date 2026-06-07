using System;

[SpacetimeDB.Type]
public partial struct DbVector3
{
    public float x;
    public float y;
    public float z;

    public DbVector3(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public float SqrMagnitude => x * x + y * y + z * z;
    public float Magnitude => MathF.Sqrt(SqrMagnitude);

    public DbVector3 SafeNormalized
    {
        get
        {
            var magnitude = Magnitude;
            return magnitude > 0.0001f ? this / magnitude : Zero;
        }
    }

    public static DbVector3 Zero => new(0f, 0f, 0f);

    public static float Distance(DbVector3 a, DbVector3 b) => (a - b).Magnitude;

    public static DbVector3 Lerp(DbVector3 from, DbVector3 to, float weight)
        => from + (to - from) * weight;

    public static DbVector3 operator +(DbVector3 a, DbVector3 b)
        => new(a.x + b.x, a.y + b.y, a.z + b.z);

    public static DbVector3 operator -(DbVector3 a, DbVector3 b)
        => new(a.x - b.x, a.y - b.y, a.z - b.z);

    public static DbVector3 operator *(DbVector3 a, float b)
        => new(a.x * b, a.y * b, a.z * b);

    public static DbVector3 operator /(DbVector3 a, float b)
        => new(a.x / b, a.y / b, a.z / b);
}
