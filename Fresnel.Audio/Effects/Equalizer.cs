// DSP implementation adapted from OpenAL Soft 1.25.2.
// See THIRD-PARTY-NOTICES.md for attribution and license terms.

namespace Fresnel.Audio;

/// <summary>
/// Specifies the response shape of an equalizer band.
/// </summary>
public enum EqualizerShape
{
    /// <summary>
    /// Boosts or cuts frequencies around the center frequency.
    /// </summary>
    Bell,

    /// <summary>
    /// Boosts or cuts frequencies below the transition frequency.
    /// </summary>
    LowShelf,

    /// <summary>
    /// Boosts or cuts frequencies above the transition frequency.
    /// </summary>
    HighShelf
}

/// <summary>
/// Configures one parametric equalizer band.
/// </summary>
/// <remarks>
/// Chain multiple equalizer effects to create a multiband equalizer.
/// </remarks>
public sealed record EqualizerEffect : AudioEffect
{
    /// <summary>
    /// Gets the band's response shape.
    /// </summary>
    public EqualizerShape Shape { get; init; } = EqualizerShape.Bell;

    /// <summary>
    /// Gets the center or shelf transition frequency in hertz.
    /// </summary>
    /// <value>
    /// A value from 20 to 20,000.
    /// </value>
    public float FrequencyHz { get; init; } = 1_000f;

    /// <summary>
    /// Gets the amount of boost or cut.
    /// </summary>
    /// <value>
    /// A value from -18 to 18 dB.
    /// </value>
    public Db Gain { get; init; } = new(0f);

    /// <summary>
    /// Gets the resonance or bandwidth of the band.
    /// </summary>
    /// <value>
    /// A value from 0.1 for broad to 10 for narrow.
    /// </value>
    public float Q { get; init; } = 0.707f;

    internal override void Validate()
    {
        EffectValidation.EqualizerShape(Shape, nameof(Shape));
        EffectValidation.Range(FrequencyHz, 20f, 20_000f, nameof(FrequencyHz));
        EffectValidation.Range(Gain, -18f, 18f, nameof(Gain));
        EffectValidation.Range(Q, 0.1f, 10f, nameof(Q));
    }

    internal override EffectProcessor CreateProcessor(int sampleRate, int channels)
    {
        return new EqualizerProcessor(this, sampleRate, channels);
    }
}

internal sealed class EqualizerProcessor : EffectProcessor
{
    private readonly BiquadFilter[] _filters;

    public EqualizerProcessor(EqualizerEffect effect, int sampleRate, int channels) : base(sampleRate, channels)
    {
        _filters = new BiquadFilter[channels];
        var type = effect.Shape switch
        {
            EqualizerShape.Bell => BiquadType.Peaking,
            EqualizerShape.LowShelf => BiquadType.LowShelf,
            EqualizerShape.HighShelf => BiquadType.HighShelf,
            _ => throw new ArgumentException(nameof(effect.Shape))
        };

        var gain = MathF.Sqrt(effect.Gain.ToLinear());
        for (var channel = 0; channel < channels; channel++)
        {
            _filters[channel] = BiquadFilter.FromQ(type, effect.FrequencyHz, gain, effect.Q, sampleRate);
        }
    }

    public override void Process(Span<float> pcm)
    {
        if (pcm.Length % _channels != 0)
        {
            throw new ArgumentException("PCM data must contain complete sample frames.", nameof(pcm));
        }

        for (var index = 0; index < pcm.Length; index++)
        {
            pcm[index] = _filters[index % _channels].Process(pcm[index]);
        }
    }
}
