// DSP implementation adapted from OpenAL Soft 1.25.2.
// See THIRD-PARTY-NOTICES.md for attribution and license terms.

namespace Fresnel.Audio;

/// <summary>
/// Configures a four-line feedback-delay-network reverb.
/// </summary>
public sealed record ReverbEffect : AudioEffect
{
    /// <summary>
    /// Gets the time for reverberation to decay by 60 decibels.
    /// </summary>
    /// <value>
    /// A duration from 0.1 to 20 seconds.
    /// </value>
    public TimeSpan Decay { get; init; } = TimeSpan.FromSeconds(1.49f);

    /// <summary>
    /// Gets the perceived size and modal density of the room.
    /// </summary>
    /// <value>
    /// A value from 0 to 1.
    /// </value>
    public float RoomSize { get; init; } = 1f;

    /// <summary>
    /// Gets the high-frequency absorption of the room.
    /// </summary>
    /// <value>
    /// A value from 0 for bright to 1 for dark.
    /// </value>
    public float Damping { get; init; } = 0.2f;

    /// <summary>
    /// Gets the dry/wet balance.
    /// </summary>
    /// <value>
    /// A value from 0 for dry to 1 for wet.
    /// </value>
    public float Mix { get; init; } = 0.5f;

    internal override void Validate()
    {
        EffectValidation.Range(Decay, TimeSpan.FromSeconds(0.1), TimeSpan.FromSeconds(20), nameof(Decay));
        EffectValidation.Range(RoomSize, 0f, 1f, nameof(RoomSize));
        EffectValidation.Range(Damping, 0f, 1f, nameof(Damping));
        EffectValidation.Range(Mix, 0f, 1f, nameof(Mix));
    }

    internal override EffectProcessor CreateProcessor(int sampleRate, int channels)
    {
        return new ReverbProcessor(this, sampleRate, channels);
    }
}

internal sealed class ReverbProcessor : EffectProcessor
{
    private const float InputGain = 0.32f;

    private readonly DryWetMix _mix;

    private readonly OpenAlReverb[] _reverbs;

    public ReverbProcessor(ReverbEffect effect, int sampleRate, int channels) : base(sampleRate, channels)
    {
        _mix = new DryWetMix(effect.Mix);
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
            var dry = pcm[index];
            var wet = _reverbs[index % _channels].Process(dry, InputGain);
            pcm[index] = _mix.Blend(dry, wet);
        }
    }
}
