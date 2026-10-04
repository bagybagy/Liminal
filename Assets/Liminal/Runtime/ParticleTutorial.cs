using System;
using System.Collections.Generic;
using UnityEngine;

namespace Liminal
{
    public sealed class ParticleTutorial : MonoBehaviour
    {
        const int ActionLimit = 8;
        const int ParticleLimit = 15000;
        const int HoldPrompt = -2;
        const int ReleasePrompt = -3;
        const float BoardDistance = 26f;
        const float LookGoalDegrees = 12f;

        sealed class GlyphGroup
        {
            public int step, action;
            public GameObject obj;
            public MeshRenderer renderer;
            public MaterialPropertyBlock properties;
            public Vector3 moteCenter;
            public float moteRadius;
            public bool dispersed;
            public GlyphInputMode inputMode;
        }

        enum GlyphInputMode { Desktop, Vr, Any }

        readonly List<GlyphGroup> glyphs = new();
        readonly List<Mesh> ownedMeshes = new();
        readonly List<LockTarget> activeTargets = new();
        readonly LockTarget[] tutorialTargets = new LockTarget[ActionLimit];
        readonly GameObject[] targetVisuals = new GameObject[ActionLimit];
        readonly Vector3[] targetOffsets = new Vector3[ActionLimit];
        readonly bool[] actionDone = new bool[ActionLimit];
        readonly bool[] releaseLocked = new bool[ActionLimit];
        readonly bool[] releaseScheduled = new bool[ActionLimit];
        readonly bool[] releaseResolved = new bool[ActionLimit];
        readonly int[] reservedBeforeRelease = new int[ActionLimit];
        readonly float[] holdTimes = new float[ActionLimit];

        ParticleWorld world;
        Encounter combat;
        Flight flight;
        MusicTransport music;
        Transform board;
        Material tutorialMaterial;
        Mesh socketMesh;
        bool initialized, isEnabled, complete, resetPending, restoreEnabledAfterReset;
        bool lookArmed, holdTracking, holdPromptDispersed;
        bool vrInputMode;
        bool releaseAwaitingRegistration, releaseAwaitingCallbacks, releaseQualified;
        int releaseLesson, releaseScheduledCount, releaseResolvedCount;
        int stepIndex, completedActions, particleCount;
        float lookDegrees, lockHoldSeconds;
        int songId, pulseId, dispersingId, disperseAtId, moteCenterId, moteRadiusId, tintId, gainId;

        public bool Enabled => isEnabled;
        public bool Complete => complete;
        public int StepIndex => stepIndex;
        public int CompletedActions => completedActions;
        public int ParticleCount => particleCount;
        public bool UsesVrInstructions => vrInputMode;
        public IReadOnlyList<LockTarget> Targets => activeTargets;
        public string Status
        {
            get
            {
                if (!initialized) return "TUTORIAL";
                if (!isEnabled) return "TUTORIAL OFF";
                if (flight != null && flight.VrEnabled) {
                    switch (stepIndex) {
                        case 0: return "HEADLOOK OR RIGHT STICK";
                        case 1: return "LEFT STICK: FORWARD, STRAFE, BACK";
                        case 2: return "RIGHT STICK: UP/DOWN, LEFT GRIP: BOOST";
                        case 3: return "RIGHT TRIGGER: HOLD, RELEASE TO FIRE";
                        case 4: return "LOCK 8 TARGETS, HOLD, RELEASE";
                        default: return "TUTORIAL COMPLETE";
                    }
                }
                switch (stepIndex) {
                    case 0: return "MOVE MOUSE TO LOOK";
                    case 1: return "WASD / SWIM: HOLD EACH DIRECTION";
                    case 2: return "Q/E ALTITUDE, SHIFT + MOVE TO BOOST";
                    case 3: return "HOLD TO LOCK, RELEASE TO FIRE";
                    case 4: return "LOCK 8 TARGETS, HOLD, RELEASE";
                    default: return "TUTORIAL COMPLETE";
                }
            }
        }

        public void Initialize(ParticleWorld particleWorld, Encounter encounter, Flight pilot, MusicTransport transport)
        {
            if (initialized) return;
            if (!particleWorld || !encounter || !pilot || !transport)
                throw new ArgumentNullException("Tutorial dependencies must be initialized first.");

            world = particleWorld;
            combat = encounter;
            flight = pilot;
            music = transport;
            songId = Shader.PropertyToID("_TutorialSong");
            pulseId = Shader.PropertyToID("_TutorialPulse");
            dispersingId = Shader.PropertyToID("_Dispersing");
            disperseAtId = Shader.PropertyToID("_DisperseAt");
            moteCenterId = Shader.PropertyToID("_MoteCenter");
            moteRadiusId = Shader.PropertyToID("_MoteRadius");
            tintId = Shader.PropertyToID("_Tint");
            gainId = Shader.PropertyToID("_Gain");

            Shader shader = Resources.Load<Shader>("TutorialMatter");
            if (!shader) throw new InvalidOperationException("Resources/TutorialMatter.shader was not found.");
            tutorialMaterial = new Material(shader) { name = "LIMINAL Tutorial Matter" };
            tutorialMaterial.SetColor(tintId, Color.white);
            tutorialMaterial.SetFloat(gainId, 2.6f);
            tutorialMaterial.enableInstancing = true;

            var boardObject = new GameObject("Particle tutorial / floating glyphs");
            boardObject.transform.SetParent(transform, false);
            board = boardObject.transform;
            FollowBoard(0f, true);
            BuildTargetVisuals();
            BuildLessons();
            initialized = true;
            UpdateShaderClock((float)music.Time);
        }

        public void Tick(float song, float dt, bool controls)
        {
            if (!initialized) return;
            if (flight.VrEnabled) {
                UpdateInputMode();
                return;
            }
            dt = Mathf.Max(0f, dt);

            Vector2 look = Vector2.zero;
            Vector3 movement = Vector3.zero;
            bool boost = false, fireHeld = false, fireReleased = false;
            if (controls) {
                movement.x = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
                movement.y = (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f);
                movement.z = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
                boost = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                fireHeld = Input.GetMouseButton(0);
                fireReleased = Input.GetMouseButtonUp(0);
                if (flight.IsCursorCaptured) {
                    if (!lookArmed) lookArmed = true;
                    else look = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
                } else {
                    lookArmed = false;
                }
            }

            ObserveInput(look, movement, boost, fireHeld, fireReleased, song, dt);
        }

        public void ObserveInput(Vector2 look, Vector3 movement, bool boost, bool fireHeld,
            bool fireReleased, float song, float dt)
        {
            if (!initialized) return;
            UpdateInputMode();
            dt = Mathf.Max(0f, dt);
            TryFinishDeferredReset(song);
            if (isEnabled) FollowBoard(dt, false);
            UpdateTargetPositions();
            UpdateShaderClock(song);
            CaptureScheduledShots(song);

            if (!isEnabled || complete || resetPending) return;

            if (stepIndex == 0) {
                lookDegrees += Mathf.Abs(look.x) * 2.1f + Mathf.Abs(look.y) * 1.7f;
                if (lookDegrees >= LookGoalDegrees)
                    CompleteAction(0, 0, BoardWorldPosition(Vector3.zero), song);
                return;
            }

            if (stepIndex == 1) {
                ObserveHold(0, movement.z > 0.5f, dt, 0.18f, song);
                ObserveHold(1, movement.x < -0.5f, dt, 0.18f, song);
                ObserveHold(2, movement.z < -0.5f, dt, 0.18f, song);
                ObserveHold(3, movement.x > 0.5f, dt, 0.18f, song);
                return;
            }

            if (stepIndex == 2) {
                ObserveHold(0, movement.y < -0.5f, dt, 0.18f, song);
                ObserveHold(1, movement.y > 0.5f, dt, 0.18f, song);
                ObserveHold(2, boost && movement.sqrMagnitude > 0.01f, dt, 0.25f, song);
                return;
            }

            if (stepIndex == 3 || stepIndex == 4) {
                bool ownsLock = HasTutorialLock();
                if (fireHeld && ownsLock) {
                    holdTracking = true;
                    lockHoldSeconds += dt;
                    if (lockHoldSeconds >= 0.5f && !holdPromptDispersed) {
                        holdPromptDispersed = true;
                        DisperseGroup(stepIndex, HoldPrompt, song);
                    }
                }

                if (fireReleased) CaptureRelease(song);
                else if (!fireHeld && !releaseAwaitingRegistration && !releaseAwaitingCallbacks)
                    ResetHoldTracking();
            }
        }

        public void ResetTutorial()
        {
            if (!initialized) return;
            bool restoreEnabled = isEnabled || (resetPending && restoreEnabledAfterReset);
            if (HasPendingTutorialShots()) {
                restoreEnabledAfterReset = restoreEnabled;
                resetPending = true;
                isEnabled = false;
                HidePresentation();
                return;
            }
            resetPending = false;
            restoreEnabledAfterReset = false;
            ResetProgress((float)music.Time);
            if (restoreEnabled) SetEnabled(true);
        }

        public void SetEnabled(bool value)
        {
            if (!initialized) return;
            UpdateInputMode();
            if (resetPending) {
                restoreEnabledAfterReset = value;
                if (!value) { isEnabled = false; HidePresentation(); }
                return;
            }
            if (value == isEnabled) {
                if (value && !resetPending) RefreshCurrentStep();
                return;
            }

            isEnabled = value;
            if (!value) {
                HidePresentation();
                return;
            }

            if (resetPending) return;
            FollowBoard(0f, true);
            EnsureRegisteredTargets();
            RefreshCurrentStep();
        }

        void BuildTargetVisuals()
        {
            var sockets = new PointCloud();
            TutorialGlyphs.AddSocket(sockets, Vector2.zero, 0.34f, 0.065f, Color.white, 401);
            socketMesh = sockets.Build("Tutorial lock sockets", 4f);
            ownedMeshes.Add(socketMesh);
            particleCount += sockets.Count * ActionLimit;

            for (int i = 0; i < ActionLimit; i++) {
                float angle = Mathf.PI * 0.5f - i * Mathf.PI * 2f / ActionLimit;
                float radius = 3.7f;
                targetOffsets[i] = new Vector3(Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius, 0.035f);
                targetVisuals[i] = PointCloud.Place("Tutorial lock " + (i + 1), socketMesh,
                    tutorialMaterial, board);
                targetVisuals[i].transform.localPosition = targetOffsets[i];
                targetVisuals[i].SetActive(false);
                var renderer = targetVisuals[i].GetComponent<MeshRenderer>();
                var properties = new MaterialPropertyBlock();
                properties.SetColor(tintId, new Color(0.18f, 0.88f, 0.95f));
                properties.SetFloat(dispersingId, 0f);
                renderer.SetPropertyBlock(properties);
            }
            if (particleCount > ParticleLimit)
                throw new InvalidOperationException("Tutorial particle budget exceeded.");
        }

        void BuildLessons()
        {
            Color cyan = new(0.12f, 0.76f, 0.91f);
            Color pearl = new(0.64f, 0.94f, 0.88f);
            Color gold = new(1f, 0.62f, 0.2f);
            Color dim = new(0.19f, 0.48f, 0.56f);

            AddTextGroup(0, -1, "MOUSE / LOOK", new Vector2(0f, 2.3f), 0.23f, 0.105f, pearl, 101, 2.2f);
            var look = new PointCloud();
            TutorialGlyphs.AddMouse(look, new Vector2(0f, 0.05f), 0.82f, 1.38f,
                0.095f, cyan, gold, 111);
            TutorialGlyphs.AddArrow(look, new Vector2(-1.05f, 0.15f), Vector2.left,
                0.9f, 0.075f, dim, 117);
            TutorialGlyphs.AddArrow(look, new Vector2(1.05f, 0.15f), Vector2.right,
                0.9f, 0.075f, dim, 119);
            AddGroup(0, 0, look, new Vector3(0f, 0.05f, 0f), 1.7f);

            AddTextGroup(1, -1, "WASD / SWIM", new Vector2(0f, 3.35f), 0.20f, 0.09f, pearl, 201, 2.7f);
            var forward = new PointCloud();
            TutorialGlyphs.AddText(forward, "W", new Vector2(0f, 1.72f), 0.22f, 0.09f, gold, 211);
            TutorialGlyphs.AddArrow(forward, new Vector2(0f, 0.83f), Vector2.up, 0.72f, 0.08f, cyan, 212);
            AddGroup(1, 0, forward, new Vector3(0f, 1.2f, 0f), 1.1f);
            var left = new PointCloud();
            TutorialGlyphs.AddText(left, "A", new Vector2(-2.18f, 0.35f), 0.22f, 0.09f, gold, 221);
            TutorialGlyphs.AddArrow(left, new Vector2(-1.37f, 0.35f), Vector2.left, 0.72f, 0.08f, cyan, 222);
            AddGroup(1, 1, left, new Vector3(-1.8f, 0.35f, 0f), 1.2f);
            var back = new PointCloud();
            TutorialGlyphs.AddText(back, "S", new Vector2(0f, -0.52f), 0.22f, 0.09f, gold, 231);
            TutorialGlyphs.AddArrow(back, new Vector2(0f, -1.42f), Vector2.down, 0.72f, 0.08f, cyan, 232);
            AddGroup(1, 2, back, new Vector3(0f, -1f, 0f), 1.1f);
            var right = new PointCloud();
            TutorialGlyphs.AddText(right, "D", new Vector2(2.18f, 0.35f), 0.22f, 0.09f, gold, 241);
            TutorialGlyphs.AddArrow(right, new Vector2(1.37f, 0.35f), Vector2.right, 0.72f, 0.08f, cyan, 242);
            AddGroup(1, 3, right, new Vector3(1.8f, 0.35f, 0f), 1.2f);

            AddTextGroup(2, -1, "Q E / DOWN UP", new Vector2(0f, 3.4f), 0.19f, 0.085f, pearl, 301, 2.7f);
            var ascend = new PointCloud();
            TutorialGlyphs.AddText(ascend, "Q", new Vector2(-2.5f, 1.35f), 0.38f, 0.115f, gold, 311);
            TutorialGlyphs.AddArrow(ascend, new Vector2(-2.5f, 0.05f), Vector2.down, 0.9f, 0.09f, cyan, 312);
            TutorialGlyphs.AddText(ascend, "DOWN", new Vector2(-2.5f, -1.03f), 0.18f, 0.07f, pearl, 313);
            AddGroup(2, 0, ascend, new Vector3(-2.5f, 0.1f, 0f), 1.5f);
            var descend = new PointCloud();
            TutorialGlyphs.AddText(descend, "E", new Vector2(2.5f, 1.35f), 0.38f, 0.115f, gold, 321);
            TutorialGlyphs.AddArrow(descend, new Vector2(2.5f, 0.05f), Vector2.up, 0.9f, 0.09f, cyan, 322);
            TutorialGlyphs.AddText(descend, "UP", new Vector2(2.5f, -1.03f), 0.17f, 0.065f, pearl, 323);
            AddGroup(2, 1, descend, new Vector3(2.5f, 0.1f, 0f), 1.5f);
            var boost = new PointCloud();
            TutorialGlyphs.AddText(boost, "SHIFT / BOOST", new Vector2(0f, -2.42f), 0.19f, 0.085f, gold, 331);
            TutorialGlyphs.AddArrow(boost, new Vector2(0f, -3.32f), Vector2.up, 0.6f, 0.075f, cyan, 332);
            AddGroup(2, 2, boost, new Vector3(0f, -2.85f, 0f), 2f);

            AddTextGroup(3, HoldPrompt, "HOLD / LOCK", new Vector2(0f, 2.9f), 0.22f, 0.1f, gold, 401, 2f);
            var heldMouse = new PointCloud();
            TutorialGlyphs.AddMouse(heldMouse, new Vector2(-1.35f, 0.9f), 0.72f, 1.2f,
                0.085f, cyan, gold, 411);
            TutorialGlyphs.AddSocket(heldMouse, Vector2.zero, 0.48f, 0.07f, pearl, 413);
            AddGroup(3, 0, heldMouse, Vector3.zero, 1.5f);
            var release = new PointCloud();
            TutorialGlyphs.AddText(release, "RELEASE / FIRE", new Vector2(0f, -2.8f),
                0.20f, 0.09f, pearl, 421);
            TutorialGlyphs.AddArrow(release, new Vector2(0f, -1.72f), Vector2.up,
                0.7f, 0.08f, gold, 422);
            AddGroup(3, 0, release, new Vector3(0f, -2.35f, 0f), 1.7f);

            AddTextGroup(4, -1, "8 LOCKS", new Vector2(0f, 5.9f), 0.24f, 0.105f,
                pearl, 501, 2.2f, GlyphInputMode.Any);
            AddTextGroup(4, HoldPrompt, "HOLD / LOCK", new Vector2(0f, -5.05f), 0.20f, 0.09f, gold, 511, 2.2f);
            AddTextGroup(4, ReleasePrompt, "RELEASE / FIRE", new Vector2(0f, -6.65f), 0.19f, 0.085f, pearl, 521, 2.4f);
            for (int i = 0; i < ActionLimit; i++) {
                float angle = Mathf.PI * 0.5f - i * Mathf.PI * 2f / ActionLimit;
                Vector2 center = new(Mathf.Cos(angle) * 3.7f, Mathf.Sin(angle) * 3.7f);
                var socket = new PointCloud();
                TutorialGlyphs.AddSocket(socket, center, 0.5f, 0.075f, i % 2 == 0 ? cyan : gold, 531 + i);
                TutorialGlyphs.AddText(socket, (i + 1).ToString(), center + Vector2.up * 0.72f,
                    0.16f, 0.06f, pearl, 551 + i);
                AddGroup(4, i, socket, new Vector3(center.x, center.y, 0f), 1.45f, GlyphInputMode.Any);
            }

            BuildVrLessons(cyan, pearl, gold, dim);
        }

        void BuildVrLessons(Color cyan, Color pearl, Color gold, Color dim)
        {
            AddTextGroup(0, -1, "HEAD / STICK LOOK", new Vector2(0f, 2.3f),
                0.17f, 0.075f, pearl, 601, 2f, GlyphInputMode.Vr);
            var look = new PointCloud();
            TutorialGlyphs.AddSocket(look, Vector2.zero, 0.58f, 0.075f, cyan, 611);
            TutorialGlyphs.AddArrow(look, new Vector2(0f, 1.05f), Vector2.up, 0.62f, 0.07f, gold, 612);
            TutorialGlyphs.AddArrow(look, new Vector2(0f, -1.05f), Vector2.down, 0.62f, 0.07f, gold, 613);
            TutorialGlyphs.AddArrow(look, new Vector2(-1.05f, 0f), Vector2.left, 0.62f, 0.07f, dim, 614);
            TutorialGlyphs.AddArrow(look, new Vector2(1.05f, 0f), Vector2.right, 0.62f, 0.07f, dim, 615);
            AddGroup(0, 0, look, Vector3.zero, 1.8f, GlyphInputMode.Vr);

            AddTextGroup(1, -1, "LEFT STICK / SWIM", new Vector2(0f, 3.35f),
                0.16f, 0.07f, pearl, 621, 2.4f, GlyphInputMode.Vr);
            var swimStick = new PointCloud();
            TutorialGlyphs.AddSocket(swimStick, Vector2.zero, 0.48f, 0.065f, cyan, 631);
            AddGroup(1, -1, swimStick, Vector3.zero, 1.4f, GlyphInputMode.Vr);
            var swimForward = new PointCloud();
            TutorialGlyphs.AddArrow(swimForward, new Vector2(0f, 1.22f), Vector2.up, 0.58f, 0.07f, gold, 632);
            TutorialGlyphs.AddText(swimForward, "FORWARD", new Vector2(0f, 1.78f), 0.12f, 0.055f, pearl, 636);
            AddGroup(1, 0, swimForward, new Vector3(0f, 1.3f, 0f), 1.2f, GlyphInputMode.Vr);
            var swimLeft = new PointCloud();
            TutorialGlyphs.AddArrow(swimLeft, new Vector2(-1.22f, 0f), Vector2.left, 0.58f, 0.07f, dim, 642);
            TutorialGlyphs.AddText(swimLeft, "LEFT", new Vector2(-1.78f, 0f), 0.12f, 0.055f, pearl, 643);
            AddGroup(1, 1, swimLeft, new Vector3(-1.25f, 0f, 0f), 1.2f, GlyphInputMode.Vr);
            var swimBack = new PointCloud();
            TutorialGlyphs.AddArrow(swimBack, new Vector2(0f, -1.22f), Vector2.down, 0.58f, 0.07f, gold, 652);
            TutorialGlyphs.AddText(swimBack, "BACK", new Vector2(0f, -1.82f), 0.12f, 0.055f, pearl, 653);
            AddGroup(1, 2, swimBack, new Vector3(0f, -1.25f, 0f), 1.2f, GlyphInputMode.Vr);
            var swimRight = new PointCloud();
            TutorialGlyphs.AddArrow(swimRight, new Vector2(1.22f, 0f), Vector2.right, 0.58f, 0.07f, dim, 662);
            TutorialGlyphs.AddText(swimRight, "RIGHT", new Vector2(1.78f, 0f), 0.12f, 0.055f, pearl, 663);
            AddGroup(1, 3, swimRight, new Vector3(1.25f, 0f, 0f), 1.2f, GlyphInputMode.Vr);

            AddTextGroup(2, -1, "RIGHT STICK / UP DOWN", new Vector2(0f, 3.35f),
                0.14f, 0.06f, pearl, 641, 2.5f, GlyphInputMode.Vr);
            var altitudeDown = new PointCloud();
            TutorialGlyphs.AddArrow(altitudeDown, new Vector2(-1.2f, 0.05f), Vector2.down, 0.72f, 0.08f, cyan, 651);
            TutorialGlyphs.AddText(altitudeDown, "DOWN", new Vector2(-1.2f, -0.75f), 0.15f, 0.065f, pearl, 652);
            AddGroup(2, 0, altitudeDown, new Vector3(-1.2f, 0f, 0f), 1.3f, GlyphInputMode.Vr);
            var altitudeUp = new PointCloud();
            TutorialGlyphs.AddArrow(altitudeUp, new Vector2(1.2f, 0.05f), Vector2.up, 0.72f, 0.08f, cyan, 661);
            TutorialGlyphs.AddText(altitudeUp, "UP", new Vector2(1.2f, 0.85f), 0.16f, 0.07f, pearl, 662);
            AddGroup(2, 1, altitudeUp, new Vector3(1.2f, 0f, 0f), 1.3f, GlyphInputMode.Vr);
            var vrBoost = new PointCloud();
            TutorialGlyphs.AddText(vrBoost, "LEFT GRIP / BOOST", new Vector2(0f, -2.35f),
                0.14f, 0.06f, gold, 671);
            TutorialGlyphs.AddArrow(vrBoost, new Vector2(0f, -3.05f), Vector2.up, 0.52f, 0.07f, cyan, 672);
            AddGroup(2, 2, vrBoost, new Vector3(0f, -2.6f, 0f), 1.4f, GlyphInputMode.Vr);

            AddTextGroup(3, HoldPrompt, "RIGHT TRIGGER", new Vector2(0f, 2.9f),
                0.17f, 0.075f, gold, 661, 2f, GlyphInputMode.Vr);
            AddTextGroup(3, HoldPrompt, "HOLD / LOCK", new Vector2(0f, 2.15f),
                0.18f, 0.075f, pearl, 662, 2f, GlyphInputMode.Vr);
            AddTextGroup(3, ReleasePrompt, "RELEASE / FIRE", new Vector2(0f, -2.8f),
                0.18f, 0.075f, pearl, 663, 2f, GlyphInputMode.Vr);

            AddTextGroup(4, HoldPrompt, "RIGHT TRIGGER / HOLD", new Vector2(0f, -5.05f),
                0.15f, 0.065f, gold, 681, 2.2f, GlyphInputMode.Vr);
            AddTextGroup(4, ReleasePrompt, "RELEASE / FIRE", new Vector2(0f, -6.65f),
                0.17f, 0.07f, pearl, 691, 2.4f, GlyphInputMode.Vr);
        }

        void AddTextGroup(int step, int action, string text, Vector2 center, float cell,
            float size, Color color, int seed, float moteRadius, GlyphInputMode mode = GlyphInputMode.Desktop)
        {
            var cloud = new PointCloud();
            TutorialGlyphs.AddText(cloud, text, center, cell, size, color, seed);
            AddGroup(step, action, cloud, new Vector3(center.x, center.y, 0f), moteRadius, mode);
        }

        void AddGroup(int step, int action, PointCloud cloud, Vector3 moteCenter, float moteRadius,
            GlyphInputMode mode = GlyphInputMode.Desktop)
        {
            if (cloud.Count == 0) return;
            if (particleCount + cloud.Count > ParticleLimit)
                throw new InvalidOperationException("Tutorial particle budget exceeded.");
            Mesh mesh = cloud.Build("Tutorial glyph " + step + " / " + action, 48f);
            ownedMeshes.Add(mesh);
            particleCount += cloud.Count;
            GameObject obj = PointCloud.Place("Tutorial glyph " + step + " / " + action, mesh,
                tutorialMaterial, board);
            obj.SetActive(false);
            var renderer = obj.GetComponent<MeshRenderer>();
            var properties = new MaterialPropertyBlock();
            properties.SetFloat(dispersingId, 0f);
            properties.SetFloat(disperseAtId, 0f);
            properties.SetVector(moteCenterId, Vector4.zero);
            properties.SetFloat(moteRadiusId, moteRadius);
            renderer.SetPropertyBlock(properties);
            glyphs.Add(new GlyphGroup {
                step = step, action = action, obj = obj, renderer = renderer,
                properties = properties, moteCenter = moteCenter, moteRadius = moteRadius,
                inputMode = mode
            });
        }

        void ObserveHold(int action, bool held, float dt, float required, float song)
        {
            if (actionDone[action]) return;
            holdTimes[action] = held ? holdTimes[action] + dt : 0f;
            if (holdTimes[action] >= required)
                CompleteAction(stepIndex, action, BoardWorldPosition(Vector3.zero), song);
        }

        void CompleteAction(int step, int action, Vector3 position, float song, bool feedback = true)
        {
            if (step != stepIndex || actionDone[action]) return;
            actionDone[action] = true;
            completedActions++;
            DisperseGroup(step, action, song);
            if (feedback) {
                music.LockSound();
                world.BurstAt(position, song, step == 3 ? new Color(1f, 0.64f, 0.22f) : new Color(0.18f, 0.84f, 0.94f), 0.48f);
            }
            if (AllActionsDone(step)) AdvanceStep(song);
        }

        bool AllActionsDone(int step)
        {
            int count = step == 1 ? 4 : step == 2 ? 3 : step == 4 ? 8 : 1;
            for (int i = 0; i < count; i++) if (!actionDone[i]) return false;
            return true;
        }

        void AdvanceStep(float song)
        {
            int finished = stepIndex;
            DisperseRemainingGroups(finished, song);
            if (finished >= 4) {
                complete = true;
                stepIndex = 5;
                HideTargets();
                return;
            }
            StartStep(finished + 1, song);
        }

        void StartStep(int step, float song)
        {
            stepIndex = step;
            Array.Clear(actionDone, 0, actionDone.Length);
            Array.Clear(holdTimes, 0, holdTimes.Length);
            lookDegrees = 0f;
            ResetHoldTracking();
            ClearReleaseTracking();
            foreach (GlyphGroup glyph in glyphs)
                glyph.obj.SetActive(IsGlyphVisible(glyph));
            SetTargetsForStep();
            UpdateShaderClock(song);
        }

        void RefreshCurrentStep()
        {
            if (!initialized || resetPending) return;
            foreach (GlyphGroup glyph in glyphs)
                glyph.obj.SetActive(IsGlyphVisible(glyph));
            if (complete) HideTargets();
            else SetTargetsForStep();
        }

        void SetTargetsForStep()
        {
            activeTargets.Clear();
            EnsureRegisteredTargets();
            for (int i = 0; i < ActionLimit; i++) {
                bool active = isEnabled && ((stepIndex == 3 && i == 0) ||
                    (stepIndex == 4 && !actionDone[i]));
                LockTarget target = tutorialTargets[i];
                if (target == null) continue;
                targetVisuals[i].transform.localPosition = stepIndex == 3 && i == 0
                    ? new Vector3(0f, 0f, 0.035f) : targetOffsets[i];
                target.position = targetVisuals[i].transform.position;
                if (active) {
                    if (target.reserved == 0 && target.hp <= 0) target.hp = 1;
                    targetVisuals[i].SetActive(true);
                    activeTargets.Add(target);
                } else {
                    targetVisuals[i].SetActive(false);
                }
            }
        }

        void HideTargets()
        {
            activeTargets.Clear();
            for (int i = 0; i < ActionLimit; i++)
                if (targetVisuals[i]) targetVisuals[i].SetActive(false);
        }

        void EnsureRegisteredTargets()
        {
            if (!initialized && board == null) return;
            for (int i = 0; i < ActionLimit; i++) {
                LockTarget target = tutorialTargets[i];
                if (target != null && combat.Targets.Contains(target)) continue;
                if (target != null) {
                    target.onHit = null;
                    target.hp = target.reserved = 0;
                }
                int index = i;
                target = combat.RegisterEnvironment(targetVisuals[i], (hitTarget, song) =>
                    OnTutorialTargetHit(index, hitTarget, song));
                target.acquireRange = Encounter.LockRange;
                tutorialTargets[i] = target;
            }
        }

        void UpdateTargetPositions()
        {
            if (board == null) return;
            for (int i = 0; i < ActionLimit; i++) {
                if (!targetVisuals[i]) continue;
                targetVisuals[i].transform.localPosition = stepIndex == 3 && i == 0
                    ? new Vector3(0f, 0f, 0.035f) : targetOffsets[i];
                LockTarget target = tutorialTargets[i];
                if (target != null) target.position = targetVisuals[i].transform.position;
            }
        }

        void FollowBoard(float dt, bool immediate)
        {
            if (board == null || !flight || !flight.View) return;
            Transform view = flight.View.transform;
            Vector3 destination = flight.Position + view.forward * BoardDistance;
            if (immediate) {
                board.SetPositionAndRotation(destination, view.rotation);
                return;
            }
            float positionBlend = 1f - Mathf.Exp(-3.2f * dt);
            float rotationBlend = 1f - Mathf.Exp(-3.6f * dt);
            board.position = Vector3.Lerp(board.position, destination, positionBlend);
            board.rotation = Quaternion.Slerp(board.rotation, view.rotation, rotationBlend);
        }

        Vector3 BoardWorldPosition(Vector3 local)
        {
            return board ? board.TransformPoint(local) : flight.Position + flight.View.transform.forward * BoardDistance;
        }

        void CaptureRelease(float song)
        {
            if (releaseAwaitingRegistration || releaseAwaitingCallbacks) return;
            ClearReleaseTracking();
            releaseLesson = stepIndex;
            bool exact = false;
            if (stepIndex == 3) {
                exact = combat.Locks.Count == 1 && combat.Locks.Contains(tutorialTargets[0]);
            } else if (stepIndex == 4) {
                exact = combat.Locks.Count == ActionLimit;
                for (int i = 0; i < ActionLimit; i++) exact &= combat.Locks.Contains(tutorialTargets[i]);
            }
            releaseQualified = exact && holdTracking && lockHoldSeconds >= 0.5f;
            for (int i = 0; i < ActionLimit; i++) {
                releaseLocked[i] = combat.Locks.Contains(tutorialTargets[i]);
                reservedBeforeRelease[i] = tutorialTargets[i] != null ? tutorialTargets[i].reserved : 0;
            }
            releaseAwaitingRegistration = true;
            if (!releaseQualified) ResetHoldTracking();
        }

        void CaptureScheduledShots(float song)
        {
            if (!releaseAwaitingRegistration) return;
            releaseAwaitingRegistration = false;
            releaseAwaitingCallbacks = true;
            releaseScheduledCount = 0;
            releaseResolvedCount = 0;
            for (int i = 0; i < ActionLimit; i++) {
                LockTarget target = tutorialTargets[i];
                releaseScheduled[i] = releaseLocked[i] && target != null &&
                    target.reserved > reservedBeforeRelease[i];
                if (releaseScheduled[i]) releaseScheduledCount++;
                releaseResolved[i] = false;
            }
            if (releaseScheduledCount == 0) {
                ClearReleaseTracking();
                if (isEnabled && !complete) SetTargetsForStep();
                return;
            }
            for (int i = 0; i < ActionLimit; i++)
                if (releaseLocked[i] && targetVisuals[i]) targetVisuals[i].SetActive(false);
        }

        void OnTutorialTargetHit(int index, LockTarget target, float song)
        {
            if (resetPending) return;
            if (!isEnabled) {
                target.hp = 1;
                return;
            }
            if (!releaseAwaitingCallbacks || releaseLesson != stepIndex ||
                index < 0 || index >= ActionLimit || !releaseScheduled[index] || releaseResolved[index]) {
                if ((stepIndex == 3 && index == 0) || stepIndex == 4) target.hp = 1;
                return;
            }

            releaseResolved[index] = true;
            releaseResolvedCount++;
            if (releaseQualified) {
                world.BurstAt(target.position, song, new Color(1f, 0.68f, 0.25f), 0.66f);
                music.LockSound();
            } else {
                target.hp = 1;
            }

            if (releaseResolvedCount >= releaseScheduledCount)
                FinishReleaseCallbacks(song);
        }

        void FinishReleaseCallbacks(float song)
        {
            bool valid = releaseQualified && releaseLesson == stepIndex;
            if (releaseLesson == 3) {
                valid &= releaseScheduledCount == 1 && releaseResolved[0];
                if (valid) {
                    CompleteAction(3, 0, tutorialTargets[0].position, song, false);
                } else {
                    for (int i = 0; i < ActionLimit; i++)
                        if (releaseResolved[i]) tutorialTargets[i].hp = 1;
                }
            } else if (releaseLesson == 4) {
                valid &= releaseScheduledCount == ActionLimit;
                for (int i = 0; i < ActionLimit; i++) valid &= releaseResolved[i];
                if (valid) {
                    for (int i = 0; i < ActionLimit; i++)
                        CompleteAction(4, i, tutorialTargets[i].position, song, false);
                } else {
                    for (int i = 0; i < ActionLimit; i++)
                        if (releaseResolved[i]) tutorialTargets[i].hp = 1;
                }
            }
            ClearReleaseTracking();
            if (isEnabled && !complete) SetTargetsForStep();
        }

        void DisperseGroup(int step, int action, float song)
        {
            foreach (GlyphGroup glyph in glyphs)
                if (glyph.step == step && glyph.action == action) BeginDisperse(glyph, song);
        }

        void DisperseRemainingGroups(int step, float song)
        {
            foreach (GlyphGroup glyph in glyphs)
                if (glyph.step == step && !glyph.dispersed) BeginDisperse(glyph, song);
        }

        void BeginDisperse(GlyphGroup glyph, float song)
        {
            if (glyph.dispersed) return;
            Vector3 center = board.TransformPoint(glyph.moteCenter);
            glyph.obj.transform.SetParent(null, true);
            glyph.properties.SetFloat(dispersingId, 1f);
            glyph.properties.SetFloat(disperseAtId, song);
            glyph.properties.SetVector(moteCenterId, center);
            glyph.properties.SetFloat(moteRadiusId, glyph.moteRadius);
            glyph.renderer.SetPropertyBlock(glyph.properties);
            glyph.dispersed = true;
            glyph.obj.SetActive(IsGlyphVisible(glyph));
        }

        bool IsGlyphVisible(GlyphGroup glyph)
        {
            bool correctInputMode = glyph.inputMode == GlyphInputMode.Any ||
                (glyph.inputMode == GlyphInputMode.Vr) == vrInputMode;
            return isEnabled && correctInputMode && (glyph.dispersed || glyph.step == stepIndex);
        }

        void UpdateInputMode()
        {
            bool nextMode = flight != null && flight.VrEnabled;
            if (nextMode == vrInputMode) return;
            vrInputMode = nextMode;
            RefreshCurrentStep();
        }

        void UpdateShaderClock(float song)
        {
            Shader.SetGlobalFloat(songId, song);
            Shader.SetGlobalFloat(pulseId, Score.Pulse(song));
        }

        bool HasTutorialLock()
        {
            for (int i = 0; i < ActionLimit; i++)
                if (tutorialTargets[i] != null && combat.Locks.Contains(tutorialTargets[i])) return true;
            return false;
        }

        bool HasPendingTutorialShots()
        {
            for (int i = 0; i < ActionLimit; i++) {
                LockTarget target = tutorialTargets[i];
                if (target != null && combat.Targets.Contains(target) && target.reserved > 0) return true;
            }
            return false;
        }

        void TryFinishDeferredReset(float song)
        {
            if (!resetPending || HasPendingTutorialShots()) return;
            bool restoreEnabled = restoreEnabledAfterReset;
            resetPending = false;
            restoreEnabledAfterReset = false;
            ResetProgress(song);
            if (restoreEnabled) SetEnabled(true);
        }

        void ResetProgress(float song)
        {
            isEnabled = false;
            complete = false;
            stepIndex = 0;
            completedActions = 0;
            lookDegrees = 0f;
            Array.Clear(actionDone, 0, actionDone.Length);
            Array.Clear(holdTimes, 0, holdTimes.Length);
            ResetHoldTracking();
            ClearReleaseTracking();

            EnsureRegisteredTargets();
            for (int i = 0; i < ActionLimit; i++) {
                LockTarget target = tutorialTargets[i];
                if (target != null) {
                    combat.Locks.Remove(target);
                    target.hp = 0;
                    target.reserved = 0;
                }
                if (targetVisuals[i]) targetVisuals[i].SetActive(false);
            }
            activeTargets.Clear();

            foreach (GlyphGroup glyph in glyphs) {
                glyph.obj.transform.SetParent(board, false);
                glyph.obj.transform.localPosition = Vector3.zero;
                glyph.obj.transform.localRotation = Quaternion.identity;
                glyph.obj.transform.localScale = Vector3.one;
                glyph.dispersed = false;
                glyph.properties.Clear();
                glyph.properties.SetFloat(dispersingId, 0f);
                glyph.properties.SetFloat(disperseAtId, 0f);
                glyph.properties.SetVector(moteCenterId, Vector4.zero);
                glyph.properties.SetFloat(moteRadiusId, glyph.moteRadius);
                glyph.renderer.SetPropertyBlock(glyph.properties);
                glyph.obj.SetActive(false);
            }
            UpdateShaderClock(song);
        }

        void ResetHoldTracking()
        {
            holdTracking = false;
            holdPromptDispersed = false;
            lockHoldSeconds = 0f;
        }

        void HidePresentation()
        {
            for (int i = 0; i < ActionLimit; i++) {
                LockTarget target = tutorialTargets[i];
                if (target != null) combat.Locks.Remove(target);
                if (targetVisuals[i]) targetVisuals[i].SetActive(false);
            }
            activeTargets.Clear();
            foreach (GlyphGroup glyph in glyphs) glyph.obj.SetActive(false);
            ClearReleaseTracking();
            ResetHoldTracking();
        }

        void ClearReleaseTracking()
        {
            releaseAwaitingRegistration = false;
            releaseAwaitingCallbacks = false;
            releaseQualified = false;
            releaseLesson = -1;
            releaseScheduledCount = 0;
            releaseResolvedCount = 0;
            Array.Clear(releaseLocked, 0, releaseLocked.Length);
            Array.Clear(releaseScheduled, 0, releaseScheduled.Length);
            Array.Clear(releaseResolved, 0, releaseResolved.Length);
            Array.Clear(reservedBeforeRelease, 0, reservedBeforeRelease.Length);
        }

        void OnDestroy()
        {
            foreach (GlyphGroup glyph in glyphs)
                if (glyph.obj) Destroy(glyph.obj);
            for (int i = 0; i < ActionLimit; i++) {
                if (tutorialTargets[i] != null && combat) combat.UnregisterEnvironment(tutorialTargets[i]);
                else if (targetVisuals[i]) Destroy(targetVisuals[i]);
            }
            foreach (Mesh mesh in ownedMeshes)
                if (mesh) Destroy(mesh);
            if (tutorialMaterial) Destroy(tutorialMaterial);
            if (board) Destroy(board.gameObject);
        }
    }
}
