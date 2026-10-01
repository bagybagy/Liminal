using UnityEngine;
using UnityEngine.Rendering;

namespace Liminal
{
    public sealed class VrWorldHud : MonoBehaviour
    {
        const int RingSegments = 40;
        const int MenuItemCount = 5;
        const float OnboardingSeconds = 24f;
        const float MinimumTargetRadius = 0.13f;
        const float TargetRadiusRadians = 0.038f;

        readonly LineRenderer[] lockRings = new LineRenderer[8];
        Experience experience;
        PcVrSession session;
        Transform hudRoot;
        TextMesh stageText;
        TextMesh statusText;
        TextMesh onboardingText;
        TextMesh pauseTitle;
        TextMesh pauseItems;
        LineRenderer reticle;
        Material hudMaterial;
        Material textMaterial;
        Font font;
        float onboardingUntil;
        float nextMenuMoveAt;
        float nextBrightnessStepAt;
        int selectedMenuItem;
        bool active;

        public void Initialize(Experience owner, PcVrSession vrSession)
        {
            experience = owner;
            session = vrSession;
            if (experience == null || experience.Flight == null)
                return;

            Shader shader = Resources.Load<Shader>("VrHud");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogWarning("[LIMINAL PCVR] No shader is available for the world HUD.", this);
                return;
            }

            hudMaterial = new Material(shader) { name = "LIMINAL PCVR HUD" };
            hudMaterial.mainTexture = Texture2D.whiteTexture;
            hudMaterial.renderQueue = (int)RenderQueue.Transparent;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                textMaterial = new Material(shader) { name = "LIMINAL PCVR HUD Text" };
                textMaterial.mainTexture = font.material.mainTexture;
                textMaterial.renderQueue = (int)RenderQueue.Transparent;
            }

            hudRoot = new GameObject("VR body HUD").transform;
            hudRoot.SetParent(experience.Flight.transform, false);
            CreateText("Stage and status", Vector3.zero, 0.034f, out stageText);
            CreateText("Onboarding", new Vector3(0f, -0.23f, 0f), 0.029f, out onboardingText);
            CreateText("VR status", new Vector3(0f, -0.53f, 0f), 0.024f, out statusText);
            CreateText("Pause title", new Vector3(0f, 0.21f, 0f), 0.04f, out pauseTitle);
            CreateText("Pause choices", new Vector3(0f, -0.18f, 0f), 0.029f, out pauseItems);
            stageText.color = new Color(0.72f, 1f, 0.94f, 1f);
            onboardingText.color = Color.white;
            statusText.color = new Color(0.65f, 0.85f, 0.9f, 0.9f);
            pauseTitle.color = new Color(1f, 0.79f, 0.42f, 1f);
            pauseItems.color = Color.white;
            pauseTitle.gameObject.SetActive(false);
            pauseItems.gameObject.SetActive(false);

            reticle = CreateLine("Gaze reticle", 37, 0.008f);
            reticle.startColor = reticle.endColor = new Color(0.8f, 1f, 0.93f, 0.86f);
            for (int i = 0; i < lockRings.Length; i++)
            {
                lockRings[i] = CreateLine("Lock ring " + (i + 1), RingSegments + 1, 0.018f);
                lockRings[i].startColor = lockRings[i].endColor = new Color(0.3f, 1f, 0.88f, 0.92f);
                lockRings[i].enabled = false;
            }
            hudRoot.gameObject.SetActive(false);
        }

        public void SetVrActive(bool value)
        {
            if (hudRoot == null)
                return;

            active = value;
            hudRoot.gameObject.SetActive(value);
            if (!value)
                return;

            Transform view = experience.Flight.View != null ? experience.Flight.View.transform : null;
            Vector3 localPosition = view != null ? view.localPosition : new Vector3(0f, 1.55f, 0f);
            Vector3 localForward = view != null
                ? experience.Flight.transform.InverseTransformDirection(view.forward)
                : Vector3.forward;
            localForward = Vector3.ProjectOnPlane(localForward, Vector3.up);
            float yaw = localForward.sqrMagnitude > 0.0001f
                ? Mathf.Atan2(localForward.x, localForward.z) * Mathf.Rad2Deg
                : 0f;
            Quaternion facing = Quaternion.Euler(0f, yaw, 0f);
            hudRoot.localPosition = localPosition + facing * new Vector3(0f, -0.12f, 1.65f);
            hudRoot.localRotation = facing;
            onboardingUntil = Time.realtimeSinceStartup + OnboardingSeconds;
            selectedMenuItem = 0;
            nextMenuMoveAt = 0f;
            nextBrightnessStepAt = 0f;
        }

        public void Tick(float song, float dt)
        {
            if (!active || hudRoot == null || experience == null || experience.Flight == null)
                return;

            bool paused = experience.Music != null && experience.Music.Paused;
            UpdateReadouts(song);
            UpdateReticle();
            UpdateLockRings();
            UpdateOnboarding(paused);
            UpdatePauseMenu(paused);
        }

        void UpdateReadouts(float song)
        {
            string stage = experience.CavernMode && CaveLayout.Rooms.Length > 0
                ? CaveLayout.Rooms[Mathf.Clamp(experience.CurrentRoom, 0, CaveLayout.Rooms.Length - 1)].Name
                : Score.SectionName(experience.Combat != null ? experience.Combat.Section : Score.Section(song));
            if (stageText != null && experience.Combat != null)
            {
                stageText.text = stage + "\nLIFE " + experience.Combat.Life.ToString("00") +
                    "   CHARGE " + Mathf.RoundToInt(experience.Combat.Charge * 100f).ToString("00") +
                    "%   LOCKS " + experience.Combat.Locks.Count + "/8";
            }
            if (statusText != null)
                statusText.text = session != null ? session.Status : "PCVR";
        }

        void UpdateOnboarding(bool paused)
        {
            bool show = !paused && Time.realtimeSinceStartup < onboardingUntil;
            if (onboardingText != null)
            {
                onboardingText.gameObject.SetActive(show);
                if (show)
                {
                    onboardingText.text = "PCVR FLIGHT\nLEFT STICK UP/DOWN: FLY ALONG YOUR GAZE\n" +
                        "LEFT GRIP: BOOST   RIGHT TRIGGER: HOLD TO LOCK, RELEASE TO FIRE\n" +
                        "X / A: OVERDRIVE   MENU: PAUSE";
                }
            }
            if (statusText != null)
                statusText.gameObject.SetActive(show);
        }

        void UpdateReticle()
        {
            if (reticle == null || experience.Flight.View == null)
                return;

            Transform view = experience.Flight.View.transform;
            const float distance = 5f;
            float radius = Mathf.Max(0.018f, distance * Mathf.Tan(0.18f * Mathf.Deg2Rad));
            SetCircle(reticle, view.position + view.forward * distance, view.right, view.up, radius, 36);
        }

        void UpdateLockRings()
        {
            if (experience.Combat == null || experience.Flight.View == null)
                return;

            int count = Mathf.Min(lockRings.Length, experience.Combat.Locks.Count);
            Transform view = experience.Flight.View.transform;
            for (int i = 0; i < lockRings.Length; i++)
            {
                LineRenderer ring = lockRings[i];
                if (i >= count || experience.Combat.Locks[i] == null)
                {
                    ring.enabled = false;
                    continue;
                }

                LockTarget target = experience.Combat.Locks[i];
                Vector3 offset = target.position - view.position;
                float distance = Mathf.Max(0.1f, offset.magnitude);
                float radius = Mathf.Max(MinimumTargetRadius, distance * Mathf.Tan(TargetRadiusRadians));
                SetCircle(ring, target.position, view.right, view.up, radius, RingSegments);
                Color color = target.kind == TargetKind.Threat || target.kind == TargetKind.Ray
                    ? new Color(1f, 0.58f, 0.26f, 0.96f)
                    : new Color(0.3f, 1f, 0.88f, 0.96f);
                ring.startColor = ring.endColor = color;
                ring.enabled = true;
            }
        }

        void UpdatePauseMenu(bool paused)
        {
            if (pauseTitle == null || pauseItems == null)
                return;

            pauseTitle.gameObject.SetActive(paused);
            pauseItems.gameObject.SetActive(paused);
            if (!paused)
                return;

            Vector2 axis = session != null ? session.MenuAxis : Vector2.zero;
            if (Mathf.Abs(axis.y) > 0.62f && Time.unscaledTime >= nextMenuMoveAt)
            {
                selectedMenuItem = (selectedMenuItem + (axis.y > 0f ? MenuItemCount - 1 : 1)) % MenuItemCount;
                nextMenuMoveAt = Time.unscaledTime + 0.24f;
            }

            if (selectedMenuItem == 4 && Mathf.Abs(axis.x) > 0.55f && Time.unscaledTime >= nextBrightnessStepAt &&
                experience.Brightness != null && experience.Brightness.Available)
            {
                float next = experience.Brightness.Offset + Mathf.Sign(axis.x) * 0.1f;
                experience.Brightness.SetOffset(next);
                experience.Brightness.Save();
                PlayerPrefs.Save();
                nextBrightnessStepAt = Time.unscaledTime + 0.1f;
            }

            string brightness = experience.Brightness != null && experience.Brightness.Available
                ? experience.Brightness.Offset.ToString("+0.0;-0.0;0.0") + " EV"
                : "UNAVAILABLE";
            string[] labels =
            {
                "RESUME",
                "RESTART RUN",
                "RECENTER VIEW",
                "EXIT PCVR",
                "BRIGHTNESS  " + brightness + "  LEFT/RIGHT ADJUST"
            };
            string menu = "RIGHT STICK: SELECT   RIGHT TRIGGER: CONFIRM\n";
            for (int i = 0; i < labels.Length; i++)
                menu += (i == selectedMenuItem ? "> " : "  ") + labels[i] + (i + 1 < labels.Length ? "\n" : "");
            pauseItems.text = menu;

            if (session != null && session.MenuConfirmPressed)
                ConfirmMenuSelection();
        }

        void ConfirmMenuSelection()
        {
            switch (selectedMenuItem)
            {
                case 0:
                    experience.TogglePause();
                    break;
                case 1:
                    experience.Restart();
                    break;
                case 2:
                    session.Recenter();
                    break;
                case 3:
                    session.RequestDisable();
                    break;
            }
        }

        void CreateText(string objectName, Vector3 localPosition, float characterSize, out TextMesh text)
        {
            GameObject child = new GameObject(objectName);
            child.transform.SetParent(hudRoot, false);
            child.transform.localPosition = localPosition;
            text = child.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = characterSize;
            text.richText = false;
            if (font != null)
                text.font = font;

            MeshRenderer renderer = child.GetComponent<MeshRenderer>();
            if (textMaterial != null)
                renderer.sharedMaterial = textMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        LineRenderer CreateLine(string objectName, int points, float width)
        {
            GameObject child = new GameObject(objectName);
            child.transform.SetParent(hudRoot, false);
            LineRenderer line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = false;
            line.positionCount = points;
            line.widthMultiplier = width;
            line.sharedMaterial = hudMaterial;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            return line;
        }

        static void SetCircle(LineRenderer line, Vector3 center, Vector3 right, Vector3 up, float radius, int segments)
        {
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 point = center + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius;
                line.SetPosition(i, point);
            }
        }

        void OnDestroy()
        {
            if (hudMaterial != null)
                Destroy(hudMaterial);
            if (textMaterial != null)
                Destroy(textMaterial);
        }
    }
}
