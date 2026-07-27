using UnityEngine;

namespace Rill.Audio
{
    /// <summary>
    /// Water is the instrument. There are no audio files in RILL: the stream's sound is
    /// synthesised from its own state, so a trickle becomes a chuckle becomes a rush as the
    /// channel deepens. Your mountain literally sounds more alive over time — progression you
    /// can hear with your eyes closed.
    ///
    /// Attach to the AudioListener object (the camera). OnAudioFilterRead then post-processes the
    /// final mix, which is the one place a filter runs with no clip to play.
    /// </summary>
    [RequireComponent(typeof(AudioListener))]
    public sealed class FlowAudio : MonoBehaviour
    {
        [Range(0f, 1f)] public float MasterVolume = 0.55f;
        [Range(0f, 1f)] public float AmbienceVolume = 0.22f;

        // Written on the main thread, read on the audio thread. Floats are atomic here; the
        // values are smoothed in the audio callback so tearing is inaudible by construction.
        volatile float _speed01;
        volatile float _volume01;
        volatile float _polish01;
        volatile float _lakePresence;
        volatile bool _flowing;

        // Idle ambience, world-derived (AmbienceParams): the mountain's own room tone.
        volatile float _ambStream;
        volatile float _ambBirds;
        volatile float _ambWind;

        // --- audio-thread state
        float _lp1, _lp2, _bp;
        float _envFlow;
        float _grainPhase, _grainEnv, _grainFreq;
        uint _rng = 0x9e3779b9u;

        // Idle ambience voices. Separate filter states from the run's water so the murmur does
        // not inherit the run filter's cutoff sweeps.
        float _ambLp, _windLp, _windGustPhase;
        float _birdPhase, _birdEnv, _birdFreq, _birdSlide;

        struct Note { public float Freq, Env, Phase, Decay; }
        Note[] _notes = new Note[8];
        volatile int _pendingNote = -1;
        volatile float _pendingNoteFreq;
        volatile float _pendingSplash;

        int _sampleRate = 48000;

        void Awake()
        {
            _sampleRate = AudioSettings.outputSampleRate;
            if (_sampleRate <= 0) _sampleRate = 48000;
        }

        /// <summary>Called every frame while a run is live.</summary>
        public void SetFlowState(bool flowing, float speed, float maxSpeed, float volume, float startVolume, float polish)
        {
            _flowing = flowing;
            _speed01 = Mathf.Clamp01(speed / Mathf.Max(maxSpeed, 0.01f));
            _volume01 = Mathf.Clamp01(volume / Mathf.Max(startVolume, 0.01f));
            _polish01 = Mathf.Clamp01(polish);
        }

        /// <summary>Standing water in view: the mountain's resting hum, present even when idle.</summary>
        public void SetAmbientWater(float totalWaterVolume)
        {
            _lakePresence = Mathf.Clamp01(totalWaterVolume / 20000f);
        }

        /// <summary>
        /// The idle soundscape, read off the world: carved channels murmur in the distance, living
        /// slopes have birds, bare rock has wind. Between runs used to be dead air, which made the
        /// mountain read as paused; a mature mountain should be audibly different from a virgin
        /// one with your eyes closed.
        /// </summary>
        public void SetIdleAmbience(float stream01, float birds01, float wind01)
        {
            _ambStream = Mathf.Clamp01(stream01);
            _ambBirds = Mathf.Clamp01(birds01);
            _ambWind = Mathf.Clamp01(wind01);
        }

        public void Splash(float strength)
        {
            _pendingSplash = Mathf.Clamp01(strength);
        }

        /// <summary>
        /// The body of a plunge: a low, fast-dying thud under the splash's hiss. The hiss alone
        /// read as spray; weight lives below 100 Hz.
        /// </summary>
        public void Thump(float strength01)
        {
            _pendingThump = Mathf.Clamp01(strength01);
        }
        volatile float _pendingThump;

        /// <summary>A felted piano note pinned to a depth milestone. Sparse by design.</summary>
        public void DepthNote(int step)
        {
            float[] scale = { 261.63f, 293.66f, 329.63f, 392.00f, 440.00f, 523.25f, 587.33f };
            _pendingNoteFreq = scale[Mathf.Abs(step) % scale.Length];
            _pendingNote = 1;
        }

        float NextNoise()
        {
            _rng ^= _rng << 13;
            _rng ^= _rng >> 17;
            _rng ^= _rng << 5;
            return ((_rng & 0xffffff) / 8388608f) - 1f;
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            float sr = _sampleRate;
            float dt = 1f / sr;

            // Latch the volatile control state once per block.
            float speed = _speed01;
            float vol = _volume01;
            float polish = _polish01;
            bool flowing = _flowing;
            float lake = _lakePresence;

            if (_pendingNote > 0)
            {
                _pendingNote = -1;
                for (int i = 0; i < _notes.Length; i++)
                {
                    if (_notes[i].Env > 0.001f) continue;
                    _notes[i].Freq = _pendingNoteFreq;
                    _notes[i].Env = 0.9f;
                    _notes[i].Phase = 0f;
                    _notes[i].Decay = 1.6f;
                    break;
                }
            }

            float splash = _pendingSplash;
            if (splash > 0f) { _pendingSplash = 0f; _envFlow = Mathf.Max(_envFlow, splash); }

            float thump = _pendingThump;
            if (thump > 0f)
            {
                _pendingThump = 0f;
                // Rides the same note voices as the depth chimes: a 72 Hz sine dying in a third
                // of a second is a thud, not a tone.
                for (int i = 0; i < _notes.Length; i++)
                {
                    if (_notes[i].Env > 0.001f) continue;
                    _notes[i].Freq = 72f;
                    _notes[i].Env = 0.7f * thump;
                    _notes[i].Phase = 0f;
                    _notes[i].Decay = 6.5f;
                    break;
                }
            }

            float targetFlow = flowing ? Mathf.Clamp01(0.15f + speed * 0.85f) * Mathf.Clamp01(0.3f + vol) : 0f;

            for (int i = 0; i < data.Length; i += channels)
            {
                // Envelope follows the run: attack fast, release slow, so the water never clicks.
                float k = targetFlow > _envFlow ? 12f : 2.4f;
                _envFlow += (targetFlow - _envFlow) * k * dt;

                float n = NextNoise();

                // Two cascaded one-pole lowpasses: the body of moving water.
                float cutoff = Mathf.Lerp(600f, 5200f, speed) * Mathf.Lerp(1f, 1.35f, polish);
                float a = Mathf.Clamp01(cutoff * dt * 2f * Mathf.PI);
                _lp1 += (n - _lp1) * a;
                _lp2 += (_lp1 - _lp2) * a;

                // A resonant band adds the hiss of fast water over a polished bed.
                float bpCut = Mathf.Clamp01(Mathf.Lerp(2800f, 8000f, speed) * dt * 2f * Mathf.PI);
                _bp += ((n - _lp1) - _bp) * bpCut;

                float body = _lp2 * 0.9f + _bp * (0.25f + 0.5f * speed) * (0.4f + 0.6f * polish);

                // Grains: the "chuckle" of water over stones. Rate rises with speed.
                _grainEnv -= _grainEnv * 26f * dt;
                if (_grainEnv < 0.01f && NextNoise() > 0.9985f - speed * 0.0012f)
                {
                    _grainEnv = 0.35f + 0.4f * NextNoise() * 0.5f;
                    _grainFreq = 420f + 900f * (NextNoise() * 0.5f + 0.5f) * (0.5f + speed);
                    _grainPhase = 0f;
                }
                _grainPhase += _grainFreq * dt;
                float grain = Mathf.Sin(_grainPhase * 2f * Mathf.PI) * _grainEnv * 0.25f;

                float sample = (body * 1.1f + grain) * _envFlow * MasterVolume;

                // Lake hum: very low, very quiet, always there once the mountain holds water.
                if (lake > 0.001f)
                {
                    _lp1 += 0f; // keep the filter state warm
                    sample += _lp2 * lake * AmbienceVolume * 0.5f;
                }

                // Depth notes
                for (int nI = 0; nI < _notes.Length; nI++)
                {
                    if (_notes[nI].Env <= 0.001f) continue;
                    _notes[nI].Phase += _notes[nI].Freq * dt;
                    float s = Mathf.Sin(_notes[nI].Phase * 2f * Mathf.PI);
                    s += 0.28f * Mathf.Sin(_notes[nI].Phase * 4f * Mathf.PI);   // felted, not bell-like
                    sample += s * _notes[nI].Env * 0.16f * MasterVolume;
                    _notes[nI].Env -= _notes[nI].Env * _notes[nI].Decay * dt;
                }

                // --- idle ambience: the mountain's room tone, ducked while a run is loud so the
                // player's own water stays the foreground instrument.
                float duck = 1f - _envFlow * 0.8f;
                float amb = 0f;

                float stream = _ambStream;
                if (stream > 0.001f)
                {
                    // Distant water: heavily lowpassed noise, no grains — a murmur, not a river.
                    float sa = Mathf.Clamp01(700f * dt * 2f * Mathf.PI);
                    _ambLp += (n - _ambLp) * sa;
                    amb += _ambLp * stream * 0.5f;
                }

                float wind = _ambWind;
                if (wind > 0.001f)
                {
                    // Wind: darker noise with a slow gust envelope. Two LFO rates beat against
                    // each other so the gusts never loop audibly.
                    _windGustPhase += dt;
                    float gust = 0.55f + 0.45f * Mathf.Sin(_windGustPhase * 0.5f)
                                              * Mathf.Sin(_windGustPhase * 0.13f + 1.7f);
                    float wa = Mathf.Clamp01(240f * dt * 2f * Mathf.PI);
                    _windLp += (n - _windLp) * wa;
                    amb += _windLp * wind * gust * 0.6f;
                }

                float birds = _ambBirds;
                if (birds > 0.001f && !flowing)
                {
                    // Sparse chirps: a short sine with a falling slide. Trigger probability scales
                    // with how alive the mountain is — a few a minute on moss, a conversation on a
                    // wooded slope.
                    _birdEnv -= _birdEnv * 9f * dt;
                    if (_birdEnv < 0.01f && NextNoise() > 0.99997f - birds * 0.00006f)
                    {
                        _birdEnv = 0.5f + 0.3f * (NextNoise() * 0.5f + 0.5f);
                        _birdFreq = 2400f + 1800f * (NextNoise() * 0.5f + 0.5f);
                        _birdSlide = -_birdFreq * (0.3f + 0.5f * (NextNoise() * 0.5f + 0.5f));
                        _birdPhase = 0f;
                    }
                    _birdFreq += _birdSlide * dt;
                    if (_birdFreq < 600f) _birdEnv = 0f;
                    _birdPhase += _birdFreq * dt;
                    amb += Mathf.Sin(_birdPhase * 2f * Mathf.PI) * _birdEnv * 0.10f;
                }

                sample += amb * duck * AmbienceVolume * MasterVolume;

                sample = Mathf.Clamp(sample, -1f, 1f);
                for (int c = 0; c < channels; c++) data[i + c] += sample;
            }
        }
    }
}
