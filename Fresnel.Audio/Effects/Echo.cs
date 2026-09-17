// DSP implementation adapted from OpenAL Soft 1.25.2.
// Source: alc/effects/echo.cpp.
// Upstream notice: Copyright (C) 2009 Chris Robinson.
// Adapted portions are licensed under LGPL-2.0-or-later.
// See THIRD-PARTY-NOTICES.md for attribution and license terms.

namespace Fresnel.Audio;

/// <summary>
/// Configures a damped two-tap echo effect.
/// </summary>
public readonly record struct EchoEffect() : IEffect
{
    /// <summary>
    /// Gets the time from the dry signal to the first echo tap.
    /// </summary>
    /// <value>
    /// A duration from zero to 207 milliseconds.
    /// </value>
    public TimeSpan Delay { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets the additional delay between the first and second echo taps.
    /// </summary>
    /// <value>
    /// A duration from zero to 404 milliseconds.
    /// </value>
    public TimeSpan TapDelay { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets the high-frequency attenuation applied to the feedback signal.
    /// </summary>
    /// <value>
    /// A value from 0 for no damping to 0.99 for maximum damping.
    /// </value>
    public float Damping { get; init; } = 0.5f;

    /// <summary>
    /// Gets the proportion of the second tap fed back into the delay line.
    /// </summary>
    /// <value>
    /// A value from 0 to 1.
    /// </value>
    public float Feedback { get; init; } = 0.5f;

    /// <summary>
    /// Gets the stereo separation and ordering of the two taps.
    /// </summary>
    /// <value>
    /// A value from -1 to 1. Zero centers both taps; either extreme places them
    /// on opposite sides, with the sign selecting which tap is on the left.
    /// </value>
    public float Spread { get; init; } = -1f;

    /// <inheritdoc/>
    public void Validate()
    {
        EffectValidation.Range(Delay, TimeSpan.Zero, TimeSpan.FromSeconds(0.207), nameof(Delay));
        EffectValidation.Range(TapDelay, TimeSpan.Zero, TimeSpan.FromSeconds(0.404), nameof(TapDelay));
        EffectValidation.Range(Damping, 0f, 0.99f, nameof(Damping));
        EffectValidation.Range(Feedback, 0f, 1f, nameof(Feedback));
        EffectValidation.Range(Spread, -1f, 1f, nameof(Spread));
    }
}

internal sealed class EchoProcessor : EffectProcessor
{
    private const float WetGain = 0.5f;

    private readonly int _channels;

    private readonly float[][] _delayLines;

    private readonly BiquadFilter[] _dampingFilters;

    private readonly float _feedback;

    private readonly int _firstTapDelay;

    private readonly int _secondTapDelay;

    private readonly float _spread;

    private int _writeIndex;

    public EchoProcessor(EchoEffect effect, int sampleRate, int channels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channels);
        _channels = channels;
        _feedback = effect.Feedback;
        _spread = effect.Spread;
        _firstTapDelay = Math.Max((int)MathF.Round((float)effect.Delay.TotalSeconds * sampleRate), 1);
        _secondTapDelay = _firstTapDelay +
                          (int)MathF.Round((float)effect.TapDelay.TotalSeconds * sampleRate);

        var length = 1;
        var required = checked((int)MathF.Ceiling((0.207f + 0.404f) * sampleRate) + 1);
        while (length < required)
        {
            length <<= 1;
        }

        // A stereo echo follows OpenAL's mono send model, allowing Spread to pan
        // the two taps. Other layouts retain one direct delay line per channel.
        var lines = channels == 2 ? 1 : channels;
        _delayLines = new float[lines][];
        _dampingFilters = new BiquadFilter[lines];
        var highGain = Math.Max(1f - effect.Damping, 0.0625f);
        for (var line = 0; line < lines; line++)
        {
            _delayLines[line] = new float[length];
            _dampingFilters[line] = BiquadFilter.FromSlope(BiquadType.HighShelf, 5_000f,
                highGain, 1f, sampleRate);
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
            if (_channels == 2)
            {
                var input = (pcm[frameOffset] + pcm[frameOffset + 1]) * 0.5f;
                var (first, second) = ProcessLine(input, 0);
                var firstPosition = -_spread;
                var secondPosition = _spread;
                Pan(firstPosition, out var firstLeft, out var firstRight);
                Pan(secondPosition, out var secondLeft, out var secondRight);
                pcm[frameOffset] += ((first * firstLeft) + (second * secondLeft)) * WetGain;
                pcm[frameOffset + 1] += ((first * firstRight) + (second * secondRight)) * WetGain;
            }
            else
            {
                for (var channel = 0; channel < _channels; channel++)
                {
                    var (first, second) = ProcessLine(pcm[frameOffset + channel], channel);
                    pcm[frameOffset + channel] += (first + second) * WetGain;
                }
            }

            _writeIndex = (_writeIndex + 1) & (_delayLines[0].Length - 1);
        }
    }

    private (float First, float Second) ProcessLine(float input, int line)
    {
        var delayLine = _delayLines[line];
        var mask = delayLine.Length - 1;
        delayLine[_writeIndex] = input;
        var first = delayLine[(_writeIndex - _firstTapDelay) & mask];
        var second = delayLine[(_writeIndex - _secondTapDelay) & mask];
        delayLine[_writeIndex] += _dampingFilters[line].Process(second) * _feedback;
        return (first, second);
    }

    private static void Pan(float position, out float left, out float right)
    {
        left = MathF.Sqrt((1f - position) * 0.5f);
        right = MathF.Sqrt((1f + position) * 0.5f);
    }
}
