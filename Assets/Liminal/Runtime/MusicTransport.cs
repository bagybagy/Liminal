using System;
using System.Collections.Generic;
using UnityEngine;

namespace Liminal
{
    public sealed class MusicTransport : MonoBehaviour
    {
        public AudioClip soundtrack;
        public bool LoopSoundtrack;
        public bool EnableStageMusic;
        const float MusicLevel = 0.83f;
        const int VoiceCount = 64;
        const double CrossfadeBars = 2;
        const int ReleaseToneOctavesAboveRoot = 2;
        static readonly float HermitShotBoost = (float)Math.Pow(10, 3 / 20.0);

        readonly AudioSource[] musicSources = new AudioSource[2];
        readonly AudioClip[] themeClips = new AudioClip[AuthoredScore.ThemeCount];
        readonly AudioSource[] voices = new AudioSource[VoiceCount];
        readonly double[] voiceEnds = new double[VoiceCount];
        readonly Dictionary<int, AudioClip> notes = new();
        readonly Dictionary<int, AudioClip> harpNotes = new();
        readonly Dictionary<int, AudioClip> releaseTones = new();
        AudioClip impact, lockTone, whaleTone;
        double origin;
        int activeMusicSource, scheduledMusicSource = -1, fadeOutMusicSource = -1;
        int pendingTheme = -1;
        double fadeStartSong, fadeEndSong;
        bool stageMusicAvailable, crossfading, initialized, playbackStarted;
        float lastVoiceVolume;

        public bool Paused { get; private set; }
        public double Time => playbackStarted ? Math.Max(0, AudioSettings.dspTime - origin) : 0;
        public double DspOrigin => origin;
        public float Volume { get; private set; } = 0.8f;
        public int ScheduledNotes { get; private set; }
        public int ScheduledHarpNotes { get; private set; }
        public int LastShotPitch { get; private set; }
        public AudioClip LastShotClip { get; private set; }
        public float LastShotVolume { get; private set; }
        public bool SerpentHarpReady => harpNotes.Count == 19;
        public double MaxGridError { get; private set; }
        public double MaxPlaybackPhaseError { get; private set; }
        public int DroppedNotes { get; private set; }
        public int BossReleaseEvents { get; private set; }
        public int ScheduledReleaseNotes { get; private set; }
        public double MaxReleaseGridError { get; private set; }
        public int[] LastReleasePitches { get; private set; } = new int[0];
        public double[] LastReleaseOnsets { get; private set; } = new double[0];
        public AudioClip ReleaseTonePreview(int midi) => GetReleaseTone(midi);
        public static float ReleaseToneFrequency(int midi) =>
            (float)(440 * Math.Pow(2, (midi + 12 * ReleaseToneOctavesAboveRoot - 69) / 12.0));
        public int ThemeCount => AuthoredScore.ThemeCount;
        public bool StageMusicEnabled => stageMusicAvailable;
        public AudioClip ActiveSoundtrack => musicSources[activeMusicSource] ? musicSources[activeMusicSource].clip : null;
        public bool ActiveSoundtrackLoops => musicSources[activeMusicSource] && musicSources[activeMusicSource].loop;
        public int CurrentTheme { get; private set; } = -1;
        public int PendingTheme => pendingTheme;
        public int TransitionCount { get; private set; }
        public double ScheduledBoundary { get; private set; } = -1;
        public double LastTransitionTime { get; private set; } = -1;
        public double NextTransitionTime => pendingTheme >= 0 ? ScheduledBoundary : -1;
        public bool IsCrossfading => crossfading;
        public double CrossfadeDuration => fadeEndSong - fadeStartSong;
        public float CrossfadeProgress { get; private set; }
        public float IncomingFadeGain => MusicGain(scheduledMusicSource >= 0 ? scheduledMusicSource : activeMusicSource);
        public float OutgoingFadeGain => MusicGain(scheduledMusicSource >= 0 ? activeMusicSource : 1 - activeMusicSource);

        public static Vector2 CrossfadeGains(float progress)
        {
            progress = Mathf.Clamp01(progress);
            if (progress <= 0) return new Vector2(1, 0);
            if (progress >= 1) return new Vector2(0, 1);
            float smooth = progress * progress * (3 - 2 * progress);
            float angle = smooth * Mathf.PI * .5f;
            return new Vector2(Mathf.Clamp01(Mathf.Cos(angle)), Mathf.Clamp01(Mathf.Sin(angle)));
        }

        float MusicGain(int source) => musicSources[source] ? musicSources[source].volume / MusicLevel : 0;

        public void Initialize(bool deferPlayback = false)
        {
            if (!soundtrack) throw new InvalidOperationException("Missing soundtrack.");
            if (soundtrack.frequency != AuthoredScore.Data.sampleRate || soundtrack.samples != AuthoredScore.Data.sampleCount)
                throw new InvalidOperationException("Audio samples do not match the authored timeline.");

            for (int i = 0; i < musicSources.Length; i++) {
                musicSources[i] = gameObject.AddComponent<AudioSource>();
                musicSources[i].playOnAwake = false;
                musicSources[i].spatialBlend = 0;
            }
            stageMusicAvailable = EnableStageMusic && LoopSoundtrack && LoadStageMusic();
            CacheHarmony(AuthoredScore.Data);
            if (stageMusicAvailable)
                for (int i = 0; i < themeClips.Length; i++) CacheHarmony(AuthoredScore.ThemeData(i));
            if (stageMusicAvailable) LoadHarpNotes();

            for (int i = 0; i < voices.Length; i++) {
                voices[i] = gameObject.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
                voices[i].spatialBlend = 0;
            }
            impact = Synthesize(38, 0.4f, true);
            lockTone = Synthesize(98, 0.1f, false);
            whaleTone = MakeWhaleCall();
            SetVolume(PlayerPrefs.GetFloat("volume", 0.8f));
            initialized = true;
            if (!deferPlayback) Restart();
        }

        bool LoadStageMusic()
        {
            try {
                for (int i = 0; i < themeClips.Length; i++) {
                    themeClips[i] = AuthoredScore.UsesMainSoundtrack(i) ? soundtrack :
                        Resources.Load<AudioClip>(AuthoredScore.ThemeResourcePath(i));
                    if (!themeClips[i]) throw new InvalidOperationException("Missing stage audio: " + AuthoredScore.ThemeNames[i]);
                    var timeline = AuthoredScore.ThemeData(i);
                    if (themeClips[i].frequency != timeline.sampleRate || themeClips[i].samples != timeline.sampleCount)
                        throw new InvalidOperationException("Stage audio samples do not match timeline: " + AuthoredScore.ThemeNames[i]);
                }
                return true;
            }
            catch (Exception exception) {
                Debug.LogWarning("Stage music unavailable; using the original soundtrack. " + exception.Message);
                return false;
            }
        }

        void CacheHarmony(AuthoredScore.Timeline timeline)
        {
            foreach (var chord in timeline.harmony) {
                if (chord.notes == null || chord.notes.Length == 0) continue;
                for (int i = 0; i < 8; i++) {
                    int midi = chord.notes[i % chord.notes.Length] + 12 + 12 * (i / chord.notes.Length);
                    if (!notes.ContainsKey(midi)) notes.Add(midi, Synthesize(midi, 1.8f, false));
                }
                for (int i = 0; i < 3; i++) {
                    int midi = chord.notes[i % chord.notes.Length] + 12 + 12 * (i / chord.notes.Length);
                    if (!releaseTones.ContainsKey(midi)) releaseTones.Add(midi, MakeReleaseTone(midi));
                }
            }
        }

        void LoadHarpNotes()
        {
            for (int midi = 60; midi <= 78; midi++) {
                var clip = Resources.Load<AudioClip>("StageNoteAudio/SerpentHarp/Note_" + midi);
                if (!clip || clip.frequency != 44100 || clip.samples != 52920 || clip.channels != 2)
                    throw new InvalidOperationException("Missing or invalid approved harp note: " + midi);
                harpNotes.Add(midi, clip);
            }
        }

        public void Restart()
        {
            SetPaused(false);
            for (int i = 0; i < musicSources.Length; i++) {
                if (!musicSources[i]) continue;
                musicSources[i].Stop();
                musicSources[i].clip = null;
                musicSources[i].volume = 0;
            }
            for (int i = 0; i < voices.Length; i++) { voices[i].Stop(); voiceEnds[i] = 0; }

            origin = AudioSettings.dspTime + 0.3;
            playbackStarted = true;
            activeMusicSource = 0;
            scheduledMusicSource = fadeOutMusicSource = -1;
            pendingTheme = -1;
            fadeStartSong = fadeEndSong = 0;
            CrossfadeProgress = 0;
            crossfading = false;
            ScheduledBoundary = -1;
            LastTransitionTime = -1;
            CurrentTheme = stageMusicAvailable ? 0 : -1;
            TransitionCount = 0;
            AuthoredScore.ResetThemeSchedule(CurrentTheme);

            var source = musicSources[activeMusicSource];
            source.clip = stageMusicAvailable ? themeClips[0] : soundtrack;
            source.loop = LoopSoundtrack;
            source.volume = MusicLevel;
            source.PlayScheduled(origin);
            ScheduledNotes = 0;
            ScheduledHarpNotes = 0;
            LastShotPitch = 0;
            LastShotClip = null;
            LastShotVolume = 0;
            MaxGridError = 0;
            MaxPlaybackPhaseError = 0;
            DroppedNotes = 0;
            BossReleaseEvents = 0;
            ScheduledReleaseNotes = 0;
            MaxReleaseGridError = 0;
            LastReleasePitches = new int[0];
            LastReleaseOnsets = new double[0];
        }

        public void RequestTheme(int room, bool ending = false, BossId endingMask = BossId.None)
        {
            if (!initialized || !playbackStarted || !stageMusicAvailable || !LoopSoundtrack) return;
            AdvanceThemeState();
            int target;
            if (ending) target = AuthoredScore.EndingThemeFor(endingMask);
            else {
                if (room < 0 || room >= AuthoredScore.RoomThemeCount) return;
                target = room;
            }

            if (pendingTheme == target) return;
            if (target == CurrentTheme) {
                CancelPendingTheme();
                return;
            }

            CancelPendingTheme();
            pendingTheme = target;
            double earliest = crossfading ? Math.Max(Time, fadeEndSong) : Time;
            ScheduledBoundary = AuthoredScore.NextFourBarBoundary(earliest, SchedulingLead());
            AuthoredScore.QueueThemeTransition(target, ScheduledBoundary);
            if (!crossfading) SchedulePendingClip();
        }

        void CancelPendingTheme()
        {
            if (pendingTheme < 0) return;
            if (scheduledMusicSource >= 0) {
                musicSources[scheduledMusicSource].Stop();
                musicSources[scheduledMusicSource].clip = null;
                musicSources[scheduledMusicSource].volume = 0;
                scheduledMusicSource = -1;
            }
            AuthoredScore.CancelThemeTransitionsFrom(ScheduledBoundary);
            pendingTheme = -1;
            ScheduledBoundary = crossfading ? fadeStartSong : -1;
        }

        void SchedulePendingClip()
        {
            if (pendingTheme < 0 || crossfading || scheduledMusicSource >= 0) return;
            double lead = SchedulingLead();
            if (ScheduledBoundary <= Time + lead) {
                AuthoredScore.CancelThemeTransitionsFrom(ScheduledBoundary);
                ScheduledBoundary = AuthoredScore.NextFourBarBoundary(Time, lead);
                AuthoredScore.QueueThemeTransition(pendingTheme, ScheduledBoundary);
            }
            scheduledMusicSource = 1 - activeMusicSource;
            var incoming = musicSources[scheduledMusicSource];
            incoming.Stop();
            incoming.clip = themeClips[pendingTheme];
            incoming.loop = !AuthoredScore.IsEndingTheme(pendingTheme);
            incoming.volume = 0;
            CrossfadeProgress = 0;
            incoming.PlayScheduled(origin + ScheduledBoundary);
        }

        double SchedulingLead()
        {
            AudioSettings.GetDSPBufferSize(out int bufferLength, out int bufferCount);
            int sampleRate = Math.Max(1, AudioSettings.outputSampleRate);
            return Math.Max(0.12, (double)bufferLength * bufferCount / sampleRate);
        }

        void AdvanceThemeState()
        {
            if (Paused || !stageMusicAvailable) return;
            double songTime = Time;
            if (scheduledMusicSource >= 0 && pendingTheme >= 0 && songTime + 1e-9 >= ScheduledBoundary) {
                fadeOutMusicSource = activeMusicSource;
                activeMusicSource = scheduledMusicSource;
                CurrentTheme = pendingTheme;
                pendingTheme = -1;
                scheduledMusicSource = -1;
                fadeStartSong = ScheduledBoundary;
                fadeEndSong = AuthoredScore.TimeAfterBeats(fadeStartSong, (int)(CrossfadeBars * 4));
                crossfading = true;
                TransitionCount++;
                LastTransitionTime = fadeStartSong;
            }

            if (crossfading) {
                float progress = Mathf.Clamp01((float)((songTime - fadeStartSong) / Math.Max(0.001, fadeEndSong - fadeStartSong)));
                CrossfadeProgress = progress;
                Vector2 gains = CrossfadeGains(progress);
                musicSources[fadeOutMusicSource].volume = MusicLevel * gains.x;
                musicSources[activeMusicSource].volume = MusicLevel * gains.y;
                if (progress >= 1) {
                    musicSources[fadeOutMusicSource].Stop();
                    musicSources[fadeOutMusicSource].clip = null;
                    musicSources[fadeOutMusicSource].volume = 0;
                    fadeOutMusicSource = -1;
                    crossfading = false;
                }
            }

            if (!crossfading && pendingTheme >= 0 && scheduledMusicSource < 0) SchedulePendingClip();
        }

        public bool ScheduleNote(int index, double songTime, float pan, float strength = 1)
        {
            if (!playbackStarted) return false;
            int midi = AuthoredScore.ShotNote(index, songTime);
            // Resolve the scheduled onset's theme, not the current frame's theme.
            int theme = AuthoredScore.ThemeAt(songTime);
            bool harp = stageMusicAvailable && AuthoredScore.UsesHarpShot(theme);
            AudioClip note;
            if (harp) note = harpNotes[midi];
            else if (!notes.TryGetValue(midi, out note)) {
                note = Synthesize(midi, 1.8f, false);
                notes.Add(midi, note);
            }
            float volume = 0.62f * strength * (harp && theme == 3 ? HermitShotBoost : 1);
            if (!Play(note, origin + songTime, pan, volume)) {
                DroppedNotes++; return false;
            }
            MaxGridError = Math.Max(MaxGridError, AuthoredScore.GridError(songTime));
            ScheduledNotes++;
            if (harp) ScheduledHarpNotes++;
            LastShotPitch = midi;
            LastShotClip = note;
            LastShotVolume = lastVoiceVolume;
            return true;
        }

        void Update()
        {
            if (!initialized || !playbackStarted || Paused) return;
            AdvanceThemeState();
            var source = musicSources[activeMusicSource];
            if (!source || !source.isPlaying || AudioSettings.dspTime <= origin + .1) return;
            var timeline = AuthoredScore.TimelineAt(Time);
            double expected = AuthoredScore.ThemeAt(Time) >= 0
                ? AuthoredScore.LocalSampleAt(Time) / (double)timeline.sampleRate
                : LoopSoundtrack ? Time % AuthoredScore.Duration : Time;
            double actual = source.timeSamples / (double)source.clip.frequency;
            double error = Math.Abs(actual - expected);
            if (source.loop) error = Math.Min(error, Math.Abs(timeline.sampleCount / (double)timeline.sampleRate - error));
            MaxPlaybackPhaseError = Math.Max(MaxPlaybackPhaseError, error);
        }

        public void LockSound() => Play(lockTone, AudioSettings.dspTime + 0.01, 0, 0.06f);
        public void DamageSound() => Play(impact, AudioSettings.dspTime + 0.01, 0, 0.6f);
        public void WhaleCall() => Play(whaleTone, origin + Score.NextEighth(Time, .15), 0, .35f);

        public void BossRelease(float song, int voice = 0)
        {
            if (!playbackStarted || Paused) return;
            double boundary = AuthoredScore.Next(Math.Max(song, Time), SchedulingLead(), false);
            int root = AuthoredScore.Note(0, boundary);
            AudioClip fallback = GetReleaseTone(root);
            BossAudioSettings settings = BossAudioSettings.Current;
            AudioClip tone = BossAudioSettings.SelectReleaseClip(settings, voice, fallback, out bool configured);
            if (!tone) return;
            float volume = configured ? settings.volume : voice == 3 ? .68f : .76f;

            if (configured) {
                if (!Play(tone, origin + boundary, 0, volume)) return;
                LastReleasePitches = new int[0];
                LastReleaseOnsets = new[] { boundary };
                ScheduledReleaseNotes++;
            }
            else {
                var pitches = new int[3];
                var onsets = new double[3];
                var clips = new AudioClip[3];
                onsets[0] = boundary;
                for (int i = 0; i < pitches.Length; i++) {
                    if (i > 0) onsets[i] = NextReleaseOnset(onsets[i - 1]);
                    pitches[i] = AuthoredScore.Note(i, onsets[i]);
                    clips[i] = GetReleaseTone(pitches[i]);
                }
                if (!PlayReleasePhrase(clips, onsets, volume)) return;
                LastReleasePitches = pitches;
                LastReleaseOnsets = onsets;
                ScheduledReleaseNotes += pitches.Length;
            }

            BossReleaseEvents++;
            foreach (double onset in LastReleaseOnsets)
                MaxReleaseGridError = Math.Max(MaxReleaseGridError, AuthoredScore.GridError(onset));
        }

        static double NextReleaseOnset(double onset)
        {
            double oneSample = 1.0 / AuthoredScore.TimelineAt(onset).sampleRate;
            return AuthoredScore.Next(onset, oneSample, false);
        }

        AudioClip GetReleaseTone(int midi)
        {
            if (!releaseTones.TryGetValue(midi, out AudioClip clip)) {
                clip = MakeReleaseTone(midi);
                releaseTones.Add(midi, clip);
            }
            return clip;
        }

        bool PlayReleasePhrase(AudioClip[] clips, double[] onsets, float volume)
        {
            double now = AudioSettings.dspTime;
            var available = new int[clips.Length];
            for (int i = 0; i < clips.Length; i++)
                if (!clips[i] || origin + onsets[i] < now) return false;

            int found = 0;
            for (int i = 0; i < voiceEnds.Length && found < available.Length; i++)
                if (voiceEnds[i] <= now) available[found++] = i;
            if (found != available.Length) return false;

            for (int i = 0; i < clips.Length; i++) {
                double dspTime = origin + onsets[i];
                var source = voices[available[i]];
                source.clip = clips[i];
                source.pitch = 1f;
                source.panStereo = 0;
                source.volume = volume;
                source.PlayScheduled(dspTime);
                voiceEnds[available[i]] = dspTime + clips[i].length;
            }
            return true;
        }

        bool Play(AudioClip clip, double dspTime, float pan, float volume)
        {
            int voice = -1;
            for (int i = 0; i < voices.Length; i++) if (voiceEnds[i] <= AudioSettings.dspTime) { voice = i; break; }
            if (voice < 0 || dspTime < AudioSettings.dspTime) return false;
            var source = voices[voice];
            source.clip = clip;
            source.pitch = 1f;
            source.panStereo = Mathf.Clamp(pan, -0.75f, 0.75f);
            source.volume = volume;
            source.PlayScheduled(dspTime);
            voiceEnds[voice] = dspTime + clip.length;
            lastVoiceVolume = source.volume;
            return true;
        }

        public void SetPaused(bool value) { Paused = value; AudioListener.pause = value; }
        public void SetVolume(float value) { Volume = Mathf.Clamp01(value); AudioListener.volume = Volume; }

        void OnDestroy()
        {
            AudioListener.pause = false;
            foreach (var clip in notes.Values) if (clip) Destroy(clip);
            foreach (var clip in releaseTones.Values) if (clip) Destroy(clip);
            if (impact) Destroy(impact);
            if (lockTone) Destroy(lockTone);
            if (whaleTone) Destroy(whaleTone);
        }

        static AudioClip MakeWhaleCall()
        {
            const int rate = 44100, frames = rate * 7;
            var samples = new float[frames * 2];
            double phase = 0;
            for (int i = 0; i < frames; i++) {
                double t = i / (double)rate;
                phase += 2 * Math.PI * (73.416 + 2.1 * Math.Sin(t * .75)) / rate;
                double envelope = Math.Min(t / .65, 1) * Math.Exp(-t * .35) * Math.Min((7 - t) / 1.5, 1);
                float v = (float)((Math.Sin(phase) * .32 + Math.Sin(phase * 2) * .12 + Math.Sin(phase * 3) * .04) * envelope);
                samples[i * 2] = v;
                samples[i * 2 + 1] = v;
            }
            var clip = AudioClip.Create("Horizon song", frames, 2, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        static AudioClip MakeReleaseTone(int midi)
        {
            const int rate = 44100;
            const float length = .55f;
            int frames = Mathf.CeilToInt(rate * length);
            var samples = new float[frames * 2];
            double frequency = ReleaseToneFrequency(midi);
            for (int i = 0; i < frames; i++) {
                double t = i / (double)rate;
                double attack = Math.Min(t / .003, 1);
                double release = Math.Min((length - t) / .09, 1);
                double p = 2 * Math.PI * frequency * t;
                double fundamental = Math.Sin(p) * .30 * Math.Exp(-t * 5.8);
                double second = .16 * Math.Exp(-t * 9.5);
                double third = .065 * Math.Exp(-t * 14);
                double glassPartial = .045 * Math.Exp(-t * 18);
                double left = fundamental + Math.Sin(p * 2 + .012) * second +
                    Math.Sin(p * 3 - .024) * third + Math.Sin(p * 4.07 + .06) * glassPartial;
                double right = fundamental + Math.Sin(p * 2 - .012) * second +
                    Math.Sin(p * 3 + .024) * third + Math.Sin(p * 4.07 - .06) * glassPartial;
                double envelope = attack * release;
                samples[i * 2] = (float)(left * envelope);
                samples[i * 2 + 1] = (float)(right * envelope);
            }
            var clip = AudioClip.Create("Release_" + midi, frames, 2, rate, false);
            clip.SetData(samples, 0);
            return clip;
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
