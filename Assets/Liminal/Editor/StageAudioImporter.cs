using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Liminal.Editor
{
    public sealed class StageAudioImporter : AssetPostprocessor
    {
        const string StageAudioPrefix = "Assets/Liminal/Resources/StageAudio/";

        bool IsStageTrack()
        {
            string normalized = assetPath.Replace('\\', '/');
            if (!normalized.StartsWith(StageAudioPrefix, StringComparison.Ordinal) ||
                !normalized.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) return false;
            string name = Path.GetFileNameWithoutExtension(normalized);
            for (int i = 0; i < AuthoredScore.ThemeCount; i++)
                if (name == AuthoredScore.ThemeNames[i]) return true;
            return false;
        }

        void OnPreprocessAudio()
        {
            if (!IsStageTrack()) return;
            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
            settings.sampleRateOverride = 44100;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.90f;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = false;
            importer.loadInBackground = false;
        }

        void OnPostprocessAudio(AudioClip clip)
        {
            if (!IsStageTrack()) return;
            string timelinePath = Path.ChangeExtension(assetPath, null) + "Timeline.json";
            if (!File.Exists(timelinePath)) {
                Debug.LogError("Stage audio has no authored timeline: " + assetPath);
                return;
            }
            var timeline = JsonUtility.FromJson<AuthoredScore.Timeline>(File.ReadAllText(timelinePath));
            if (timeline == null || clip.frequency != 44100 || clip.frequency != timeline.sampleRate ||
                clip.samples != timeline.sampleCount)
                Debug.LogError("Stage audio import changed its authored 44.1 kHz sample grid: " + assetPath);
        }
    }
}
