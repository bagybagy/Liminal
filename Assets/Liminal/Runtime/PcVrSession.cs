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
        bool tutorialWasEnabled;
        bool previousRunInBackground;
        bool changedRunInBackground;
        float trackingLostSince = -1f;
        Vector3 lastHeadPosition;
        Quaternion lastHeadRotation = Quaternion.identity;
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
            ReadControllerInput(out Vector2 leftAxis, out Vector2 rightAxis,
                out float leftGrip, out float rightTrigger,
                out bool leftPrimary, out bool rightPrimary, out bool menuDown);

            PausePressed = menuDown && !pauseWasDown;
            pauseWasDown = menuDown;

            bool triggerDown = triggerWasDown ? rightTrigger > 0.42f : rightTrigger >= 0.62f;
            MenuConfirmPressed = paused && triggerDown && !triggerWasDown;
            MenuAxis = paused ? ApplyDeadzone(rightAxis, 0.28f) : Vector2.zero;
            triggerWasDown = triggerDown;

            bool activeControls = controls && !paused && hasPose;
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
            ResetInputEdges();
            if (Enabled && flight != null)
            {
                flight.ResetVrYaw();
                UpdateHeadPose();
                if (experience.Tutorial != null)
                    experience.Tutorial.SetEnabled(false);
            }
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
            tutorialWasEnabled = experience.Tutorial != null && experience.Tutorial.Enabled;
            if (experience.Tutorial != null)
                experience.Tutorial.SetEnabled(false);

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

            if (tutorialWasEnabled && experience != null && experience.Tutorial != null)
                experience.Tutorial.SetEnabled(true);
            tutorialWasEnabled = false;
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
            if (tutorialWasEnabled && experience != null && experience.Tutorial != null)
                experience.Tutorial.SetEnabled(true);
            tutorialWasEnabled = false;
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
            out bool leftPrimary, out bool rightPrimary, out bool menuDown)
        {
            leftAxis = Vector2.zero;
            rightAxis = Vector2.zero;
            leftGrip = 0f;
            rightTrigger = 0f;
            leftPrimary = rightPrimary = menuDown = false;

            InputDevice left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            InputDevice right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (left.isValid)
            {
                left.TryGetFeatureValue(CommonUsages.primary2DAxis, out leftAxis);
                left.TryGetFeatureValue(CommonUsages.grip, out leftGrip);
                left.TryGetFeatureValue(CommonUsages.primaryButton, out leftPrimary);
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
