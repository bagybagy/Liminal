using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Liminal
{
    public sealed class EncounterReviewProof : MonoBehaviour
    {
        Experience game;
        string output;
        float started;
        bool finished;
        readonly List<string> errors = new();
        readonly Report report = new();

        public void Initialize(Experience owner)
        {
            game = owner;
            started = Time.realtimeSinceStartup;
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "--output");
            output = at >= 0 && at + 1 < args.Length ? args[at + 1] : Path.GetFullPath("Verification/EncounterReview");
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
            if (!finished && Time.realtimeSinceStartup - started > 60) {
                errors.Add("Encounter review exceeded its bounded deadline.");
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
            while (!game.Ready) yield return null;
            game.Tutorial.SetEnabled(false);
            VrWorldHud hud = game.GetComponent<VrWorldHud>();
            game.Flight.EnableVr(true);
            hud.SetVrActive(true);
            for (int room = 0; room < CaveLayout.Rooms.Length; room++) {
                game.Flight.SetPose(CaveLayout.Rooms[room].Center, Quaternion.identity);
                yield return null;
                hud.Tick((float)game.Music.Time, .016f);
                Check(hud.VisiblePassageLabels == CaveLayout.IncidentPassages(room).Count,
                    "VR room must announce every incident passage: " + room);
                foreach (int passage in CaveLayout.IncidentPassages(room)) {
                    int destination = CaveLayout.FromRoom(passage) == room ? CaveLayout.ToRoom(passage) : CaveLayout.FromRoom(passage);
                    bool found = false;
                    foreach (string label in hud.PassageLabelTexts)
                        found |= label.Contains(CaveLayout.Rooms[destination].Name) && label.Contains(" M");
                    Check(found, "VR route label must have the correct destination and distance.");
                }
            }
            CaveLayout.GetPortal(1, true, out Vector3 mouth, out Vector3 direction);
            game.Flight.SetPose(mouth - direction * 80f, Quaternion.LookRotation(direction));
            yield return null;
            hud.Tick((float)game.Music.Time, .016f);
            foreach (var text in hud.GetComponentsInChildren<TextMesh>(true))
                if (text.name == "Onboarding" || text.name == "VR status") text.gameObject.SetActive(false);
            Capture("vr-route-announcement.png");
            game.TogglePause(); hud.Tick((float)game.Music.Time, .016f);
            Check(hud.VisiblePassageLabels == 0, "Paused VR must hide route labels.");
            game.TogglePause(); hud.SetVrActive(false);
            Check(hud.VisiblePassageLabels == 0, "Leaving VR must hide route labels.");
            game.Flight.EnableVr(false);
            report.vrRoutes = true;

            // State-machine coverage uses synthetic damage; the final volley still uses the real DSP/flight path.
            SubmarineEncounter sub = game.Submarines;
            game.Flight.SetPose(CaveLayout.Rooms[4].Center, Quaternion.identity);
            float song = (float)game.Music.Time;
            sub.Tick(song, .016f, true);
            for (int round = 0; round < 12 && !sub.Transitioning; round++) {
                DamageSubTargets(_ => true);
                sub.Tick(song, .016f, true);
            }
            Check(sub.TotalHits == SubmarineEncounter.SubmarineGoal && sub.Transitioning,
                "First submarine form must retain its damage cap.");
            AdvanceMorph(ref song);
            Check(sub.Phase == 1, "Three craft must form after the main hull.");

            LockTarget protectedTarget = FindCraftTarget(1);
            Check(protectedTarget != null, "Second craft must have live targets.");
            protectedTarget.reserved = 1;
            for (int round = 0; round < 8 && !sub.FleetCraftDefeated(0); round++) {
                DamageSubTargets(target => sub.FleetCraftIndexOf(target) == 0);
                sub.Tick(song += .05f, .05f, true);
            }
            Check(sub.FleetCraftDefeated(0) && sub.FleetCraftHits(0) == 16 && sub.PhaseHits == 16,
                "One craft must be independently beatable without harming the other two.");
            Check(protectedTarget.reserved == 1 && game.Combat.Targets.Contains(protectedTarget),
                "Independent rearm must preserve another craft's in-flight reservation.");
            protectedTarget.reserved = 0;
            Vector3 deathPosition = sub.FleetCraftCenter(0);
            game.Flight.SetPose(CaveLayout.Rooms[4].Center + new Vector3(80, 20, 100), Quaternion.identity);
            sub.Tick(song += 1.5f, .05f, true);
            Check(Vector3.Distance(deathPosition, sub.FleetCraftCenter(0)) < .001f,
                "Defeated craft matter must remain anchored at its death location.");
            foreach (LockTarget target in game.Combat.Targets)
                Check(!(target.isPressureShot && target.pressureOwner is GameObject owner &&
                    owner.name.Contains("submersible 1 pressure") && target.Available),
                    "A defeated craft must cease pressure fire.");
            Frame(deathPosition + new Vector3(25, 15, -70), deathPosition);
            Capture("defeated-craft-matter.png");
            for (int craft = 1; craft < 3; craft++)
                for (int round = 0; round < 8 && !sub.FleetCraftDefeated(craft); round++) {
                    int selected = craft;
                    DamageSubTargets(target => sub.FleetCraftIndexOf(target) == selected);
                    sub.Tick(song += .05f, .05f, true);
                }
            Check(sub.PhaseHits == 48 && sub.Transitioning, "All three defeated craft must initiate collective morphing: " +
                sub.PhaseHits + " / " + sub.FleetCraftHits(0) + "," + sub.FleetCraftHits(1) + "," + sub.FleetCraftHits(2));
            AdvanceMorph(ref song);
            Check(sub.Phase == 2 && SubmarineEncounter.PoseidonGoal == 128, "Poseidon must have doubled endurance.");

            for (int round = 0; round < 24 && sub.PhaseHits < 120; round++) {
                DamageSubTargets(_ => true, Mathf.Min(8, 120 - sub.PhaseHits));
                sub.Tick(song += .05f, .05f, true);
            }
            Check(sub.PhaseHits == 120 && !sub.Transitioning, "Poseidon must remain active before its 128th impact.");
            Vector3 focus = Vector3.zero;
            int count = 0;
            foreach (var target in sub.Targets) if (target.Available) { focus += target.position; count++; }
            Check(count >= 8, "Final Poseidon volley must expose eight live points.");
            focus /= count;
            Frame(focus + new Vector3(0, 20, -180), focus);
            foreach (var target in sub.Targets) if (game.Combat.Locks.Count < 8) game.Combat.Acquire(target);
            Check(game.Combat.Locks.Count == 8, "Eight Poseidon points must be normally lockable.");
            int hits = game.Combat.Hits;
            game.Combat.Release();
            float deadline = Time.realtimeSinceStartup + 5;
            while (game.Combat.HasPending && Time.realtimeSinceStartup < deadline) yield return null;
            Check(game.Combat.Hits == hits + 8 && sub.PhaseHits == 128 && sub.Transitioning,
                "Eight normally scheduled impacts must finish the enlarged Poseidon.");
            song = (float)game.Music.Time;
            AdvanceMorph(ref song);
            Check(sub.Complete && sub.TotalHits == SubmarineEncounter.TotalGoal, "Three submarine forms must total 224 hits.");
            report.submarineHits = sub.TotalHits;
            report.bossReleaseEvents = game.Music.BossReleaseEvents;
            Check(report.bossReleaseEvents == 5 && game.Music.MaxReleaseGridError < .00003,
                "Hull, three craft and Poseidon must each emit one beat-aligned release sound.");
            report.gridError = game.Music.MaxGridError;
            Check(game.Music.DroppedNotes == 0 && game.Combat.MissedScheduledHits == 0 && report.gridError < .00003,
                "Scheduled audio and impacts must not be lost.");

            game.Restart(); game.Tutorial.SetEnabled(false);
            game.Flight.SetPose(CaveLayout.Rooms[2].Center, Quaternion.identity);
            game.Combat.ActiveRoom = 2;
            Check(game.Marine.Dolphins.TrySpawn(0, game.Flight.Position + new Vector3(30, 0, 35), Quaternion.identity, 0),
                "Vertical swimming sample must spawn.");
            float minY = float.MaxValue, maxY = float.MinValue, maxDistance = 0;
            for (int i = 0; i < 1800; i++) {
                game.Marine.Dolphins.Tick(i / 60f, 1f / 60, false);
                var pose = game.Marine.Dolphins.PoseAt(0);
                minY = Mathf.Min(minY, pose.Position.y); maxY = Mathf.Max(maxY, pose.Position.y);
                maxDistance = Mathf.Max(maxDistance, Vector3.Distance(pose.Position, game.Flight.Position));
                Check(pose.Speed <= 80.01f, "Vertical swimming must preserve the dolphin speed cap.");
            }
            report.dolphinVerticalSpan = maxY - minY; report.dolphinMaxDistance = maxDistance;
            Check(report.dolphinVerticalSpan > 10 && maxDistance < 120, "Dolphins must vary height while remaining in nearby/midrange space.");

            game.Restart(); game.Tutorial.SetEnabled(false);
            Vector3 player = game.Marine.Arrival.Origin + new Vector3(0, 10, -140);
            game.Flight.SetPose(player, Quaternion.identity);
            Color previousColor = game.Marine.WhaleLightColor;
            bool previousComplete = false;
            for (int i = 0; i < 400; i++) {
                game.Marine.Tick(i * .05f, .05f, player);
                Color color = game.Marine.WhaleLightColor;
                Check(!float.IsNaN(color.r) && !float.IsNaN(color.g) && !float.IsNaN(color.b), "Whale color must remain finite before the first hit.");
                if (previousComplete) report.whaleMaxColorStep = Mathf.Max(report.whaleMaxColorStep,
                    new Vector3(color.r - previousColor.r, color.g - previousColor.g, color.b - previousColor.b).magnitude);
                previousColor = color; previousComplete = game.Marine.WhaleEntranceComplete;
            }
            Check(game.Marine.WhaleEntranceComplete && game.Marine.WhaleResonance == 0,
                "Idle whale sample must finish arrival without any player impacts.");
            Check(report.whaleMaxColorStep < .15f, "Idle whale must not chatter between surface and arrival colors.");
            report.whaleIdle = true;
            for (int school = 0; school < AtlantisGeometry.SerpentSchoolCount; school++) {
                var a = AtlantisGeometry.EvaluateSchoolPose(school, 0);
                var b = AtlantisGeometry.EvaluateSchoolPose(school, 4);
                Check(new Vector2(a.Center.x, a.Center.z).magnitude > 185 && a.Center.y > 75 &&
                    Vector3.Distance(a.Center, b.Center) > 4, "Sardine schools must swim independently beyond the temple roof.");
            }
            game.Finale.Begin(RunProgress.OptionalBosses, 0);
            for (int i = 0; i < 182; i++) game.Finale.Tick(20, .1f);
            Vector3 city = AtlantisGeometry.CityOrigin;
            Frame(city + new Vector3(310, 120, -320), city + Vector3.up * 55);
            Capture("atlantis-separated-shoals.png");
            report.atlantisSchools = AtlantisGeometry.SerpentSchoolCount;
            Finish();
        }

        LockTarget FindCraftTarget(int craft)
        {
            foreach (var target in game.Submarines.Targets)
                if (target.Available && game.Submarines.FleetCraftIndexOf(target) == craft) return target;
            return null;
        }

        void DamageSubTargets(Func<LockTarget, bool> predicate, int maximum = 8)
        {
            var snapshot = new List<LockTarget>(game.Submarines.Targets);
            foreach (var target in snapshot) {
                if (maximum <= 0) break;
                if (!target.Available || !predicate(target)) continue;
                target.hp--;
                target.onHit?.Invoke(target, (float)game.Music.Time);
                maximum--;
            }
        }

        void AdvanceMorph(ref float song)
        {
            for (int i = 0; i < 110 && game.Submarines.Transitioning; i++)
                game.Submarines.Tick(song += .05f, .05f, true);
            Check(!game.Submarines.Transitioning, "Morph must finish in its bounded duration.");
        }

        void Frame(Vector3 position, Vector3 focus) => game.Flight.SetPose(position, Quaternion.LookRotation(focus - position));

        void Capture(string name)
        {
            var target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32); target.Create();
            RenderPipeline.SubmitRenderRequest(game.Flight.View, new RenderPipeline.StandardRequest { destination = target });
            var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
            File.WriteAllBytes(Path.Combine(output, name), image.EncodeToPNG());
            RenderTexture.active = previous; Destroy(image); target.Release(); Destroy(target);
        }

        void Finish()
        {
            if (finished) return; finished = true;
            report.errors = errors.ToArray(); report.passed = errors.Count == 0;
            File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
            Application.Quit(report.passed ? 0 : 2);
        }

        void OnDestroy() => Application.logMessageReceived -= OnLog;
        [Serializable] sealed class Report
        {
            public bool passed, vrRoutes, whaleIdle;
            public int submarineHits, bossReleaseEvents, atlantisSchools;
            public float dolphinVerticalSpan, dolphinMaxDistance;
            public float whaleMaxColorStep;
            public double gridError;
            public string[] errors;
        }
    }
}
