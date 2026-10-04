using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Liminal
{
    public sealed class SharpMatterProof : MonoBehaviour
    {
        const float DeadlineSeconds = 60f;
        const int CaptureWidth = 1600;
        const int CaptureHeight = 900;
        const float Step = 1f / 60f;

        Experience game;
        string output;
        float started, lastManualRealtime;
        float lastAdvanceSeconds;
        bool finished;
        ParticleLook look;
        PointStudy study;
        readonly Report report = new();

        public void Initialize(Experience owner)
        {
            game = owner;
            started = Time.realtimeSinceStartup;
            lastManualRealtime = started;
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "--output");
            output = at >= 0 && at + 1 < args.Length
                ? args[at + 1]
                : Path.GetFullPath("Verification/SharpMatter");
            Directory.CreateDirectory(output);
            Application.logMessageReceived += OnLog;
        }

        IEnumerator Start()
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(RunProof());
            try
            {
                while (stack.Count > 0)
                {
                    IEnumerator current = stack.Peek();
                    bool moved;
                    object yielded = null;
                    try
                    {
                        moved = current.MoveNext();
                        if (moved) yielded = current.Current;
                    }
                    catch (Exception exception)
                    {
                        AddError(exception.ToString());
                        break;
                    }
                    if (!moved)
                    {
                        stack.Pop();
                        (current as IDisposable)?.Dispose();
                    }
                    else if (yielded is IEnumerator nested) stack.Push(nested);
                    else yield return yielded;
                }
            }
            finally
            {
                while (stack.Count > 0)
                {
                    (stack.Pop() as IDisposable)?.Dispose();
                }
            }
            if (!finished) Finish();
        }

        IEnumerator RunProof()
        {
            float readyDeadline = Time.realtimeSinceStartup + 8f;
            while (!game.Ready && Time.realtimeSinceStartup < readyDeadline) yield return null;
            Check(game.Ready, "Experience did not become ready within 8 seconds.");
            if (report.errors.Length > 0) yield break;

            game.ManualProofTick = true;
            float originalAudio = AudioListener.volume;
            look = game.GetComponent<ParticleLook>();
            study = game.PointStudy;
            Check(game.CavernMode && game.Marine != null && game.Marine.Matter.Ready,
                "Sharp matter proof requires the initialized cavern marine matter pool.");
            Check(look != null, "Production ParticleLook component was not initialized.");
            Check(study != null, "PointStudy must be initialized for the requested SharpQuad stress capture.");
            if (report.errors.Length > 0) yield break;
            AudioListener.volume = 0f;

            report.gpu = SystemInfo.graphicsDeviceName;
            report.graphicsApi = SystemInfo.graphicsDeviceType.ToString();
            report.captureWidth = CaptureWidth;
            report.captureHeight = CaptureHeight;
            report.actualVrMeasured = false;
            report.persistentPoolSize = game.Marine.ParticleCount;
            report.particleLookDefaults = new[] { look.bodyRadiance, look.sparkRadiance, look.surfaceFlow, look.grainFootprint };
            Check(Mathf.Approximately(look.bodyRadiance, 1f) && Mathf.Approximately(look.sparkRadiance, 3f) &&
                Mathf.Approximately(look.surfaceFlow, 2f) && Mathf.Approximately(look.grainFootprint, 1f),
                "Production ParticleLook defaults must be body=1, spark=3, flow=2, pixel=1.");

            var camera = game.Flight.View;
            int originalMask = camera.cullingMask;
            CameraClearFlags originalClearFlags = camera.clearFlags;
            Color originalBackground = camera.backgroundColor;
            var cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
            bool originalPostProcessing = cameraData != null && cameraData.renderPostProcessing;
            bool originalStudyVisible = study && study.Visible;
            PointStudy.RenderMode originalStudyMode = study ? study.Mode : PointStudy.RenderMode.NativePoint;
            int originalStudyDensity = study ? study.DensityMultiplier : 3;
            float originalStudyGain = study ? study.Gain : 4f;
            float originalStudyFlow = study ? study.Flow : 1f;
            try
            {
                if (study) study.SetVisible(false);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;

                game.Flight.SetPose(CaveLayout.Spawn, CaveLayout.SpawnRotation);
                for (int i = 0; i < 3; i++) { ManualTick(Step); yield return null; }
                report.firstJellyGroup = game.Marine.FirstJellyGroup;
                report.matterInitializationCount = game.Marine.Matter.InitializationCount;
                int initializationCount = game.Marine.Matter.InitializationCount;
                MatterParticle[] initial = game.Marine.Matter.Readback();
                int[] jellyIds = GroupIds(initial, report.firstJellyGroup);
                VerifyFiniteParticles(initial, jellyIds, "initial");
                report.firstJellyPointCount = jellyIds.Length;
                report.firstJellyIdChecksum = IdChecksum(jellyIds);
                Check(report.firstJellyPointCount == 5118,
                    "The actual first jelly must contain 5,118 persistent seed IDs at 3x density.");

                Vector3 jelly = game.Marine.JellyTargets[0].position;
                SetFrame(jelly + new Vector3(0, 2, -32), jelly);
                Capture("jelly-near.png");
                SetFrame(jelly + new Vector3(0, 0, -1.2f), jelly + Vector3.forward * 2f);
                Capture("jelly-inside.png");
                SetFrame(jelly + new Vector3(0, 8, -82), jelly);
                Capture("jelly-mid.png");
                look.sparkRadiance = 12f;
                look.surfaceFlow = 2f;
                look.Apply();
                SetFrame(jelly + new Vector3(0, 2, -32), jelly);
                Capture("jelly-near-stress-spark12.png");
                report.sparkStressCaptured = true;
                look.sparkRadiance = 3f;
                look.surfaceFlow = 2f;
                look.Apply();

                MatterParticle[] movingA = game.Marine.Matter.Readback();
                yield return AdvanceFor(.72f);
                MatterParticle[] movingB = game.Marine.Matter.Readback();
                VerifyFiniteParticles(movingB, jellyIds, "moving");
                report.jellyFlowSeconds = lastAdvanceSeconds;
                report.movingMeanDisplacement = MeanDisplacement(movingA, movingB, jellyIds);
                report.identityStableMoving = StableIds(initial, movingB, jellyIds, report.firstJellyGroup);
                Check(report.jellyFlowSeconds >= .65f && report.movingMeanDisplacement > .01f,
                    "The same first-jelly GPU IDs must visibly move across at least 0.65 seconds.");
                Check(report.identityStableMoving, "First-jelly persistent GPU IDs changed during free motion.");

                Vector3 targetPosition = game.Marine.JellyTargets[0].position;
                SetFrame(targetPosition + new Vector3(0, 0, -24), targetPosition);
                int hitsBefore = game.Combat.Hits;
                Check(game.Combat.Acquire(game.Marine.JellyTargets[0]), "Could not acquire the actual first jelly for its production transfer.");
                game.Combat.Release();
                yield return WaitForPending(5f);
                report.firstHitCount = game.Combat.Hits - hitsBefore;
                Check(game.Marine.JellyTargets[0].hp == 1, "First production shot did not light the actual jelly.");

                targetPosition = game.Marine.JellyTargets[0].position;
                SetFrame(targetPosition + new Vector3(0, 0, -24), targetPosition);
                hitsBefore = game.Combat.Hits;
                Check(game.Combat.Acquire(game.Marine.JellyTargets[0]), "Could not reacquire the actual first jelly for transfer.");
                game.Combat.Release();
                yield return WaitForPending(5f);
                report.secondHitCount = game.Combat.Hits - hitsBefore;
                Check(!game.Marine.JellyTargets[0].Available && game.Marine.CompletedJellies == 1,
                    "Second production shot did not deplete and transfer the actual first jelly.");

                yield return AdvanceFor(1.25f);
                MatterParticle[] transfer = game.Marine.Matter.Readback();
                VerifyFiniteParticles(transfer, jellyIds, "transfer");
                report.transferParticleCount = CountPhase(transfer, jellyIds, MatterPhase.Transfer);
                report.identityStableTransfer = StableIds(initial, transfer, jellyIds, report.firstJellyGroup);
                Check(report.transferParticleCount == jellyIds.Length && report.identityStableTransfer,
                    "Every original first-jelly ID must remain in its group during plant transfer.");
                Vector3 gardenCenter = GroupCenter(transfer, jellyIds);
                SetFrame(gardenCenter + new Vector3(28, 14, -42), gardenCenter);
                Capture("jelly-to-plant-transfer.png");

                yield return AdvanceFor(9.1f);
                MatterParticle[] settled = game.Marine.Matter.Readback();
                VerifyFiniteParticles(settled, jellyIds, "settled");
                report.settledParticleCount = CountPhase(settled, jellyIds, MatterPhase.Settled);
                report.identityStableSettled = StableIds(initial, settled, jellyIds, report.firstJellyGroup);
                report.settledDestinationError = MeanDestinationError(settled, jellyIds);
                Check(report.settledParticleCount == jellyIds.Length && report.identityStableSettled,
                    "Every original first-jelly ID must settle into the permanent garden.");
                Check(report.settledDestinationError < 1.5f,
                    "Settled first-jelly particles must reach their authored garden destinations.");
                gardenCenter = GroupCenter(settled, jellyIds);
                SetFrame(gardenCenter + new Vector3(30, 16, -48), gardenCenter);
                Capture("settled-garden.png");

                float serpentSong = (float)game.Music.Time;
                Vector3 centerline = Anatomy.Center(.5f, serpentSong);
                Vector3 tangent = (Anatomy.Center(.54f, serpentSong) - Anatomy.Center(.46f, serpentSong)).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;
                SetFrame(centerline + side * 145f + Vector3.up * 28f, centerline);
                Capture("serpent-side-anatomy.png");
                look.sparkRadiance = 12f;
                look.surfaceFlow = 2f;
                look.Apply();
                Capture("serpent-side-stress-spark12.png");
                look.sparkRadiance = 3f;
                look.surfaceFlow = 2f;
                look.Apply();
                Vector3 head = Anatomy.Head(serpentSong);
                Vector3 headTangent = (Anatomy.Head(serpentSong + .03f) - Anatomy.Head(serpentSong - .03f)).normalized;
                SetFrame(head + headTangent * 38f + Vector3.up * 3f, head + headTangent * 4f);
                Capture("serpent-head-on-anatomy.png");

                Vector3 baseline = CaveLayout.Rooms[0].Center;
                SetFrame(baseline + new Vector3(0, 18, -78), baseline);
                yield return MeasureScene("baseline-room", 30, 60);
                SpawnBattleOverlap();
                yield return MeasureScene("battle-overlap", 30, 60);
                SpawnWhaleSplash();
                yield return AdvanceFor(1f);
                report.whaleSplashAge = game.Horizon.ImpactSplash.Age;
                Check(report.whaleSplashAge >= .9f && report.whaleSplashAge <= 1.3f,
                    "Whale splash capture should be taken near its visible peak at one second.");
                Capture("whale-splash-peak.png");
                yield return MeasureScene("whale-splash", 30, 60);

                yield return CaptureProjectileDissolve();
                CaptureSharpPointStudy(camera, cameraData);
                Check(report.pointStudyStressCaptured, "SharpQuad stress capture was not produced.");
                Check(report.sparkStressCaptured, "Spark-radiance stress captures were not produced for jelly and serpent.");
                report.matterInitializationStable = initializationCount == game.Marine.Matter.InitializationCount;
                Check(report.matterInitializationStable,
                    "Persistent matter must keep its original GPU pool initialization through all phase changes.");
                CheckDeadline();
            }
            finally
            {
                camera.cullingMask = originalMask;
                camera.clearFlags = originalClearFlags;
                camera.backgroundColor = originalBackground;
                if (cameraData != null) cameraData.renderPostProcessing = originalPostProcessing;
                if (study)
                {
                    study.SetVisible(originalStudyVisible);
                    study.SetMode(originalStudyMode);
                    study.SetDensity(originalStudyDensity);
                    study.SetGain(originalStudyGain);
                    study.SetFlow(originalStudyFlow);
                }
                if (look)
                {
                    look.bodyRadiance = 1f;
                    look.sparkRadiance = 3f;
                    look.surfaceFlow = 2f;
                    look.grainFootprint = 1f;
                    look.Apply();
                }
                AudioListener.volume = originalAudio;
            }

        }

        void ManualTick(float dt)
        {
            dt = Mathf.Clamp(dt, .001f, .05f);
            float song = (float)game.Music.Time;
            var flight = game.Flight;
            flight.SetTravelContext(game.CanTravelBoost(song), song);
            int room = CaveLayout.NearestRoom(flight.Position);
            game.Combat.ActiveRoom = room;
            game.World.Serpent.SetResonance(game.Combat.BossDamage / (float)game.Combat.BossDamageGoal,
                game.Combat.SerpentComplete, song);
            game.Marine.Tick(song, dt, flight.Position);
            game.Hermits.Tick(song, dt, room == 3);
            game.Submarines.Tick(song, dt, room == 4);
            game.Horizon.Tick(song, dt, game.Marine.WhalePosition, game.Marine.WhaleRotation,
                game.Marine.WhaleVelocity, game.Marine.WhaleReleased);
            if (game.PointStudy) game.PointStudy.Tick(song, dt);
            game.World.Caverns.Tick(song, dt, flight.Position);
            game.Combat.Tick(dt, false);
            game.World.Tick(song, .25f, 0f, game.ReducedMotion);
        }

        IEnumerator AdvanceFor(float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds + 3f;
            lastAdvanceSeconds = 0f;
            while (lastAdvanceSeconds < seconds && Time.realtimeSinceStartup < deadline)
            {
                CheckDeadline();
                lastAdvanceSeconds += TickFromRealtime();
                yield return null;
            }
            Check(lastAdvanceSeconds >= seconds, "Manual simulation did not advance through the requested interval.");
        }

        IEnumerator WaitUntil(float targetSong, float maximumSeconds)
        {
            float deadline = Time.realtimeSinceStartup + maximumSeconds;
            while (game.Music.Time < targetSong && Time.realtimeSinceStartup < deadline)
            {
                CheckDeadline();
                TickFromRealtime();
                yield return null;
            }
            Check(game.Music.Time >= targetSong, "Timed out while advancing the requested production matter phase.");
        }

        IEnumerator WaitForPending(float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (game.Combat.HasPending && Time.realtimeSinceStartup < deadline)
            {
                CheckDeadline();
                TickFromRealtime();
                yield return null;
            }
            Check(!game.Combat.HasPending, "A scheduled production combat shot did not resolve before its timeout.");
            TickFromRealtime();
            yield return null;
        }

        float TickFromRealtime()
        {
            float now = Time.realtimeSinceStartup;
            float dt = Mathf.Clamp(now - lastManualRealtime, .001f, .05f);
            lastManualRealtime = now;
            ManualTick(dt);
            return dt;
        }

        IEnumerator MeasureScene(string name, int warmupFrames, int sampleFrames)
        {
            var result = new TimingReport { scene = name, warmupRequested = warmupFrames, samplesRequested = sampleFrames };
            report.timings.Add(result);
            var target = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32);
            target.Create();
            try {
            for (int i = 0; i < warmupFrames; i++)
            {
                CheckDeadline();
                TickFromRealtime();
                RenderTimingFrame(target, result);
                yield return null;
            }

            var samples = new List<FrameTiming>(sampleFrames);
            var seenFrameStarts = new HashSet<ulong>();
            var batch = new FrameTiming[4];
            for (int i = 0; i < sampleFrames; i++)
            {
                CheckDeadline();
                TickFromRealtime();
                RequestFrameTiming(result);
                RenderTimingFrame(target, result);
                yield return null;
                CollectFrameTimings(result, samples, seenFrameStarts, batch);
            }

            double cpu = 0, main = 0, render = 0, gpu = 0;
            int cpuCount = 0, gpuCount = 0;
            foreach (FrameTiming frame in samples)
            {
                if (frame.cpuFrameTime > 0)
                {
                    cpu += frame.cpuFrameTime;
                    main += frame.cpuMainThreadFrameTime;
                    render += frame.cpuRenderThreadFrameTime;
                    cpuCount++;
                }
                if (frame.gpuFrameTime > 0) { gpu += frame.gpuFrameTime; gpuCount++; }
            }
            result.timingApiAvailable = cpuCount > 0;
            result.cpuSamples = cpuCount;
            result.gpuSamples = gpuCount;
            if (cpuCount > 0)
            {
                result.cpuFrameMs = cpu / cpuCount;
                result.cpuMainThreadMs = main / cpuCount;
                result.cpuRenderThreadMs = render / cpuCount;
            }
            if (gpuCount > 0) result.gpuFrameMs = gpu / gpuCount;
            result.gpuAvailable = gpuCount > 0;
            result.uniqueFrameRecords = samples.Count;
            if (cpuCount == 0 && string.IsNullOrEmpty(result.unavailableReason))
                result.unavailableReason = "No CPU FrameTiming records were returned for the sampled frames.";
            if (gpuCount == 0 && string.IsNullOrEmpty(result.unavailableReason))
                result.unavailableReason = "GPU timestamps were not returned in this offscreen batch run; this is not a GPU performance result.";
            result.method = "Explicit URP offscreen render each frame + Unity FrameTimingManager; no screenshot readback";
            }
            finally { target.Release(); Destroy(target); }
        }

        void RenderTimingFrame(RenderTexture target, TimingReport result)
        {
            RenderPipeline.SubmitRenderRequest(game.Flight.View,
                new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            result.renderRequests++;
        }

        static void CollectFrameTimings(TimingReport result, List<FrameTiming> samples,
            HashSet<ulong> seenFrameStarts, FrameTiming[] batch)
        {
            uint count;
            if (!result.captureRequestSupported) return;
            try { count = FrameTimingManager.GetLatestTimings((uint)batch.Length, batch); }
            catch (Exception exception)
            {
                result.captureRequestSupported = false;
                result.unavailableReason = exception.GetType().Name + ": " + exception.Message;
                return;
            }
            for (int i = 0; i < count; i++)
            {
                FrameTiming frame = batch[i];
                if (frame.frameStartTimestamp != 0 && seenFrameStarts.Add(frame.frameStartTimestamp))
                    samples.Add(frame);
            }
        }

        static void RequestFrameTiming(TimingReport result)
        {
            if (!result.captureRequestSupported) return;
            try { FrameTimingManager.CaptureFrameTimings(); }
            catch (Exception exception)
            {
                result.captureRequestSupported = false;
                result.unavailableReason = exception.GetType().Name + ": " + exception.Message;
            }
        }

        void SpawnBattleOverlap()
        {
            Vector3 center = CaveLayout.Rooms[1].Center;
            game.Flight.SetPose(center + new Vector3(0, 0, -38), Quaternion.identity);
            game.Combat.ActiveRoom = 1;
            float song = (float)game.Music.Time;
            for (int i = 0; i < 14; i++)
            {
                Vector3 origin = center + new Vector3((i % 5 - 2) * 4.5f, 4f + (i / 5) * 3.5f, 12f + (i % 3) * 2f);
                var shot = game.Combat.RegisterPressureShot(origin, Vector3.back, song,
                    Color.Lerp(new Color(.16f, .82f, .94f), new Color(1f, .38f, .14f), i / 13f),
                    owner: this, speed: 1f, radius: 1.2f, lifetimeOverride: 12f);
                if (shot != null) report.battleProjectiles++;
            }
            Capture("battle-overlap.png");
        }

        void SpawnWhaleSplash()
        {
            Vector3 origin = game.Marine.WhalePosition;
            Vector3 velocity = new(18f, -64f, 42f);
            report.whaleSplashStarted = game.Horizon.ImpactSplash.Burst(origin, velocity, (float)game.Music.Time, 5f);
            Check(report.whaleSplashStarted, "Production whale impact splash did not start through WhaleImpactSplash.Burst.");
            SetFrame(origin + new Vector3(340f, 110f, 320f), origin + Vector3.up * 85f);
        }

        IEnumerator CaptureProjectileDissolve()
        {
            Vector3 origin = CaveLayout.Rooms[1].Center;
            game.Flight.SetPose(origin + new Vector3(0, 0, -24), Quaternion.identity);
            game.Combat.ActiveRoom = 1;
            float song = (float)game.Music.Time;
            var bubble = game.Combat.RegisterPressureShot(game.Flight.Position + Vector3.forward * 13f,
                Vector3.forward, song, new Color(.18f, .84f, 1f), owner: this, speed: 1f, radius: 1.2f,
                lifetimeOverride: 12f);
            Check(bubble != null, "Production pressure-bubble API refused the diagnostic projectile.");
            if (bubble == null) yield break;
            Capture("projectile-before-dissolve.png");
            Check(game.Combat.Acquire(bubble), "Could not acquire the production diagnostic pressure bubble.");
            game.Combat.Release();
            yield return WaitForPending(5f);
            report.projectileDissolveAt = bubble.pressureDissolveAt;
            Check(bubble.hp == 0 && bubble.pressureDissolveAt >= 0,
                "Production pressure-bubble interception did not enter the shared dissolve path.");
            yield return WaitUntil(bubble.pressureDissolveAt + .35f, 2f);
            report.projectileVisibleDuringDissolve = bubble.visual != null && bubble.visual.activeSelf;
            Capture("projectile-dissolve.png");
            Check(report.projectileVisibleDuringDissolve,
                "Production pressure bubble must remain visible during the shared dissolve envelope.");
        }

        void CaptureSharpPointStudy(Camera camera, UniversalAdditionalCameraData cameraData)
        {
            bool priorVisible = study.Visible;
            PointStudy.RenderMode priorMode = study.Mode;
            int priorDensity = study.DensityMultiplier;
            float priorGain = study.Gain, priorFlow = study.Flow;
            int priorMask = camera.cullingMask;
            CameraClearFlags priorFlags = camera.clearFlags;
            Color priorBackground = camera.backgroundColor;
            bool priorPost = cameraData != null && cameraData.renderPostProcessing;
            try
            {
                study.SetVisible(true);
                study.SetMode(PointStudy.RenderMode.SharpQuad);
                study.SetDensity(3);
                study.SetGain(12f);
                study.SetFlow(2f);
                look.sparkRadiance = 12f;
                look.surfaceFlow = 2f;
                look.Apply();
                camera.cullingMask = 1 << 30;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                if (cameraData != null) cameraData.renderPostProcessing = false;
                study.Tick((float)game.Music.Time, 0f);
                Vector3 head = study.SerpentHeadPosition;
                Vector3 facing = study.SerpentHeadForward;
                SetFrame(head + facing * 35f + Vector3.up * 2f, head);
                Capture("sharpquad-12xflow2-headon.png");
                report.pointStudyStressCaptured = true;
            }
            finally
            {
                if (cameraData != null) cameraData.renderPostProcessing = priorPost;
                camera.cullingMask = priorMask;
                camera.clearFlags = priorFlags;
                camera.backgroundColor = priorBackground;
                study.SetVisible(priorVisible);
                study.SetMode(priorMode);
                study.SetDensity(priorDensity);
                study.SetGain(priorGain);
                study.SetFlow(priorFlow);
            }
        }

        CaptureReport Capture(string filename)
        {
            CheckDeadline();
            var target = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            Texture2D image = null;
            try
            {
                target.Create();
                RenderPipeline.SubmitRenderRequest(game.Flight.View,
                    new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                image = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(output, filename), image.EncodeToPNG());
                var result = new CaptureReport { file = filename };
                foreach (Color32 pixel in image.GetPixels32())
                {
                    if (pixel.r > 8 || pixel.g > 8 || pixel.b > 8) result.nonBlackPixels++;
                    if (pixel.r > 245 && pixel.g > 245 && pixel.b > 245) result.whitePixels++;
                }
                result.nonBlackFraction = result.nonBlackPixels / (float)(CaptureWidth * CaptureHeight);
                result.whiteFraction = result.whitePixels / (float)(CaptureWidth * CaptureHeight);
                result.nonBlank = result.nonBlackPixels > 40;
                report.captures.Add(result);
                report.maximumWhiteFraction = Mathf.Max(report.maximumWhiteFraction, result.whiteFraction);
                if (filename != "jelly-inside.png")
                    Check(result.nonBlank, filename + " rendered no visible production matter.");
                if ((filename.Contains("stress") || filename.StartsWith("sharpquad-")) && result.whiteFraction >= .10f)
                    Check(false, filename + " exceeded the diagnostic full-frame white-pixel limit of 0.10.");
                CheckDeadline();
                return result;
            }
            finally
            {
                RenderTexture.active = previous;
                if (image != null) Destroy(image);
                target.Release();
                Destroy(target);
            }
        }

        static int[] GroupIds(MatterParticle[] particles, int group)
        {
            var ids = new List<int>();
            for (int i = 0; i < particles.Length; i++)
                if (Mathf.RoundToInt(particles[i].identityState.y) == group) ids.Add(i);
            return ids.ToArray();
        }

        static int CountPhase(MatterParticle[] particles, int[] ids, MatterPhase phase)
        {
            int count = 0;
            foreach (int id in ids)
                if (id < particles.Length && Mathf.RoundToInt(particles[id].identityState.z) == (int)phase) count++;
            return count;
        }

        static bool StableIds(MatterParticle[] initial, MatterParticle[] current, int[] ids, int group)
        {
            if (initial.Length != current.Length) return false;
            foreach (int id in ids)
                if (Mathf.RoundToInt(initial[id].identityState.x) != id ||
                    Mathf.RoundToInt(current[id].identityState.x) != id ||
                    Mathf.RoundToInt(current[id].identityState.y) != group) return false;
            return true;
        }

        static int IdChecksum(int[] ids)
        {
            unchecked
            {
                int hash = 17;
                foreach (int id in ids) hash = hash * 31 + id;
                return hash;
            }
        }

        void VerifyFiniteParticles(MatterParticle[] particles, int[] ids, string phase)
        {
            foreach (int id in ids) {
                Vector4 p = particles[id].positionAge;
                if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) ||
                    float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z))
                    throw new InvalidOperationException("Non-finite " + phase + " jelly particle " + id +
                        "; seed=" + game.Marine.SeedAt(id).form);
            }
        }

        static float MeanDisplacement(MatterParticle[] a, MatterParticle[] b, int[] ids)
        {
            if (a.Length != b.Length || ids.Length == 0) return 0;
            double sum = 0;
            foreach (int id in ids) sum += Vector3.Distance(a[id].positionAge, b[id].positionAge);
            return (float)(sum / ids.Length);
        }

        float MeanDestinationError(MatterParticle[] particles, int[] ids)
        {
            if (ids.Length == 0) return float.PositiveInfinity;
            double sum = 0;
            foreach (int id in ids) sum += Vector3.Distance(particles[id].positionAge, game.Marine.SeedAt(id).destination);
            return (float)(sum / ids.Length);
        }

        static Vector3 GroupCenter(MatterParticle[] particles, int[] ids)
        {
            if (ids.Length == 0) return Vector3.zero;
            Vector3 center = Vector3.zero;
            foreach (int id in ids) center += (Vector3)particles[id].positionAge;
            return center / ids.Length;
        }

        void SetFrame(Vector3 position, Vector3 focus)
        {
            game.Flight.SetPose(position, Quaternion.LookRotation(focus - position, Vector3.up));
        }

        void CheckDeadline()
        {
            if (Time.realtimeSinceStartup - started > DeadlineSeconds)
                throw new TimeoutException("Sharp matter proof exceeded its 60-second deadline.");
        }

        void Check(bool value, string message)
        {
            report.tests.Add((value ? "PASS: " : "FAIL: ") + message);
            if (!value) AddError(message);
        }

        void AddError(string message)
        {
            if (report.errors.Length >= 24 || Array.IndexOf(report.errors, message) >= 0) return;
            var next = new string[report.errors.Length + 1];
            Array.Copy(report.errors, next, report.errors.Length);
            next[next.Length - 1] = message;
            report.errors = next;
        }

        void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                AddError(message + "\n" + stack);
        }

        void Update()
        {
            if (!finished && Time.realtimeSinceStartup - started > DeadlineSeconds)
            {
                AddError("Sharp matter proof exceeded its 60-second deadline.");
                Finish();
            }
        }

        void Finish()
        {
            if (finished) return;
            finished = true;
            StopAllCoroutines();
            game.ManualProofTick = false;
            report.elapsedSeconds = Time.realtimeSinceStartup - started;
            report.passed = report.errors.Length == 0;
            report.captureList = report.captures.ToArray();
            report.timingList = report.timings.ToArray();
            report.testsRun = report.tests.ToArray();
            File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
            Application.Quit(report.passed ? 0 : 2);
        }

        void OnDestroy() => Application.logMessageReceived -= OnLog;

        [Serializable]
        sealed class CaptureReport
        {
            public string file;
            public bool nonBlank;
            public int nonBlackPixels, whitePixels;
            public float nonBlackFraction, whiteFraction;
        }

        [Serializable]
        sealed class TimingReport
        {
            public string scene, method, unavailableReason;
            public bool captureRequestSupported = true, timingApiAvailable, gpuAvailable;
            public int warmupRequested, samplesRequested, uniqueFrameRecords, cpuSamples, gpuSamples, renderRequests;
            public double cpuFrameMs, cpuMainThreadMs, cpuRenderThreadMs, gpuFrameMs;
        }

        [Serializable]
        sealed class Report
        {
            public bool passed, actualVrMeasured, identityStableMoving, identityStableTransfer, identityStableSettled;
            public bool matterInitializationStable, whaleSplashStarted, projectileVisibleDuringDissolve, pointStudyStressCaptured;
            public bool sparkStressCaptured;
            public int captureWidth, captureHeight, persistentPoolSize, matterInitializationCount;
            public int firstJellyGroup, firstJellyPointCount, firstJellyIdChecksum, transferParticleCount, settledParticleCount;
            public int firstHitCount, secondHitCount, battleProjectiles;
            public float jellyFlowSeconds, movingMeanDisplacement, settledDestinationError;
            public float projectileDissolveAt, whaleSplashAge, elapsedSeconds, maximumWhiteFraction;
            public float[] particleLookDefaults;
            public string gpu, graphicsApi;
            public string[] errors = Array.Empty<string>(), testsRun;
            public CaptureReport[] captureList;
            public TimingReport[] timingList;
            [NonSerialized] public readonly List<CaptureReport> captureListInternal = new();
            [NonSerialized] public readonly List<TimingReport> timings = new();
            [NonSerialized] public readonly List<string> tests = new();
            public List<CaptureReport> captures => captureListInternal;
        }
    }
}
