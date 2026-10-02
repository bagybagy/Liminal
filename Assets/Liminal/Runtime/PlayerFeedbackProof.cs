using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Liminal
{
    public sealed class PlayerFeedbackProof : MonoBehaviour
    {
        Experience game;
        string output;
        float started;
        bool finished;
        readonly List<string> errors = new();
        readonly List<LockTarget> temporaryTargets = new();
        readonly Report report = new();

        public void Initialize(Experience owner)
        {
            game = owner;
            started = Time.realtimeSinceStartup;
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "--output");
            output = at >= 0 && at + 1 < args.Length ? args[at + 1] : Path.GetFullPath("Verification/PlayerFeedback");
            Directory.CreateDirectory(output);
            Application.logMessageReceived += OnLog;
        }

        void OnLog(string message, string stack, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && errors.Count < 16)
                errors.Add(message + "\n" + stack);
        }

        void Update()
        {
            if (!finished && Time.realtimeSinceStartup - started > 35)
            {
                errors.Add("Player-feedback proof exceeded its bounded deadline.");
                Finish();
            }
        }

        void Check(bool passed, string message)
        {
            if (passed) return;
            errors.Add(message);
            Finish();
            throw new InvalidOperationException(message);
        }

        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(.45f);
            game.Tutorial.SetEnabled(false);
            report.phaseAtStart = MeasurePhaseError();
            double origin = game.Music.DspOrigin;
            Check(!game.Music.StageMusicEnabled && game.Music.CurrentTheme == -1 &&
                game.Music.ActiveSoundtrack == game.soundtrack && game.soundtrack.name == "TidalMemory",
                "Default play must use TidalMemory instead of rejected stage tracks.");
            for (int room = 0; room < 5; room++)
            {
                game.Music.RequestTheme(room);
                yield return null;
            }
            game.Music.RequestTheme(2, true);
            yield return null;
            Check(game.Music.ActiveSoundtrack == game.soundtrack && game.Music.TransitionCount == 0 &&
                game.Music.PendingTheme == -1 && game.Music.DspOrigin == origin &&
                ReferenceEquals(AuthoredScore.TimelineAt(game.Music.Time), AuthoredScore.Data),
                "Rooms and ending must retain the same soundtrack and authored clock.");
            report.tidalThroughout = true;

            Camera camera = game.Flight.View;
            float fov = camera.fieldOfView, aspect = camera.aspect;
            var headPosition = new Vector3(.12f, 1.65f, .08f);
            var headRotation = Quaternion.Euler(-18, 35, 0);
            game.Flight.SetPose(CaveLayout.Rooms[2].Center, Quaternion.identity);
            game.Flight.EnableVr(true);
            game.Flight.SetVrHeadPose(headPosition, headRotation);
            Vector3 start = game.Flight.Position;
            Vector3 expectedRight = Vector3.ProjectOnPlane(camera.transform.right, Vector3.up).normalized;
            for (int i = 0; i < 42; i++) game.Flight.StepVr(1f / 60, Vector2.right, 0, 0, false);
            Check(Vector3.Dot((game.Flight.Position - start).normalized, expectedRight) > .999f &&
                Mathf.Abs(game.Flight.Speed - 20) < .02f, "Left-stick X must strafe at cruise after 0.7 seconds.");
            report.strafe = true;

            ResetPose(headPosition, headRotation);
            start = game.Flight.Position;
            for (int i = 0; i < 42; i++) game.Flight.StepVr(1f / 60, Vector2.zero, 1, 0, false);
            Check(Vector3.Dot((game.Flight.Position - start).normalized, Vector3.up) > .999f,
                "Right-stick Y must rise vertically independently of head pitch.");
            report.vertical = true;

            ResetPose(headPosition, headRotation);
            Vector3 expectedForward = camera.transform.forward;
            start = game.Flight.Position;
            for (int i = 0; i < 42; i++) game.Flight.StepVr(1f / 60, Vector2.up, 0, 0, false);
            Check(Vector3.Dot((game.Flight.Position - start).normalized, expectedForward) > .999f,
                "Left-stick Y must follow the complete head-forward direction.");
            report.gazeFlight = true;
            for (int i = 0; i < 42; i++) game.Flight.StepVr(1f / 60, Vector2.one, 1, 0, true);
            Check(game.Flight.Speed <= 40.01f && game.Flight.Speed > 39.9f,
                "Diagonal boost must reach, but not exceed, forty units per second.");
            for (int i = 0; i < 24; i++) game.Flight.StepVr(1f / 60, Vector2.zero, 0, 0, false);
            Check(game.Flight.Speed < .01f, "Released movement must brake to rest.");
            report.speedCaps = true;

            float yaw = game.Flight.transform.eulerAngles.y;
            game.Flight.StepVr(1f / 60, Vector2.zero, 0, 1, false);
            Check(Mathf.Abs(Mathf.DeltaAngle(yaw, game.Flight.transform.eulerAngles.y) - 1f) < .002f,
                "Turning must advance continuously by one degree at 60Hz, not snap.");
            Check(Vector3.Distance(camera.transform.localPosition, headPosition) < .0001f &&
                Quaternion.Angle(camera.transform.localRotation, headRotation) < .002f &&
                Mathf.Abs(camera.fieldOfView - fov) < .001f,
                "Locomotion must not change the physical head pose or projection.");
            report.continuousYaw = report.headPosePreserved = true;

            ResetPose(Vector3.zero, Quaternion.identity);
            camera.fieldOfView = 90;
            camera.aspect = 2;
            Vector3 viewer = camera.transform.position;
            Vector3 inside = viewer + Quaternion.Euler(0, 40, 0) * Vector3.forward * 150;
            Vector3 outside = viewer + Quaternion.Euler(0, 49, 0) * Vector3.forward * 150;
            Check(game.Combat.TryGetVrGazeScore(inside, out _) && !game.Combat.TryGetVrGazeScore(outside, out _),
                "VR gaze must accept the central70%-FOV ellipse and reject its outer region.");
            Check(!game.Combat.TryGetVrGazeScore(viewer - Vector3.forward * 10, out _), "VR must reject targets behind the viewer.");
            report.gazeCone = true;

            var far = AddTarget(viewer + Vector3.forward * 180);
            Check(Mathf.Abs(game.Combat.EffectiveAcquireRange(far) - 210) < .001f && game.Combat.CanAcquire(far),
                "VR must double the usual105-unit range from the head-camera position.");
            far.position = viewer + Vector3.forward * 211;
            Check(!game.Combat.CanAcquire(far), "VR must still enforce the doubled range limit.");
            far.position = viewer + Vector3.forward * 180;
            far.reserved = 1;
            Check(!game.Combat.CanAcquire(far), "In-flight reserved points must not be reacquired in VR.");
            far.reserved = 0;
            var beyondWall = AddTarget(viewer + camera.transform.forward * 1100, 10000);
            Check(game.Combat.TryGetVrGazeScore(beyondWall.position, out _) && !game.Combat.CanAcquire(beyondWall),
                "VR must reject a target beyond the cave wall even when its gaze and distance gates pass.");
            report.rangeAndWalls = true;

            for (int i = 0; i < 8; i++) AddTarget(viewer + new Vector3((i - 3.5f) * 15, i % 2 == 0 ? -14 : 14, 180));
            for (int i = 0; i < 10; i++) game.Combat.AcquireAt(Vector2.zero);
            Check(game.Combat.Locks.Count == 8 && new HashSet<LockTarget>(game.Combat.Locks).Count == 8,
                "Held gaze acquisition must fill exactly eight distinct valid points.");
            report.eightLocks = true;
            VrWorldHud hud = game.GetComponent<VrWorldHud>();
            hud.SetVrActive(true);
            hud.Tick((float)game.Music.Time, .016f);
            Check(hud.VisibleLocks == 8 && !hud.PauseMenuVisible, "Active VR HUD must show numbered locks without a pause overlay.");
            Check(Mathf.Abs(VrWorldHud.LockRingWidth(200) / VrWorldHud.LockRingWidth(20) - 10) < .001f,
                "Lock-ring stroke thickness must preserve its visual angle across distance.");
            Capture("vr-lock-feedback.png");
            report.angularLockStroke = true;
            report.phaseBeforePause = MeasurePhaseError();
            game.TogglePause();
            hud.Tick((float)game.Music.Time, .016f);
            Check(hud.PauseMenuVisible && hud.VisibleLocks == 0, "Pause must show its own menu and hide combat overlays.");
            double frozen = game.Music.Time;
            yield return new WaitForSecondsRealtime(.15f);
            Check(Math.Abs(game.Music.Time - frozen) < .003, "Pause must freeze the DSP music clock.");
            game.TogglePause();
            hud.Tick((float)game.Music.Time, .016f);
            Check(!hud.PauseMenuVisible, "Resume must remove the world-space pause menu.");
            report.pauseUi = true;
            report.phaseAfterPause = MeasurePhaseError();

            for (int i = 0; i < 8; i++) game.Combat.AcquireAt(Vector2.zero);
            int fired = game.Combat.Fired, hits = game.Combat.Hits;
            game.Combat.Release();
            float deadline = Time.realtimeSinceStartup + 4;
            while (game.Combat.HasPending && Time.realtimeSinceStartup < deadline) yield return null;
            Check(game.Combat.Fired - fired == 8 && game.Combat.Hits - hits == 8 && !game.Combat.HasPending &&
                game.Music.DroppedNotes == 0 && game.Music.MaxGridError <= 1.0 / AuthoredScore.Data.sampleRate,
                "VR's eight-note volley must retain all impacts on the TidalMemory grid.");
            report.scheduledHits = game.Combat.Hits - hits;
            report.gridError = game.Music.MaxGridError;
            report.playbackPhaseError = game.Music.MaxPlaybackPhaseError;
            report.phaseAtFinish = MeasurePhaseError();
            Check(Math.Abs(report.phaseAtStart) < .08 && Math.Abs(report.phaseAtFinish) < .08 && report.playbackPhaseError < .08,
                "TidalMemory's real playback cursor must follow its authored clock from startup onward.");
            camera.fieldOfView = fov; camera.aspect = aspect;
            hud.SetVrActive(false);
            game.Flight.EnableVr(false);
            Check(Math.Abs(game.Combat.EffectiveAcquireRange(far) - 105) < .001,
                "Exiting VR must restore desktop acquisition range.");
            InspectTravel();
            Finish();
        }

        void InspectTravel()
        {
            Flight flight = game.Flight;
            flight.SetPose(CaveLayout.Rooms[2].Center, Quaternion.identity);
            flight.SetTravelContext(true, (float)game.Music.Time);
            int pulses = flight.TravelPulseCount;
            for (int i = 0; i < 42; i++) flight.Step(0, 1f / 60, Vector3.forward, Vector2.zero, true);
            Check(Mathf.Abs(flight.Speed - 52f) < .02f && flight.TravelProgress < .001f,
                "Initial dash must retain its original 0.7-second acceleration.");
            for (int i = 0; i < 210; i++) flight.Step(0, 1f / 60, Vector3.forward, Vector2.zero, true);
            report.desktopTravelSpeed = flight.Speed;
            Check(Mathf.Abs(flight.Speed - 78f) < .02f && flight.TravelPulseCount == pulses + 1,
                "Sustained peaceful boost must reach 1.5x and signal exactly once.");
            flight.SetTravelContext(false, 0);
            for (int i = 0; i < 60; i++) flight.Step(0, 1f / 60, Vector3.forward, Vector2.zero, true);
            Check(Mathf.Abs(flight.Speed - 52f) < .02f && flight.TravelProgress == 0,
                "Combat must blend cruising back to the ordinary dash speed.");

            flight.SetPose(CaveLayout.Rooms[2].Center, Quaternion.identity);
            flight.EnableVr(true);
            flight.SetVrHeadPose(new Vector3(.1f, 1.6f, 0), Quaternion.Euler(-12, 0, 0));
            float fov = flight.View.fieldOfView;
            Quaternion head = flight.View.transform.localRotation;
            flight.SetTravelContext(true, (float)game.Music.Time);
            for (int i = 0; i < 252; i++) flight.StepVr(1f / 60, Vector2.up, 0, 0, true);
            report.vrTravelSpeed = flight.Speed;
            Check(Mathf.Abs(flight.Speed - 60f) < .02f &&
                Quaternion.Angle(head, flight.View.transform.localRotation) < .002f &&
                Mathf.Abs(fov - flight.View.fieldOfView) < .001f,
                "VR travel must reach 1.5x without altering head pose or projection.");
            flight.SuspendInput();
            Check(flight.TravelSpeedMultiplier == 1, "Pause/focus transitions must cancel travel gear.");
            flight.EnableVr(false);
            report.travel = true;
        }

        void ResetPose(Vector3 position, Quaternion rotation)
        {
            game.Flight.SetPose(CaveLayout.Rooms[2].Center, Quaternion.identity);
            game.Flight.SetVrHeadPose(position, rotation);
        }

        double MeasurePhaseError()
        {
            foreach (var source in game.GetComponents<AudioSource>())
                if (source.clip == game.soundtrack)
                    return game.Music.Time % AuthoredScore.Duration - source.timeSamples / (double)source.clip.frequency;
            return double.NaN;
        }

        LockTarget AddTarget(Vector3 position, float range = Encounter.LockRange)
        {
            var target = new LockTarget { hp = 1, kind = TargetKind.Environment, position = position, acquireRange = range,
                visual = new GameObject("Feedback proof target") };
            target.visual.transform.position = position;
            temporaryTargets.Add(target); game.Combat.Targets.Add(target);
            return target;
        }

        void Capture(string filename)
        {
            var texture = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            texture.Create();
            RenderPipeline.SubmitRenderRequest(game.Flight.View, new RenderPipeline.StandardRequest { destination = texture });
            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
            File.WriteAllBytes(Path.Combine(output, filename), image.EncodeToPNG());
            RenderTexture.active = previous;
            Destroy(image); texture.Release(); Destroy(texture);
        }

        void Finish()
        {
            if (finished) return;
            finished = true;
            foreach (var target in temporaryTargets)
            {
                game.Combat.Targets.Remove(target);
                if (target.visual) Destroy(target.visual);
            }
            report.errors = errors.ToArray(); report.passed = errors.Count == 0;
            report.hardwareVrTested = false;
            File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
            Application.Quit(report.passed ? 0 : 2);
        }

        void OnDestroy() { Application.logMessageReceived -= OnLog; }
        [Serializable] sealed class Report
        {
            public bool passed, tidalThroughout, strafe, vertical, gazeFlight, speedCaps, continuousYaw, headPosePreserved;
            public bool gazeCone, rangeAndWalls, eightLocks, angularLockStroke, pauseUi, hardwareVrTested;
            public int scheduledHits;
            public bool travel;
            public float desktopTravelSpeed, vrTravelSpeed;
            public double gridError, playbackPhaseError;
            public double phaseAtStart, phaseBeforePause, phaseAfterPause, phaseAtFinish;
            public string[] errors;
        }
    }
}
