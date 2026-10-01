using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Liminal
{
    public sealed class ExpansionProof : MonoBehaviour
    {
        Experience experience;
        string directory;
        readonly List<string> errors = new();
        readonly List<float> visibleFractions = new();
        readonly Report report = new();
        float started;
        bool finished;

        public void Initialize(Experience value)
        {
            experience = value;
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "--output");
            directory = at >= 0 && at + 1 < args.Length ? args[at + 1] :
                Path.GetFullPath(Path.Combine(Application.dataPath, "../Verification/Expansion"));
            Directory.CreateDirectory(directory);
            started = Time.realtimeSinceStartup;
            Application.logMessageReceived += OnLog;
        }

        void OnLog(string message, string stack, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && errors.Count < 20)
                errors.Add(message + "\n" + stack);
        }
        void Update()
        {
            if (!finished && Time.realtimeSinceStartup - started > 210) {
                Require(false, "Expansion proof exceeded its bounded runtime"); Finish();
            }
        }

        IEnumerator Start()
        {
            yield return null;
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--navigation-only")>=0) {
                yield return InspectNavigation();yield break;
            }
            report.connectedRooms = TravelRoutes();
            Require(report.connectedRooms, "All five chambers must have traversable open passages");
            experience.Restart();
            yield return new WaitForSecondsRealtime(.4f);
            yield return InspectTutorial();
            experience.Tutorial.SetEnabled(false);

            var hermits = experience.Hermits;
            report.hermitParticles = hermits.ParticleCount;
            int hermitInitializations = hermits.InitializationCount;
            Frame(CaveLayout.Rooms[3].Center + Vector3.down * 95, new Vector3(0, 30, -120));
            yield return new WaitForSecondsRealtime(1f);
            Capture("02-hermit-swarm.png");
            yield return HitBatch(hermits.SmallTargets, 8);
            Require(hermits.SmallDefeated == 8 && hermits.Merging, "One third of the swarm must trigger particle coalescence");
            yield return new WaitForSecondsRealtime(4.5f);
            report.hermitMerge = hermits.MergeProgress;
            Require(hermits.MergeProgress > .99f && hermits.BossTargets.Count == 16, "Persistent crab matter must form a sixteen-point giant");
            Frame(hermits.BossPosition, new Vector3(60, 45, -100));
            Capture("03-giant-hermit.png");
            float deadline = Time.realtimeSinceStartup + 35;
            while (!hermits.Complete && !experience.Combat.Ended && Time.realtimeSinceStartup < deadline) {
                if (HasAvailable(hermits.BossTargets)) yield return HitBatch(hermits.BossTargets, 8);
                else yield return null;
            }
            report.hermitHits = hermits.BossHits;
            report.hermitComplete = hermits.Complete;
            Require(hermits.Complete && hermits.BossHits == 32, "Giant must finish after thirty-two normally scheduled impacts");
            Require(hermits.ParticleCount == report.hermitParticles && hermits.InitializationCount == hermitInitializations,
                "Hermit state changes must retain their original particle pool");
            Frame(hermits.BossPosition, new Vector3(60, 45, -100));
            yield return new WaitForSecondsRealtime(2f);
            Capture("04-shell-refuge.png");

            var submarines = experience.Submarines;
            report.submarineParticles = submarines.ParticleCount;
            int submarineInitializations = submarines.InitializationCount;
            Frame(submarines.Focus, new Vector3(70, 35, -150));
            yield return new WaitForSecondsRealtime(1f);
            for (int phase = 0; phase < 3; phase++) {
                Require(submarines.Phase == phase, "Submarine phases must preserve their authored order");
                Frame(submarines.Focus, new Vector3(100, 60, -160));
                Capture("05-submarine-phase-" + phase + ".png");
                deadline = Time.realtimeSinceStartup + 45;
                while (submarines.Phase == phase && !experience.Combat.Ended && Time.realtimeSinceStartup < deadline) {
                    if (!submarines.Transitioning && HasAvailable(submarines.Targets)) yield return HitBatch(submarines.Targets, 8);
                    else yield return null;
                }
                while (submarines.Transitioning && Time.realtimeSinceStartup < deadline) yield return null;
            }
            report.submarineHits = submarines.TotalHits;
            report.completedPhases = submarines.CompletedPhases;
            report.submarineComplete = submarines.Complete;
            Require(submarines.Complete && submarines.TotalHits == 160 && submarines.CompletedPhases == 3,
                "Large hull, three craft and mechanical giant must all be beatable through normal fire");
            Require(submarines.ParticleCount == report.submarineParticles && submarines.InitializationCount == submarineInitializations,
                "Submarine transformations must preserve their original particle identities");
            Frame(submarines.Focus, new Vector3(100, 70, -180));
            yield return new WaitForSecondsRealtime(2f);
            Capture("06-engine-reef.png");
            report.pressureShots = experience.Combat.SpawnedPressureShots;
            report.interceptions = experience.Combat.InterceptedPressureShots;
            report.damageTaken = experience.Combat.DamageTaken;
            report.gridError = experience.Music.MaxGridError;
            report.hits = experience.Combat.Hits;
            report.notes = experience.Music.ScheduledNotes;
            Require(report.pressureShots > 0, "New stages must launch genuinely interceptable pressure shots");
            Require(report.interceptions > 0, "New pressure patterns must be intercepted through normal lock fire");
            Require(report.gridError < .00003 && experience.Music.DroppedNotes == 0 && experience.Combat.MissedScheduledHits == 0,
                "New stage impacts must remain on the actual soundtrack sample grid without dropped notes");
            InspectHalo();

            experience.Restart();
            yield return null;
            report.restartClean = hermits.SmallDefeated == 0 && hermits.BossHits == 0 && !hermits.Complete &&
                submarines.Phase == 0 && submarines.TotalHits == 0 && !submarines.Complete;
            var unique = new HashSet<LockTarget>(experience.Combat.Targets);
            report.restartClean &= unique.Count == experience.Combat.Targets.Count;
            foreach (var target in hermits.SmallTargets) report.restartClean &= experience.Combat.Targets.Contains(target) && target.Available;
            foreach (var target in submarines.Targets) report.restartClean &= experience.Combat.Targets.Contains(target) && target.hp == 1 && target.reserved == 0;
            experience.Tutorial.SetEnabled(true);
            report.restartClean &= experience.Tutorial.StepIndex == 0 && !experience.Tutorial.Complete;
            Require(report.restartClean, "Restart must restore new stages and replayable tutorial without duplicate targets");
            Finish();
        }

        IEnumerator InspectNavigation()
        {
            report.mode="navigation";
            Require(experience.PassageGuide.PortalCount==CaveLayout.Passages.Length*2,"Every passage must have two visible mouths");
            for(int room=0;room<CaveLayout.Passages.Length;room++) {
                CaveLayout.GetPortal(room,true,out Vector3 portal,out Vector3 direction);
                CaveLayout.GetPortal(room,false,out Vector3 reverse,out _);
                Require(Mathf.Abs(CaveLayout.RoomDistance(room,portal)-1)<.0001f &&
                    Mathf.Abs(CaveLayout.RoomDistance(room+1,reverse)-1)<.0001f,"Beacons must mark actual wall openings");
                Vector3 start=room==0?CaveLayout.Spawn:CaveLayout.Passages[room-1][CaveLayout.Passages[room-1].Length-1];
                experience.Flight.SetPose(start,Quaternion.LookRotation(portal-start));
                Require(CaveLayout.NextPassage(start,out Vector3 waypoint,out int next) && next==room+1 &&
                    Vector3.Distance(portal,waypoint)<.01f,"Uncleared rooms must point to the next passage rather than the boss");
                yield return null;
                Capture("navigation-room-"+room+".png");
                var image=Render(experience.Flight.View,1600,900);
                Vector3 projected=experience.Flight.View.WorldToScreenPoint(portal);
                int cx=Mathf.RoundToInt(projected.x*1600/Screen.width),cy=Mathf.RoundToInt(projected.y*900/Screen.height);
                int bright=0;
                for(int y=Mathf.Max(0,cy-55);y<Mathf.Min(900,cy+55);y++)
                    for(int x=Mathf.Max(0,cx-55);x<Mathf.Min(1600,cx+55);x++) {
                        Color color=image.GetPixel(x,y);
                        if(Mathf.Max(color.g,color.b)>.45f) bright++;
                    }
                Require(bright>30,"Passage mouth must be rendered visibly from chamber "+room);
                Destroy(image);
                Require(CaveLayout.Contains(portal+direction*8),"Forward passage marker must lead into traversable space");
            }
            report.connectedRooms=TravelRoutes();
            Require(report.connectedRooms,"Navigation changes must preserve all physical passage routes");
            Finish();
        }

        IEnumerator InspectTutorial()
        {
            var tutorial = experience.Tutorial;
            tutorial.SetEnabled(true);
            tutorial.ResetTutorial();
            yield return null;
            Capture("01-particle-tutorial.png");
            for (int i = 0; i < 30; i++) Observe(Vector2.zero, Vector3.zero, false, false, false);
            Require(tutorial.StepIndex == 0, "Idle time must not advance the input tutorial");
            for (int i = 0; i < 12; i++) Observe(Vector2.right, Vector3.zero, false, false, false);
            yield return new WaitForSecondsRealtime(.8f);
            foreach (var direction in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                for (int i = 0; i < 18; i++) Observe(Vector2.zero, direction, false, false, false);
            yield return new WaitForSecondsRealtime(.8f);
            foreach (var direction in new[] { Vector3.up, Vector3.down })
                for (int i = 0; i < 18; i++) Observe(Vector2.zero, direction, false, false, false);
            for (int i = 0; i < 24; i++) Observe(Vector2.zero, Vector3.forward, true, false, false);
            yield return new WaitForSecondsRealtime(.8f);
            Require(tutorial.StepIndex == 3, "Real look, swim, ascent/descent and boost observations must reveal shooting lesson");
            int before = experience.Music.ScheduledNotes;
            yield return ShootTutorial(1);
            yield return new WaitForSecondsRealtime(.8f);
            Require(tutorial.StepIndex == 4, "Only actual scheduled target impact may advance the single-lock lesson");
            yield return ShootTutorial(8);
            yield return new WaitForSecondsRealtime(.8f);
            report.tutorialComplete = tutorial.Complete;
            report.tutorialActions = tutorial.CompletedActions;
            report.tutorialNotes = experience.Music.ScheduledNotes - before;
            Require(tutorial.Complete && report.tutorialNotes == 9, "Tutorial must finish with one and eight genuine musical locks");
        }

        void Observe(Vector2 look, Vector3 move, bool boost, bool held, bool released)
        {
            float song = (float)experience.Music.Time;
            experience.Flight.Step(song, .02f, move, look, boost);
            experience.Tutorial.ObserveInput(look, move, boost, held, released, song, .02f);
        }

        IEnumerator ShootTutorial(int count)
        {
            var targets = experience.Tutorial.Targets;
            int acquired = 0;
            foreach (var target in targets) {
                if (!target.Available || acquired >= count) continue;
                Vector3 projection = experience.Flight.View.WorldToScreenPoint(target.position);
                int before = experience.Combat.Locks.Count;
                experience.Combat.AcquireAt(projection);
                if (experience.Combat.Locks.Count > before) acquired++;
            }
            Require(acquired == count, "Particle instruction board must offer the requested real lock count");
            for (int i = 0; i < 30; i++) Observe(Vector2.zero, Vector3.zero, false, true, false);
            Observe(Vector2.zero, Vector3.zero, false, false, true);
            experience.Combat.Release();
            yield return ResolveShots();
        }

        IEnumerator HitBatch(IReadOnlyList<LockTarget> targets, int maximum)
        {
            int accepted = 0;
            foreach (var target in targets) {
                if (accepted >= maximum) break;
                if (!target.Available) continue;
                Vector3 center = CaveLayout.Rooms[CaveLayout.NearestRoom(target.position)].Center;
                Vector3 toward = (center - target.position).normalized;
                if (toward.sqrMagnitude < .01f) toward = Vector3.back;
                Frame(target.position, toward * 30);
                int before = experience.Combat.Locks.Count;
                experience.Combat.AcquireAt(experience.Flight.View.WorldToScreenPoint(target.position));
                if (experience.Combat.Locks.Count > before) accepted++;
            }
            Require(accepted > 0, "Stage targets must be visible and genuinely acquirable");
            experience.Combat.Release();
            yield return ResolveShots();
        }
        IEnumerator ResolveShots()
        {
            float deadline = Time.realtimeSinceStartup + 4;
            int intercepted = 0;
            while (experience.Combat.HasPending && Time.realtimeSinceStartup < deadline) {
                if (!experience.Tutorial.Enabled) {
                    experience.Flight.Step((float)experience.Music.Time, Mathf.Min(Time.unscaledDeltaTime, .05f),
                        Vector3.right, Vector2.zero, false);
                    if (intercepted < 3) {
                        foreach (var target in experience.Combat.Targets) {
                            if (!target.isPressureShot || !experience.Combat.CanAcquire(target)) continue;
                            int before = experience.Combat.Locks.Count;
                            experience.Combat.AcquireAt(experience.Flight.View.WorldToScreenPoint(target.position));
                            if (experience.Combat.Locks.Count > before) {intercepted++;break;}
                        }
                        if (experience.Combat.Locks.Count > 0) experience.Combat.Release();
                    }
                }
                yield return null;
            }
            Require(!experience.Combat.HasPending, "Accepted musical impacts must resolve in bounded time");
            yield return null;
        }
        static bool HasAvailable(IReadOnlyList<LockTarget> targets)
        {
            foreach (var target in targets) if (target.Available) return true;
            return false;
        }
        bool TravelRoutes()
        {
            foreach (var route in CaveLayout.Passages) {
                for (int p = 1; p < route.Length; p++) {
                    var flight = experience.Flight;
                    flight.SetPose(route[p-1], Quaternion.LookRotation(route[p] - route[p-1]));
                    int steps = Mathf.CeilToInt(Vector3.Distance(route[p-1], route[p]) / 24 * 60) + 120;
                    for (int i = 0; i < steps && Vector3.Distance(flight.Position, route[p]) > 1; i++)
                        flight.Step(0, 1f/60, Vector3.forward, Vector2.zero, false);
                    if (Vector3.Distance(flight.Position, route[p]) > 2) return false;
                }
            }
            return CaveLayout.Rooms.Length == 5;
        }
        void Frame(Vector3 focus, Vector3 offset)
        {
            Vector3 velocity = Vector3.zero;
            Vector3 position = CaveLayout.Constrain(focus + offset, ref velocity);
            experience.Flight.SetPose(position, Quaternion.LookRotation(focus - position));
            // Camera setup is a teleport, not a swept gameplay movement.
            foreach (var target in experience.Combat.Targets)
                if (target.isPressureShot) target.previousPlayerPosition = position;
        }
        Texture2D Render(Camera camera, int width, int height)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32); rt.Create();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
            var previous = RenderTexture.active; RenderTexture.active = rt;
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
            RenderTexture.active = previous; rt.Release(); Destroy(rt);
            return pixels;
        }
        void Capture(string name)
        {
            var pixels = Render(experience.Flight.View, 1600, 900);
            int lit = 0;
            foreach (var color in pixels.GetPixels32()) if (Mathf.Max(color.r, Mathf.Max(color.g, color.b)) > 60) lit++;
            visibleFractions.Add(lit / (float)(pixels.width * pixels.height));
            File.WriteAllBytes(Path.Combine(directory, name), pixels.EncodeToPNG()); Destroy(pixels);
        }
        void InspectHalo()
        {
            var root = new GameObject("Halo screen-size proof");
            var halo = root.AddComponent<HitHaloVfx>(); halo.Initialize();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.gameObject.layer = 30;
            var cameraObject = new GameObject("Halo proof camera");
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
            camera.backgroundColor = Color.black; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.cullingMask = 1 << 30; camera.farClipPlane = 1000; camera.fieldOfView = 54;
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
            int[] diameters = new int[2];
            for (int i = 0; i < 2; i++) {
                halo.ResetEffects(); halo.Spawn(Vector3.forward * (i == 0 ? 30 : 400), 0, new Color(.02f, .65f, 1), .8f);
                halo.Tick(.4f, false);
                var pixels = Render(camera, 640, 360);
                int min = pixels.width, max = -1;
                var colors = pixels.GetPixels32();
                for (int p = 0; p < colors.Length; p++) if (colors[p].b > 55) {
                    min = Mathf.Min(min, p % pixels.width); max = Mathf.Max(max, p % pixels.width);
                }
                diameters[i] = max >= min ? max - min + 1 : 0;
                File.WriteAllBytes(Path.Combine(directory, i == 0 ? "halo-near.png" : "halo-far.png"), pixels.EncodeToPNG());
                Destroy(pixels);
            }
            report.haloNear = diameters[0]; report.haloFar = diameters[1];
            Require(report.haloNear >= 55 && Mathf.Abs(report.haloFar - report.haloNear) <= 4,
                "Rendered hit halo must keep a readable screen footprint at 30 and 400 meters");
            Destroy(root); Destroy(cameraObject);
        }
        void Require(bool success, string message) { if (!success && errors.Count < 40) errors.Add(message); }
        void Finish()
        {
            if (finished) return;
            finished = true;
            report.errors = errors.ToArray(); report.passed = errors.Count == 0;
            report.visibleFractions = visibleFractions.ToArray(); report.gpu = SystemInfo.graphicsDeviceName;
            File.WriteAllText(Path.Combine(directory, "report.json"), JsonUtility.ToJson(report, true));
            Debug.Log("LIMINAL_EXPANSION_PROOF " + JsonUtility.ToJson(report));
            Application.Quit(report.passed ? 0 : 2);
        }
        void OnDestroy() { Application.logMessageReceived -= OnLog; }
        [Serializable] sealed class Report
        {
            public string mode="expansion";
            public bool passed, connectedRooms, tutorialComplete, hermitComplete, submarineComplete, restartClean;
            public int tutorialActions, tutorialNotes, hermitParticles, hermitHits, submarineParticles, submarineHits;
            public int completedPhases, pressureShots, interceptions, damageTaken, hits, notes, haloNear, haloFar;
            public float hermitMerge;
            public double gridError;
            public float[] visibleFractions;
            public string gpu;
            public string[] errors;
        }
    }
}
