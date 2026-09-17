namespace Fresnel.Audio;

/// <summary>
/// Controls when supported encoded audio is decoded.
/// </summary>
public enum AudioLoadMode
{
    /// <summary>
    /// Decodes audio before it is loaded.
    /// </summary>
    Decoded,

    /// <summary>
    /// Keeps supported audio formats encoded until SDL_mixer needs them.
    /// </summary>
    /// <remarks>
    /// QOA audio is always decoded because SDL_mixer has no QOA decoder.
    /// </remarks>
    Encoded
}
