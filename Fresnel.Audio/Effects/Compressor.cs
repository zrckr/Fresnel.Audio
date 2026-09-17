// DSP implementation adapted from OpenAL Soft 1.25.2.
// Source: alc/effects/compressor.cpp.
// Upstream notice: Copyright (C) 2013 Anis A. Hireche.
// Adapted portions are licensed under BSD-3-Clause.
// See THIRD-PARTY-NOTICES.md for attribution and license terms.

namespace Fresnel.Audio;

/// <summary>
/// Configures an automatic gain effect that compresses the signal's dynamic range.
/// </summary>
public readonly record struct CompressorEffect() : IEffect
{
    /// <summary>
    /// Gets whether dynamic-range compression is applied.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <inheritdoc/>
    public void Validate()
    {
    }
}

internal sealed class CompressorProcessor : EffectProcessor
{
    private const float EnvelopeMinimum = 0.5f;

    private const float EnvelopeMaximum = 2f;

    private const float AttackSeconds = 0.1f;

    private const float ReleaseSeconds = 0.2f;

    private readonly float _attackMultiplier;

    private readonly int _channels;

    private readonly bool _enabled;

    private readonly float _releaseMultiplier;

    private float _envelope = 1f;

    public CompressorProcessor(CompressorEffect effect, int sampleRate, int channels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channels);

        _channels = channels;
        _enabled = effect.Enabled;
        _attackMultiplier = MathF.Pow(EnvelopeMaximum / EnvelopeMinimum, 1f / (sampleRate * AttackSeconds));
        _releaseMultiplier = MathF.Pow(EnvelopeMinimum / EnvelopeMaximum, 1f / (sampleRate * ReleaseSeconds));
    }

    public override void Process(Span<float> pcm)
    {
        if (pcm.Length % _channels != 0)
        {
            throw new ArgumentException("PCM data must contain complete sample frames.", nameof(pcm));
        }

        if (!_enabled)
        {
            return;
        }

        for (var frameOffset = 0; frameOffset < pcm.Length; frameOffset += _channels)
        {
            var peak = 0f;
            for (var channel = 0; channel < _channels; channel++)
            {
                peak = Math.Max(peak, MathF.Abs(pcm[frameOffset + channel]));
            }

            var amplitude = Math.Clamp(peak, EnvelopeMinimum, EnvelopeMaximum);
            if (amplitude > _envelope)
            {
                _envelope = Math.Min(_envelope * _attackMultiplier, amplitude);
            }
            else if (amplitude < _envelope)
            {
                _envelope = Math.Max(_envelope * _releaseMultiplier, amplitude);
            }

            var gain = 1f / _envelope;

            for (var channel = 0; channel < _channels; channel++)
            {
                pcm[frameOffset + channel] *= gain;
            }
        }
    }
}
