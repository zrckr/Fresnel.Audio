using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fresnel.Audio;

/// <summary>
/// Represents an audio level in decibels.
/// </summary>
[JsonConverter(typeof(DbJsonConverter))]
public readonly record struct Db : IComparable<Db>
{
    /// <summary>
    /// The silent audio level, represented by negative infinity decibels.
    /// </summary>
    public static readonly Db Silence = new(float.NegativeInfinity);

    private readonly float _db;

    /// <summary>
    /// Creates an audio level from a decibel value.
    /// </summary>
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

    /// <summary>
    /// Creates an audio level from a linear gain multiplier.
    /// </summary>
    public static Db FromLinear(float linear)
    {
        if (float.IsNaN(linear))
        {
            throw new ArgumentOutOfRangeException(nameof(linear), "Linear must not be NaN.");
        }

        return new Db(MathF.Log10(linear) * 20f);
    }

    /// <summary>
    /// Converts a decibel value to an audio level.
    /// </summary>
    public static implicit operator Db(float value)
    {
        return new Db(value);
    }

    /// <summary>
    /// Converts an audio level to its decibel value.
    /// </summary>
    public static implicit operator float(Db db)
    {
        return db._db;
    }

    /// <summary>
    /// Converts this audio level to a linear gain multiplier.
    /// </summary>
    public float ToLinear()
    {
        return MathF.Pow(10f, _db / 20f);
    }

    /// <inheritdoc/>
    public int CompareTo(Db other)
    {
        return _db.CompareTo(other._db);
    }

    /// <summary>
    /// Determines whether one audio level is quieter than another.
    /// </summary>
    public static bool operator <(Db a, Db b)
    {
        return a._db < b._db;
    }

    /// <summary>
    /// Determines whether one audio level is louder than another.
    /// </summary>
    public static bool operator >(Db a, Db b)
    {
        return a._db > b._db;
    }

    /// <summary>
    /// Determines whether one audio level is quieter than or equal to another.
    /// </summary>
    public static bool operator <=(Db a, Db b)
    {
        return a._db <= b._db;
    }

    /// <summary>
    /// Determines whether one audio level is louder than or equal to another.
    /// </summary>
    public static bool operator >=(Db a, Db b)
    {
        return a._db >= b._db;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"{_db} dB";
    }
}

/// <summary>
/// Converts <see cref="Db"/> values to and from JSON numbers.
/// </summary>
/// <remarks>
/// <see cref="Db.Silence"/> is represented by a JSON <see langword="null"/> value.
/// </remarks>
public sealed class DbJsonConverter : JsonConverter<Db>
{
    /// <inheritdoc/>
    public override bool HandleNull => true;

    /// <inheritdoc/>
    public override Db Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Null => Db.Silence,
            JsonTokenType.Number => new Db(reader.GetSingle()),
            _ => throw new JsonException("A dB value must be a JSON number or null for silence.")
        };
    }

    /// <inheritdoc/>
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
