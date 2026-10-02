using System.Collections.Generic;
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
        const float PassageLabelAngularWidthDegrees = 12f;
        const float PassageLabelAngularHeightDegrees = 3.5f;
        public const float LockRingAngularWidthDegrees = 0.16f;

        readonly LineRenderer[] lockRings = new LineRenderer[8];
        readonly TextMesh[] lockLabels = new TextMesh[8];
        readonly List<string> passageLabelTexts = new();
        Experience experience;
        PcVrSession session;
        Transform hudRoot;
        TextMesh stageText;
        TextMesh statusText;
        TextMesh onboardingText;
        TextMesh pauseTitle;
        TextMesh pauseItems;
        TextMesh[] passageLabels;
        Transform passageLabelsRoot;
        LineRenderer reticle;
        Material hudMaterial;
        Material textMaterial;
        Font font;
        float onboardingUntil;
        float nextMenuMoveAt;
        float nextBrightnessStepAt;
        int selectedMenuItem;
        bool active;
        bool wasPaused;
        public bool PauseMenuVisible => active && pauseTitle != null && pauseTitle.gameObject.activeInHierarchy;
        public int VisibleLocks { get; private set; }
        public int VisiblePassageLabels { get; private set; }
        public IReadOnlyList<string> PassageLabelTexts => passageLabelTexts;
        public static float LockRingWidth(float distance) => Mathf.Max(.012f, distance * Mathf.Tan(LockRingAngularWidthDegrees * Mathf.Deg2Rad));

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
            int passageLabelCapacity = 1;
            for (int room = 0; room < CaveLayout.Rooms.Length; room++)
                passageLabelCapacity = Mathf.Max(passageLabelCapacity, CaveLayout.IncidentPassages(room).Count);
            passageLabels = new TextMesh[passageLabelCapacity];
            passageLabelsRoot = new GameObject("VR passage destination labels").transform;
            passageLabelsRoot.SetParent(experience.transform, false);
            for (int i = 0; i < passageLabels.Length; i++)
            {
                CreateText("Passage destination " + (i + 1), Vector3.zero, 0.028f,
                    out passageLabels[i], passageLabelsRoot);
                passageLabels[i].color = new Color(0.48f, 1f, 0.86f, 0.98f);
                passageLabels[i].gameObject.SetActive(false);
            }
            passageLabelsRoot.gameObject.SetActive(false);
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
                CreateText("Lock number " + (i + 1), Vector3.zero, .04f, out lockLabels[i]);
                lockLabels[i].text = (i + 1).ToString();
                lockLabels[i].color = Color.white;
                lockLabels[i].gameObject.SetActive(false);
            }
            hudRoot.gameObject.SetActive(false);
        }

        public void SetVrActive(bool value)
        {
            if (hudRoot == null)
                return;

            active = value;
            hudRoot.gameObject.SetActive(value);
            passageLabelsRoot.gameObject.SetActive(value);
            wasPaused = false;
            pauseTitle.gameObject.SetActive(false);
            pauseItems.gameObject.SetActive(false);
            if (!value)
            {
                HidePassageLabels();
                return;
            }

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
            if (paused != wasPaused)
            {
                PositionPanel(paused ? 2f : 1.65f, paused ? 0f : -.12f);
                wasPaused = paused;
            }
            stageText.gameObject.SetActive(!paused);
            UpdateReadouts(song);
            UpdateReticle();
            reticle.enabled = !paused;
            UpdateLockRings(paused);
            UpdatePassageLabels(paused);
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
                FitText(stageText, 1.1f, .12f);
            }
            if (statusText != null)
            {
                statusText.text = session != null ? session.Status : "PCVR";
                FitText(statusText, 1.5f, .045f);
            }
        }

        void UpdateOnboarding(bool paused)
        {
            bool show = !paused && Time.realtimeSinceStartup < onboardingUntil;
            if (onboardingText != null)
            {
                onboardingText.gameObject.SetActive(show);
                if (show)
                {
                    onboardingText.text = "PCVR FLIGHT\nLEFT STICK: FORWARD / BACK / STRAFE\n" +
                        "RIGHT STICK: TURN / RISE / DESCEND\n" +
                        "LEFT GRIP: BOOST   RIGHT TRIGGER: HOLD TO LOCK, RELEASE TO FIRE\n" +
                        "X / A: OVERDRIVE   MENU: PAUSE";
                    FitText(onboardingText, 1.85f, .28f);
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

        void UpdateLockRings(bool paused)
        {
            if (experience.Combat == null || experience.Flight.View == null)
                return;

            int count = paused ? 0 : Mathf.Min(lockRings.Length, experience.Combat.Locks.Count);
            VisibleLocks = count;
            Transform view = experience.Flight.View.transform;
            for (int i = 0; i < lockRings.Length; i++)
            {
                LineRenderer ring = lockRings[i];
                if (i >= count || experience.Combat.Locks[i] == null)
                {
                    ring.enabled = false;
                    lockLabels[i].gameObject.SetActive(false);
                    continue;
                }

                LockTarget target = experience.Combat.Locks[i];
                Vector3 offset = target.position - view.position;
                float distance = Mathf.Max(0.1f, offset.magnitude);
                float radius = Mathf.Max(MinimumTargetRadius, distance * Mathf.Tan(TargetRadiusRadians));
                SetCircle(ring, target.position, view.right, view.up, radius, RingSegments);
                ring.widthMultiplier = LockRingWidth(distance);
                Color color = target.kind == TargetKind.Threat || target.kind == TargetKind.Ray
                    ? new Color(1f, 0.58f, 0.26f, 0.96f)
                    : new Color(0.3f, 1f, 0.88f, 0.96f);
                ring.startColor = ring.endColor = color;
                ring.enabled = true;
                TextMesh label = lockLabels[i];
                label.transform.SetPositionAndRotation(target.position + view.right * radius * .95f + view.up * radius * .78f, view.rotation);
                FitText(label, distance * Mathf.Tan(1.1f * Mathf.Deg2Rad), distance * Mathf.Tan(.85f * Mathf.Deg2Rad));
                label.gameObject.SetActive(true);
            }
        }

        void UpdatePassageLabels(bool paused)
        {
            VisiblePassageLabels = 0;
            passageLabelTexts.Clear();
            if (paused || !active || !experience.CavernMode || experience.Flight.View == null)
            {
                HidePassageLabels();
                return;
            }

            Transform view = experience.Flight.View.transform;
            Vector3 position = experience.Flight.Position;
            int room = Mathf.Clamp(experience.CurrentRoom, 0, CaveLayout.Rooms.Length - 1);
            int nextLabel = 0;
            if (CaveLayout.RoomDistance(room, position) < 1f)
            {
                foreach (int passage in CaveLayout.IncidentPassages(room))
                {
                    bool forward = CaveLayout.FromRoom(passage) == room;
                    int destination = forward ? CaveLayout.ToRoom(passage) : CaveLayout.FromRoom(passage);
                    CaveLayout.GetPortal(passage, forward, out Vector3 mouth, out Vector3 direction);
                    float distance = Vector3.Distance(position, mouth);
                    Vector3 labelPosition = mouth + direction * 7f + Vector3.up * 8f;
                    ShowPassageLabel(nextLabel++, CaveLayout.Rooms[destination].Name + "\n" +
                        distance.ToString("F0") + " M", labelPosition, view);
                }
            }
            else if (CaveLayout.NextPassage(position, out Vector3 waypoint, out int destination))
            {
                float distance = Vector3.Distance(position, waypoint);
                Vector3 labelPosition = waypoint + Vector3.up * 8f;
                ShowPassageLabel(nextLabel++, "NEXT  " + CaveLayout.Rooms[destination].Name + "\n" +
                    distance.ToString("F0") + " M", labelPosition, view);
            }

            for (int i = nextLabel; i < passageLabels.Length; i++)
                if (passageLabels[i].gameObject.activeSelf)
                    passageLabels[i].gameObject.SetActive(false);
            VisiblePassageLabels = nextLabel;
        }

        void ShowPassageLabel(int index, string value, Vector3 position, Transform view)
        {
            if (index >= passageLabels.Length)
                return;

            TextMesh label = passageLabels[index];
            label.text = value;
            label.transform.SetPositionAndRotation(position, view.rotation);
            float distance = Mathf.Max(0.1f, Vector3.Distance(view.position, position));
            float width = 2f * distance * Mathf.Tan(PassageLabelAngularWidthDegrees * 0.5f * Mathf.Deg2Rad);
            float height = 2f * distance * Mathf.Tan(PassageLabelAngularHeightDegrees * 0.5f * Mathf.Deg2Rad);
            FitText(label, width, height);
            label.gameObject.SetActive(true);
            passageLabelTexts.Add(value);
        }

        void HidePassageLabels()
        {
            VisiblePassageLabels = 0;
            passageLabelTexts.Clear();
            if (passageLabels == null)
                return;
            foreach (TextMesh label in passageLabels)
                if (label != null)
                    label.gameObject.SetActive(false);
        }

        void PositionPanel(float distance, float height)
        {
            Transform view = experience.Flight.View.transform;
            Vector3 forward = view.forward;
            Vector3 up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > .98f ? view.up : Vector3.up;
            Quaternion facing = Quaternion.LookRotation(forward, up);
            hudRoot.SetPositionAndRotation(view.position + forward * distance + Vector3.up * height, facing);
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
            FitText(pauseItems, 1.8f, .38f);
            pauseTitle.text = "PAUSED";
            FitText(pauseTitle, 1.2f, .09f);

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

        void CreateText(string objectName, Vector3 localPosition, float characterSize, out TextMesh text,
            Transform parent = null)
        {
            GameObject child = new GameObject(objectName);
            child.transform.SetParent(parent != null ? parent : hudRoot, false);
            child.transform.localPosition = localPosition;
            text = child.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = Mathf.Min(characterSize, .01f);
            text.richText = false;
            if (font != null)
                text.font = font;

            MeshRenderer renderer = child.GetComponent<MeshRenderer>();
            if (textMaterial != null)
                renderer.sharedMaterial = textMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static void FitText(TextMesh text, float width, float height)
        {
            Vector3 size = text.GetComponent<MeshRenderer>().localBounds.size;
            if (size.x <= .00001f || size.y <= .00001f) return;
            float scale = Mathf.Min(width / size.x, height / size.y);
            text.transform.localScale = Vector3.one * scale;
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
            HidePassageLabels();
            if (passageLabelsRoot != null)
                Destroy(passageLabelsRoot.gameObject);
            if (hudRoot != null)
                Destroy(hudRoot.gameObject);
            if (hudMaterial != null)
                Destroy(hudMaterial);
            if (textMaterial != null)
                Destroy(textMaterial);
        }
    }
}
