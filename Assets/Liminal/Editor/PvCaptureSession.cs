using System;
using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Liminal.Editor
{
    [InitializeOnLoad]
    public static class PvCaptureSession
    {
        const string CaptureKey = "Liminal.CavernPV";
        const string LegacyCaptureKey = "Liminal.TrailerCapture.Autopilot";
        const string SessionActiveKey = "Liminal.CavernPV.SessionActive";
        const string SessionSuccessKey = "Liminal.CavernPV.SessionSuccess";
        const string SessionErrorKey = "Liminal.CavernPV.SessionError";
        const string ScenePath = "Assets/Liminal/Production/AbyssalChoir.unity";
        const double DirectorWaitSeconds = 30.0;
        const double CaptureTimeoutSeconds = 205.0;
        const float RecordingLimitSeconds = 79f;

        static RecorderControllerSettings controllerSettings;
        static MovieRecorderSettings movieSettings;
        static RecorderController controller;
        static Experience experience;
        static PvDirector director;
        static bool recorderStarted;
        static double recorderStartDsp, playEnteredAt;
        static string rawPath, metadataPath;

        [Serializable]
        sealed class CaptureMetadata
        {
            public string scene;
            public string unityVersion;
            public string rawFile;
            public int width, height, frameRate, frameCount;
            public int sampleRate;
            public int[] retainedBeatBoundaries;
            public bool proofActive, cavernMode;
            public double recordStartDsp, musicDspOrigin, audioOffsetSeconds;
            public double captureStartMusicSeconds, captureEndMusicSeconds, recordingDurationSeconds;
            public int fired, hits, bossDamage, life, spawnedDolphins;
            public int dolphinPressureShots, dolphinPressureInterceptions, dolphinFullLocks;
            public float arrivalFormation;
            public PvDirector.StageMarker[] markers;
        }

        static PvCaptureSession()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (SessionState.GetBool(SessionActiveKey, false))
                EditorApplication.update += OnEditorUpdate;
            else if (EditorPrefs.GetBool(CaptureKey, false))
                ClearPreferences();
        }

        public static void Run()
        {
            if (Application.isBatchMode)
            {
                ExitWithError("Recorder capture must run in the normal Unity Editor, not batch mode.");
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                ExitWithError("Stop Play mode before starting the PV capture session.");
                return;
            }

            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string outputDirectory = Path.Combine(projectRoot, "Video", "PV");
                Directory.CreateDirectory(outputDirectory);
                rawPath = Path.Combine(outputDirectory, "LIMINAL_raw");
                metadataPath = Path.Combine(outputDirectory, "LIMINAL_raw.json");
                if (File.Exists(rawPath + ".mp4") || File.Exists(metadataPath))
                    throw new IOException("The one-take output already exists; move it aside before a capture run.");

                EditorPrefs.SetBool(LegacyCaptureKey, false);
                EditorPrefs.SetBool(CaptureKey, true);
                SessionState.SetBool(SessionActiveKey, true);
                SessionState.SetBool(SessionSuccessKey, false);
                SessionState.SetString(SessionErrorKey, string.Empty);
                EditorApplication.update -= OnEditorUpdate;
                EditorApplication.update += OnEditorUpdate;
                Application.runInBackground = true;

                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                FocusGameView();
                Debug.Log("LIMINAL_PV_SESSION_ARMED scene=" + ScenePath + " output=" + rawPath + ".mp4" +
                    " fps=30 size=1920x1080 audio=true limit=" + RecordingLimitSeconds + "s");
                EditorApplication.isPlaying = true;
            }
            catch (Exception exception)
            {
                ExitWithError(exception.ToString());
            }
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(SessionActiveKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                playEnteredAt = EditorApplication.timeSinceStartup;
                try { StartRecorder(); }
                catch (Exception exception) { Fail(exception.ToString()); }
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                StopRecorder();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                FinishSession();
            }
        }

        static void OnEditorUpdate()
        {
            if (!SessionState.GetBool(SessionActiveKey, false))
            {
                EditorApplication.update -= OnEditorUpdate;
                return;
            }

            if (!EditorApplication.isPlaying) return;
            if (!recorderStarted)
            {
                if (playEnteredAt == 0) playEnteredAt = EditorApplication.timeSinceStartup;
                try { StartRecorder(); }
                catch (Exception exception) { Fail(exception.ToString()); }
                if (!recorderStarted) return;
            }

            if (EditorApplication.timeSinceStartup - playEnteredAt > CaptureTimeoutSeconds)
            {
                Fail("The bounded in-editor capture timeout expired.");
                return;
            }

            if (director == null)
            {
                experience = UnityEngine.Object.FindAnyObjectByType<Experience>();
                director = UnityEngine.Object.FindAnyObjectByType<PvDirector>();
                if (experience == null || !experience.Ready || director == null)
                {
                    if (EditorApplication.timeSinceStartup - playEnteredAt > DirectorWaitSeconds)
                        Fail("The parent Experience hook did not initialize PvDirector within 30 seconds.");
                    return;
                }

            }

            if (!director.CaptureStarted)
            {
                // Recorder briefly freezes simulation while it prepares the first frame.
                if (!Mathf.Approximately(Time.timeScale, 1f)) return;
                experience.Restart();
                director.BeginCapture(recorderStartDsp);
                if (director.Failed)
                {
                    Fail(director.Failure);
                    return;
                }
                Debug.Log("LIMINAL_PV_RECORDING_STARTED musicOffset=" +
                    (experience.Music.DspOrigin - recorderStartDsp).ToString("F6") + "s");
            }

            if (director.Failed)
            {
                Fail(director.Failure);
                return;
            }
            if (controller == null || !controller.IsRecording())
            {
                Fail("Unity Recorder stopped before the authored take completed.");
                return;
            }
            if (director.Completed) CompleteCapture();
        }

        static void StartRecorder()
        {
            if (recorderStarted) return;
            Application.runInBackground = true;
            if (!SessionState.GetBool(SessionActiveKey, false))
                throw new InvalidOperationException("No PV capture session is armed.");

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string outputDirectory = Path.Combine(projectRoot, "Video", "PV");
            Directory.CreateDirectory(outputDirectory);
            rawPath = Path.Combine(outputDirectory, "LIMINAL_raw");
            metadataPath = Path.Combine(outputDirectory, "LIMINAL_raw.json");

            controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            controllerSettings.FrameRate = 30;
            controllerSettings.CapFrameRate = true;
            controllerSettings.SetRecordModeToTimeInterval(0f, RecordingLimitSeconds);

            movieSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movieSettings.name = "LIMINAL Current Game PV";
            movieSettings.Enabled = true;
            movieSettings.EncoderSettings = new CoreEncoderSettings {
                Codec = CoreEncoderSettings.OutputCodec.MP4,
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High
            };
            movieSettings.ImageInputSettings = new GameViewInputSettings {
                OutputWidth = 1920,
                OutputHeight = 1080
            };
            movieSettings.CaptureAudio = true;
            movieSettings.OutputFile = rawPath;
            controllerSettings.AddRecorderSettings(movieSettings);

            controller = new RecorderController(controllerSettings);
            controller.PrepareRecording();
            recorderStartDsp = AudioSettings.dspTime;
            if (!controller.StartRecording())
                throw new InvalidOperationException("Unity Recorder did not start the one-take session.");
            recorderStarted = true;
        }

        static void CompleteCapture()
        {
            CaptureMetadata metadata = BuildMetadata();
            StopRecorder();
            string moviePath = rawPath + ".mp4";
            if (!File.Exists(moviePath) || new FileInfo(moviePath).Length == 0)
                throw new IOException("Unity Recorder returned without a non-empty raw MP4.");
            File.WriteAllText(metadataPath, JsonUtility.ToJson(metadata, true));
            SessionState.SetBool(SessionSuccessKey, true);
            Debug.Log("LIMINAL_PV_RECORDING_COMPLETE frames=" + metadata.frameCount +
                " duration=" + metadata.recordingDurationSeconds.ToString("F3") +
                "s musicOffset=" + metadata.audioOffsetSeconds.ToString("F6") +
                "s fired=" + metadata.fired + " hits=" + metadata.hits +
                " bossDamage=" + metadata.bossDamage + " life=" + metadata.life);
            EditorApplication.isPlaying = false;
        }

        static CaptureMetadata BuildMetadata()
        {
            AuthoredScore.Timeline timeline = AuthoredScore.Data;
            double endDsp = AudioSettings.dspTime;
            return new CaptureMetadata {
                scene = ScenePath,
                unityVersion = Application.unityVersion,
                rawFile = "LIMINAL_raw.mp4",
                width = 1920,
                height = 1080,
                frameRate = 30,
                frameCount = director != null ? director.CaptureFrameCount : 0,
                sampleRate = timeline.sampleRate,
                retainedBeatBoundaries = new[] { 0, 16, 48, 64, 96, 104, 128, 152 },
                proofActive = experience != null && experience.ProofActive,
                cavernMode = experience != null && experience.CavernMode,
                recordStartDsp = recorderStartDsp,
                musicDspOrigin = experience != null ? experience.Music.DspOrigin : 0,
                audioOffsetSeconds = experience != null ? experience.Music.DspOrigin - recorderStartDsp : 0,
                captureStartMusicSeconds = director != null ? director.CaptureStartMusicTime : 0,
                captureEndMusicSeconds = experience != null ? experience.Music.Time : 0,
                recordingDurationSeconds = endDsp - recorderStartDsp,
                fired = experience != null ? experience.Combat.Fired : 0,
                hits = experience != null ? experience.Combat.Hits : 0,
                bossDamage = experience != null ? experience.Combat.BossDamage : 0,
                life = experience != null ? experience.Combat.Life : 0,
                spawnedDolphins = experience != null ? experience.Marine.SpawnedDolphins : 0,
                dolphinPressureShots = experience != null ? experience.Combat.DolphinPressureShots : 0,
                dolphinPressureInterceptions = experience != null ? experience.Combat.DolphinPressureInterceptions : 0,
                dolphinFullLocks = director != null ? director.DolphinFullLocks : 0,
                arrivalFormation = director != null ? director.ArrivalFormation : 0,
                markers = director != null ? CopyMarkers(director.Markers) : Array.Empty<PvDirector.StageMarker>()
            };
        }

        static PvDirector.StageMarker[] CopyMarkers(System.Collections.Generic.IReadOnlyList<PvDirector.StageMarker> values)
        {
            var copy = new PvDirector.StageMarker[values.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = values[i];
            return copy;
        }

        static void Fail(string message)
        {
            SessionState.SetString(SessionErrorKey, message);
            SessionState.SetBool(SessionSuccessKey, false);
            Debug.LogError("LIMINAL_PV_SESSION_ERROR " + message);
            StopRecorder();
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            else FinishSession();
        }

        static void StopRecorder()
        {
            if (controller != null && controller.IsRecording())
            {
                try { controller.StopRecording(); }
                catch (Exception exception) { SessionState.SetString(SessionErrorKey, exception.ToString()); }
            }
            recorderStarted = false;
        }

        static void FinishSession()
        {
            bool success = SessionState.GetBool(SessionSuccessKey, false);
            string error = SessionState.GetString(SessionErrorKey, string.Empty);
            if (!success && string.IsNullOrEmpty(error)) error = "Unity left Play mode before the PV take completed.";
            ClearPreferences();
            SessionState.SetBool(SessionActiveKey, false);
            SessionState.SetBool(SessionSuccessKey, false);
            SessionState.SetString(SessionErrorKey, string.Empty);
            EditorApplication.update -= OnEditorUpdate;
            if (success)
            {
                EditorApplication.Exit(0);
                return;
            }
            Debug.LogError("LIMINAL_PV_SESSION_FAILED " + error);
            EditorApplication.Exit(1);
        }

        static void ExitWithError(string message)
        {
            ClearPreferences();
            SessionState.SetBool(SessionActiveKey, false);
            SessionState.SetBool(SessionSuccessKey, false);
            SessionState.SetString(SessionErrorKey, message);
            Debug.LogError("LIMINAL_PV_SESSION_FAILED " + message);
            EditorApplication.Exit(1);
        }

        static void ClearPreferences()
        {
            EditorPrefs.SetBool(CaptureKey, false);
            EditorPrefs.SetBool(LegacyCaptureKey, false);
        }

        static void FocusGameView()
        {
            Type gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameViewType == null)
                throw new InvalidOperationException("Unity's Game View editor window could not be resolved.");
            EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
            gameView.Show();
            gameView.Focus();
            gameView.Repaint();
        }
    }
}
