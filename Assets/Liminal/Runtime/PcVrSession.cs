using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace Liminal
{
    public sealed class PcVrSession : MonoBehaviour
    {
        const float LoaderTimeoutSeconds = 8f;
        const float HeadsetTimeoutSeconds = 3f;
        const float TrackingLossGraceSeconds = 3f;

        Experience experience;
        Flight flight;
        VrWorldHud worldHud;
        Coroutine transition;
        bool initialized;
        bool desiredEnabled;
        bool triggerWasDown;
        bool lockWasHeld;
        bool overdriveWasDown;
        bool pauseWasDown;
        bool tutorialFireWasHeld;
        bool tutorialConfirmReleaseGate;
        bool hasTutorialHeadRotation;
        bool previousRunInBackground;
        bool changedRunInBackground;
        float trackingLostSince = -1f;
        Vector3 lastHeadPosition;
        Quaternion lastHeadRotation = Quaternion.identity;
        Quaternion previousTutorialHeadRotation = Quaternion.identity;
        bool hasHeadPose;

        public bool Enabled { get; private set; }
        public bool Starting { get; private set; }
        public string Status { get; private set; } = "Desktop flight";
        public bool LockHeld { get; private set; }
        public bool LockReleased { get; private set; }
        public bool OverdrivePressed { get; private set; }
        public bool PausePressed { get; private set; }
        public Vector2 MenuAxis { get; private set; }
        public bool MenuConfirmPressed { get; private set; }
        public static bool ComposePauseInput(bool menu, bool secondary, bool stick)
        {
            return menu || secondary || stick;
        }
        public static bool MenuInputActive(bool paused, bool gameOver) => paused || gameOver;

        public void Initialize(Experience owner)
        {
            if (initialized)
                return;

            if (owner == null || owner.Flight == null || owner.Flight.View == null)
            {
                Status = "PCVR unavailable; desktop flight remains active.";
                return;
            }

            experience = owner;
            flight = owner.Flight;
            worldHud = owner.gameObject.AddComponent<VrWorldHud>();
            worldHud.Initialize(owner, this);
            initialized = true;
            Status = IsWindowsPcvrPlatform() ? "PCVR off; desktop flight active." :
                "PCVR is available only on Windows; desktop flight active.";

            string[] arguments = Environment.GetCommandLineArgs();
            if (Array.Exists(arguments, argument => argument == "--vr"))
                RequestEnable();
        }

        public void RequestEnable()
        {
            if (!initialized || Enabled || desiredEnabled)
                return;

            desiredEnabled = true;
            if (!IsWindowsPcvrPlatform())
            {
                FallBack("PCVR is Windows-only.", null);
                return;
            }

            if (transition == null)
                transition = StartCoroutine(RunTransitions());
        }

        public void RequestDisable()
        {
            if (!initialized || (!Enabled && !desiredEnabled && !Starting))
                return;

            desiredEnabled = false;
            if (transition == null)
                transition = StartCoroutine(RunTransitions());
        }

        public void Tick(float song, float dt, bool controls)
        {
            PausePressed = false;
            LockHeld = false;
            LockReleased = false;
            OverdrivePressed = false;
            MenuAxis = Vector2.zero;
            MenuConfirmPressed = false;
            if (!Enabled || flight == null)
                return;

            bool hasPose = UpdateHeadPose();
            bool paused = experience.Music != null && experience.Music.Paused;
            bool gameOver = experience.Combat != null && experience.Combat.Lost;
            bool menuInput = MenuInputActive(paused, gameOver);
            ReadControllerInput(out Vector2 leftAxis, out Vector2 rightAxis,
                out float leftGrip, out float rightTrigger,
                out bool leftPrimary, out bool rightPrimary, out bool menuDown,
                out bool leftSecondary, out bool leftStickClick);

            bool pauseDown = ComposePauseInput(menuDown, leftSecondary, leftStickClick);
            PausePressed = pauseDown && !pauseWasDown;
            pauseWasDown = pauseDown;

            bool triggerDown = triggerWasDown ? rightTrigger > 0.42f : rightTrigger >= 0.62f;
            MenuConfirmPressed = menuInput && triggerDown && !triggerWasDown;
            MenuAxis = menuInput ? ApplyDeadzone(rightAxis, 0.28f) : Vector2.zero;
            if (tutorialConfirmReleaseGate && !triggerDown)
                tutorialConfirmReleaseGate = false;
            if (MenuConfirmPressed)
                tutorialConfirmReleaseGate = true;
            triggerWasDown = triggerDown;

            bool activeControls = controls && !paused && !menuInput && !PausePressed &&
                !tutorialConfirmReleaseGate && hasPose;
            bool lockNow = activeControls && triggerDown;
            LockHeld = lockNow;
            LockReleased = lockWasHeld && !lockNow && !paused;
            lockWasHeld = lockNow;

            bool overdriveDown = leftPrimary || rightPrimary;
            OverdrivePressed = activeControls && overdriveDown && !overdriveWasDown;
            overdriveWasDown = overdriveDown;

            if (hasPose && !paused && experience.Music != null)
            {
                flight.StepVr(dt, activeControls ? leftAxis : Vector2.zero,
                    activeControls ? rightAxis.y : 0f,
                    activeControls ? rightAxis.x : 0f,
                    activeControls && leftGrip >= 0.55f);
            }

            if (experience.Tutorial != null)
            {
                Vector2 headLook = ReadTutorialHeadLook(hasPose);
                Vector2 look = activeControls ? rightAxis * (dt * 8f) + headLook : Vector2.zero;
                Vector3 movement = activeControls
                    ? new Vector3(leftAxis.x, rightAxis.y, leftAxis.y)
                    : Vector3.zero;
                bool tutorialFireHeld = activeControls && triggerDown;
                bool tutorialFireReleased = activeControls && tutorialFireWasHeld && !triggerDown;
                experience.Tutorial.ObserveInput(look, movement,
                    activeControls && leftGrip >= 0.55f,
                    tutorialFireHeld, tutorialFireReleased, song, dt);
                tutorialFireWasHeld = tutorialFireHeld;
            }
            else
            {
                hasTutorialHeadRotation = false;
                tutorialFireWasHeld = false;
            }

            if (hasPose)
            {
                trackingLostSince = -1f;
                Status = "PCVR active via OpenXR.";
            }
            else
            {
                Status = "Head tracking unavailable; holding the last valid pose.";
                bool xrDeviceInactive = !XRSettings.isDeviceActive;
                if (!Application.isFocused && !xrDeviceInactive)
                {
                    trackingLostSince = -1f;
                }
                else
                {
                    if (trackingLostSince < 0f)
                        trackingLostSince = Time.realtimeSinceStartup;
                    if (Time.realtimeSinceStartup - trackingLostSince >= TrackingLossGraceSeconds)
                    {
                        Debug.LogWarning("[LIMINAL PCVR] Head tracking was lost. Returning to desktop flight.", this);
                        RequestDisable();
                    }
                }
            }

            if (worldHud != null)
                worldHud.Tick(song, dt);
        }

        public void ResetPose()
        {
            bool waitForTriggerRelease = triggerWasDown || tutorialConfirmReleaseGate;
            ResetInputEdges();
            tutorialConfirmReleaseGate = waitForTriggerRelease;
            if (Enabled && flight != null)
            {
                flight.ResetVrYaw();
                UpdateHeadPose();
            }
        }

        Vector2 ReadTutorialHeadLook(bool hasPose)
        {
            if (!hasPose)
            {
                hasTutorialHeadRotation = false;
                return Vector2.zero;
            }

            if (!hasTutorialHeadRotation)
            {
                previousTutorialHeadRotation = lastHeadRotation;
                hasTutorialHeadRotation = true;
                return Vector2.zero;
            }

            Quaternion delta = Quaternion.Inverse(previousTutorialHeadRotation) * lastHeadRotation;
            previousTutorialHeadRotation = lastHeadRotation;
            Vector3 angles = delta.eulerAngles;
            float yaw = Mathf.DeltaAngle(0f, angles.y);
            float pitch = Mathf.DeltaAngle(0f, angles.x);
            return new Vector2(yaw, -pitch) * 0.5f;
        }

        public bool Recenter()
        {
            if (!Enabled)
                return false;

            var subsystems = new List<XRInputSubsystem>();
            SubsystemManager.GetInstances(subsystems);
            foreach (XRInputSubsystem subsystem in subsystems)
            {
                if (subsystem != null && subsystem.running && subsystem.TryRecenter())
                {
                    flight.ResetVrYaw();
                    UpdateHeadPose();
                    hasTutorialHeadRotation = false;
                    Status = "Tracking recentered.";
                    return true;
                }
            }

            Status = "This OpenXR runtime does not support recentering.";
            return false;
        }

        IEnumerator RunTransitions()
        {
            Starting = true;
            while (true)
            {
                if (desiredEnabled && !Enabled)
                    yield return EnableRoutine();
                else if (!desiredEnabled && Enabled)
                    yield return DisableRoutine();
                else
                    break;
            }
            Starting = false;
            transition = null;
        }

        IEnumerator EnableRoutine()
        {
            Status = "Starting PCVR...";
            yield return null;

            XRManagerSettings manager = GetManager();
            if (manager == null)
            {
                FallBack("XR Plug-in Management has no Windows OpenXR settings.", null);
                yield break;
            }

            if (manager.activeLoader != null && !(manager.activeLoader is OpenXRLoader))
            {
                FallBack("The active XR loader is not OpenXR.", null);
                yield break;
            }

            if (manager.activeLoader == null)
            {
                float startedAt = Time.realtimeSinceStartup;
                Coroutine initialize = StartCoroutine(manager.InitializeLoader());
                while (!manager.isInitializationComplete &&
                       Time.realtimeSinceStartup - startedAt < LoaderTimeoutSeconds && desiredEnabled)
                    yield return null;

                if (!desiredEnabled)
                {
                    if (initialize != null && !manager.isInitializationComplete)
                        StopCoroutine(initialize);
                    if (manager.isInitializationComplete)
                        StopOpenXr(manager);
                    Status = "PCVR cancelled; desktop flight active.";
                    yield break;
                }

                if (!manager.isInitializationComplete)
                {
                    if (initialize != null)
                        StopCoroutine(initialize);
                    FallBack("OpenXR initialization timed out.", manager);
                    yield break;
                }
            }

            if (!(manager.activeLoader is OpenXRLoader))
            {
                FallBack("No Windows OpenXR runtime is available.", manager);
                yield break;
            }

            if (!desiredEnabled)
            {
                StopOpenXr(manager);
                Status = "PCVR cancelled; desktop flight active.";
                yield break;
            }

            manager.StartSubsystems();
            float headsetWaitStarted = Time.realtimeSinceStartup;
            bool tracked = false;
            while (Time.realtimeSinceStartup - headsetWaitStarted < HeadsetTimeoutSeconds)
            {
                tracked = XRSettings.isDeviceActive && TryReadHeadPose(out lastHeadPosition, out lastHeadRotation);
                if (tracked || !desiredEnabled)
                    break;
                yield return null;
            }

            if (!desiredEnabled)
            {
                StopOpenXr(manager);
                Status = "PCVR cancelled; desktop flight active.";
                yield break;
            }

            if (!tracked)
            {
                FallBack("No tracked HMD was found; desktop flight remains active.", manager);
                yield break;
            }

            BeginVrBackgroundRun();
            flight.EnableVr(true);
            if (!flight.VrEnabled)
            {
                FallBack("The flight camera could not enter tracked mode.", manager);
                yield break;
            }
            hasHeadPose = true;
            Enabled = true;
            trackingLostSince = -1f;
            ResetInputEdges();
            Application.onBeforeRender -= BeforeRender;
            Application.onBeforeRender += BeforeRender;
            ApplyHeadPose();
            if (worldHud != null)
                worldHud.SetVrActive(true);
            Status = "PCVR active via OpenXR.";
            if (experience.Music != null && experience.Music.Paused)
                experience.TogglePause();
        }

        IEnumerator DisableRoutine()
        {
            Status = "Leaving PCVR...";
            XRManagerSettings manager = GetManager();
            if (manager != null && manager.isInitializationComplete &&
                manager.activeLoader is OpenXRLoader)
                manager.StopSubsystems();

            Application.onBeforeRender -= BeforeRender;
            Enabled = false;
            hasHeadPose = false;
            if (worldHud != null)
                worldHud.SetVrActive(false);
            if (experience != null && experience.Combat != null)
                experience.Combat.AbandonLocks();
            if (flight != null)
                flight.EnableVr(false);
            if (manager != null && manager.isInitializationComplete &&
                manager.activeLoader is OpenXRLoader)
                manager.DeinitializeLoader();
            RestoreBackgroundRun();

            ResetInputEdges();
            Status = "PCVR off; desktop flight active.";
            yield return null;
        }

        void FallBack(string reason, XRManagerSettings manager)
        {
            desiredEnabled = false;
            Application.onBeforeRender -= BeforeRender;
            Enabled = false;
            hasHeadPose = false;
            if (worldHud != null)
                worldHud.SetVrActive(false);
            if (flight != null && flight.VrEnabled)
                flight.EnableVr(false);
            StopOpenXr(manager);
            RestoreBackgroundRun();
            Status = "PCVR unavailable; desktop flight active. " + reason;
            Debug.LogWarning("[LIMINAL PCVR] " + Status, this);
            ResetInputEdges();
        }

        XRManagerSettings GetManager()
        {
            XRGeneralSettings settings = XRGeneralSettings.Instance;
            return settings != null ? settings.Manager : null;
        }

        void StopOpenXr(XRManagerSettings manager)
        {
            if (manager == null || !manager.isInitializationComplete || manager.activeLoader == null)
                return;
            if (!(manager.activeLoader is OpenXRLoader))
                return;

            manager.StopSubsystems();
            manager.DeinitializeLoader();
        }

        void LateUpdate()
        {
            if (Enabled)
                ApplyHeadPose();
        }

        void BeforeRender()
        {
            if (Enabled)
                ApplyHeadPose();
        }

        void ApplyHeadPose()
        {
            if (!TryReadHeadPose(out lastHeadPosition, out lastHeadRotation))
                return;

            hasHeadPose = true;
            flight.SetVrHeadPose(lastHeadPosition, lastHeadRotation);
        }

        bool UpdateHeadPose()
        {
            if (!TryReadHeadPose(out lastHeadPosition, out lastHeadRotation))
                return false;
            hasHeadPose = true;
            flight.SetVrHeadPose(lastHeadPosition, lastHeadRotation);
            return true;
        }

        static bool TryReadHeadPose(out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            InputDevice head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (!head.isValid ||
                !head.TryGetFeatureValue(CommonUsages.devicePosition, out position) ||
                !head.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation))
                return false;

            if (head.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && !tracked)
                return false;

            return IsFinite(position) && IsFinite(rotation) && Quaternion.Dot(rotation, rotation) > 0.5f;
        }

        void ReadControllerInput(out Vector2 leftAxis, out Vector2 rightAxis,
            out float leftGrip, out float rightTrigger,
            out bool leftPrimary, out bool rightPrimary, out bool menuDown,
            out bool leftSecondary, out bool leftStickClick)
        {
            leftAxis = Vector2.zero;
            rightAxis = Vector2.zero;
            leftGrip = 0f;
            rightTrigger = 0f;
            leftPrimary = rightPrimary = menuDown = leftSecondary = leftStickClick = false;

            InputDevice left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            InputDevice right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (left.isValid)
            {
                left.TryGetFeatureValue(CommonUsages.primary2DAxis, out leftAxis);
                left.TryGetFeatureValue(CommonUsages.grip, out leftGrip);
                left.TryGetFeatureValue(CommonUsages.primaryButton, out leftPrimary);
                left.TryGetFeatureValue(CommonUsages.secondaryButton, out leftSecondary);
                left.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out leftStickClick);
                left.TryGetFeatureValue(CommonUsages.menuButton, out menuDown);
            }
            if (right.isValid)
            {
                right.TryGetFeatureValue(CommonUsages.primary2DAxis, out rightAxis);
                right.TryGetFeatureValue(CommonUsages.trigger, out rightTrigger);
                right.TryGetFeatureValue(CommonUsages.primaryButton, out rightPrimary);
            }

            leftAxis = ApplyDeadzone(leftAxis, 0.16f);
            rightAxis = ApplyDeadzone(rightAxis, 0.16f);
            leftGrip = Mathf.Clamp01(leftGrip);
            rightTrigger = Mathf.Clamp01(rightTrigger);
        }

        static Vector2 ApplyDeadzone(Vector2 value, float deadzone)
        {
            float magnitude = value.magnitude;
            if (magnitude <= deadzone)
                return Vector2.zero;
            return value.normalized * Mathf.InverseLerp(deadzone, 1f, Mathf.Min(1f, magnitude));
        }

        void ResetInputEdges()
        {
            triggerWasDown = false;
            lockWasHeld = false;
            overdriveWasDown = false;
            pauseWasDown = false;
            tutorialFireWasHeld = false;
            tutorialConfirmReleaseGate = false;
            hasTutorialHeadRotation = false;
            LockHeld = LockReleased = OverdrivePressed = PausePressed = MenuConfirmPressed = false;
            MenuAxis = Vector2.zero;
        }

        void BeginVrBackgroundRun()
        {
            if (!changedRunInBackground)
            {
                previousRunInBackground = Application.runInBackground;
                changedRunInBackground = true;
            }
            Application.runInBackground = true;
        }

        void RestoreBackgroundRun()
        {
            if (!changedRunInBackground)
                return;
            Application.runInBackground = previousRunInBackground;
            changedRunInBackground = false;
        }

        static bool IsWindowsPcvrPlatform()
        {
            return Application.platform == RuntimePlatform.WindowsEditor ||
                   Application.platform == RuntimePlatform.WindowsPlayer;
        }

        static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        void OnDestroy()
        {
            Application.onBeforeRender -= BeforeRender;
            if (transition != null)
                StopCoroutine(transition);
            XRManagerSettings manager = GetManager();
            bool openXrRunning = manager != null && manager.isInitializationComplete &&
                manager.activeLoader is OpenXRLoader;
            if (openXrRunning)
                manager.StopSubsystems();
            if (flight != null && flight.VrEnabled)
                flight.EnableVr(false);
            if (openXrRunning)
                manager.DeinitializeLoader();
            RestoreBackgroundRun();
        }
    }
}
