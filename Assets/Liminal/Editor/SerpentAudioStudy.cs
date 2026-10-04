using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace Liminal.Editor
{
    public static class SerpentAudioStudy
    {
        const int Rate = 44100;
        const float MusicGain = .83f, NoteGain = .62f, MasterGain = .8f;
        static readonly int[] Fold = { 0, 2, 1, 0, 1, 2, 1, 0 };
        static readonly Dictionary<string, float[]> tones = new();

        [MenuItem("Liminal/Export Serpent Audio Comparison")]
        public static void Export()
        {
            string output = Path.GetFullPath("MusicReview/12-SerpentSoundStudy");
            Directory.CreateDirectory(output);
            tones.Clear();
            AuthoredScore.ResetThemeSchedule(1);
            var score = AuthoredScore.ThemeData(1);
            var source = Load("Assets/Liminal/Resources/StageAudio/Serpent_VelvetKeys.wav");
            var main = Load("Assets/Liminal/Audio/TidalMemory.wav");
            const int firstBeat = 32, beats = 48;
            int first = score.beats[firstBeat], length = score.beats[firstBeat + beats] - first;
            var report = new StudyReport {
                sourceSha256 = Hash("Assets/Liminal/Resources/StageAudio/Serpent_VelvetKeys.wav"),
                sourceStartSeconds = first / (double)Rate, durationSeconds = length / (double)Rate
            };
            for (int variant = 0; variant < 3; variant++)
            {
                var mix = new float[length * 2];
                var effects = new float[length * 2];
                for (int i = 0; i < mix.Length; i++) mix[i] = source[first * 2 + i] * MusicGain;
                foreach (int beat in new[] { 4, 12, 20, 28, 36 })
                    for (int note = 0; note < 8; note++)
                    {
                        int sample = score.eighths[(firstBeat + beat) * 2 + note];
                        double song = sample / (double)Rate;
                        int midi = variant == 0 ? AuthoredScore.Note(note, song) : FoldedNote(note, song);
                        Add(effects, variant == 2 ? SoftGlass(midi) : ExistingTone(midi, false), sample - first, NoteGain);
                        report.notes.Add(new NoteEvent { variant = variant, sample = sample - first, midi = midi });
                    }
                for (int note = 0; note < 3; note++)
                {
                    int sample = score.eighths[(firstBeat + 44) * 2 + note];
                    double song = sample / (double)Rate;
                    int midi = variant == 0 ? AuthoredScore.Note(note, song) : FoldedNote(note, song);
                    Add(effects, ExistingTone(midi, true), sample - first, .76f);
                }
                for (int i = 0; i < mix.Length; i++) mix[i] += effects[i];
                string name = new[] { "A_Current", "B_FoldedSameTone", "C_FoldedSoftGlass" }[variant];
                report.files.Add(Write(output, name, mix));
                if (variant == 2) report.files.Add(Write(output, "C_EffectsOnly", effects));
            }
            var mainScore = AuthoredScore.Data;
            int exit = mainScore.beats[64];
            int lead = Rate * 4, tail = Rate * 8;
            for (int version = 0; version < 2; version++)
            {
                int fadeSamples = score.beats[version == 0 ? 2 : 8];
                var mix = new float[(lead + tail) * 2];
                for (int frame = 0; frame < lead + tail; frame++)
                {
                    int incoming = frame - lead;
                    float progress = Mathf.Clamp01(incoming / (float)fadeSamples);
                    Vector2 gains = version == 0 ? new Vector2(1 - progress, progress) : MusicTransport.CrossfadeGains(progress);
                    for (int channel = 0; channel < 2; channel++)
                    {
                        float old = main[(exit - lead + frame) * 2 + channel];
                        float next = incoming >= 0 ? source[incoming * 2 + channel] : 0;
                        mix[frame * 2 + channel] = (old * gains.x + next * gains.y) * MusicGain;
                    }
                }
                report.files.Add(Write(output, version == 0 ? "D_FadeBefore" : "E_FadeAfter", mix));
            }
            AuthoredScore.ResetThemeSchedule(-1);
            File.WriteAllText(Path.Combine(output, "study-report.json"), JsonUtility.ToJson(report, true));
            Debug.Log("LIMINAL_AUDIO_STUDY_SUCCESS " + output);
        }

        static int FoldedNote(int index, double song)
        {
            int midi = AuthoredScore.Note(Fold[index % Fold.Length], song);
            while (midi < 60) midi += 12;
            while (midi > 78) midi -= 12;
            return midi;
        }

        // Call the game's existing generators, so A is not a substitute synthesis.
        static float[] ExistingTone(int midi, bool release)
        {
            string key = (release ? "release" : "hit") + midi;
            if (tones.TryGetValue(key, out var data)) return data;
            var method = typeof(MusicTransport).GetMethod(release ? "MakeReleaseTone" : "Synthesize",
                BindingFlags.NonPublic | BindingFlags.Static);
            var clip = (AudioClip)method.Invoke(null, release ? new object[] { midi } : new object[] { midi, 1.8f, false });
            data = new float[clip.samples * clip.channels];
            if (!clip.GetData(data, 0)) throw new InvalidOperationException("Cannot read the runtime tone.");
            UnityEngine.Object.DestroyImmediate(clip);
            tones.Add(key, data);
            return data;
        }

        static float[] SoftGlass(int midi)
        {
            string key = "soft" + midi;
            if (tones.TryGetValue(key, out var data)) return data;
            var reference = ExistingTone(midi, false);
            data = new float[reference.Length];
            double frequency = 440 * Math.Pow(2, (midi - 69) / 12.0);
            double energy = 0, referenceEnergy = 0;
            for (int frame = 0; frame < data.Length / 2; frame++)
            {
                double t = frame / (double)Rate, phase = 2 * Math.PI * frequency * t;
                double envelope = Math.Min(t / .009, 1) * Math.Exp(-t * 4.5) * Math.Min((1.8 - t) / .06, 1);
                double core = Math.Sin(phase + Math.Sin(phase * 2) * .22 * Math.Exp(-t * 7)) * .34;
                double overtone = Math.Sin(phase * 2) * .038 * Math.Exp(-t * 9) +
                    Math.Sin(phase * 4.02) * .008 * Math.Exp(-t * 13);
                float value = (float)((core + overtone) * envelope);
                data[frame * 2] = value;
                data[frame * 2 + 1] = value * (float)(.97 + .03 * Math.Sin(t * 12));
                energy += data[frame * 2] * data[frame * 2] + data[frame * 2 + 1] * data[frame * 2 + 1];
                referenceEnergy += reference[frame * 2] * reference[frame * 2] + reference[frame * 2 + 1] * reference[frame * 2 + 1];
            }
            // Keep per-note energy equal to B; C must not win just by being louder.
            float gain = (float)Math.Sqrt(referenceEnergy / Math.Max(1e-12, energy));
            for (int i = 0; i < data.Length; i++) data[i] *= gain;
            tones.Add(key, data);
            return data;
        }

        static float[] Load(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (!clip || clip.frequency != Rate || clip.channels != 2) throw new InvalidOperationException("Invalid study source: " + path);
            clip.LoadAudioData();
            var pcm = new float[clip.samples * 2];
            if (!clip.GetData(pcm, 0)) throw new InvalidOperationException("Cannot read imported soundtrack: " + path);
            return pcm;
        }

        static void Add(float[] mix, float[] tone, int firstFrame, float gain)
        {
            int first = firstFrame * 2;
            for (int i = 0; i < tone.Length && first + i < mix.Length; i++) mix[first + i] += tone[i] * gain;
        }

        static FileMetric Write(string output, string name, float[] pcm)
        {
            // Identical preview edge fades only; never normalize A/B/C independently.
            int edge = Rate / 100;
            double peak = 0, energy = 0;
            for (int i = 0; i < pcm.Length; i++)
            {
                int frame = i / 2;
                float fade = Mathf.Clamp01(Math.Min(frame, pcm.Length / 2 - 1 - frame) / (float)edge);
                pcm[i] *= fade * MasterGain;
                if (float.IsNaN(pcm[i]) || float.IsInfinity(pcm[i])) throw new InvalidOperationException("Nonfinite audio.");
                peak = Math.Max(peak, Math.Abs(pcm[i]));
                energy += pcm[i] * pcm[i];
            }
            if (peak >= .98) throw new InvalidOperationException("Preview clipping risk: " + name + " peak=" + peak);
            using (var writer = new BinaryWriter(File.Create(Path.Combine(output, name + ".wav"))))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + pcm.Length * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)2); writer.Write(Rate); writer.Write(Rate * 4);
                writer.Write((short)4); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(pcm.Length * 2);
                foreach (float value in pcm) writer.Write((short)Math.Round(value * 32767));
            }
            return new FileMetric { name = name, seconds = pcm.Length / (double)(Rate * 2), peak = peak, rms = Math.Sqrt(energy / pcm.Length) };
        }

        static string Hash(string path)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }

        [Serializable] sealed class StudyReport
        {
            public string sourceSha256;
            public double sourceStartSeconds, durationSeconds;
            public float masterGain = MasterGain;
            public bool candidateIntegratedInGame = false, musicalGridHumanVerified = false;
            public string source = "Unity-imported approved recording; same BGM excerpt, note onsets and gains in A/B/C";
            public List<FileMetric> files = new();
            public List<NoteEvent> notes = new();
        }
        [Serializable] sealed class FileMetric { public string name; public double seconds, peak, rms; }
        [Serializable] sealed class NoteEvent { public int variant, sample, midi; }
    }
}
