// DSP implementation adapted from OpenAL Soft 1.25.2.
// See THIRD-PARTY-NOTICES.md for attribution and license terms.

namespace Fresnel.Audio;

/// <summary>
/// Configures a damped, stereo two-tap echo.
/// </summary>
public sealed record EchoEffect : AudioEffect
{
    /// <summary>
    /// Gets the time to the first tap and between successive taps.
    /// </summary>
    /// <value>
    /// A duration from zero to 207 milliseconds.
    /// </value>
    public TimeSpan Delay { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets the proportion of the second tap fed back into the delay line.
    /// </summary>
    /// <value>
    /// A value from 0 to 1.
    /// </value>
    public float Feedback { get; init; } = 0.5f;

    /// <summary>
    /// Gets the high-frequency attenuation of the repeats.
    /// </summary>
    /// <value>
    /// A value from 0 for bright to 1 for dark.
    /// </value>
    public float Damping { get; init; } = 0.5f;

    /// <summary>
    /// Gets the dry/wet balance.
    /// </summary>
    /// <value>
    /// A value from 0 for dry to 1 for wet.
    /// </value>
    public float Mix { get; init; } = 0.5f;

    internal override void Validate()
    {
        EffectValidation.Range(Delay, TimeSpan.Zero, TimeSpan.FromSeconds(0.207), nameof(Delay));
        EffectValidation.Range(Feedback, 0f, 1f, nameof(Feedback));
        EffectValidation.Range(Damping, 0f, 1f, nameof(Damping));
        EffectValidation.Range(Mix, 0f, 1f, nameof(Mix));
    }

    internal override EffectProcessor CreateProcessor(int sampleRate, int channels)
    {
        return new EchoProcessor(this, sampleRate, channels);
    }
}

internal sealed class EchoProcessor : EffectProcessor
{
    private readonly float[][] _delayLines;

    private readonly BiquadFilter[] _dampingFilters;

    private readonly float _feedback;

    private readonly int _firstTapDelay;

    private readonly int _secondTapDelay;

    private readonly DryWetMix _mix;

    private int _writeIndex;

    public EchoProcessor(EchoEffect effect, int sampleRate, int channels) : base(sampleRate, channels)
    {
        _feedback = effect.Feedback;
        _mix = new DryWetMix(effect.Mix);
        _firstTapDelay = Math.Max((int)MathF.Round((float)effect.Delay.TotalSeconds * sampleRate), 1);
        _secondTapDelay = _firstTapDelay * 2;

        var length = 1;
        var required = checked((int)MathF.Ceiling(0.414f * sampleRate) + 1);
        while (length < required)
        {
            length <<= 1;
        }

        var lines = channels == 2 ? 1 : channels;
        _delayLines = new float[lines][];
        _dampingFilters = new BiquadFilter[lines];
        var highGain = Math.Max(1f - effect.Damping, 0.0625f);
        for (var line = 0; line < lines; line++)
        {
            _delayLines[line] = new float[length];
            _dampingFilters[line] = BiquadFilter.FromSlope(
                BiquadType.HighShelf, 5_000f, highGain, 1f, sampleRate);
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
                var dryLeft = pcm[frameOffset];
                var dryRight = pcm[frameOffset + 1];
                var (first, second) = ProcessLine((dryLeft + dryRight) * 0.5f, 0);
                pcm[frameOffset] = _mix.Blend(dryLeft, second * 0.5f);
                pcm[frameOffset + 1] = _mix.Blend(dryRight, first * 0.5f);
            }
            else
            {
                for (var channel = 0; channel < _channels; channel++)
                {
                    var dry = pcm[frameOffset + channel];
                    var (first, second) = ProcessLine(dry, channel);
                    pcm[frameOffset + channel] = _mix.Blend(dry, (first + second) * 0.5f);
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
}
