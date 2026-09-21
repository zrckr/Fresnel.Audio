// DSP implementation adapted from OpenAL Soft 1.25.2.
// See THIRD-PARTY-NOTICES.md for attribution and license terms.

namespace Fresnel.Audio;

/// <summary>
/// Configures an oversampled waveshaping distortion effect.
/// </summary>
public sealed record DistortionEffect : AudioEffect
{
    /// <summary>
    /// Gets the amount of nonlinear waveshaping.
    /// </summary>
    /// <value>
    /// A value from 0 for clean to 1 for maximum drive.
    /// </value>
    public float Drive { get; init; } = 0.2f;

    /// <summary>
    /// Gets the spectral brightness of the distorted signal.
    /// </summary>
    /// <value>
    /// A value from 0 for dark to 1 for bright.
    /// </value>
    public float Tone { get; init; } = 0.5f;

    /// <summary>
    /// Gets the dry/wet balance.
    /// </summary>
    /// <value>
    /// A value from 0 for dry to 1 for wet.
    /// </value>
    public float Mix { get; init; } = 0.5f;

    internal override void Validate()
    {
        EffectValidation.Range(Drive, 0f, 1f, nameof(Drive));
        EffectValidation.Range(Tone, 0f, 1f, nameof(Tone));
        EffectValidation.Range(Mix, 0f, 1f, nameof(Mix));
    }

    internal override EffectProcessor CreateProcessor(int sampleRate, int channels)
    {
        return new DistortionProcessor(this, sampleRate, channels);
    }
}

internal sealed class DistortionProcessor : EffectProcessor
{
    private const float BandwidthOctaves = 0.746268656716f;

    private readonly float _edgeCoefficient;

    private readonly float _gain;

    private readonly DryWetMix _mix;

    private readonly BiquadFilter[] _lowPassFilters;

    private readonly BiquadFilter[] _bandPassFilters;

    public DistortionProcessor(DistortionEffect effect, int sampleRate, int channels) : base(sampleRate, channels)
    {
        var edge = Math.Min(MathF.Sin(MathF.PI * 0.5f * effect.Drive), 0.99f);
        _edgeCoefficient = 2f * edge / (1f - edge);
        _gain = 1f - (0.8f * effect.Drive);
        _mix = new DryWetMix(effect.Mix);
        _lowPassFilters = new BiquadFilter[channels];
        _bandPassFilters = new BiquadFilter[channels];

        // A logarithmic sweep makes equal movements of Tone sound evenly spaced.
        var toneOffset = (2f * effect.Tone) - 1f;
        var lowPassHz = 8_000f * MathF.Pow(3f, toneOffset);
        var centerHz = 3_600f * MathF.Pow(6.6666665f, toneOffset);
        for (var channel = 0; channel < channels; channel++)
        {
            _lowPassFilters[channel] = BiquadFilter.FromBandwidth(
                BiquadType.LowPass, lowPassHz, 1f, BandwidthOctaves, sampleRate * 4);
            _bandPassFilters[channel] = BiquadFilter.FromBandwidth(
                BiquadType.BandPass, centerHz, 1f, 1f / 0.67f, sampleRate * 4);
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
            var dry = pcm[index];
            var channel = index % _channels;
            var wet = 0f;
            for (var phase = 0; phase < 4; phase++)
            {
                var sample = phase == 0 ? dry * 4f : 0f;
                sample = _lowPassFilters[channel].Process(sample);
                sample = Shape(sample, _edgeCoefficient);
                sample = Shape(sample, _edgeCoefficient, invert: true);
                sample = Shape(sample, _edgeCoefficient);
                sample = _bandPassFilters[channel].Process(sample);
                if (phase == 0)
                {
                    wet = sample;
                }
            }

            pcm[index] = _mix.Blend(dry, wet * _gain);
        }
    }

    private static float Shape(float sample, float coefficient, bool invert = false)
    {
        var result = ((1f + coefficient) * sample) / (1f + (coefficient * MathF.Abs(sample)));
        return invert ? -result : result;
    }
}
