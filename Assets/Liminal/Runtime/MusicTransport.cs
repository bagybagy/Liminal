using System;
using System.Collections.Generic;
using UnityEngine;

namespace Liminal
{
    public sealed class MusicTransport : MonoBehaviour
    {
        public AudioClip soundtrack;
        public bool LoopSoundtrack;
        AudioSource music;
        readonly AudioSource[] voices = new AudioSource[64];
        readonly double[] voiceEnds = new double[64];
        readonly Dictionary<int, AudioClip> notes = new();
        AudioClip impact, lockTone,whaleTone;
        double origin;
        public bool Paused { get; private set; }
        public double Time => Math.Max(0, AudioSettings.dspTime - origin);
        public double DspOrigin => origin;
        public float Volume { get; private set; } = 0.8f;
        public int ScheduledNotes { get; private set; }
        public double MaxGridError { get; private set; }
        public double MaxPlaybackPhaseError { get; private set; }
        public int DroppedNotes { get; private set; }

        public void Initialize()
        {
            music = gameObject.AddComponent<AudioSource>();
            music.clip = soundtrack;
            music.playOnAwake = false;
            music.loop = LoopSoundtrack;
            music.volume = 0.83f;
            for (int i = 0; i < voices.Length; i++) {
                voices[i] = gameObject.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
            }
            if (soundtrack.frequency != AuthoredScore.Data.sampleRate || soundtrack.samples != AuthoredScore.Data.sampleCount)
                throw new InvalidOperationException("Audio samples do not match the authored timeline.");
            foreach (var chord in AuthoredScore.Data.harmony)
                for (int i = 0; i < 8; i++) {
                    int midi = chord.notes[i % chord.notes.Length] + 12 + 12 * (i / chord.notes.Length);
                    if (!notes.ContainsKey(midi)) notes.Add(midi, Synthesize(midi, 1.8f, false));
                }
            impact = Synthesize(38, 0.4f, true);
            lockTone = Synthesize(98, 0.1f, false);
            whaleTone = MakeWhaleCall();
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
            MaxPlaybackPhaseError = 0;
            DroppedNotes = 0;
            music.PlayScheduled(origin);
        }

        public bool ScheduleNote(int index, double songTime, float pan, float strength = 1)
        {
            if (!Play(notes[AuthoredScore.Note(index % 8, songTime)], origin + songTime, pan, 0.62f * strength)) {
                DroppedNotes++; return false;
            }
            MaxGridError = Math.Max(MaxGridError, AuthoredScore.GridError(songTime));
            ScheduledNotes++;
            return true;
        }

        void Update()
        {
            if (!music || !music.isPlaying || Paused || AudioSettings.dspTime <= origin + .1) return;
            double expected = LoopSoundtrack ? Time % AuthoredScore.Duration : Time;
            double error = Math.Abs(music.timeSamples / (double)soundtrack.frequency - expected);
            if (LoopSoundtrack) error = Math.Min(error, AuthoredScore.Duration - error);
            MaxPlaybackPhaseError = Math.Max(MaxPlaybackPhaseError, Math.Abs(error));
        }

        public void LockSound() => Play(lockTone, AudioSettings.dspTime + 0.01, 0, 0.06f);
        public void DamageSound() => Play(impact, AudioSettings.dspTime + 0.01, 0, 0.6f);
        public void WhaleCall() => Play(whaleTone,origin+Score.NextEighth(Time,.15),0,.7f);

        bool Play(AudioClip clip, double dspTime, float pan, float volume)
        {
            int voice = -1;
            for (int i = 0; i < voices.Length; i++) if (voiceEnds[i] < AudioSettings.dspTime) { voice = i; break; }
            if (voice < 0 || dspTime < AudioSettings.dspTime) return false;
            var source = voices[voice];
            source.clip = clip;
            source.panStereo = Mathf.Clamp(pan, -0.75f, 0.75f);
            source.volume = volume;
            source.PlayScheduled(dspTime);
            voiceEnds[voice] = dspTime + clip.length;
            return true;
        }

        public void SetPaused(bool value) { Paused = value; AudioListener.pause = value; }
        public void SetVolume(float value) { Volume = Mathf.Clamp01(value); AudioListener.volume = Volume; }
        void OnDestroy() { AudioListener.pause = false; foreach (var clip in notes.Values) if (clip) Destroy(clip); if (impact) Destroy(impact); if (lockTone) Destroy(lockTone); if(whaleTone) Destroy(whaleTone); }

        static AudioClip MakeWhaleCall()
        {
            const int rate=44100,frames=rate*7;
            var samples=new float[frames*2];
            double phase=0;
            for(int i=0;i<frames;i++) {
                double t=i/(double)rate;
                phase+=2*Math.PI*(73.416+2.1*Math.Sin(t*.75))/rate;
                double envelope=Math.Min(t/.65,1)*Math.Exp(-t*.35)*Math.Min((7-t)/1.5,1);
                float v=(float)((Math.Sin(phase)*.32+Math.Sin(phase*2)*.12+Math.Sin(phase*3)*.04)*envelope);
                samples[i*2]=v;samples[i*2+1]=v;
            }
            var clip=AudioClip.Create("Horizon song",frames,2,rate,false);clip.SetData(samples,0);return clip;
        }

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
