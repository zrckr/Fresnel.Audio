// DSP implementation adapted from OpenAL Soft 1.25.2.
// Source: alc/effects/modulator.cpp.
// Upstream notice: Copyright (C) 2009 Chris Robinson.
// Adapted portions are licensed under LGPL-2.0-or-later.
// See THIRD-PARTY-NOTICES.md for attribution and license terms.

namespace Fresnel.Audio;

/// <summary>
/// Configures a ring modulator that multiplies the signal by a periodic carrier.
/// </summary>
public sealed record RingModulatorEffect : AudioEffect
{
    /// <summary>
    /// Gets the carrier waveform.
    /// </summary>
    public OscillatorWaveform Waveform { get; init; } = OscillatorWaveform.Sine;

    /// <summary>
    /// Gets the carrier frequency, in hertz.
    /// </summary>
    /// <value>
    /// A value from 0 to 8,000. Zero disables modulation.
    /// </value>
    public float FrequencyHz { get; init; } = 440f;

    /// <summary>
    /// Gets the dry/wet balance.
    /// </summary>
    /// <value>
    /// A value from 0 for dry to 1 for wet.
    /// </value>
    public float Mix { get; init; } = 1f;

    /// <inheritdoc/>
    internal override void Validate()
    {
        EffectValidation.Waveform(Waveform, nameof(Waveform));
        EffectValidation.Range(FrequencyHz, 0f, 8_000f, nameof(FrequencyHz));
        EffectValidation.Range(Mix, 0f, 1f, nameof(Mix));
    }

    internal override EffectProcessor CreateProcessor(int sampleRate, int channels)
    {
        return new RingModulatorProcessor(this, sampleRate, channels);
    }
}

internal sealed class RingModulatorProcessor : EffectProcessor
{
    private readonly BiquadFilter[] _highPassFilters;

    private readonly int _range;

    private readonly float _scale;

    private readonly OscillatorWaveform _waveform;

    private readonly DryWetMix _mix;

    private int _index;

    public RingModulatorProcessor(RingModulatorEffect effect, int sampleRate, int channels) : base(sampleRate, channels)
    {
        _waveform = effect.Waveform;
        _mix = new DryWetMix(effect.Mix);

        // OpenAL rounds the oscillator to a whole number of samples per cycle.
        // Besides matching its waveform generation, this avoids discontinuities
        // where a table-free saw or square oscillator wraps between blocks.
        _range = effect.FrequencyHz > 0f
            ? (int)Math.Clamp(MathF.Floor((sampleRate / effect.FrequencyHz) + 0.5f), 1f, sampleRate)
            : 1;
        if (_waveform == OscillatorWaveform.Square && _range > 1)
        {
            _range = (_range + 1) & ~1;
        }

        _scale = _waveform switch
        {
            OscillatorWaveform.Sine or OscillatorWaveform.Triangle => MathF.Tau / _range,
            OscillatorWaveform.Sawtooth => _range > 1 ? 2f / (_range - 1) : 0f,
            OscillatorWaveform.Square => _range > 1 ? 1f / (_range - 1) : 0f,
            _ => 0f
        };
        _highPassFilters = new BiquadFilter[channels];

        var cutoff = Math.Clamp(800f, sampleRate / 512f, sampleRate * 0.49f);
        for (var channel = 0; channel < channels; channel++)
        {
            _highPassFilters[channel] = BiquadFilter.FromBandwidth(BiquadType.HighPass, cutoff,
                1f, 0.75f, sampleRate);
        }
    }

    public override void Process(Span<float> pcm)
    {
        if (pcm.Length % _channels != 0)
        {
            throw new ArgumentException("PCM data must contain complete sample frames.", nameof(pcm));
        }

        for (var frameOffset = 0; frameOffset < pcm.Length; frameOffset += _channels)
        {
            var carrier = Carrier();
            for (var channel = 0; channel < _channels; channel++)
            {
                var index = frameOffset + channel;
                var dry = pcm[index];
                var wet = _highPassFilters[channel].Process(dry) * carrier;
                pcm[index] = _mix.Blend(dry, wet);
            }

            if (++_index == _range)
            {
                _index = 0;
            }
        }
    }

    private float Carrier()
    {
        if (_range == 1)
        {
            return 1f;
        }

        return _waveform switch
        {
            OscillatorWaveform.Sine => MathF.Sin(_index * _scale),
            OscillatorWaveform.Sawtooth => (_index * _scale) - 1f,
            OscillatorWaveform.Square => (_index * _scale) < 0.5f ? 1f : -1f,
            // Triangle is a Fresnel extension; OpenAL EFX only defines the
            // other three ring-modulator waveforms.
            OscillatorWaveform.Triangle => ModulatedDelay.PhaseWaveform(_waveform, _index * _scale),
            _ => 1f
        };
    }
}
