// DSP implementation adapted from OpenAL Soft 1.25.2.
// See THIRD-PARTY-NOTICES.md for attribution and license terms.

namespace Fresnel.Audio;

/// <summary>
/// Configures a chorus produced by a modulated delay line.
/// </summary>
public sealed record ChorusEffect : AudioEffect
{
    /// <summary>
    /// Gets the modulation rate in hertz.
    /// </summary>
    /// <value>
    /// A value from 0 to 10.
    /// </value>
    public float RateHz { get; init; } = 1.1f;

    /// <summary>
    /// Gets the perceptual modulation depth.
    /// </summary>
    /// <value>
    /// A value from 0 to 1.
    /// </value>
    public float Depth { get; init; } = 0.1f;

    /// <summary>
    /// Gets the stereo separation of the modulation.
    /// </summary>
    /// <value>
    /// A value from 0 for mono modulation to 1 for a 180-degree offset.
    /// </value>
    public float StereoWidth { get; init; } = 0.5f;

    /// <summary>
    /// Gets the dry/wet balance.
    /// </summary>
    /// <value>
    /// A value from 0 for dry to 1 for wet.
    /// </value>
    public float Mix { get; init; } = 0.5f;

    internal override void Validate()
    {
        EffectValidation.Range(RateHz, 0f, 10f, nameof(RateHz));
        EffectValidation.Range(Depth, 0f, 1f, nameof(Depth));
        EffectValidation.Range(StereoWidth, 0f, 1f, nameof(StereoWidth));
        EffectValidation.Range(Mix, 0f, 1f, nameof(Mix));
    }

    internal override EffectProcessor CreateProcessor(int sampleRate, int channels)
    {
        return new ChorusProcessor(this, sampleRate, channels);
    }
}

internal sealed class ChorusProcessor : EffectProcessor
{
    private readonly ModulatedDelay _delay;

    public ChorusProcessor(ChorusEffect effect, int sampleRate, int channels) : base(sampleRate, channels)
    {
        _delay = new ModulatedDelay(
            OscillatorWaveform.Triangle,
            effect.StereoWidth * 180f,
            effect.RateHz,
            effect.Depth,
            0.25f,
            TimeSpan.FromMilliseconds(16),
            0.016f,
            effect.Mix,
            sampleRate,
            channels
        );
    }

    public override void Process(Span<float> pcm)
    {
        if (pcm.Length % _channels != 0)
        {
            throw new ArgumentException("PCM data must contain complete sample frames.", nameof(pcm));
        }

        _delay.Process(pcm);
    }
}
