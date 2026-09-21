using System.Collections;

namespace Fresnel.Audio;

/// <summary>
/// A mutable collection of effects processed by an audio bus.
/// </summary>
public sealed class EffectCollection : IList<AudioEffect>
{
    private List<AudioEffect> _effects = [];

    private readonly List<Registration> _registrations = [];

    /// <inheritdoc/>
    public AudioEffect this[int index]
    {
        get => _effects[index];
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (_effects[index] != value)
            {
                Change(updated => updated[index] = value);
            }
        }
    }

    /// <inheritdoc/>
    public int Count => _effects.Count;

    /// <inheritdoc/>
    public bool IsReadOnly => false;

    /// <inheritdoc/>
    public void Add(AudioEffect item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Change(updated => updated.Add(item));
    }

    /// <inheritdoc/>
    public void Clear()
    {
        if (_effects.Count > 0)
        {
            Change(updated => updated.Clear());
        }
    }

    /// <inheritdoc/>
    public bool Contains(AudioEffect item)
    {
        return _effects.Contains(item);
    }

    /// <inheritdoc/>
    public void CopyTo(AudioEffect[] array, int arrayIndex)
    {
        _effects.CopyTo(array, arrayIndex);
    }

    /// <inheritdoc/>
    public IEnumerator<AudioEffect> GetEnumerator()
    {
        return _effects.GetEnumerator();
    }

    /// <inheritdoc/>
    public int IndexOf(AudioEffect item)
    {
        return _effects.IndexOf(item);
    }

    /// <inheritdoc/>
    public void Insert(int index, AudioEffect item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Change(updated => updated.Insert(index, item));
    }

    /// <inheritdoc/>
    public bool Remove(AudioEffect item)
    {
        var index = _effects.IndexOf(item);
        if (index < 0)
        {
            return false;
        }

        Change(updated => updated.RemoveAt(index));
        return true;
    }

    /// <inheritdoc/>
    public void RemoveAt(int index)
    {
        Change(updated => updated.RemoveAt(index));
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    internal void Apply(EffectCollection configuration)
    {
        if (!ReferenceEquals(this, configuration) && !_effects.SequenceEqual(configuration._effects))
        {
            Change(updated =>
            {
                updated.Clear();
                updated.AddRange(configuration._effects);
            });
        }
    }

    internal Registration Register(int sampleRate, int channels)
    {
        Validate(_effects);
        var registration = new Registration(this, CreateProcessors(_effects, sampleRate, channels), sampleRate,
            channels);
        _registrations.Add(registration);
        return registration;
    }

    private void Change(Action<List<AudioEffect>> change)
    {
        var updated = new List<AudioEffect>(_effects);
        change(updated);

        Validate(updated);

        var replacements = new EffectProcessor[_registrations.Count][];
        for (var index = 0; index < _registrations.Count; index++)
        {
            var registration = _registrations[index];
            replacements[index] = CreateProcessors(updated, registration.SampleRate, registration.Channels);
        }

        _effects = updated;
        for (var index = 0; index < _registrations.Count; index++)
        {
            _registrations[index].Replace(replacements[index]);
        }
    }

    private static void Validate(List<AudioEffect> effects)
    {
        foreach (var effect in effects)
        {
            effect.Validate();
        }
    }

    private static EffectProcessor[] CreateProcessors(List<AudioEffect> effects, int sampleRate, int channels)
    {
        var processors = new EffectProcessor[effects.Count];
        for (var index = 0; index < effects.Count; index++)
        {
            processors[index] = effects[index].CreateProcessor(sampleRate, channels);
        }

        return processors;
    }

    internal sealed class Registration : IDisposable
    {
        private readonly EffectCollection _collection;

        private EffectProcessor[] _processors;

        internal int SampleRate { get; }

        internal int Channels { get; }

        internal Registration(EffectCollection collection, EffectProcessor[] processors, int sampleRate, int channels)
        {
            _collection = collection;
            _processors = processors;
            SampleRate = sampleRate;
            Channels = channels;
        }

        public void Dispose()
        {
            _collection._registrations.Remove(this);
        }

        internal void Process(Span<float> pcm)
        {
            foreach (var processor in Volatile.Read(ref _processors))
            {
                processor.Process(pcm);
            }
        }

        internal void Replace(EffectProcessor[] replacement)
        {
            Volatile.Write(ref _processors, replacement);
        }
    }
}
