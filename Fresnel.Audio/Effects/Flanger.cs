// DSP implementation adapted from OpenAL Soft 1.25.2.
// See THIRD-PARTY-NOTICES.md for attribution and license terms.

namespace Fresnel.Audio;

/// <summary>
/// Configures a flanger produced by a short modulated delay line.
/// </summary>
public sealed record FlangerEffect : AudioEffect
{
    /// <summary>
    /// Gets the modulation rate in hertz.
    /// </summary>
    /// <value>
    /// A value from 0 to 10.
    /// </value>
    public float RateHz { get; init; } = 0.27f;

    /// <summary>
    /// Gets the perceptual modulation depth.
    /// </summary>
    /// <value>
    /// A value from 0 to 1.
    /// </value>
    public float Depth { get; init; } = 1f;

    /// <summary>
    /// Gets the delayed-signal feedback amount.
    /// </summary>
    /// <value>
    /// A value from -1 to 1.
    /// </value>
    public float Feedback { get; init; } = -0.5f;

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
        EffectValidation.Range(Feedback, -1f, 1f, nameof(Feedback));
        EffectValidation.Range(Mix, 0f, 1f, nameof(Mix));
    }

    internal override EffectProcessor CreateProcessor(int sampleRate, int channels)
    {
        return new FlangerProcessor(this, sampleRate, channels);
    }
}

internal sealed class FlangerProcessor : EffectProcessor
{
    private readonly ModulatedDelay _delay;

    public FlangerProcessor(FlangerEffect effect, int sampleRate, int channels) : base(sampleRate, channels)
    {
        _delay = new ModulatedDelay(
            OscillatorWaveform.Triangle,
            0f,
            effect.RateHz,
            effect.Depth,
            effect.Feedback,
            TimeSpan.FromMilliseconds(2),
            0.004f,
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
