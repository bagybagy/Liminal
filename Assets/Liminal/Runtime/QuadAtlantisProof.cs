using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Liminal
{
    public sealed class QuadAtlantisProof : MonoBehaviour
    {
        const float Step = 1f / 30f;
        Experience game;
        string output;
        readonly Report report = new();
        readonly List<string> errors = new();
        float song;

        public void Initialize(Experience owner)
        {
            game = owner;
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "--output");
            output = at >= 0 && at + 1 < args.Length ? args[at + 1] : Path.GetFullPath("Verification/QuadAtlantis");
            Directory.CreateDirectory(output);
            Application.logMessageReceived += OnLog;
        }

        IEnumerator Start()
        {
            float deadline = Time.realtimeSinceStartup + 8f;
            while (!game.Ready && Time.realtimeSinceStartup < deadline) yield return null;
            try
            {
                Check(game.Ready, "Experience readiness timeout.");
                game.ManualProofTick = true;
                AudioListener.volume = 0;
                song = (float)game.Music.Time;
                report.originalQuadDefault = game.ParticleLook.IsLegacy &&
                    Shader.GetGlobalFloat("_LiminalQuadStyle") > .5f && !ParticleLook.SideBySide;
                Check(report.originalQuadDefault, "Startup must use original Quad without comparison ghosts.");
                VerifyComparison();
                VerifyVrPresentation();
                VerifyAtlantis();
            }
            catch (Exception exception) { errors.Add(exception.ToString()); }
            finally
            {
                game.ParticleLook.SetSideBySide(false);
                game.ParticleLook.SetStyle(true);
                report.errors = errors.ToArray();
                report.passed = errors.Count == 0;
                File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
                Application.logMessageReceived -= OnLog;
                Debug.Log("LIMINAL_QUAD_ATLANTIS_PROOF passed=" + report.passed);
                Application.Quit(report.passed ? 0 : 1);
            }
        }

        void VerifyComparison()
        {
            for (int i = 0; i < 16; i++) TickMarine();
            int targetCount = game.Combat.Targets.Count;
            int initializations = game.Marine.Matter.InitializationCount;
            MatterParticle[] before = game.Marine.Matter.Readback();
            Vector3 jelly = game.Marine.JellyTargets[0].position;
            Frame(jelly + new Vector3(0, 2, -32), jelly);
            game.ParticleLook.SetStyle(false);
            uint sharp = Capture("jelly-sharp.png");
            game.ParticleLook.SetStyle(true);
            uint legacy = Capture("jelly-original-quad.png");
            Check(sharp != legacy, "Sharp and original Quad did not produce distinct images at the same state and camera.");
            report.distinctRenderStyles = sharp != legacy;
            game.ParticleLook.SetSideBySide(true);
            Vector3 mid = jelly + PersistentMatter.JellyComparisonOffset * .5f;
            Frame(mid + new Vector3(0, 3, -44), mid);
            Capture("jelly-side-by-side.png");
            MatterParticle[] after = game.Marine.Matter.Readback();
            bool stable = before.Length == after.Length;
            for (int i = 0; stable && i < before.Length; i++)
                stable = before[i].identityState == after[i].identityState && before[i].positionAge == after[i].positionAge;
            report.identicalSimulationBuffer = stable;
            report.targetCountUnchanged = game.Combat.Targets.Count == targetCount;
            report.initializationCountUnchanged = game.Marine.Matter.InitializationCount == initializations;
            Check(stable && report.targetCountUnchanged && report.initializationCountUnchanged,
                "Comparison must not alter particle state, target registration, or initialize a second simulation.");

            Vector3 serpent = Anatomy.Head(song);
            mid = serpent + LeviathanVfx.ComparisonOffset * .5f;
            Frame(mid + new Vector3(0, 38, -210), mid + Vector3.back * 20f);
            game.World.Tick(song, .25f, 0, false);
            Capture("serpent-side-by-side.png");

            Vector3 player = game.Marine.Arrival.Origin + new Vector3(0, 10, -140);
            Frame(player, game.Marine.Arrival.Origin);
            for (int i = 0; i < 480 && !game.Marine.WhaleEntranceComplete; i++) TickMarine();
            Check(game.Marine.WhaleEntranceComplete, "Whale arrival did not finish within the bounded simulated sequence.");
            // The final arrival frame changes the pose; let the shared GPU particles follow before framing them.
            for (int i = 0; i < 45; i++) TickMarine();
            Vector3 whale = Vector3.zero;
            int whalePoints = 0;
            MatterParticle[] whaleState = game.Marine.Matter.Readback();
            for (int i = 0; i < whaleState.Length; i++)
            {
                if (game.Marine.SeedAt(i).traits.y < 2.5f) continue;
                Vector3 position = whaleState[i].positionAge;
                Check(float.IsFinite(position.x) && float.IsFinite(position.y) && float.IsFinite(position.z),
                    "Nonfinite whale particle at ID " + i);
                whale += position;
                whalePoints++;
            }
            whale /= Mathf.Max(1, whalePoints);
            report.whaleParticleCenter = whale;
            mid = whale + PersistentMatter.WhaleComparisonOffset * .5f;
            Frame(mid + new Vector3(0, -30, -285), mid);
            Capture("whale-side-by-side.png");
            game.ParticleLook.SetSideBySide(false);
            game.ParticleLook.SetStyle(true);
        }

        void VerifyAtlantis()
        {
            var release = typeof(MarineLife).GetMethod("ReleaseWhale", BindingFlags.Instance | BindingFlags.NonPublic);
            Check(release != null, "Whale release hook unavailable.");
            release.Invoke(game.Marine, new object[] { song });
            game.Combat.EnterAfterglow();
            game.Finale.Begin(RunProgress.OptionalBosses, song);
            for (int i = 0; i < 600; i++)
            {
                TickMarine();
                game.Finale.Tick(song, Step);
            }
            game.Brightness.SetFinaleGlow(1);
            Check(game.Finale.Formation > .99f && game.Marine.WhaleReleased, "Whale-city transition did not settle.");
            report.desktopAuxiliaryPoints = game.Finale.DesktopAuxiliaryPointCount;
            report.vrAuxiliaryPoints = game.Finale.VrAuxiliaryPointCount;
            report.flamePoints = game.Finale.FlamePointCount;
            Check(report.flamePoints == SacredFlame.DesktopParticleCount, "Sacred flame particle pool is missing.");
            var flame = game.World.GetComponentInChildren<SacredFlame>(true);
            Check(flame, "Sacred flame component is missing.");
            var flameMaterial = flame.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            Check(Mathf.Approximately(flameMaterial.GetFloat("_ColorRate"), .125f) &&
                Mathf.Approximately(flameMaterial.GetFloat("_FlameWidth"), 6f) &&
                Mathf.Approximately(flameMaterial.GetFloat("_FlameHeight"), 24.5f),
                "Sacred flame render profile does not match its half-speed palette and broader silhouette.");
            flame.SetProgress(1, 0);
            Color paletteStart = flame.PaletteColorDiagnostic;
            flame.SetProgress(1, 4);
            Color paletteMid = flame.PaletteColorDiagnostic;
            flame.SetProgress(1, 8);
            Color paletteEnd = flame.PaletteColorDiagnostic;
            report.flamePaletteEightBeatCycle = paletteStart == paletteEnd && paletteStart != paletteMid;
            Check(report.flamePaletteEightBeatCycle, "Flame color cycle must take eight beats rather than four.");
            game.Finale.Tick(song, 0);
            Check(report.desktopAuxiliaryPoints <= AtlantisGeometry.AuxiliaryPointBudget &&
                report.vrAuxiliaryPoints <= AtlantisGeometry.VrAuxiliaryPointBudget, "Atlantis auxiliary point budget exceeded.");
            Vector3 city = AtlantisGeometry.CityOrigin;
            Frame(city + new Vector3(240, 150, -275), city + Vector3.up * 42);
            Capture("atlantis-wide.png");
            Frame(city + new Vector3(26, 28, -60), city + Vector3.up * 37);
            uint first = Capture("holy-flame-a.png");
            for (int i = 0; i < 20; i++)
            {
                TickMarine();
                game.Finale.Tick(song, Step);
            }
            uint second = Capture("holy-flame-b.png");
            Check(first != second, "Holy flame and composing light must change over time.");
            report.flameDynamic = first != second;
            game.Finale.ResetFinale();
            Check(!game.Finale.Active && game.Finale.ActivePointCount == 0, "Finale reset left active city particles.");
            game.Finale.Begin(BossId.None, song);
            for (int i = 0; i < 600; i++) game.Finale.Tick(song + i * Step, Step);
            Check(game.Finale.LayerCount == 1, "Base ending must not require optional boss clears.");
            Frame(city + new Vector3(26, 28, -60), city + Vector3.up * 37);
            Capture("holy-flame-base-ending.png");
            report.baseEndingVerified = true;
        }

        void VerifyVrPresentation()
        {
            var hud = game.GetComponent<VrWorldHud>();
            Check(hud, "VR presentation component is missing.");
            game.Flight.EnableVr(true);
            game.Flight.SetVrHeadPose(Vector3.zero, Quaternion.identity);
            try
            {
                CaveLayout.GetPortal(0, true, out Vector3 mouth, out _);
                Frame(CaveLayout.Rooms[0].Center, mouth);
                hud.SetVrActive(true);
                hud.Tick(song, Step);
                report.vrGameplayHudHidden = hud.GameplayHudHidden && !hud.PauseMenuVisible;
                report.vrPassageLabelsKept = hud.VisiblePassageLabels > 0;
                Check(report.vrGameplayHudHidden && report.vrPassageLabelsKept,
                    "Normal VR presentation must hide gameplay HUD and retain passage destinations.");
                game.TogglePause();
                hud.Tick(song, Step);
                Check(hud.PauseMenuVisible, "VR pause menu did not open.");
                const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
                var selection = typeof(VrWorldHud).GetField("selectedMenuItem", privateInstance);
                var refresh = typeof(VrWorldHud).GetMethod("RefreshPauseMenu", privateInstance);
                var confirm = typeof(VrWorldHud).GetMethod("ConfirmMenuSelection", privateInstance);
                var adjust = typeof(VrWorldHud).GetMethod("AdjustSelectedOption", privateInstance);
                Check(selection != null && refresh != null && confirm != null && adjust != null,
                    "VR pause menu acceptance hooks are unavailable.");
                string[] required = { "RESUME", "MUSIC", "BRIGHTNESS", "RESET BRIGHTNESS", "REDUCED MOTION",
                    "PARTICLE STYLE", "SIDE BY SIDE", "REPLAY TUTORIAL", "SKIP TUTORIAL", "RESTART RUN",
                    "RECENTER VIEW", "EXIT PCVR", "QUIT" };
                for (int i = 0; i < required.Length; i++)
                {
                    selection.SetValue(hud, i);
                    refresh.Invoke(hud, new object[] { false, false, required.Length });
                    Check(hud.PauseMenuText.Contains(required[i]), "VR pause option is missing: " + required[i]);
                }
                report.vrPauseSettingsParity = true;
                float previousVolume = game.Music.Volume;
                selection.SetValue(hud, 1);
                adjust.Invoke(hud, new object[] { previousVolume > .05f ? -1 : 1 });
                Check(!Mathf.Approximately(previousVolume, game.Music.Volume), "VR music setting did not respond.");
                game.Music.SetVolume(previousVolume);
                selection.SetValue(hud, 7);
                refresh.Invoke(hud, new object[] { false, false, required.Length });
                Capture("vr-pause-tutorial.png");
                confirm.Invoke(hud, new object[] { false, false });
                game.Tutorial.ObserveInput(Vector2.zero, Vector3.zero, false, false, false, song, Step);
                report.vrTutorialReplay = game.Tutorial.Enabled && game.Tutorial.UsesVrInstructions &&
                    game.Tutorial.StepIndex == 0 && !game.Music.Paused;
                Check(report.vrTutorialReplay, "VR tutorial replay must enable VR instructions and resume play.");
                game.Tutorial.ObserveInput(new Vector2(6, 0), Vector3.zero, false, false, false, song, .2f);
                foreach (var direction in new[] { Vector3.forward, Vector3.left, Vector3.back, Vector3.right })
                    game.Tutorial.ObserveInput(Vector2.zero, direction, false, false, false, song, .2f);
                game.Tutorial.ObserveInput(Vector2.zero, Vector3.down, false, false, false, song, .2f);
                game.Tutorial.ObserveInput(Vector2.zero, Vector3.up, false, false, false, song, .2f);
                game.Tutorial.ObserveInput(Vector2.zero, Vector3.forward, true, false, false, song, .3f);
                report.vrTutorialMovement = game.Tutorial.StepIndex == 3 &&
                    game.Tutorial.Status.Contains("TRIGGER");
                Check(report.vrTutorialMovement, "VR movement and boost inputs must reveal the trigger shooting lesson.");
                hud.Tick(song, Step);
                Check(hud.GameplayHudHidden && !hud.PauseMenuVisible,
                    "Replaying a tutorial must not restore hidden VR HUD.");
            }
            finally
            {
                if (game.Music.Paused) game.TogglePause();
                game.Tutorial.SetEnabled(false);
                hud.SetVrActive(false);
                game.Flight.EnableVr(false);
            }
        }

        void TickMarine()
        {
            song += Step;
            game.Marine.Tick(song, Step, game.Flight.Position);
            game.World.Tick(song, .25f, 0, false);
        }

        void Frame(Vector3 position, Vector3 focus)
        {
            Quaternion rotation = Quaternion.LookRotation(focus - position, Vector3.up);
            game.Flight.SetPose(position, rotation);
            // A diagnostic framing request is not a player move: don't clamp the render camera into a passage.
            game.Flight.View.transform.SetPositionAndRotation(position, rotation);
        }

        uint Capture(string name)
        {
            const int width = 1600, height = 900;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            Texture2D image = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                target.Create();
                RenderPipeline.SubmitRenderRequest(game.Flight.View,
                    new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                Color32[] pixels = image.GetPixels32();
                int visible = 0, white = 0;
                uint hash = 2166136261;
                foreach (Color32 pixel in pixels)
                {
                    if (pixel.r > 12 || pixel.g > 12 || pixel.b > 12) visible++;
                    if (pixel.r > 245 && pixel.g > 245 && pixel.b > 245) white++;
                    unchecked { hash = (hash ^ pixel.r) * 16777619; hash = (hash ^ pixel.g) * 16777619; hash = (hash ^ pixel.b) * 16777619; }
                }
                File.WriteAllBytes(Path.Combine(output, name), image.EncodeToPNG());
                Check(visible > 100, name + " is blank.");
                report.maximumWhiteFraction = Mathf.Max(report.maximumWhiteFraction, white / (float)pixels.Length);
                if (name.StartsWith("atlantis") || name.StartsWith("holy-flame"))
                    Check(white / (float)pixels.Length < .08f, name + " is washing out with white glow.");
                return hash;
            }
            finally
            {
                RenderTexture.active = previous;
                if (image) Destroy(image);
                target.Release();
                Destroy(target);
            }
        }

        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        void OnLog(string message, string stack, LogType kind)
        {
            if (kind != LogType.Error && kind != LogType.Exception && kind != LogType.Assert) return;
            if (errors.Count < 12 && !errors.Contains(message)) errors.Add(message);
        }

        [Serializable] sealed class Report
        {
            public bool passed, distinctRenderStyles, identicalSimulationBuffer, targetCountUnchanged;
            public bool initializationCountUnchanged, flameDynamic, baseEndingVerified;
            public bool originalQuadDefault, flamePaletteEightBeatCycle;
            public bool vrGameplayHudHidden, vrPassageLabelsKept, vrTutorialReplay, vrTutorialMovement;
            public bool vrPauseSettingsParity;
            public int desktopAuxiliaryPoints, vrAuxiliaryPoints, flamePoints;
            public Vector3 whaleParticleCenter;
            public float maximumWhiteFraction;
            public bool hardwareVrTested = false;
            public string[] errors;
        }
    }
}
