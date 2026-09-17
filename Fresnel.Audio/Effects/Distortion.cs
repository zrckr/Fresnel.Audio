// DSP implementation adapted from OpenAL Soft 1.25.2.
// Source: alc/effects/distortion.cpp.
// Upstream notice: Copyright (C) 2013 Mike Gorchak.
// Adapted portions are licensed under LGPL-2.0-or-later.
// See THIRD-PARTY-NOTICES.md for attribution and license terms.

namespace Fresnel.Audio;

/// <summary>
/// Configures an oversampled waveshaping distortion effect.
/// </summary>
public readonly record struct DistortionEffect() : IEffect
{
    /// <summary>
    /// Gets the linear output gain applied after distortion.
    /// </summary>
    /// <value>
    /// A value from 0.01 to 1.
    /// </value>
    public float Gain { get; init; } = 0.2f; // [0.01, 1]

    /// <summary>
    /// Gets the amount of nonlinear waveshaping.
    /// </summary>
    /// <value>
    /// A value from 0 for the softest curve to 1 for the strongest curve.
    /// </value>
    public float Edge { get; init; } = 0.2f; // [0, 1]

    /// <summary>
    /// Gets the low-pass cutoff applied before waveshaping, in hertz.
    /// </summary>
    /// <value>
    /// A value from 80 to 24,000.
    /// </value>
    public float LowcutHz { get; init; } = 8000f; // [80, 24000]

    /// <summary>
    /// Gets the center frequency of the post-distortion band-pass filter, in hertz.
    /// </summary>
    /// <value>
    /// A value from 80 to 24,000.
    /// </value>
    public float CenterHz { get; init; } = 3600f; // [80, 24000]

    /// <summary>
    /// Gets the bandwidth of the post-distortion band-pass filter, in hertz.
    /// </summary>
    /// <value>
    /// A value from 80 to 24,000.
    /// </value>
    public float BandwidthHz { get; init; } = 3600f; // [80, 24000]

    /// <inheritdoc/>
    public void Validate()
    {
        EffectValidation.Range(Gain, 0.01f, 1f, nameof(Gain));
        EffectValidation.Range(Edge, 0f, 1f, nameof(Edge));
        EffectValidation.Range(LowcutHz, 80f, 24_000f, nameof(LowcutHz));
        EffectValidation.Range(CenterHz, 80f, 24_000f, nameof(CenterHz));
        EffectValidation.Range(BandwidthHz, 80f, 24_000f, nameof(BandwidthHz));
    }
}

internal sealed class DistortionProcessor : EffectProcessor
{
    private const float BandwidthOctaves = 0.746268656716f;

    private readonly int _channels;

    private readonly float _edgeCoefficient;

    private readonly float _gain;

    private readonly BiquadFilter[] _lowPassFilters;

    private readonly BiquadFilter[] _bandPassFilters;

    public DistortionProcessor(DistortionEffect effect, int sampleRate, int channels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channels);

        var edge = Math.Min(MathF.Sin(MathF.PI * 0.5f * effect.Edge), 0.99f);
        _edgeCoefficient = 2f * edge / (1f - edge);
        _gain = effect.Gain;
        _channels = channels;
        _lowPassFilters = new BiquadFilter[channels];
        _bandPassFilters = new BiquadFilter[channels];

        for (var channel = 0; channel < channels; channel++)
        {
            _lowPassFilters[channel] = BiquadFilter.FromBandwidth(
                BiquadType.LowPass, effect.LowcutHz, 1f, BandwidthOctaves, sampleRate * 4);
            _bandPassFilters[channel] = BiquadFilter.FromBandwidth(
                BiquadType.BandPass, effect.CenterHz, 1f, effect.BandwidthHz / (effect.CenterHz * 0.67f),
                sampleRate * 4);
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
            var channel = index % _channels;
            var output = 0f;
            for (var phase = 0; phase < 4; phase++)
            {
                var sample = phase == 0 ? pcm[index] * 4f : 0f;
                sample = _lowPassFilters[channel].Process(sample);
                sample = Shape(sample, _edgeCoefficient);
                sample = Shape(sample, _edgeCoefficient, invert: true);
                sample = Shape(sample, _edgeCoefficient);
                sample = _bandPassFilters[channel].Process(sample);
                if (phase == 0)
                {
                    output = sample;
                }
            }

            pcm[index] = output * _gain;
        }
    }

    private static float Shape(float sample, float coefficient, bool invert = false)
    {
        var numerator = (1f + coefficient) * sample;
        var result = numerator / (1f + (coefficient * MathF.Abs(sample)));
        return invert ? -result : result;
    }
}
