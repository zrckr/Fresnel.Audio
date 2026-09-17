using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fresnel.Audio;

[JsonConverter(typeof(DbJsonConverter))]
public readonly record struct Db : IComparable<Db>
{
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

        return new Db(MathF.Log10(linear) * 20f);
    }

    public static implicit operator Db(float value)
    {
        return new Db(value);
    }

    public static implicit operator float(Db db)
    {
        return db._db;
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

public sealed class DbJsonConverter : JsonConverter<Db>
{
    public override bool HandleNull => true;

    public override Db Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Null => Db.Silence,
            JsonTokenType.Number => new Db(reader.GetSingle()),
            _ => throw new JsonException("A dB value must be a JSON number or null for silence.")
        };
    }

    public override void Write(Utf8JsonWriter writer, Db value, JsonSerializerOptions options)
    {
        if (value == Db.Silence)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteNumberValue(value);
    }
}
