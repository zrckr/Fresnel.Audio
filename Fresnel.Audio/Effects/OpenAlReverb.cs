// DSP implementation adapted from OpenAL Soft 1.25.2.
// Source: alc/effects/reverb.cpp.
// Upstream notice: Copyright (C) 2008-2017 Chris Robinson and Christopher Fitzgerald.
// Adapted portions are licensed under LGPL-2.0-or-later.
// See THIRD-PARTY-NOTICES.md for attribution and license terms.

namespace Fresnel.Audio;

internal sealed class OpenAlReverb
{
    private const float DecayGain = 0.001f;

    private const float InverseSqrt2 = 0.7071067811865475f;

    private static readonly float[] EarlyTapLengths = [0f, 1.010676e-3f, 2.126553e-3f, 3.358580e-3f];

    private static readonly float[] EarlyAllPassLengths = [4.854840e-4f, 5.360178e-4f, 5.918117e-4f, 6.534130e-4f];

    private static readonly float[] EarlyLineLengths = [2.992520e-3f, 5.456575e-3f, 7.688329e-3f, 9.709681e-3f];

    private static readonly float[] LateAllPassLengths = [8.091400e-4f, 1.019453e-3f, 1.407968e-3f, 1.618280e-3f];

    private static readonly float[] LateLineLengths = [9.709681e-3f, 1.223343e-2f, 1.689561e-2f, 1.941936e-2f];

    private const float LateAllPassAverage = 1.21371025e-3f;

    private const float LateDelayAverage = 1.57782305e-2f;

    private readonly float _allPassCoefficient;

    private readonly float _densityGain;

    private readonly float _earlyCoefficient;

    private readonly float _earlyGain;

    private readonly RingDelay[] _earlyAllPass = new RingDelay[4];

    private readonly int[] _earlyTap = new int[4];

    private readonly RingDelay[] _earlyLines = new RingDelay[4];

    private readonly BiquadFilter[] _highFilters = new BiquadFilter[4];

    private readonly float _lateGain;

    private readonly RingDelay[] _lateAllPass = new RingDelay[4];

    private readonly RingDelay[] _lateFeedback = new RingDelay[4];

    private readonly RingDelay?[] _lateInput = new RingDelay?[4];

    private readonly BiquadFilter[] _lateHighFilters = new BiquadFilter[4];

    private readonly float[] _lateMidGain = new float[4];

    private readonly RingDelay[] _mainDelay = new RingDelay[4];

    private readonly float _mixX;

    private readonly float _mixY;

    internal OpenAlReverb(ReverbEffect effect, int sampleRate)
    {
        const float diffusion = 1f;
        const float earlyGain = 0.05f;
        const float lateGain = 1.26f;
        const float airAbsorption = 0.994f;
        const float earlyDelaySeconds = 0.05f;
        const float lateDelaySeconds = 0.011f;

        var densityMultiplier = Math.Max(1f, MathF.Cbrt(effect.RoomSize * 1_000f));
        var angle = diffusion * MathF.Atan(MathF.Sqrt(3f));

        _mixX = MathF.Cos(angle);
        _mixY = MathF.Sin(angle) / MathF.Sqrt(3f);
        _allPassCoefficient = diffusion * diffusion * InverseSqrt2;
        _earlyGain = earlyGain;
        _lateGain = lateGain;

        var averageEarlyLength = 0f;
        for (var line = 0; line < 4; line++)
        {
            averageEarlyLength += EarlyLineLengths[line];
        }

        averageEarlyLength = averageEarlyLength * 0.25f * densityMultiplier;
        _earlyCoefficient = DecayCoefficient(averageEarlyLength, (float)effect.Decay.TotalSeconds);

        var highRatio = 1f - (0.85f * effect.Damping);
        if (airAbsorption < 1f)
        {
            var decayLength = MathF.Log10(airAbsorption) *
                (float)effect.Decay.TotalSeconds / -3f;
            if (decayLength > 0f)
            {
                highRatio = Math.Min(highRatio, 1f / 343.3f / decayLength);
            }
        }

        var midDecay = (float)effect.Decay.TotalSeconds;
        var highDecay = Math.Clamp(midDecay * highRatio, 0.1f, 20f);
        var lateAverage = LateDelayAverage * densityMultiplier;
        var weightedDecay = (0.25f * midDecay) + (0.75f * highDecay);
        var densityDecay = DecayCoefficient(lateAverage, weightedDecay);
        _densityGain = MathF.Sqrt(1f - (densityDecay * densityDecay));

        var maximumMainDelay = earlyDelaySeconds +
                               (EarlyTapLengths[^1] * densityMultiplier) + 0.01f;
        for (var line = 0; line < 4; line++)
        {
            _mainDelay[line] = new RingDelay(ToSamples(maximumMainDelay, sampleRate));
            _earlyTap[line] = ToSamples(earlyDelaySeconds +
                                        (EarlyTapLengths[line] * densityMultiplier), sampleRate, true);
            _earlyAllPass[line] = new RingDelay(ToSamples(EarlyAllPassLengths[line] *
                                                          densityMultiplier, sampleRate));
            _earlyLines[line] = new RingDelay(ToSamples(EarlyLineLengths[line] *
                                                        densityMultiplier, sampleRate));
            _lateAllPass[line] = new RingDelay(ToSamples(LateAllPassLengths[line] *
                                                         densityMultiplier, sampleRate));

            var lateLength = LateLineLengths[line] * densityMultiplier;
            _lateFeedback[line] = new RingDelay(ToSamples(lateLength, sampleRate));
            var relativeLateLength = (LateLineLengths[line] - LateLineLengths[0]) * 0.25f;
            var lateInputDelay = ToSamples(lateDelaySeconds +
                                           (relativeLateLength * densityMultiplier), sampleRate, true);
            _lateInput[line] = lateInputDelay > 0 ? new RingDelay(lateInputDelay) : null;

            _highFilters[line] = BiquadFilter.FromSlope(BiquadType.HighShelf, 5_000f,
                1f - (0.85f * effect.Damping), 1f, sampleRate);
            // OpenAL approximates the vector all-pass absorption by blending
            // each line's length toward the average as diffusion increases.
            var allPassLength = Lerp(LateAllPassLengths[line], LateAllPassAverage,
                diffusion) * densityMultiplier;
            var decayLength = lateLength + allPassLength;
            _lateMidGain[line] = DecayCoefficient(decayLength, midDecay);
            var highGain = DecayCoefficient(decayLength, highDecay) / _lateMidGain[line];
            _lateHighFilters[line] = BiquadFilter.FromSlope(BiquadType.HighShelf, 5_000f,
                highGain, 1f, sampleRate);
        }
    }

    internal float Process(float input, float masterGain)
    {
        Span<float> primary = stackalloc float[4];
        Span<float> reflected = stackalloc float[4];
        Span<float> early = stackalloc float[4];
        Span<float> late = stackalloc float[4];
        Span<float> scattered = stackalloc float[4];
        Span<float> refeed = stackalloc float[4];
        Span<float> allPassInput = stackalloc float[4];

        for (var line = 0; line < 4; line++)
        {
            _mainDelay[line].Write(input * 0.5f * masterGain);
            var sample = _mainDelay[line].Read(_earlyTap[line]);
            sample = _highFilters[line].Process(sample);
            primary[line] = AllPass(_earlyAllPass[line], sample, _allPassCoefficient);
        }

        Reflect(primary, reflected);
        Scatter(primary, scattered, _mixX, _mixY);
        for (var line = 0; line < 4; line++)
        {
            var secondary = _earlyLines[line].Process(reflected[line]);
            early[line] = primary[line] + (secondary * _earlyCoefficient);
            var lateInput = _lateInput[line];
            var feed = (lateInput?.Read() ?? scattered[line]) * _densityGain;
            lateInput?.Write(scattered[line]);

            var feedback = _lateFeedback[line].Read();
            feedback = _lateHighFilters[line].Process(feedback) * _lateMidGain[line];
            allPassInput[line] = feedback + feed;
        }

        VectorAllPass(allPassInput, late, scattered);
        ScatterReverse(late, refeed, _mixX, _mixY);
        for (var line = 0; line < 4; line++)
        {
            _lateFeedback[line].Write(refeed[line]);
            _mainDelay[line].Advance();
            _lateInput[line]?.Advance();
            _lateFeedback[line].Advance();
        }

        var earlySum = (early[0] + early[1] + early[2] + early[3]) * 0.5f;
        var lateSum = (late[0] + late[1] + late[2] + late[3]) * 0.5f;
        return (earlySum * _earlyGain) + (lateSum * _lateGain);
    }

    private static float AllPass(RingDelay delay, float input, float coefficient)
    {
        var output = delay.Read() - (coefficient * input);
        delay.ProcessWrite(input + (coefficient * output));
        return output;
    }

    private void VectorAllPass(ReadOnlySpan<float> input, Span<float> output,
        Span<float> feedback)
    {
        for (var line = 0; line < 4; line++)
        {
            output[line] = _lateAllPass[line].Read() - (_allPassCoefficient * input[line]);
            feedback[line] = input[line] + (_allPassCoefficient * output[line]);
        }

        // This scattering occurs inside the all-pass delay element in OpenAL,
        // coupling the four lines without changing the all-pass magnitude.
        Span<float> scattered = stackalloc float[4];
        Scatter(feedback, scattered, _mixX, _mixY);
        for (var line = 0; line < 4; line++)
        {
            _lateAllPass[line].ProcessWrite(scattered[line]);
        }
    }

    private static void Scatter(ReadOnlySpan<float> input, Span<float> output, float x, float y)
    {
        output[0] = (x * input[0]) + (y * (input[1] - input[2] + input[3]));
        output[1] = (x * input[1]) + (y * (-input[0] + input[2] + input[3]));
        output[2] = (x * input[2]) + (y * (input[0] - input[1] + input[3]));
        output[3] = (x * input[3]) + (y * (-input[0] - input[1] - input[2]));
    }

    private static void ScatterReverse(ReadOnlySpan<float> input, Span<float> output, float x, float y)
    {
        Span<float> reversed = stackalloc float[4] { input[3], input[2], input[1], input[0] };
        Scatter(reversed, output, x, y);
    }

    private static void Reflect(ReadOnlySpan<float> input, Span<float> output)
    {
        output[0] = (input[0] - input[1] - input[2] - input[3]) * 0.5f;
        output[1] = (input[1] - input[0] - input[2] - input[3]) * 0.5f;
        output[2] = (input[2] - input[0] - input[1] - input[3]) * 0.5f;
        output[3] = (input[3] - input[0] - input[1] - input[2]) * 0.5f;
    }

    private static float DecayCoefficient(float length, float decayTime)
    {
        return MathF.Pow(DecayGain, length / decayTime);
    }

    private static float Lerp(float first, float second, float amount)
    {
        return first + ((second - first) * amount);
    }

    private static int ToSamples(float seconds, int sampleRate, bool allowZero = false)
    {
        var samples = (int)MathF.Round(seconds * sampleRate);
        return allowZero ? Math.Max(0, samples) : Math.Max(1, samples);
    }

    private sealed class RingDelay
    {
        private readonly float[] _buffer;
        private int _index;

        internal RingDelay(int delaySamples)
        {
            _buffer = new float[Math.Max(delaySamples, 1)];
        }

        internal float Read(int delay = -1)
        {
            if (delay < 0)
            {
                delay = _buffer.Length;
            }

            var index = _index - delay;
            index %= _buffer.Length;
            if (index < 0)
            {
                index += _buffer.Length;
            }

            return _buffer[index];
        }

        internal void Write(float value)
        {
            _buffer[_index] = value;
        }

        internal void ProcessWrite(float value)
        {
            _buffer[_index] = value;
            Advance();
        }

        internal float Process(float value)
        {
            var output = _buffer[_index];
            _buffer[_index] = value;
            Advance();
            return output;
        }

        internal void Advance()
        {
            _index++;
            if (_index == _buffer.Length)
            {
                _index = 0;
            }
        }
    }
}
