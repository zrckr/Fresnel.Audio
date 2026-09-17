// DSP implementation adapted from OpenAL Soft 1.25.2.
// Source: alc/effects/reverb.cpp.
// Upstream notice: Copyright (C) 2008-2017 Chris Robinson and Christopher Fitzgerald.
// Adapted portions are licensed under LGPL-2.0-or-later.
// See THIRD-PARTY-NOTICES.md for attribution and license terms.

namespace Fresnel.Audio;

/// <summary>
/// Configures a four-line feedback-delay-network reverb.
/// </summary>
public readonly record struct ReverbEffect() : IEffect
{
    /// <summary>
    /// Gets the linear input gain of the wet reverb path.
    /// </summary>
    /// <value>A value from 0 to 1.</value>
    public float Gain { get; init; } = 0.32f;

    /// <summary>
    /// Gets the relative high-frequency gain applied to the reverb input.
    /// </summary>
    /// <value>A value from 0 to 1.</value>
    public float HighGain { get; init; } = 0.89f;

    /// <summary>
    /// Gets the modal density used to scale the internal delay-line lengths.
    /// </summary>
    /// <value>A value from 0 to 1.</value>
    public float Density { get; init; } = 1f;

    /// <summary>
    /// Gets the amount of scattering between the reverb delay lines.
    /// </summary>
    /// <value>A value from 0 for sparse reflections to 1 for maximum diffusion.</value>
    public float Diffusion { get; init; } = 1f;

    /// <summary>
    /// Gets the time for the mid-frequency reverberation to decay by 60 decibels.
    /// </summary>
    /// <value>A duration from 0.1 to 20 seconds.</value>
    public TimeSpan DecayTime { get; init; } = TimeSpan.FromSeconds(1.49f);

    /// <summary>
    /// Gets the ratio of the high-frequency decay time to <see cref="DecayTime"/>.
    /// </summary>
    /// <value>A value from 0.1 to 2.</value>
    public float DecayHighRatio { get; init; } = 0.83f;

    /// <summary>
    /// Gets the linear output gain of the early reflections.
    /// </summary>
    /// <value>
    /// A value from 0 to 3.16.
    /// </value>
    public float EarlyGain { get; init; } = 0.05f;

    /// <summary>
    /// Gets the delay from the dry signal to the first early reflection.
    /// </summary>
    /// <value>
    /// A duration from zero to 300 milliseconds.
    /// </value>
    public TimeSpan EarlyDelay { get; init; } = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Gets the linear output gain of the late reverberation.
    /// </summary>
    /// <value>
    /// A value from 0 to 10.
    /// </value>
    public float LateGain { get; init; } = 1.26f;

    /// <summary>
    /// Gets the delay from the early-reflection input to the late reverberation.
    /// </summary>
    /// <value>
    /// A duration from zero to 100 milliseconds.
    /// </value>
    public TimeSpan LateDelay { get; init; } = TimeSpan.FromMilliseconds(11);

    /// <summary>
    /// Gets the high-frequency gain retained per meter of air propagation.
    /// </summary>
    /// <remarks>
    /// This setting limits high-frequency decay when <see cref="HighLimit"/> is enabled.
    /// </remarks>
    /// <value>
    /// A value from 0.892 to 1.
    /// </value>
    public float AirAbsorption { get; init; } = 0.994f;

    /// <summary>
    /// Gets whether air absorption places an upper limit on high-frequency decay time.
    /// </summary>
    public bool HighLimit { get; init; } = true;

    /// <inheritdoc/>
    public void Validate()
    {
        EffectValidation.Range(Gain, 0f, 1f, nameof(Gain));
        EffectValidation.Range(HighGain, 0f, 1f, nameof(HighGain));
        EffectValidation.Range(Density, 0f, 1f, nameof(Density));
        EffectValidation.Range(Diffusion, 0f, 1f, nameof(Diffusion));
        EffectValidation.Range(DecayTime, TimeSpan.FromSeconds(0.1), TimeSpan.FromSeconds(20), nameof(DecayTime));
        EffectValidation.Range(DecayHighRatio, 0.1f, 2f, nameof(DecayHighRatio));
        EffectValidation.Range(EarlyGain, 0f, 3.16f, nameof(EarlyGain));
        EffectValidation.Range(EarlyDelay, TimeSpan.Zero, TimeSpan.FromSeconds(0.3), nameof(EarlyDelay));
        EffectValidation.Range(LateGain, 0f, 10f, nameof(LateGain));
        EffectValidation.Range(LateDelay, TimeSpan.Zero, TimeSpan.FromSeconds(0.1), nameof(LateDelay));
        EffectValidation.Range(AirAbsorption, 0.892f, 1f, nameof(AirAbsorption));
    }
}

internal sealed class ReverbProcessor : EffectProcessor
{
    private readonly int _channels;

    private readonly float _gain;

    private readonly OpenAlReverb[] _reverbs;

    public ReverbProcessor(ReverbEffect effect, int sampleRate, int channels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channels);
        _channels = channels;
        _gain = effect.Gain;
        _reverbs = new OpenAlReverb[channels];
        for (var channel = 0; channel < channels; channel++)
        {
            _reverbs[channel] = new OpenAlReverb(effect, sampleRate);
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
            pcm[index] += _reverbs[channel].Process(pcm[index], _gain);
        }
    }
}
