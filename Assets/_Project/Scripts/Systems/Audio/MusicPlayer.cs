using UnityEngine;

namespace RulesShift
{
    /// <summary>
    /// Music with a short crossfade. Real tracks (see MusicTracks) loop on their loop points; a key with
    /// variants plays one of them; keys without a track use the procedural chiptune (ProcMusic).
    /// Beat follows the playing track when its tempo is known (Neon), otherwise a steady metronome.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class MusicPlayer : MonoBehaviour
    {
        public static MusicPlayer I { get; private set; }

        [Range(0f, 1f)] public float volume = 0.4f;
        [Tooltip("Beat used for the Neon law while a real track (not the chiptune) is playing.")]
        public float fallbackBpm = 120f;
        public float fadeTime = 0.6f;

        AudioSource a, b;
        AudioSource active;
        string currentKey;
        /// <summary>Key of the music playing now (before variants).</summary>
        public string CurrentKey => currentKey;
        float bpm = 120f;
        float keyVolume = 1f;
        bool procedural;
        bool synced;          // Beat is read from the playing clip
        float beatOffset;
        MusicTracks.Track track;
        float metronomeStart;
        float lastBeatFrac;

        void Awake()
        {
            I = this;
            a = NewSource();
            b = NewSource();
            active = a;
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        AudioSource NewSource()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.loop = true;
            s.playOnAwake = false;
            s.volume = 0f;
            return s;
        }

        /// <summary>The track for a key: one of its variants that is in the project (a different one than last time when possible).</summary>
        string Resolve(string key)
        {
            var options = MusicTracks.Variants(key);
            if (options.Length == 0 || !GameAssets.I) return key;
            var have = new System.Collections.Generic.List<string>();
            foreach (var o in options) if (GameAssets.I.Music(o)) have.Add(o);
            if (have.Count == 0) return key;
            if (have.Count > 1 && lastPick.TryGetValue(key, out var last)) have.Remove(last);
            string pick = have[Random.Range(0, have.Count)];
            lastPick[key] = pick;
            return pick;
        }

        readonly System.Collections.Generic.Dictionary<string, string> lastPick = new System.Collections.Generic.Dictionary<string, string>();

        public void Play(string key)
        {
            if (key == currentKey) return;
            currentKey = key;
            string resolved = Resolve(key);

            var file = GameAssets.I ? GameAssets.I.Music(resolved) : null;
            AudioClip clip;
            if (file)
            {
                clip = file;
                procedural = false;
                var info = MusicTracks.Get(resolved);
                keyVolume = info != null ? info.gain : 1f;
                synced = info != null && info.bpm > 0f;
                bpm = synced ? info.bpm : fallbackBpm;
                beatOffset = synced ? info.beatOffset : 0f;
                track = info != null && info.loopLength > 0f ? info : null;   // otherwise loops as a whole file
            }
            else
            {
                clip = ProcMusic.Get(key != null && key.StartsWith("neon") ? "neon" : key, out bpm);
                keyVolume = key == "treasury" ? 0.7f : key == "crypt" ? 0.8f : 1f;
                procedural = true;
                synced = true;
                beatOffset = 0f;
                track = null;
            }
            metronomeStart = Time.time;

            var next = active == a ? b : a;
            next.clip = clip;
            next.loop = track == null;
            next.time = 0f;
            next.volume = 0f;
            next.Play();
            active = next;
        }

        AudioSource jingleSource;
        float jingleEnd, duck = 1f, jingleGain = 1f;

        /// <summary>
        /// A short one-shot over the music (Peaceful Night, Never Give Up): the music ducks while it plays.
        /// Missing clip = nothing happens.
        /// </summary>
        public void Jingle(string key)
        {
            var clip = GameAssets.I ? GameAssets.I.Music(key) : null;
            if (!clip) return;
            if (!jingleSource)
            {
                jingleSource = gameObject.AddComponent<AudioSource>();
                jingleSource.playOnAwake = false;
                jingleSource.loop = false;
            }
            var info = MusicTracks.Get(key);
            jingleGain = info != null ? info.gain : 1f;
            jingleSource.clip = clip;
            jingleSource.volume = volume * jingleGain;
            jingleSource.Play();
            jingleEnd = Time.unscaledTime + clip.length;
        }

        public void Stop()
        {
            currentKey = null;
            active = null;
        }

        void Update()
        {
            float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeTime);
            // a jingle ducks the music and lets it back in during its last half second
            duck = Mathf.MoveTowards(duck, Time.unscaledTime < jingleEnd - 0.5f ? 0.15f : 1f, Time.unscaledDeltaTime * 3f);
            if (jingleSource && jingleSource.isPlaying) jingleSource.volume = volume * jingleGain;
            Fade(a, a == active ? volume * keyVolume * duck : 0f, step);
            Fade(b, b == active ? volume * keyVolume * duck : 0f, step);

            // seamless loop: after the intro, jump back to the loop start
            if (active && track != null && active.clip)
            {
                if (active.isPlaying)
                {
                    float end = Mathf.Min(track.loopStart + track.loopLength, active.clip.length - 0.02f);
                    if (active.time >= end) active.time = Mathf.Max(0f, active.time - track.loopLength);
                }
                else if (active.volume > 0f)
                {
                    active.time = track.loopStart;
                    active.Play();
                }
            }

            // the Neon law: a soft tick on every beat (the fraction wraps; a loop jump keeps it, so no double ticks)
            float beat = Beat;
            float frac = beat - Mathf.Floor(beat);
            if (Rules.BeatMove && !procedural && frac < lastBeatFrac) Sfx.Play("beat", 0.3f);
            lastBeatFrac = frac;
        }

        static void Fade(AudioSource s, float target, float step)
        {
            s.volume = Mathf.MoveTowards(s.volume, target, step);
            if (s.volume <= 0f && s.isPlaying && target <= 0f) s.Stop();
        }

        /// <summary>Beats since the music started (fractional): from the clip when its tempo is known, else a metronome.</summary>
        public static float Beat
        {
            get
            {
                if (I == null) return Time.time * 2f;
                if (I.synced && I.active != null && I.active.isPlaying && I.active.clip)
                    return (I.active.timeSamples / (float)I.active.clip.frequency - I.beatOffset - Latency) * I.bpm / 60f;
                return (Time.time - I.metronomeStart) * I.bpm / 60f;
            }
        }

        public static float Bpm => I != null ? I.bpm : 120f;

        static float latency = -1f, latencyCheck;

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern float RulecrawlAudioLatency();
#endif

        /// <summary>
        /// The clip position runs ahead of the speakers; the beat should match what is heard. Desktop: the audio
        /// buffer. Browser: the Web Audio output latency (it changes with headphones, so it is read again every
        /// two seconds). Plus the player's own correction from the settings (Bluetooth headphones are late).
        /// </summary>
        static float Latency
        {
            get
            {
                float user = SaveSystem.Data.beatOffsetMs / 1000f;
#if UNITY_WEBGL && !UNITY_EDITOR
                if (latency < 0f || Time.unscaledTime > latencyCheck)
                {
                    latencyCheck = Time.unscaledTime + 2f;
                    float l = RulecrawlAudioLatency();
                    latency = l >= 0f ? Mathf.Clamp(l, 0f, 0.4f) : 0.1f;
                }
                return latency + user;
#else
                if (latency < 0f)
                {
                    AudioSettings.GetDSPBufferSize(out int length, out int buffers);
                    int rate = AudioSettings.outputSampleRate;
                    latency = rate > 0 ? Mathf.Clamp(length * 2f / rate, 0f, 0.12f) : 0.04f;
                }
                return latency + user;
#endif
            }
        }
    }
}
