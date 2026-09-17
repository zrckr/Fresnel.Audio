// DSP implementation adapted from OpenAL Soft 1.25.2.
// Source: alc/effects/equalizer.cpp.
// Upstream notice: Copyright (C) 2013 Mike Gorchak.
// Adapted portions are licensed under LGPL-2.0-or-later.
// See THIRD-PARTY-NOTICES.md for attribution and license terms.

namespace Fresnel.Audio;

/// <summary>
/// Configures a four-band parametric equalizer.
/// </summary>
public readonly record struct EqualizerEffect() : IEffect
{
    /// <summary>
    /// Gets the linear gain of the low-frequency shelf.
    /// </summary>
    /// <value>
    /// A value from 0.126 to 7.943.
    /// </value>
    public float LowGain { get; init; } = 1f; // [0.126, 7.943]

    /// <summary>
    /// Gets the transition frequency of the low-frequency shelf, in hertz.
    /// </summary>
    /// <value>
    /// A value from 50 to 800.
    /// </value>
    public float LowCutHz { get; init; } = 200f; // [50, 800]

    /// <summary>
    /// Gets the linear gain of the low-mid peaking band.
    /// </summary>
    /// <value>
    /// A value from 0.126 to 7.943.
    /// </value>
    public float LowMidGain { get; init; } = 1f; // [0.126, 7.943]

    /// <summary>
    /// Gets the center frequency of the low-mid peaking band, in hertz.
    /// </summary>
    /// <value>
    /// A value from 200 to 3,000.
    /// </value>
    public float LowMidFrequencyHz { get; init; } = 500f; // [200, 3000]

    /// <summary>
    /// Gets the bandwidth of the low-mid peaking band, in octaves.
    /// </summary>
    /// <value>
    /// A value from 0.01 to 1.
    /// </value>
    public float LowMidBandwidth { get; init; } = 1f; // [0.01, 1]

    /// <summary>
    /// Gets the linear gain of the high-mid peaking band.
    /// </summary>
    /// <value>
    /// A value from 0.126 to 7.943.
    /// </value>
    public float HighMidGain { get; init; } = 1f; // [0.126, 7.943]

    /// <summary>
    /// Gets the center frequency of the high-mid peaking band, in hertz.
    /// </summary>
    /// <value>
    /// A value from 1,000 to 8,000.
    /// </value>
    public float HighMidFrequencyHz { get; init; } = 3000f; // [1000, 8000]

    /// <summary>
    /// Gets the bandwidth of the high-mid peaking band, in octaves.
    /// </summary>
    /// <value>
    /// A value from 0.01 to 1.
    /// </value>
    public float HighMidBandwidth { get; init; } = 1f; // [0.01, 1]

    /// <summary>
    /// Gets the linear gain of the high-frequency shelf.
    /// </summary>
    /// <value>
    /// A value from 0.126 to 7.943.
    /// </value>
    public float HighGain { get; init; } = 1f; // [0.126, 7.943]

    /// <summary>
    /// Gets the transition frequency of the high-frequency shelf, in hertz.
    /// </summary>
    /// <value>
    /// A value from 4,000 to 16,000.
    /// </value>
    public float HighCutHz { get; init; } = 6000f; // [4000, 16000]

    /// <inheritdoc/>
    public void Validate()
    {
        EffectValidation.Range(LowGain, 0.126f, 7.943f, nameof(LowGain));
        EffectValidation.Range(LowCutHz, 50f, 800f, nameof(LowCutHz));
        EffectValidation.Range(LowMidGain, 0.126f, 7.943f, nameof(LowMidGain));
        EffectValidation.Range(LowMidFrequencyHz, 200f, 3_000f, nameof(LowMidFrequencyHz));
        EffectValidation.Range(LowMidBandwidth, 0.01f, 1f, nameof(LowMidBandwidth));
        EffectValidation.Range(HighMidGain, 0.126f, 7.943f, nameof(HighMidGain));
        EffectValidation.Range(HighMidFrequencyHz, 1_000f, 8_000f, nameof(HighMidFrequencyHz));
        EffectValidation.Range(HighMidBandwidth, 0.01f, 1f, nameof(HighMidBandwidth));
        EffectValidation.Range(HighGain, 0.126f, 7.943f, nameof(HighGain));
        EffectValidation.Range(HighCutHz, 4_000f, 16_000f, nameof(HighCutHz));
    }
}

internal sealed class EqualizerProcessor : EffectProcessor
{
    private readonly BiquadFilter[] _highMidFilters;

    private readonly BiquadFilter[] _highShelfFilters;

    private readonly BiquadFilter[] _lowMidFilters;

    private readonly BiquadFilter[] _lowShelfFilters;

    private readonly int _channels;

    public EqualizerProcessor(EqualizerEffect effect, int sampleRate, int channels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channels);

        _channels = channels;
        _lowShelfFilters = new BiquadFilter[channels];
        _lowMidFilters = new BiquadFilter[channels];
        _highMidFilters = new BiquadFilter[channels];
        _highShelfFilters = new BiquadFilter[channels];

        for (var channel = 0; channel < channels; channel++)
        {
            _lowShelfFilters[channel] = BiquadFilter.FromSlope(BiquadType.LowShelf, effect.LowCutHz,
                MathF.Sqrt(effect.LowGain), 0.75f, sampleRate);
            _lowMidFilters[channel] = BiquadFilter.FromBandwidth(BiquadType.Peaking,
                effect.LowMidFrequencyHz, MathF.Sqrt(effect.LowMidGain), effect.LowMidBandwidth, sampleRate);
            _highMidFilters[channel] = BiquadFilter.FromBandwidth(BiquadType.Peaking,
                effect.HighMidFrequencyHz, MathF.Sqrt(effect.HighMidGain), effect.HighMidBandwidth, sampleRate);
            _highShelfFilters[channel] = BiquadFilter.FromSlope(BiquadType.HighShelf, effect.HighCutHz,
                MathF.Sqrt(effect.HighGain), 0.75f, sampleRate);
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
            var sample = _lowShelfFilters[channel].Process(pcm[index]);
            sample = _lowMidFilters[channel].Process(sample);
            sample = _highMidFilters[channel].Process(sample);
            pcm[index] = _highShelfFilters[channel].Process(sample);
        }
    }
}
