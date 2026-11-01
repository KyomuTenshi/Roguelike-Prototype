using UnityEngine;

namespace RulesShift
{
    /// <summary>
    /// Music with a short crossfade. Real tracks (xDeviruchi pack, see MusicTracks) loop on their
    /// documented loop points; keys without a track use the procedural chiptune (ProcMusic).
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

        public void Play(string key)
        {
            if (key == currentKey) return;
            currentKey = key;

            var file = GameAssets.I ? GameAssets.I.Music(key) : null;
            AudioClip clip;
            if (file)
            {
                clip = file;
                procedural = false;
                var info = MusicTracks.Get(key);
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

        public void Stop()
        {
            currentKey = null;
            active = null;
        }

        void Update()
        {
            float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeTime);
            Fade(a, a == active ? volume * keyVolume : 0f, step);
            Fade(b, b == active ? volume * keyVolume : 0f, step);

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

        static float latency = -1f;

        /// <summary>The clip position runs ahead of the speakers by the audio buffer; the beat should match what is heard.</summary>
        static float Latency
        {
            get
            {
                if (latency >= 0f) return latency;
                AudioSettings.GetDSPBufferSize(out int length, out int buffers);
                int rate = AudioSettings.outputSampleRate;
                latency = rate > 0 ? Mathf.Clamp(length * 2f / rate, 0f, 0.12f) : 0.04f;
                return latency;
            }
        }
    }
}
