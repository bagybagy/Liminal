using System;
using UnityEngine;

namespace Liminal
{
    public sealed class MusicTransport : MonoBehaviour
    {
        public AudioClip soundtrack;
        AudioSource music;
        readonly AudioSource[] voices = new AudioSource[32];
        readonly double[] voiceEnds = new double[32];
        readonly AudioClip[] notes = new AudioClip[8];
        AudioClip impact, lockTone;
        double origin;
        public bool Paused { get; private set; }
        public double Time => Math.Max(0, AudioSettings.dspTime - origin);
        public double DspOrigin => origin;
        public float Volume { get; private set; } = 0.8f;
        public int ScheduledNotes { get; private set; }
        public double MaxGridError { get; private set; }

        public void Initialize()
        {
            music = gameObject.AddComponent<AudioSource>();
            music.clip = soundtrack;
            music.playOnAwake = false;
            music.volume = 0.83f;
            for (int i = 0; i < voices.Length; i++) {
                voices[i] = gameObject.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
            }
            for (int i = 0; i < notes.Length; i++) notes[i] = Synthesize(Score.Scale[i], 1.8f, false);
            impact = Synthesize(38, 0.4f, true);
            lockTone = Synthesize(98, 0.1f, false);
            SetVolume(PlayerPrefs.GetFloat("volume", 0.8f));
            Restart();
        }

        public void Restart()
        {
            SetPaused(false);
            music.Stop();
            for (int i = 0; i < voices.Length; i++) { voices[i].Stop(); voiceEnds[i] = 0; }
            origin = AudioSettings.dspTime + 0.3;
            ScheduledNotes = 0;
            MaxGridError = 0;
            music.PlayScheduled(origin);
        }

        public void ScheduleNote(int index, double songTime, float pan, float strength = 1)
        {
            Play(notes[index % notes.Length], origin + songTime, pan, 0.62f * strength);
            double eighth = songTime / (Score.BeatSeconds * 0.5);
            MaxGridError = Math.Max(MaxGridError, Math.Abs(eighth - Math.Round(eighth)) * Score.BeatSeconds * 0.5);
            ScheduledNotes++;
        }

        public void LockSound() => Play(lockTone, AudioSettings.dspTime + 0.01, 0, 0.06f);
        public void DamageSound() => Play(impact, AudioSettings.dspTime + 0.01, 0, 0.6f);

        void Play(AudioClip clip, double dspTime, float pan, float volume)
        {
            int voice = -1;
            for (int i = 0; i < voices.Length; i++) if (voiceEnds[i] < AudioSettings.dspTime) { voice = i; break; }
            if (voice < 0) return;
            var source = voices[voice];
            source.clip = clip;
            source.panStereo = Mathf.Clamp(pan, -0.75f, 0.75f);
            source.volume = volume;
            source.PlayScheduled(dspTime);
            voiceEnds[voice] = dspTime + clip.length;
        }

        public void SetPaused(bool value) { Paused = value; AudioListener.pause = value; }
        public void SetVolume(float value) { Volume = Mathf.Clamp01(value); AudioListener.volume = Volume; }
        void OnDestroy() { AudioListener.pause = false; foreach (var clip in notes) if (clip) Destroy(clip); if (impact) Destroy(impact); if (lockTone) Destroy(lockTone); }

        static AudioClip Synthesize(int midi, float length, bool low)
        {
            const int rate = 44100;
            int frames = Mathf.CeilToInt(rate * length);
            var samples = new float[frames * 2];
            double frequency = 440 * Math.Pow(2, (midi - 69) / 12.0);
            for (int i = 0; i < frames; i++) {
                double t = (double)i / rate, p = 2 * Math.PI * frequency * t;
                double envelope = Math.Min(t / 0.004, 1) * Math.Exp(-t * (low ? 15 : 4.5)) * Math.Min((length - t) / 0.03, 1);
                double signal = Math.Sin(p + Math.Sin(p * 2) * Math.Exp(-t * 8) * (low ? 3 : 1.6));
                float v = (float)(signal * envelope * 0.38);
                samples[i * 2] = v;
                samples[i * 2 + 1] = v * (float)(0.97 + 0.03 * Math.Sin(t * 12));
            }
            AudioClip clip = AudioClip.Create("Liminal_" + midi, frames, 2, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
