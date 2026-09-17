// DSP implementation adapted from OpenAL Soft 1.25.2.
// Source: alc/effects/chorus.cpp and core/cubic_tables.hpp.
// Upstream notice: Copyright (C) 2013 Mike Gorchak.
// Adapted portions are licensed under LGPL-2.0-or-later.
// See THIRD-PARTY-NOTICES.md for attribution and license terms.

namespace Fresnel.Audio;

internal sealed class ModulatedDelay
{
    private const int MinimumDelaySamples = 24;

    private readonly float _baseDelay;

    private readonly int _channels;

    private readonly float[][] _delayLines;

    private readonly float _depth;

    private readonly float _feedback;

    private readonly float _phaseIncrement;

    private readonly float _phaseOffset;

    private readonly EffectWaveform _waveform;

    private float _phase;

    private int _writeIndex;

    internal ModulatedDelay(EffectWaveform waveform, float phaseDegrees, float rateHz, float depth,
        float feedback, TimeSpan delay, float maximumDelaySeconds, int sampleRate, int channels)
    {
        _waveform = waveform;
        _channels = channels;
        _feedback = feedback;
        if (rateHz > 0f)
        {
            // OpenAL constrains the LFO to a whole number of samples per cycle.
            var lfoRange = Math.Max(MathF.Round(sampleRate / rateHz), 1f);
            _phaseIncrement = MathF.Tau / lfoRange;
            _phaseOffset = phaseDegrees * MathF.PI / 180f;
        }
        else
        {
            // Its zero-rate mode disables phase displacement as well as motion.
            _phaseIncrement = 0f;
            _phaseOffset = 0f;
        }

        var requestedDelay = (float)(delay.TotalSeconds * sampleRate);
        _baseDelay = Math.Max(requestedDelay, MinimumDelaySamples);
        _depth = Math.Min(_baseDelay * depth, _baseDelay - MinimumDelaySamples);

        var length = 1;
        var required = checked((int)MathF.Ceiling(maximumDelaySeconds * sampleRate * 2f) + 1);
        while (length < required)
        {
            length <<= 1;
        }

        _delayLines = new float[channels][];
        for (var channel = 0; channel < channels; channel++)
        {
            _delayLines[channel] = new float[length];
        }
    }

    internal void Process(Span<float> pcm)
    {
        var frames = pcm.Length / _channels;
        for (var frame = 0; frame < frames; frame++)
        {
            for (var channel = 0; channel < _channels; channel++)
            {
                var index = (frame * _channels) + channel;
                var input = pcm[index];
                var line = _delayLines[channel];

                // OpenAL feeds the current sample before tapping, which is required for
                // fractional delays below one sample.
                line[_writeIndex] = input;
                // Treat direct channels as left/right pairs. This preserves the
                // two LFO phases without importing OpenAL's ambisonic router.
                var channelPhase = (channel & 1) == 0 ? _phase : _phase + _phaseOffset;
                var modulatedDelay = _baseDelay + (_waveform.Phased(channelPhase) * _depth);
                var delayed = ReadCubic(line, _writeIndex, modulatedDelay);
                var feedbackIndex = Wrap(_writeIndex - (int)MathF.Round(_baseDelay), line.Length);
                line[_writeIndex] += line[feedbackIndex] * _feedback;
                pcm[index] = (input + delayed) * 0.5f;
            }

            _writeIndex = (_writeIndex + 1) & (_delayLines[0].Length - 1);
            _phase = EffectExtensions.WrapPhase(_phase + _phaseIncrement);
        }
    }

    private static float ReadCubic(float[] line, int writeIndex, float delay)
    {
        var whole = (int)MathF.Floor(delay);
        var mu = delay - whole;
        var mu2 = mu * mu;
        var mu3 = mu * mu2;
        var coefficient0 = (-mu / 3f) + (0.5f * mu2) - (mu3 / 6f);
        var coefficient1 = 1f - (0.5f * mu) - mu2 + (0.5f * mu3);
        var coefficient2 = mu + (0.5f * mu2) - (0.5f * mu3);
        var coefficient3 = (-mu / 6f) + (mu3 / 6f);
        var mask = line.Length - 1;
        var offset = writeIndex - whole;
        return (line[(offset + 1) & mask] * coefficient0) +
               (line[offset & mask] * coefficient1) +
               (line[(offset - 1) & mask] * coefficient2) +
               (line[(offset - 2) & mask] * coefficient3);
    }

    private static int Wrap(int value, int length)
    {
        value %= length;
        return value < 0 ? value + length : value;
    }
}
