// DSP implementation adapted from OpenAL Soft 1.25.2.
// Source: alc/effects/chorus.cpp and core/cubic_tables.hpp.
// Upstream notice: Copyright (C) 2013 Mike Gorchak.
// Adapted portions are licensed under LGPL-2.0-or-later.
// See THIRD-PARTY-NOTICES.md for attribution and license terms.

namespace Fresnel.Audio;

/// <summary>
/// Configures a chorus effect produced by a modulated delay line.
/// </summary>
public readonly record struct ChorusEffect() : IEffect
{
    /// <summary>
    /// Gets or sets the low-frequency oscillator waveform.
    /// </summary>
    public EffectWaveform Waveform { get; init; } = EffectWaveform.Triangle;

    /// <summary>
    /// Gets or sets the right-channel oscillator phase offset in degrees.
    /// </summary>
    /// <value>
    /// A value from -180 to 180.
    /// </value>
    public float PhaseDegrees { get; init; } = 90f;

    /// <summary>
    /// Gets or sets the low-frequency oscillator rate in hertz.
    /// </summary>
    /// <value>
    /// A value from 0 to 10.
    /// </value>
    public float RateHz { get; init; } = 1.1f;

    /// <summary>
    /// Gets or sets modulation depth as a proportion of <see cref="Delay"/>.
    /// </summary>
    /// <value>
    /// A value from 0 to 1.
    /// </value>
    public float Depth { get; init; } = 0.1f;

    /// <summary>
    /// Gets or sets the delayed-signal feedback amount.
    /// </summary>
    /// <value>
    /// A value from -1 to 1.
    /// </value>
    public float Feedback { get; init; } = 0.25f;

    /// <summary>
    /// Gets or sets the base delay before modulation.
    /// </summary>
    /// <value>
    /// A value from zero to 16 milliseconds.
    /// </value>
    public TimeSpan Delay { get; init; } = TimeSpan.FromMilliseconds(16);

    /// <inheritdoc/>
    public void Validate()
    {
        EffectValidation.Waveform(Waveform, nameof(Waveform));
        EffectValidation.Range(PhaseDegrees, -180f, 180f, nameof(PhaseDegrees));
        EffectValidation.Range(RateHz, 0f, 10f, nameof(RateHz));
        EffectValidation.Range(Depth, 0f, 1f, nameof(Depth));
        EffectValidation.Range(Feedback, -1f, 1f, nameof(Feedback));
        EffectValidation.Range(Delay, TimeSpan.Zero, TimeSpan.FromMilliseconds(16), nameof(Delay));
    }
}

internal sealed class ChorusProcessor : EffectProcessor
{
    private const float MaximumDelaySeconds = 0.016f;

    private readonly int _channels;

    private readonly ModulatedDelay _delay;

    public ChorusProcessor(ChorusEffect effect, int sampleRate, int channels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channels);
        _channels = channels;
        _delay = new ModulatedDelay(effect.Waveform, effect.PhaseDegrees, effect.RateHz, effect.Depth,
            effect.Feedback, effect.Delay, MaximumDelaySeconds, sampleRate, channels);
    }

    public override void Process(Span<float> pcm)
    {
        if (pcm.Length % _channels != 0)
        {
            throw new ArgumentException("PCM data must contain complete sample frames.", nameof(pcm));
        }

        _delay.Process(pcm);
    }
}
