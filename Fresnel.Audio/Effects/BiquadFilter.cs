// DSP implementation adapted from OpenAL Soft 1.25.2.
// Source: core/filters/biquad.{h,cpp}.
// Upstream notice: OpenAL Soft contributors.
// Adapted portions are licensed under LGPL-2.0-or-later.
// See THIRD-PARTY-NOTICES.md for attribution and license terms.

namespace Fresnel.Audio;

internal enum BiquadType
{
    HighShelf,
    LowShelf,
    Peaking,
    LowPass,
    HighPass,
    BandPass
}

internal struct BiquadFilter
{
    private float _a1;

    private float _a2;

    private float _b0;

    private float _b1;

    private float _b2;

    private float _state1;

    private float _state2;

    internal float Process(float sample)
    {
        var output = (sample * _b0) + _state1;
        _state1 = (sample * _b1) - (output * _a1) + _state2;
        _state2 = (sample * _b2) - (output * _a2);
        return output;
    }

    internal static BiquadFilter FromSlope(BiquadType type, float frequencyHz, float gain, float slope, int sampleRate)
    {
        gain = Math.Max(gain, 0.001f);
        var reciprocalQ = MathF.Sqrt(((gain + (1f / gain)) * ((1f / slope) - 1f)) + 2f);
        return Create(type, frequencyHz, gain, reciprocalQ, sampleRate);
    }

    internal static BiquadFilter FromBandwidth(BiquadType type, float frequencyHz, float gain, float bandwidthOctaves,
        int sampleRate)
    {
        var normalized = Math.Clamp(frequencyHz / sampleRate, 1f / sampleRate, 0.49f);
        var omega = MathF.Tau * normalized;
        var reciprocalQ = 2f * MathF.Sinh(MathF.Log(2f) * 0.5f * bandwidthOctaves * omega / MathF.Sin(omega));
        return Create(type, frequencyHz, gain, reciprocalQ, sampleRate);
    }

    private static BiquadFilter Create(BiquadType type, float frequencyHz, float gain, float reciprocalQ,
        int sampleRate)
    {
        gain = Math.Max(gain, 0.00001f);
        var omega = MathF.Tau * Math.Min(frequencyHz / sampleRate, 0.49f);
        var sine = MathF.Sin(omega);
        var cosine = MathF.Cos(omega);
        var alpha = sine * 0.5f * reciprocalQ;

        float b0;
        float b1;
        float b2;
        float a0;
        float a1;
        float a2;

        switch (type)
        {
            case BiquadType.HighShelf:
                {
                    var twiceRootGainAlpha = 2f * MathF.Sqrt(gain) * alpha;
                    b0 = gain * (gain + 1f + ((gain - 1f) * cosine) + twiceRootGainAlpha);
                    b1 = -2f * gain * (gain - 1f + ((gain + 1f) * cosine));
                    b2 = gain * (gain + 1f + ((gain - 1f) * cosine) - twiceRootGainAlpha);
                    a0 = gain + 1f - ((gain - 1f) * cosine) + twiceRootGainAlpha;
                    a1 = 2f * (gain - 1f - ((gain + 1f) * cosine));
                    a2 = gain + 1f - ((gain - 1f) * cosine) - twiceRootGainAlpha;
                    break;
                }

            case BiquadType.LowShelf:
                {
                    var twiceRootGainAlpha = 2f * MathF.Sqrt(gain) * alpha;
                    b0 = gain * (gain + 1f - ((gain - 1f) * cosine) + twiceRootGainAlpha);
                    b1 = 2f * gain * (gain - 1f - ((gain + 1f) * cosine));
                    b2 = gain * (gain + 1f - ((gain - 1f) * cosine) - twiceRootGainAlpha);
                    a0 = gain + 1f + ((gain - 1f) * cosine) + twiceRootGainAlpha;
                    a1 = -2f * (gain - 1f + ((gain + 1f) * cosine));
                    a2 = gain + 1f + ((gain - 1f) * cosine) - twiceRootGainAlpha;
                    break;
                }

            case BiquadType.Peaking:
                {
                    b0 = 1f + (alpha * gain);
                    b1 = -2f * cosine;
                    b2 = 1f - (alpha * gain);
                    a0 = 1f + (alpha / gain);
                    a1 = -2f * cosine;
                    a2 = 1f - (alpha / gain);
                    break;
                }

            case BiquadType.LowPass:
                {
                    b0 = (1f - cosine) * 0.5f;
                    b1 = 1f - cosine;
                    b2 = b0;
                    a0 = 1f + alpha;
                    a1 = -2f * cosine;
                    a2 = 1f - alpha;
                    break;
                }

            case BiquadType.HighPass:
                {
                    b0 = (1f + cosine) * 0.5f;
                    b1 = -(1f + cosine);
                    b2 = b0;
                    a0 = 1f + alpha;
                    a1 = -2f * cosine;
                    a2 = 1f - alpha;
                    break;
                }

            case BiquadType.BandPass:
                {
                    b0 = alpha;
                    b1 = 0f;
                    b2 = -alpha;
                    a0 = 1f + alpha;
                    a1 = -2f * cosine;
                    a2 = 1f - alpha;
                    break;
                }

            default:
                {
                    throw new ArgumentOutOfRangeException(nameof(type));
                }
        }

        return new BiquadFilter
        {
            _b0 = b0 / a0,
            _b1 = b1 / a0,
            _b2 = b2 / a0,
            _a1 = a1 / a0,
            _a2 = a2 / a0
        };
    }
}
