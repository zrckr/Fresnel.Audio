# Third-party notices

## OpenAL Soft 1.25.2

The DSP implementations under `Fresnel.Audio/Effects` contain C# translations
and adaptations of OpenAL Soft 1.25.2 source code from `alc/effects` and
`core/filters`.

Upstream project: https://github.com/kcat/openal-soft

The chorus, flanger, distortion, echo, equalizer, ring modulator, reverb,
biquad, and cubic interpolation code is licensed under the GNU Library General
Public License, version 2 or (at your option) any later version. The complete
license is distributed in `licenses/LGPL-2.0-or-later.txt`.

Relevant upstream copyright notices:

- Chorus, flanger, distortion, and equalizer: Copyright (C) 2013 Mike Gorchak.
- Echo and ring modulator: Copyright (C) 2009 Chris Robinson.
- Reverb: Copyright (C) 2008-2017 Chris Robinson and Christopher Fitzgerald.

The compressor implementation is adapted from OpenAL Soft's separately
BSD-3-Clause-licensed `alc/effects/compressor.cpp`:

> Copyright (C) 2013 Anis A. Hireche

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice,
   this list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.
3. Neither the name of Spherical-Harmonic-Transform nor the names of its
   contributors may be used to endorse or promote products derived from this
   software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
POSSIBILITY OF SUCH DAMAGE.

### Fresnel adaptations

The upstream C++ was translated to managed C#, changed from OpenAL's
ambisonic wet-effect pipeline to Fresnel's interleaved direct-channel insert
pipeline, and integrated with Fresnel's existing public effect settings and
dry/wet behavior on 18 September 2026.

The repository's MIT license continues to describe original Fresnel code.
Distributions containing the translated OpenAL Soft DSP must also comply with
the LGPL-2.0-or-later terms. This notice is informational and is not legal
advice.
