using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace Liminal
{
    public sealed class WhaleSplashProof : MonoBehaviour
    {
        const float TimeoutSeconds = 180f;
        const float StepSeconds = 1f / 60f;
        const int CaptureWidth = 1600;
        const int CaptureHeight = 900;
        static readonly float[] SplashCaptureAges = { .3f, 1.2f, 2.2f, 4.4f, 6.1f };

        Experience game;
        string output;
        float started, simulationSong;
        bool finished;
        bool combatReview;
        readonly Report report = new();
        readonly string[] captureNames =
        {
            "splash-age-0.3.png", "splash-age-1.2.png", "splash-age-2.2.png",
            "splash-age-4.4.png", "splash-age-6.1.png"
        };

        public void Initialize(Experience owner)
        {
            game = owner;
            started = Time.realtimeSinceStartup;
            string[] args = Environment.GetCommandLineArgs();
            combatReview = Array.IndexOf(args, "--combat-review") >= 0;
            int at = Array.IndexOf(args, "--output");
            output = at >= 0 && at + 1 < args.Length
                ? args[at + 1]
                : Path.GetFullPath("Verification/WhaleSplash");
            Directory.CreateDirectory(output);
            Application.logMessageReceived += OnLog;
        }

        void OnLog(string message, string stack, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) &&
                report.errors.Length < 16)
            {
                var errors = new string[report.errors.Length + 1];
                Array.Copy(report.errors, errors, report.errors.Length);
                errors[errors.Length - 1] = message + "\n" + stack;
                report.errors = errors;
            }
        }

        void Update()
        {
            if (!finished && Time.realtimeSinceStartup - started > TimeoutSeconds)
            {
                AddError("Whale splash proof exceeded its bounded deadline.");
                Finish();
            }
        }

        IEnumerator Start()
        {
            while (!game.Ready && Time.realtimeSinceStartup - started < TimeoutSeconds)
                yield return null;
            if (!game.Ready)
            {
                AddError("Experience did not become ready before the proof deadline.");
                Finish();
                yield break;
            }

            Check(game.CavernMode && game.ProofActive, "Whale splash proof requires cavern proof mode.");
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--background-proof") >= 0)
            {
                report.backgroundCapture = game.BackgroundProof && AudioListener.volume == 0 && game.Flight.SuppressCursorChanges;
                var xr = UnityEngine.XR.Management.XRGeneralSettings.Instance;
                report.backgroundCapture &= xr == null || xr.Manager == null || xr.Manager.activeLoader == null;
                Check(report.backgroundCapture, "Passive capture must be batch-mode, silent, and leave cursor ownership untouched.");
            }
            if (report.errors.Length > 0) { Finish(); yield break; }

            game.Restart();
            game.ManualProofTick = true;
            game.Tutorial.SetEnabled(false);
            var marine = game.Marine;
            var arrival = marine.Arrival;
            var splash = game.Horizon.ImpactSplash;
            Check(game.Horizon.Ready && splash != null && splash.Ready,
                "Horizon water and its whale impact splash must be ready after Restart.");
            if (report.errors.Length > 0) { Finish(); yield break; }

            report.initialImpactCount = splash.ImpactCount;
            report.initialSplashAge = splash.Age;
            Check(report.initialImpactCount == 0 && report.initialSplashAge == -1f,
                "Restart must clear the whale splash impact count and reset its age to -1.");
            Vector3 player = arrival.Origin + new Vector3(0f, 10f, -140f);
            game.Flight.SetPose(player, Quaternion.LookRotation(arrival.Origin - player, Vector3.up));

            float song = (float)game.Music.Time;
            simulationSong = song;
            bool latefallCaptured = false;
            bool[] captured = new bool[SplashCaptureAges.Length];
            int initialImpactCount = splash.ImpactCount;
            for (int step = 0; step < 1800; step++)
            {
                song += StepSeconds;
                simulationSong = song;
                marine.Tick(song, StepSeconds, player);
                game.Horizon.Tick(song, StepSeconds, marine.WhalePosition, marine.WhaleRotation,
                    marine.WhaleVelocity, marine.WhaleReleased);

                if (!arrival.Triggered) continue;

                if (!latefallCaptured && arrival.Age >= arrival.ImpactAge - .08f)
                {
                    report.latefallAge = arrival.Age;
                    report.latefallPosition = marine.WhalePosition;
                    Frame(report.latefallPosition + new Vector3(340f, 80f, 300f),
                        arrival.Origin + Vector3.up * 90f);
                    report.latefallCapture = Capture("whale-late-fall.png");
                    latefallCaptured = true;
                }

                if (splash.ImpactCount > initialImpactCount)
                {
                    if (report.impactCountAtFirstActive == 0)
                    {
                        report.impactCountAtFirstActive = splash.ImpactCount;
                        report.splashOrigin = splash.Origin;
                        report.lastVelocity = splash.LastVelocity;
                        report.impactAge = arrival.ImpactAge;
                    }

                    for (int i = 0; i < SplashCaptureAges.Length; i++)
                    {
                        if (captured[i] || splash.Age < SplashCaptureAges[i]) continue;
                        Vector3 focus = splash.Origin + Vector3.up * 90f;
                        Frame(splash.Origin + new Vector3(340f, 80f, 300f), focus);
                        report.captures[i] = Capture(captureNames[i]);
                        captured[i] = true;
                        if (i == 1) SamplePlume(splash, 1.2f, out report.heightAt1_2, out report.extentAt1_2);
                        if (i == 2) SamplePlume(splash, 2.2f, out report.heightAt2_2, out report.extentAt2_2);
                        if (i == 4)
                        {
                            SamplePlume(splash, 6.1f, out report.expiredMaxHeight, out report.expiredLateralExtent);
                            report.expiredSplashActive = splash.Active;
                        }
                    }
                }

                if (splash.ImpactCount > 1)
                {
                    AddError("One whale arrival created more than one impact splash.");
                    break;
                }

                bool allCaptured = true;
                foreach (bool value in captured) allCaptured &= value;
                if (arrival.Complete && allCaptured && splash.ImpactCount == initialImpactCount + 1)
                {
                    report.arrivalCompleted = true;
                    break;
                }
            }

            report.finalImpactCount = splash.ImpactCount;
            report.arrivalTriggered = arrival.Triggered;
            report.arrivalCompleted &= arrival.Complete;
            report.splashParticleCount = splash.ParticleCount;
            report.splashPeakHeight = splash.PeakHeight;
            Check(latefallCaptured, "The actual descending whale surface contact was not captured.");
            Check(report.arrivalTriggered && report.arrivalCompleted,
                "The real proximity-triggered whale arrival must complete within the bounded simulation.");
            Check(report.initialImpactCount == 0 && report.impactCountAtFirstActive == 1 &&
                report.finalImpactCount == 1, "The initial arrival must produce exactly one splash, with no duplicate impact.");
            Check(Vector3.Distance(report.splashOrigin, report.latefallPosition) <= 80f &&
                Mathf.Abs(report.splashOrigin.y - game.Horizon.SurfaceHeight) <= 80f,
                "Splash origin must remain near the whale's actual surface contact.");
            Check(report.lastVelocity.y < 0f, "The splash must retain the whale's descending impact velocity.");
            Check(report.heightAt1_2 >= 180f || report.heightAt2_2 >= 180f,
                "CPU-mirrored plume samples must reach at least 180 metres above impact origin at 1.2 or 2.2 seconds.");
            Check(report.extentAt1_2 >= 200f || report.extentAt2_2 >= 200f,
                "CPU-mirrored plume samples must extend at least 200 metres laterally at 1.2 or 2.2 seconds.");
            Check(report.latefallCapture != null && report.latefallCapture.nonBlank &&
                report.latefallCapture.whiteFraction < .15f,
                "The late-fall scene capture must be nonblank and not washed out.");
            for (int i = 0; i < report.captures.Length; i++)
                Check(captured[i] && report.captures[i] != null && report.captures[i].nonBlank &&
                    report.captures[i].whiteFraction < .15f,
                    "Splash capture at age " + SplashCaptureAges[i] + " must be nonblank and not washed out.");
            CaptureReport afterLifetime = report.captures[4];
            Check(afterLifetime != null && !report.expiredSplashActive && report.expiredMaxHeight == 0f,
                "At 6.1 seconds the splash must be inactive and its sampled plume height must be zero.");
            for (int i = 0; i <= 2; i++)
            {
                report.captures[i].afterLifetimeDifferenceFraction =
                    ImageDifferenceFraction(report.captures[i], afterLifetime);
                Check(report.captures[i].blueCyanFraction >= .005f &&
                    report.captures[i].afterLifetimeDifferenceFraction >= .005f,
                    "Active plume capture at age " + SplashCaptureAges[i] +
                    " must contain blue-cyan pixels and visibly differ from the after-lifetime frame.");
            }
            if (combatReview && report.errors.Length == 0) yield return ReviewCombatAndFinale();
            Finish();
        }

        IEnumerator ReviewCombatAndFinale()
        {
            MarineLife marine = game.Marine;
            float first = marine.CombatFirstActionAt;
            Check(first > simulationSong, "Combat actions must begin only after the completed arrival has settled.");
            float cruiseOffset = marine.Arrival.CruiseTime - simulationSong;
            for (int cycle = 0; cycle < 12; cycle++)
            {
                float at = first + cycle * WhaleCombatMotion.Interval;
                float[] knots = (cycle & 1) == 0 ? new[] { 0f, 2.5f, 4.2f, 6.3f, 8.2f, 12.5f, WhaleCombatMotion.Duration } :
                    new[] { 0f, 3f, 5.1f, 7.1f, 11.5f, WhaleCombatMotion.Duration };
                const float h = .01f;
                foreach (float knot in knots)
                {
                    float knotAge = knot == WhaleCombatMotion.Duration ? knot : knot * WhaleCombatMotion.MotionStretch;
                    Vector3 before = WhaleCombatMotion.Evaluate(at + knotAge - h + cruiseOffset, at + knotAge - h, first).Velocity;
                    Vector3 center = WhaleCombatMotion.Evaluate(at + knotAge + cruiseOffset, at + knotAge, first).Velocity;
                    Vector3 after = WhaleCombatMotion.Evaluate(at + knotAge + h + cruiseOffset, at + knotAge + h, first).Velocity;
                    report.maxKnotAccelerationGap = Mathf.Max(report.maxKnotAccelerationGap,
                        Vector3.Distance((center - before) / h, (after - center) / h));
                }
                for (float age = 0; age <= WhaleCombatMotion.Duration; age += .1f)
                {
                    var pose = WhaleCombatMotion.Evaluate(at + age + cruiseOffset, at + age, first);
                    for (int target = 0; target < MarineLife.WhaleOrganCount; target++)
                    {
                        Vector3 point = pose.Position + pose.Rotation *
                            (WhaleAnatomy.TargetLocal(target, at + age, pose.Gesture) * 1.8f);
                        report.allCyclesMaxRoomDistance = Mathf.Max(report.allCyclesMaxRoomDistance, CaveLayout.RoomDistance(2, point));
                    }
                }
            }
            Check(report.allCyclesMaxRoomDistance < .99f,
                "Both leap types must remain inside the cave across twelve changing cruise approach phases.");
            Check(report.maxKnotAccelerationGap < 8,
                "Acceleration must connect across the leap knots and ordinary cruise, not only position and velocity.");
            report.combatActions = new CombatReport[2];
            Vector3 player = game.Flight.Position;
            for (int action = 0; action < 2; action++)
            {
                var result = report.combatActions[action] = new CombatReport();
                int impactsBefore = game.Horizon.ImpactSplash.ImpactCount;
                float start = first + action * WhaleCombatMotion.Interval;
                bool riseCaptured = false, apexCaptured = false, fallCaptured = false, splashCaptured = false;
                Quaternion previousRotation = Quaternion.identity;
                Vector3 previousPosition = Vector3.zero;
                Vector3 followCameraOrigin = Vector3.zero;
                int followFrame = 0;
                for (int step = -30; step <= (WhaleCombatMotion.Duration + .25f) / StepSeconds; step++)
                {
                    simulationSong = start + step * StepSeconds;
                    marine.Tick(simulationSong, StepSeconds, player);
                    game.Horizon.Tick(simulationSong, StepSeconds, marine.WhalePosition, marine.WhaleRotation,
                        marine.WhaleVelocity, false);
                    var pose = marine.CombatPose;
                    if (step >= 0 && pose.Kind != WhaleCombatMotion.Action.Cruise)
                    {
                        result.kind = pose.Kind.ToString();
                        result.maxHeightAboveWater = Mathf.Max(result.maxHeightAboveWater,
                            marine.WhalePosition.y - game.Horizon.SurfaceHeight);
                        result.maxSpeed = Mathf.Max(result.maxSpeed, marine.WhaleVelocity.magnitude);
                        result.maxRoll = Mathf.Max(result.maxRoll,
                            Mathf.Acos(Mathf.Clamp(Vector3.Dot(marine.WhaleRotation * Vector3.up, Vector3.up), -1, 1)) * Mathf.Rad2Deg);
                    }
                    if (step > -30)
                    {
                        result.maxFrameTravel = Mathf.Max(result.maxFrameTravel, Vector3.Distance(previousPosition, marine.WhalePosition));
                        result.maxFrameRotation = Mathf.Max(result.maxFrameRotation, Quaternion.Angle(previousRotation, marine.WhaleRotation));
                        if (action == 0 && step * StepSeconds >= 8.2f * WhaleCombatMotion.MotionStretch &&
                            step * StepSeconds <= 12.5f * WhaleCombatMotion.MotionStretch)
                        {
                            Vector3 delta = marine.WhalePosition - previousPosition;
                            result.recoveryHorizontalTravel += Vector3.ProjectOnPlane(delta, Vector3.up).magnitude;
                            result.recoveryVerticalTravel += Mathf.Abs(delta.y);
                        }
                    }
                    previousPosition = marine.WhalePosition; previousRotation = marine.WhaleRotation;
                    foreach (LockTarget target in marine.WhaleResonatorTargets)
                        result.maxRoomDistance = Mathf.Max(result.maxRoomDistance, CaveLayout.RoomDistance(2, target.position));
                    for (int i = 0; i < MarineLife.WhaleOrganCount; i++)
                    {
                        Vector3 expected = marine.WhalePosition + marine.WhaleRotation *
                            (WhaleAnatomy.TargetLocal(i, simulationSong, marine.WhaleGesture) * 1.8f);
                        result.targetError = Mathf.Max(result.targetError,
                            Vector3.Distance(expected, marine.WhaleResonatorTargets[i].position));
                    }
                    float age = step * StepSeconds / WhaleCombatMotion.MotionStretch;
                    string prefix = action == 0 ? "breach" : "slam";
                    if (action == 0 && age >= 8.2f && followCameraOrigin == Vector3.zero)
                        followCameraOrigin = marine.WhalePosition;
                    float[] followAges = { 9f, 12f, 16f };
                    if (action == 0 && followFrame < followAges.Length && age >= followAges[followFrame])
                    {
                        Frame(followCameraOrigin + new Vector3(390, 170, 360), followCameraOrigin + Vector3.down * 35);
                        result.followThrough[followFrame] = Capture("breach-followthrough-" + (followFrame + 1) + ".png");
                        followFrame++;
                    }
                    if (action == 0 && age > 9f)
                        result.delayedTailTorsion = Mathf.Max(result.delayedTailTorsion,
                            pose.Gesture.z - Mathf.Abs(Vector3.SignedAngle(Vector3.up,
                                marine.WhaleRotation * Vector3.up, marine.WhaleRotation * Vector3.forward)) / 112f);
                    if (!riseCaptured && age >= (action == 0 ? 4.7f : 4.3f))
                    {
                        Frame(marine.WhalePosition + new Vector3(330, 100, 310), marine.WhalePosition);
                        result.rise = Capture(prefix + "-rise.png"); riseCaptured = true;
                    }
                    if (!apexCaptured && age >= (action == 0 ? 6.3f : 5.1f))
                    {
                        Frame(marine.WhalePosition + new Vector3(330, 70, 310), marine.WhalePosition);
                        result.apex = Capture(prefix + "-apex.png"); apexCaptured = true;
                    }
                    if (!fallCaptured && age >= (action == 0 ? 7.2f : 6.1f))
                    {
                        Frame(marine.WhalePosition + new Vector3(330, 80, 310), marine.WhalePosition);
                        result.fall = Capture(prefix + "-fall.png"); fallCaptured = true;
                        MatterParticle[] particles = marine.Matter.Readback();
                        for (int id = 0; id < particles.Length; id += 53)
                        {
                            MatterSeed seed = marine.SeedAt(id);
                            if (seed.traits.y < 2.5f) continue;
                            Vector3 expected = marine.WhalePosition + marine.WhaleRotation *
                                (WhaleAnatomy.Deform(seed.form, simulationSong, marine.WhaleGesture) * 1.8f);
                            result.gpuSurfaceError = Mathf.Max(result.gpuSurfaceError,
                                Vector3.Distance(expected, particles[id].positionAge));
                        }
                    }
                    if (game.Horizon.ImpactSplash.ImpactCount > impactsBefore)
                    {
                        if (!result.impacted)
                        {
                            result.impacted = true; result.impactAge = age;
                            result.impactVelocity = game.Horizon.ImpactSplash.LastVelocity;
                            result.impactUpDot = Vector3.Dot(marine.WhaleRotation * Vector3.up, Vector3.up);
                            result.impactForwardY = (marine.WhaleRotation * Vector3.forward).y;
                        }
                        if (!splashCaptured && game.Horizon.ImpactSplash.Age >= .8f)
                        {
                            Vector3 origin = game.Horizon.ImpactSplash.Origin;
                            Frame(origin + new Vector3(350, 100, 330), origin + Vector3.up * 85);
                            result.splash = Capture(prefix + "-impact.png"); splashCaptured = true;
                        }
                    }
                }
                result.impacts = game.Horizon.ImpactSplash.ImpactCount - impactsBefore;
                Check(result.impacted && result.impacts == 1 && result.impactVelocity.y < -30,
                    "Each combat action must create exactly one curtain on fast descending hull contact.");
                Check(result.maxHeightAboveWater > 85 && result.maxSpeed > 65 && result.maxRoomDistance < .99f,
                    "The accelerating leap must clear the surface and keep the sampled whale silhouette inside the cave.");
                Check(result.maxFrameTravel < 4 && result.maxFrameRotation < 9 && result.targetError < .001f &&
                    result.gpuSurfaceError < 15, "Pose joins, target deformation and GPU body tracking must remain continuous.");
                Check(action == 0 ? result.impactUpDot < -.15f : result.impactForwardY < -.45f,
                    "Breach must land side/back first; sky slam must descend with a visibly pitched body.");
                Check(result.rise != null && result.apex != null && result.fall != null && result.splash != null &&
                    result.rise.nonBlank && result.fall.nonBlank && result.splash.whiteFraction < .12f,
                    "Combat motion and impact images must be visible without a screen-filling white flash.");
                if (action == 0)
                    Check(followFrame == 3 && result.recoveryHorizontalTravel > result.recoveryVerticalTravel * .8f &&
                        result.delayedTailTorsion > .15f,
                        "Landing must carry forward along a submerged arc while rear torsion outlasts the body recovery.");
            }
            MethodInfo release = typeof(MarineLife).GetMethod("ReleaseWhale", BindingFlags.Instance | BindingFlags.NonPublic);
            release.Invoke(marine, new object[] { simulationSong });
            game.Combat.EnterAfterglow();
            game.Finale.Begin(RunProgress.OptionalBosses, simulationSong);
            for (int step = 0; step < 440; step++)
            {
                simulationSong += .05f;
                marine.Tick(simulationSong, .05f, player);
                game.Finale.Tick(simulationSong, .05f);
            }
            game.Brightness.SetOffset(0);
            game.Brightness.SetFinaleGlow(1);
            Vector3 city = AtlantisGeometry.CityOrigin;
            report.cityBounds = AtlantisGeometry.DestinationBounds;
            Frame(city + new Vector3(440, 240, -500), city + Vector3.up * 65);
            report.cityWide = Capture("atlantis-continent.png");
            Frame(city + new Vector3(170, 105, -215), city + new Vector3(0, 70, -20));
            report.cityTemple = Capture("atlantis-temple.png");
            MatterParticle[] cityBefore = marine.Matter.Readback();
            for (int step = 0; step < 120; step++)
            {
                simulationSong += .05f;
                marine.Tick(simulationSong, .05f, player);
                game.Finale.Tick(simulationSong, .05f);
            }
            report.cityShimmer = Capture("atlantis-shimmer-6s.png");
            MatterParticle[] cityAfter = marine.Matter.Readback();
            float radiusSum = 0, radiusSquareSum = 0;
            report.stableCityParticleIds = true;
            for (int id = 0; id < cityBefore.Length; id += 17)
            {
                if (marine.SeedAt(id).traits.y < 2.5f || cityBefore[id].identityState.z != 3f) continue;
                report.sampledCityParticles++;
                Vector3 beforeColor = cityBefore[id].colorSize, afterColor = cityAfter[id].colorSize;
                report.cityMeanLightChange += Vector3.Distance(beforeColor, afterColor);
                float movement = Vector3.Distance(cityBefore[id].positionAge, cityAfter[id].positionAge);
                report.cityMeanParticleTravel += movement;
                report.cityMaxParticleTravel = Mathf.Max(report.cityMaxParticleTravel, movement);
                report.stableCityParticleIds &= cityBefore[id].identityState.x == cityAfter[id].identityState.x;
                float radius = cityAfter[id].colorSize.w;
                radiusSum += radius; radiusSquareSum += radius * radius;
            }
            int sampleCount = Mathf.Max(1, report.sampledCityParticles);
            report.cityMeanLightChange /= sampleCount;
            report.cityMeanParticleTravel /= sampleCount;
            float meanRadius = radiusSum / sampleCount;
            report.cityRadiusVariation = Mathf.Sqrt(Mathf.Max(0, radiusSquareSum / sampleCount - meanRadius * meanRadius)) /
                Mathf.Max(.0001f, meanRadius);
            report.cityVisibleChange = ImageDifferenceFraction(report.cityTemple, report.cityShimmer);
            Check(report.sampledCityParticles > 5000 && report.stableCityParticleIds && report.cityMeanParticleTravel > .02f &&
                report.cityMaxParticleTravel < 1.5f && report.cityMeanLightChange > .04f && report.cityRadiusVariation > .23f &&
                report.cityVisibleChange > .01f && report.cityShimmer.whiteFraction < .08f,
                "The city must retain its matter IDs and shape, with perceptible light change, bounded flow and diverse particle radii.");
            Check(game.Finale.Formation > .99f && game.Finale.LayerCount == 4 && report.cityBounds.size.x >= 500 &&
                report.cityWide.nonBlank && report.cityTemple.nonBlank && report.cityWide.whiteFraction < .08f &&
                report.cityTemple.whiteFraction < .12f, "The expanded full city must form without a whiteout.");
            report.cityFishCount = AtlantisGeometry.SerpentSchoolCount * AtlantisGeometry.FishPerSchool;
            report.desktopCityPoints = game.Finale.ActivePointCount;
            Check(report.desktopCityPoints == game.Finale.DesktopPointCount && report.desktopCityPoints >= 300000,
                "Full desktop city must render its declared dense geometry budget.");
            for (int school = 0; school < AtlantisGeometry.SerpentSchoolCount; school++)
            {
                var a = AtlantisGeometry.EvaluateSchoolPose(school, 0);
                var b = AtlantisGeometry.EvaluateSchoolPose(school, 6);
                report.fishMaxTravel = Mathf.Max(report.fishMaxTravel, Vector3.Distance(a.Center, b.Center));
                Check(new Vector2(a.Center.x, a.Center.z).magnitude < 270 && Vector3.Distance(a.Center, b.Center) > 20,
                    "Dense fish schools must travel over the continent, not orbit at remote static centers.");
            }
            Check(report.cityFishCount >= 560, "The city must have dense coherent schools rather than isolated tiny fish sets.");
            yield return MeasureFinaleFrames();
            for (int combination = 0; combination < 8; combination++)
            {
                BossId mask = BossId.None;
                if ((combination & 1) != 0) mask |= BossId.Serpent;
                if ((combination & 2) != 0) mask |= BossId.Hermit;
                if ((combination & 4) != 0) mask |= BossId.Submarine;
                game.Finale.Begin(mask, simulationSong);
                Check(game.Finale.LayerCount == 1 + RunProgress.Count(mask), "All eight ending masks must retain their optional-layer contract.");
            }
            game.Flight.EnableVr(true);
            game.Finale.Begin(RunProgress.OptionalBosses, simulationSong);
            game.Finale.Tick(simulationSong, .016f);
            report.vrCityPoints = game.Finale.ActivePointCount;
            Check(game.Finale.UsesReducedDensity && report.vrCityPoints == game.Finale.VrPointCount &&
                report.vrCityPoints < 205000 && report.vrCityPoints < report.desktopCityPoints * .7f,
                "Enabling VR after game startup must switch to actual reduced geometry, not keep desktop meshes.");
            game.Flight.EnableVr(false);
            game.Finale.Tick(simulationSong, .016f);
            Check(!game.Finale.UsesReducedDensity && game.Finale.ActivePointCount == report.desktopCityPoints,
                "Exiting VR must restore desktop detail without rebuilding or losing optional layers.");
            game.Restart();
            Check(marine.CombatFirstActionAt == -1 && game.Horizon.ImpactSplash.ImpactCount == 0 && !game.Finale.Active,
                "Restart must reset leap scheduling, curtain events and every city layer.");
        }

        IEnumerator MeasureFinaleFrames()
        {
            var target = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32);
            var pixel = new Texture2D(1, 1, TextureFormat.RGB24, false);
            var samples = new float[8];
            RenderTexture previous = RenderTexture.active;
            target.Create();
            try
            {
                for (int i = -2; i < samples.Length; i++)
                {
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    simulationSong += StepSeconds;
                    game.Marine.Tick(simulationSong, StepSeconds, game.Flight.Position);
                    game.Finale.Tick(simulationSong, StepSeconds);
                    game.World.Tick(simulationSong, 1f, 0f, game.ReducedMotion);
                    RenderPipeline.SubmitRenderRequest(game.Flight.View,
                        new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target;
                    // Readback waits for this actual render, even when the player window is hidden.
                    pixel.ReadPixels(new Rect(CaptureWidth / 2, CaptureHeight / 2, 1, 1), 0, 0);
                    timer.Stop();
                    if (i >= 0) samples[i] = (float)timer.Elapsed.TotalMilliseconds;
                    RenderTexture.active = previous;
                    yield return null;
                }
                Array.Sort(samples);
                foreach (float ms in samples) report.renderAndReadbackMeanMs += ms / samples.Length;
                report.renderAndReadbackP95Ms = samples[(int)(samples.Length * .95f)];
                report.renderSamples = samples.Length;
                report.performanceMethod = "1600x900 full-city URP render plus synchronous 1-pixel readback; not GPU-only or HMD timing";
                report.gpu = SystemInfo.graphicsDeviceName;
                report.actualVrMeasured = false;
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                Destroy(target);
                Destroy(pixel);
            }
        }

        void SamplePlume(WhaleImpactSplash splash, float age, out float height, out float extent)
        {
            height = 0f;
            extent = 0f;
            int count = splash.ParticleCount;
            for (int id = 0; id < count; id++)
            {
                Vector3 position = splash.EvaluateParticle(id, age);
                height = Mathf.Max(height, position.y - splash.Origin.y);
                float lateral = Vector2.Distance(new Vector2(position.x, position.z),
                    new Vector2(splash.Origin.x, splash.Origin.z));
                extent = Mathf.Max(extent, lateral);
            }
        }

        CaptureReport Capture(string name)
        {
            var target = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            Texture2D image = null;
            try
            {
                target.Create();
                game.World.Tick(simulationSong, 1f, 0f, game.ReducedMotion);
                RenderPipeline.SubmitRenderRequest(game.Flight.View,
                    new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                image = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(output, name), image.EncodeToPNG());

                var result = new CaptureReport { file = name };
                Color32[] pixels = image.GetPixels32();
                result.pixels = pixels;
                foreach (Color32 pixel in pixels)
                {
                    if (pixel.r > 245 && pixel.g > 245 && pixel.b > 245) result.whitePixels++;
                    if (pixel.r > 8 || pixel.g > 8 || pixel.b > 8) result.nonBlackPixels++;
                    if (pixel.b > 55 && pixel.b > pixel.r * 1.2f && pixel.g > pixel.r * 1.1f)
                        result.blueCyanPixels++;
                }
                result.whiteFraction = result.whitePixels / (float)pixels.Length;
                result.nonBlackFraction = result.nonBlackPixels / (float)pixels.Length;
                result.blueCyanFraction = result.blueCyanPixels / (float)pixels.Length;
                result.nonBlank = result.nonBlackPixels > pixels.Length / 10000;
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

        static float ImageDifferenceFraction(CaptureReport image, CaptureReport baseline)
        {
            if (image == null || baseline == null || image.pixels == null || baseline.pixels == null ||
                image.pixels.Length != baseline.pixels.Length) return 0f;
            int changed = 0;
            for (int i = 0; i < image.pixels.Length; i++)
            {
                Color32 a = image.pixels[i], b = baseline.pixels[i];
                if (Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) >= 30)
                    changed++;
            }
            return changed / (float)image.pixels.Length;
        }

        void Frame(Vector3 position, Vector3 focus) =>
            game.Flight.SetPose(position, Quaternion.LookRotation(focus - position, Vector3.up));

        void Check(bool value, string message)
        {
            if (!value) AddError(message);
        }

        void AddError(string message)
        {
            var errors = new string[report.errors.Length + 1];
            Array.Copy(report.errors, errors, report.errors.Length);
            errors[errors.Length - 1] = message;
            report.errors = errors;
        }

        void Finish()
        {
            if (finished) return;
            finished = true;
            StopAllCoroutines();
            report.elapsedSeconds = Time.realtimeSinceStartup - started;
            report.passed = report.errors.Length == 0;
            File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
            Application.Quit(report.passed ? 0 : 2);
        }

        void OnDestroy() => Application.logMessageReceived -= OnLog;

        [Serializable] sealed class CaptureReport
        {
            public string file;
            public int whitePixels, nonBlackPixels, blueCyanPixels;
            public float whiteFraction, nonBlackFraction, blueCyanFraction, afterLifetimeDifferenceFraction;
            public bool nonBlank;
            [NonSerialized] public Color32[] pixels;
        }

        [Serializable] sealed class Report
        {
            public bool passed, arrivalTriggered, arrivalCompleted;
            public int initialImpactCount, impactCountAtFirstActive, finalImpactCount, splashParticleCount;
            public float elapsedSeconds, impactAge, latefallAge, splashPeakHeight, initialSplashAge;
            public float heightAt1_2, heightAt2_2, extentAt1_2, extentAt2_2;
            public float expiredMaxHeight, expiredLateralExtent;
            public Vector3 splashOrigin, lastVelocity, latefallPosition;
            public bool expiredSplashActive;
            public CaptureReport latefallCapture;
            public CaptureReport[] captures = new CaptureReport[SplashCaptureAges.Length];
            public string[] errors = Array.Empty<string>();
            public CombatReport[] combatActions;
            public Bounds cityBounds;
            public CaptureReport cityWide, cityTemple, cityShimmer;
            public int cityFishCount, desktopCityPoints, vrCityPoints;
            public int sampledCityParticles;
            public bool stableCityParticleIds;
            public float cityMeanLightChange, cityMeanParticleTravel, cityMaxParticleTravel, cityRadiusVariation, cityVisibleChange;
            public float maxKnotAccelerationGap;
            public float fishMaxTravel, renderAndReadbackMeanMs, renderAndReadbackP95Ms, allCyclesMaxRoomDistance;
            public int renderSamples;
            public string gpu, performanceMethod;
            public bool actualVrMeasured;
            public bool backgroundCapture;
        }

        [Serializable] sealed class CombatReport
        {
            public string kind;
            public bool impacted;
            public int impacts;
            public float impactAge, impactUpDot, impactForwardY, maxHeightAboveWater, maxSpeed, maxRoll;
            public float maxFrameTravel, maxFrameRotation, maxRoomDistance, targetError, gpuSurfaceError;
            public float recoveryHorizontalTravel, recoveryVerticalTravel, delayedTailTorsion;
            public Vector3 impactVelocity;
            public CaptureReport rise, apex, fall, splash;
            public CaptureReport[] followThrough = new CaptureReport[3];
        }
    }
}
