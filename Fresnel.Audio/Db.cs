namespace Fresnel.Audio;

public readonly record struct Db : IComparable<Db>
{
    public static readonly Db Zero = default;

    public static readonly Db Silence = new(float.NegativeInfinity);

    private readonly float _db;

    public Db(float db = 0f)
    {
        if (float.IsNaN(db))
        {
            throw new ArgumentOutOfRangeException(nameof(db), "dB must not be NaN.");
        }

        if (float.IsPositiveInfinity(db))
        {
            throw new ArgumentOutOfRangeException(nameof(db), "dB must not be +∞.");
        }

        _db = db;
    }

    public static Db FromLinear(float linear)
    {
        if (float.IsNaN(linear))
        {
            throw new ArgumentOutOfRangeException(nameof(linear), "Linear must not be NaN.");
        }

        return new Db(MathF.Log10(Math.Clamp(linear, 0f, 1f)) * 20f);
    }

    public float ToLinear()
    {
        return MathF.Pow(10f, _db / 20f);
    }

    public int CompareTo(Db other)
    {
        return _db.CompareTo(other._db);
    }

    public static bool operator <(Db a, Db b)
    {
        return a._db < b._db;
    }

    public static bool operator >(Db a, Db b)
    {
        return a._db > b._db;
    }

    public static bool operator <=(Db a, Db b)
    {
        return a._db <= b._db;
    }

    public static bool operator >=(Db a, Db b)
    {
        return a._db >= b._db;
    }

    public override string ToString()
    {
        return $"{_db} dB";
    }
}
