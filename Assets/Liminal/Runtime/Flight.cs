using System;
using UnityEngine;

namespace Liminal
{
    public sealed class Flight : MonoBehaviour
    {
        const float CruiseSpeed = 24f;
        const float BoostSpeed = 52f;
        const float Acceleration = CruiseSpeed / 0.7f;
        const float BoostAcceleration = BoostSpeed / 0.7f;
        const float TravelRampSeconds = 3f;
        const float TravelMultiplier = 1.5f;
        const float CoastDamping = 3.53f;
        const float VrAccelerationTime = 0.7f;
        const float VrBrakingTime = 0.35f;
        const float VrYawDegreesPerSecond = 60f;
        const float ArenaSoftStart = 300f;
        const float ArenaRadius = 350f;

        public Camera View { get; private set; }
        public Vector3 Position => transform.position;
        public Vector3 Velocity => velocity;
        public float Speed => velocity.magnitude;
        public Vector3 Emitter => vrEnabled && View != null ? View.transform.position : Position;
        public Vector2 AimScreenPosition => new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        public bool IsCursorCaptured => ownsCursorLock;
        public bool SuppressCursorChanges { get; set; }
        public bool VrEnabled => vrEnabled;
        public float TravelProgress => travelProgress;
        public float TravelSpeedMultiplier => 1f + (TravelMultiplier - 1f) * travelProgress;
        public int TravelPulseCount { get; private set; }
        public bool ReducedMotion;
        public float VrCruiseMetersPerSecond = 20f;
        public float VrBoostMetersPerSecond = 40f;

        Vector3 velocity;
        Vector3 cameraVelocity;
        Vector3 arenaCenter;
        Quaternion playerRotation = Quaternion.identity;
        float yaw;
        float pitch;
        float bank;
        bool ownsCursorLock;
        bool ignoreLookDelta;
        bool vrEnabled;
        float vrYawOffset;
        float vrBrakeSpeed;
        float travelHold, travelProgress, travelSong;
        bool travelAllowed, travelPulseSent;
        Transform previousCameraParent;
        Vector3 previousCameraLocalPosition;
        Quaternion previousCameraLocalRotation;
        Vector3 previousCameraLocalScale;
        bool avatarWasActive;
        ParticleWorld world;
        GameObject avatar;

        public void Initialize(ParticleWorld world, Camera camera)
        {
            this.world = world;
            View = camera;
            DestroyAvatar();

            var pointCloud = new PointCloud();
            var random = new System.Random(41);
            for (int i = 0; i < 1700; i++)
            {
                float u = (float)random.NextDouble();
                float side = i % 2 == 0 ? 1f : -1f;
                float x = side * u * 1.1f;
                float y = Mathf.Sin(u * 3.8f) * 0.25f;
                float z = -u * u * 0.6f;
                if (i % 4 == 0)
                {
                    x *= 0.16f;
                    y = u * 0.7f - 0.25f;
                    z = 0f;
                }
                pointCloud.Add(new Vector3(x, y, z), 0.013f, new Color(0.8f, 0.93f, 1f) * 0.9f, u);
            }

            avatar = PointCloud.Place("Traveler", pointCloud.Build("Traveler", 10), world.NodeMaterial, transform);
            ResetFlight();
        }

        public void ResetFlight()
        {
            if (world != null && world.Caverns != null)
            {
                SetPose(CaveLayout.Spawn, CaveLayout.SpawnRotation);
                return;
            }
            Vector3 focus = Anatomy.Focus(0f);
            Vector3 start = focus + new Vector3(30f, 10f, -75f);
            arenaCenter = new Vector3(0f,10f,35f);
            SetPose(start, Quaternion.LookRotation(focus - start, Vector3.up));
        }

        public void SetPose(Vector3 position, Quaternion orientation)
        {
            SuspendInput();
            velocity = Vector3.zero;
            cameraVelocity = Vector3.zero;
            bank = 0f;

            float norm = orientation.x * orientation.x + orientation.y * orientation.y +
                orientation.z * orientation.z + orientation.w * orientation.w;
            playerRotation = norm > 0.000001f ? orientation.normalized : Quaternion.identity;
            Vector3 forward = playerRotation * Vector3.forward;
            yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            pitch = Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            if (vrEnabled)
            {
                transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw + vrYawOffset, 0f));
                if (avatar != null)
                    avatar.SetActive(false);
            }
            else
            {
                transform.SetPositionAndRotation(position, playerRotation);
                SnapRig();
            }
        }

        public void SuspendInput()
        {
            ResetTravel();
            if (vrEnabled)
            {
                ownsCursorLock = false;
                ignoreLookDelta = true;
                return;
            }
            if (!SuppressCursorChanges)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            ownsCursorLock = false;
            ignoreLookDelta = true;
        }

        public void Tick(float song, float dt, bool controls)
        {
            if (vrEnabled)
                return;

            Vector3 localMove = Vector3.zero;
            Vector2 lookDelta = Vector2.zero;
            bool boost = false;

            if (controls)
            {
                localMove.x = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
                localMove.y = (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f);
                localMove.z = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
                boost = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

                if (Application.isFocused)
                {
                    if (!ownsCursorLock)
                    {
                        Cursor.lockState = CursorLockMode.Locked;
                        Cursor.visible = false;
                        ownsCursorLock = true;
                        ignoreLookDelta = true;
                    }
                    Vector2 delta = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
                    if (ignoreLookDelta) ignoreLookDelta = false;
                    else lookDelta = delta;
                }
                else
                {
                    SuspendInput();
                }
            }
            else
            {
                SuspendInput();
            }

            Step(song, dt, localMove, lookDelta, boost);
        }

        public void Step(float song, float dt, Vector3 localMove, Vector2 lookDelta, bool boost)
        {
            dt = Mathf.Max(0f, dt);
            float oldYaw = yaw;
            if (lookDelta.sqrMagnitude > 0f)
            {
                yaw += lookDelta.x * 2.1f;
                pitch = Mathf.Clamp(pitch + lookDelta.y * 1.7f, -89f, 89f);
                playerRotation = Quaternion.Euler(-pitch, yaw, 0f);
                transform.rotation = playerRotation;
            }

            float yawRate = dt > 0f ? Mathf.DeltaAngle(oldYaw, yaw) / dt : 0f;
            Vector3 move = new Vector3(localMove.x, localMove.y, localMove.z);
            if (move.sqrMagnitude > 1f)
                move.Normalize();

            Vector3 direction = playerRotation * Vector3.right * move.x +
                Vector3.up * move.y + playerRotation * Vector3.forward * move.z;
            if (direction.sqrMagnitude > 1f)
                direction.Normalize();

            UpdateTravel(dt, boost && direction.sqrMagnitude > .04f);

            if (direction.sqrMagnitude > 0f)
            {
                float targetSpeed = boost ? BoostSpeed * TravelSpeedMultiplier : CruiseSpeed;
                velocity = Vector3.MoveTowards(velocity, direction * targetSpeed,
                    (boost ? BoostAcceleration : Acceleration) * dt);
            }
            else if (dt > 0f)
            {
                velocity *= Mathf.Exp(-CoastDamping * dt);
            }

            if (world == null || world.Caverns == null)
                ApplyArenaBoundary(dt);
            Vector3 nextPosition = transform.position + velocity * dt;
            if (world != null && world.Caverns != null)
                nextPosition = CaveLayout.Constrain(nextPosition, ref velocity);
            transform.position = nextPosition;
            UpdateRig(song, dt, yawRate);
        }

        public void EnableVr(bool enabled)
        {
            if (vrEnabled == enabled)
                return;
            ResetTravel();

            if (enabled)
            {
                if (View == null)
                    return;

                previousCameraParent = View.transform.parent;
                previousCameraLocalPosition = View.transform.localPosition;
                previousCameraLocalRotation = View.transform.localRotation;
                previousCameraLocalScale = View.transform.localScale;
                avatarWasActive = avatar != null && avatar.activeSelf;
                vrEnabled = true;
                vrYawOffset = 0f;
                velocity = Vector3.zero;
                cameraVelocity = Vector3.zero;
                if (!SuppressCursorChanges)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                ownsCursorLock = false;
                ignoreLookDelta = true;
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                View.transform.SetParent(transform, false);
                View.transform.localPosition = Vector3.zero;
                View.transform.localRotation = Quaternion.identity;
                View.transform.localScale = Vector3.one;
                if (avatar != null)
                    avatar.SetActive(false);
                return;
            }

            vrEnabled = false;
            vrYawOffset = 0f;
            velocity = Vector3.zero;
            cameraVelocity = Vector3.zero;
            transform.rotation = playerRotation;
            if (View != null)
            {
                View.transform.SetParent(previousCameraParent, false);
                View.transform.localPosition = previousCameraLocalPosition;
                View.transform.localRotation = previousCameraLocalRotation;
                View.transform.localScale = previousCameraLocalScale;
            }
            if (avatar != null)
                avatar.SetActive(avatarWasActive);
            SnapRig();
        }

        public void SetVrHeadPose(Vector3 localPosition, Quaternion localRotation)
        {
            if (!vrEnabled || View == null)
                return;

            View.transform.localPosition = localPosition;
            View.transform.localRotation = localRotation;
        }

        public void RotateVrYaw(float degrees)
        {
            if (!vrEnabled)
                return;

            vrYawOffset = Mathf.DeltaAngle(0f, vrYawOffset + degrees);
            transform.rotation = Quaternion.Euler(0f, yaw + vrYawOffset, 0f);
        }

        public void ResetVrYaw()
        {
            if (!vrEnabled)
                return;

            vrYawOffset = 0f;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        public void StepVr(float dt, Vector3 headForward, float throttle, bool boost)
        {
            if (!vrEnabled)
                return;

            dt = Mathf.Max(0f, dt);
            throttle = Mathf.Clamp(throttle, -1f, 1f);
            headForward = headForward.sqrMagnitude > 0.0001f ? headForward.normalized : transform.forward;
            StepVrMovement(dt, headForward * throttle, boost);
        }

        public void StepVr(float dt, Vector2 moveAxis, float vertical, float yawInput, bool boost)
        {
            if (!vrEnabled)
                return;

            dt = Mathf.Max(0f, dt);
            moveAxis = Vector2.ClampMagnitude(moveAxis, 1f);
            vertical = Mathf.Clamp(vertical, -1f, 1f);
            yawInput = Mathf.Clamp(yawInput, -1f, 1f);
            if (yawInput != 0f)
                RotateVrYaw(yawInput * VrYawDegreesPerSecond * dt);

            Vector3 headForward = View != null ? View.transform.forward : transform.forward;
            headForward = headForward.sqrMagnitude > 0.0001f ? headForward.normalized : transform.forward;
            Vector3 strafeRight = View != null
                ? Vector3.ProjectOnPlane(View.transform.right, Vector3.up)
                : Vector3.zero;
            if (strafeRight.sqrMagnitude <= 0.0001f)
                strafeRight = Vector3.ProjectOnPlane(transform.right, Vector3.up);
            strafeRight.Normalize();

            Vector3 direction = strafeRight * moveAxis.x + headForward * moveAxis.y + Vector3.up * vertical;
            direction = Vector3.ClampMagnitude(direction, 1f);
            StepVrMovement(dt, direction, boost);
        }

        void StepVrMovement(float dt, Vector3 direction, bool boost)
        {
            direction = Vector3.ClampMagnitude(direction, 1f);
            UpdateTravel(dt, boost && direction.sqrMagnitude > .04f);

            if (direction.sqrMagnitude > 0.0016f)
            {
                vrBrakeSpeed = 0f;
                float cruiseSpeed = Mathf.Max(1f, VrCruiseMetersPerSecond);
                float baseBoostSpeed = Mathf.Max(cruiseSpeed, VrBoostMetersPerSecond);
                float targetSpeed = boost ? baseBoostSpeed * TravelSpeedMultiplier : cruiseSpeed;
                Vector3 targetVelocity = direction * targetSpeed;
                velocity = Vector3.MoveTowards(velocity, targetVelocity,
                    (boost ? baseBoostSpeed : cruiseSpeed) / VrAccelerationTime * dt);
            }
            else if (dt > 0f)
            {
                if (vrBrakeSpeed <= 0f)
                    vrBrakeSpeed = Mathf.Max(Mathf.Max(1f, VrCruiseMetersPerSecond), velocity.magnitude) / VrBrakingTime;
                velocity = Vector3.MoveTowards(velocity, Vector3.zero, vrBrakeSpeed * dt);
                if (velocity.sqrMagnitude < .0001f) vrBrakeSpeed = 0f;
            }

            if (world == null || world.Caverns == null)
                ApplyArenaBoundary(dt);
            Vector3 nextPosition = transform.position + velocity * dt;
            if (world != null && world.Caverns != null)
                nextPosition = CaveLayout.Constrain(nextPosition, ref velocity);
            transform.position = nextPosition;
            if (avatar != null && avatar.activeSelf)
                avatar.SetActive(false);
        }

        void ApplyArenaBoundary(float dt)
        {
            if (dt <= 0f)
                return;

            Vector3 offset = transform.position - arenaCenter;
            float distance = offset.magnitude;
            if (distance <= ArenaSoftStart)
                return;

            float t = Mathf.Clamp01((distance - ArenaSoftStart) / (ArenaRadius - ArenaSoftStart));
            float acceleration = Mathf.SmoothStep(0f, 1f, t) * 64f;
            velocity -= offset / distance * acceleration * dt;
        }

        public void SetTravelContext(bool allowed, float song)
        {
            travelAllowed = allowed;
            travelSong = song;
        }

        void UpdateTravel(float dt, bool sustainedBoost)
        {
            if (travelAllowed && sustainedBoost) travelHold += dt;
            else travelHold = 0f;
            float desired = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((travelHold - .7f) / TravelRampSeconds));
            travelProgress = desired > travelProgress ? desired : Mathf.MoveTowards(travelProgress, desired, dt / .65f);
            if (travelProgress >= .98f && !travelPulseSent) {
                travelPulseSent = true;
                TravelPulseCount++;
                if (world != null && velocity.sqrMagnitude > 1f)
                    world.BurstAt(Emitter - velocity.normalized * 22f, travelSong, new Color(.08f, .62f, 1f), .18f);
            }
            if (travelHold == 0f && travelProgress <= .001f) travelPulseSent = false;
        }

        void ResetTravel()
        {
            travelHold = travelProgress = 0f;
            travelAllowed = travelPulseSent = false;
        }

        void UpdateRig(float song, float dt, float yawRate)
        {
            if (View != null)
            {
                View.transform.rotation = playerRotation;
                Vector3 desiredPosition = transform.position - playerRotation * Vector3.forward * (9f + Speed * 0.025f) +
                    Vector3.up * 2.8f;
                View.transform.position = Vector3.SmoothDamp(
                    View.transform.position, desiredPosition, ref cameraVelocity, 0.18f, Mathf.Infinity, dt);
                float behind = Vector3.Dot(Position - View.transform.position, View.transform.forward);
                if (behind < 7f)
                {
                    View.transform.position -= View.transform.forward * (7f - behind);
                    cameraVelocity = Vector3.ProjectOnPlane(cameraVelocity, View.transform.forward);
                }
                if (world != null && world.Caverns != null)
                    View.transform.position = CaveLayout.Constrain(View.transform.position, ref cameraVelocity);

                float targetFov = 54f + (ReducedMotion ? 0f : Score.Pulse(song) * 0.3f);
                float fovBlend = 1f - Mathf.Exp(-5f * dt);
                View.fieldOfView = Mathf.Lerp(View.fieldOfView, targetFov, fovBlend);
            }

            if (avatar != null)
            {
                Vector3 localVelocity = Quaternion.Inverse(playerRotation) * velocity;
                float targetBank = ReducedMotion
                    ? 0f
                    : Mathf.Clamp(-localVelocity.x * 0.5f - yawRate * 0.08f, -24f, 24f);
                bank = Mathf.Lerp(bank, targetBank, 1f - Mathf.Exp(-7f * dt));
                avatar.transform.localPosition = Vector3.zero;
                avatar.transform.localRotation = Quaternion.Euler(0f, 0f, bank);
            }
        }

        void SnapRig()
        {
            if (View != null)
            {
                Vector3 cameraPosition = transform.position - playerRotation * Vector3.forward * 9f + Vector3.up * 2.8f;
                if (world != null && world.Caverns != null)
                {
                    Vector3 cameraMotion = Vector3.zero;
                    cameraPosition = CaveLayout.Constrain(cameraPosition, ref cameraMotion);
                }
                View.transform.SetPositionAndRotation(cameraPosition, playerRotation);
                View.fieldOfView = 54f;
            }

            if (avatar != null)
            {
                avatar.transform.localPosition = Vector3.zero;
                avatar.transform.localRotation = Quaternion.identity;
            }
        }

        void DestroyAvatar()
        {
            if (avatar == null)
                return;

            foreach (MeshFilter filter in avatar.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh != null)
                    Destroy(filter.sharedMesh);
            }
            Destroy(avatar);
            avatar = null;
        }

        void OnDestroy()
        {
            if (vrEnabled)
                EnableVr(false);
            SuspendInput();
            DestroyAvatar();
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused && !vrEnabled) SuspendInput();
        }
    }
}
