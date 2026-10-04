using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace Liminal
{
    public sealed class VrWorldHud : MonoBehaviour
    {
        enum PauseOption
        {
            Resume, Music, Brightness, ResetBrightness, ReducedMotion, ParticleStyle, SideBySide,
            ReplayTutorial, SkipTutorial, Restart, Recenter, ExitPcVr, Quit, PointStudy
        }

        enum StudyOption { Visible, RenderMode, Density, Gain, Flow, Back }

        const int VisibleMenuItems = 6;
        const float PassageLabelAngularWidthDegrees = 12f;
        const float PassageLabelAngularHeightDegrees = 3.5f;
        public const float LockRingAngularWidthDegrees = 0.16f;

        readonly List<string> passageLabelTexts = new();
        readonly StringBuilder pauseMenuBuilder = new(512);
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
        Material textMaterial;
        Font font;
        float nextMenuMoveAt;
        float nextMenuAdjustAt;
        float nextPassageLabelUpdateAt;
        float nextBossStatusUpdateAt;
        int selectedMenuItem;
        int previousPassageRoom = -1;
        Vector3 previousPassagePosition;
        bool active;
        bool wasMenuVisible;
        bool wasGameOver;
        bool pauseMenuDirty = true;
        bool passageLabelsDirty = true;
        int previousMenuItemCount = -1;
        int previousStatusRoom = -1;
        bool studySubmenu;
        int savedMainMenuSelection;
        public bool PauseMenuVisible => active && pauseTitle != null && pauseTitle.gameObject.activeInHierarchy;
        public string BossStatusText { get; private set; } = string.Empty;
        public string PauseButtonLabel => "Y / LEFT STICK CLICK / MENU";
        public int VisibleLocks { get; private set; }
        public int VisiblePassageLabels { get; private set; }
        public IReadOnlyList<string> PassageLabelTexts => passageLabelTexts;
        public string PauseMenuText => pauseItems != null ? pauseItems.text : string.Empty;
        public bool GameplayHudHidden => active &&
            (stageText == null || !stageText.gameObject.activeSelf) &&
            (statusText == null || !statusText.gameObject.activeSelf) &&
            (onboardingText == null || !onboardingText.gameObject.activeSelf) && VisibleLocks == 0;
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
            CreateText("Onboarding", new Vector3(0f, -0.25f, 0f), 0.029f, out onboardingText);
            CreateText("VR status", new Vector3(0f, -0.53f, 0f), 0.024f, out statusText);
            CreateText("Pause title", new Vector3(0f, 0.21f, 0f), 0.04f, out pauseTitle);
            CreateText("Pause choices", new Vector3(0f, -0.34f, 0f), 0.029f, out pauseItems);
            int passageLabelCapacity = 1;
            for (int room = 0; room < CaveLayout.Rooms.Length; room++)
                passageLabelCapacity = Mathf.Max(passageLabelCapacity, CaveLayout.IncidentPassages(room).Count);
            passageLabels = new TextMesh[passageLabelCapacity];
            passageLabelTexts.Capacity = passageLabelCapacity;
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
            stageText.gameObject.SetActive(false);
            statusText.gameObject.SetActive(false);
            onboardingText.gameObject.SetActive(false);
            hudRoot.gameObject.SetActive(false);
        }

        public void SetVrActive(bool value)
        {
            if (hudRoot == null)
                return;

            active = value;
            hudRoot.gameObject.SetActive(value);
            passageLabelsRoot.gameObject.SetActive(value);
            wasMenuVisible = false;
            studySubmenu = false;
            pauseMenuDirty = true;
            passageLabelsDirty = true;
            VisibleLocks = 0;
            stageText.gameObject.SetActive(false);
            statusText.gameObject.SetActive(false);
            onboardingText.gameObject.SetActive(false);
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
            selectedMenuItem = 0;
            nextMenuMoveAt = 0f;
            nextMenuAdjustAt = 0f;
            nextPassageLabelUpdateAt = 0f;
        }

        public void Tick(float song, float dt)
        {
            if (!active || hudRoot == null || experience == null || experience.Flight == null)
                return;

            bool paused = experience.Music != null && experience.Music.Paused;
            UpdateBossStatusDiagnostic();
            bool menuVisible = paused || experience.Combat != null && experience.Combat.Lost;
            UpdatePassageLabels(menuVisible);
            UpdatePauseMenu(paused);
        }

        void UpdateBossStatusDiagnostic()
        {
            int room = experience.CurrentRoom;
            if (room == previousStatusRoom && Time.unscaledTime < nextBossStatusUpdateAt)
                return;
            previousStatusRoom = room;
            nextBossStatusUpdateAt = Time.unscaledTime + 0.25f;
            BossStatusText = BuildBossStatusText();
        }

        string BuildBossStatusText()
        {
            Encounter combat = experience.Combat;
            if (combat == null)
                return "";

            string bossStatus = "";
            if (!experience.CavernMode)
            {
                bossStatus = FormatBossStatus("BOSS", combat.BossDamageGoal - combat.BossDamage,
                    combat.BossDamageGoal, combat.BossDamage / (float)combat.BossDamageGoal);
            }
            else
            {
                switch (Mathf.Clamp(experience.CurrentRoom, 0, CaveLayout.Rooms.Length - 1))
                {
                    case 1:
                        bossStatus = FormatBossStatus("SERPENT", combat.BossDamageGoal - combat.BossDamage,
                            combat.BossDamageGoal, combat.BossDamage / (float)combat.BossDamageGoal);
                        break;
                    case 2:
                        MarineLife marine = experience.Marine;
                        if (marine != null)
                        {
                            string whalePhase = marine.WhaleReleased ? "HORIZON RELEASED" :
                                marine.WhaleRegenerating ? "HORIZON REASSEMBLING" : "HORIZON";
                            bossStatus = FormatBossStatus(whalePhase,
                                MarineLife.WhaleDamageGoal - marine.WhaleResonance,
                                MarineLife.WhaleDamageGoal,
                                marine.WhaleResonance / (float)MarineLife.WhaleDamageGoal);
                        }
                        break;
                    case 3:
                        HermitEncounter hermits = experience.Hermits;
                        if (hermits != null)
                        {
                            const int totalProgress = HermitGeometry.MergeSourceCount + HermitEncounter.BossHitGoal;
                            float progress = hermits.Progress;
                            int completed = Mathf.RoundToInt(progress * totalProgress);
                            if (hermits.Complete || hermits.BossHits >= HermitEncounter.BossHitGoal)
                            {
                                bossStatus = FormatBossStatus("TIDAL REFUGE", totalProgress - completed,
                                    totalProgress, progress);
                            }
                            else if (hermits.Status.StartsWith("GIANT HERMIT"))
                            {
                                bossStatus = FormatBossStatus("GIANT HERMIT",
                                    HermitEncounter.BossHitGoal - hermits.BossHits,
                                    HermitEncounter.BossHitGoal, progress);
                            }
                            else if (hermits.Merging || hermits.Status.Contains("GATHERING"))
                            {
                                bossStatus = FormatBossStatus("HERMIT MERGE", totalProgress - completed,
                                    totalProgress, progress);
                            }
                            else
                            {
                                bossStatus = FormatBossStatus("HERMIT SWARM",
                                    HermitGeometry.MergeSourceCount - hermits.SmallDefeated,
                                    HermitGeometry.MergeSourceCount, progress);
                            }
                        }
                        break;
                    case 4:
                        SubmarineEncounter submarines = experience.Submarines;
                        if (submarines != null)
                        {
                            if (submarines.Complete)
                            {
                                bossStatus = FormatBossStatus("SCARLET COMPLETE", 0,
                                    SubmarineEncounter.TotalGoal, submarines.Progress);
                            }
                            else
                            {
                                int phaseMaximum = submarines.Phase == 0 ? SubmarineEncounter.SubmarineGoal :
                                    submarines.Phase == 1 ? SubmarineEncounter.FleetGoal :
                                    submarines.Phase == 2 ? SubmarineEncounter.PoseidonGoal : 0;
                                string phase = submarines.Phase == 0 ? "SUBMARINE" :
                                    submarines.Phase == 1 ? "FLEET" : "POSEIDON";
                                if (submarines.Transitioning)
                                    phase = submarines.Phase == 2 ? "ENGINE AWAKENING" : "MATTER FORMING";

                                if (phaseMaximum > 0)
                                {
                                    bossStatus = FormatBossStatus(phase,
                                        phaseMaximum - submarines.PhaseHits, phaseMaximum, submarines.Progress);
                                }
                                else
                                {
                                    bossStatus = FormatBossStatus("SCARLET ENGINE",
                                        SubmarineEncounter.TotalGoal - submarines.TotalHits,
                                        SubmarineEncounter.TotalGoal, submarines.Progress);
                                }
                            }
                        }
                        break;
                }
            }

            string charge = "CHARGE " + Mathf.RoundToInt(combat.Charge * 100f).ToString("00") + "%";
            return string.IsNullOrEmpty(bossStatus) ? charge : bossStatus + "   " + charge;
        }

        static string FormatBossStatus(string label, int remaining, int maximum, float progress)
        {
            int max = Mathf.Max(1, maximum);
            int hpRemaining = Mathf.Clamp(remaining, 0, max);
            int progressPercent = Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f);
            return label + " " + hpRemaining.ToString("D2") + "/" + max.ToString("D2") +
                " LEFT " + progressPercent.ToString("D2") + "%";
        }

        void UpdatePassageLabels(bool paused)
        {
            if (paused || !active || !experience.CavernMode || experience.Flight.View == null)
            {
                HidePassageLabels();
                passageLabelsDirty = true;
                return;
            }

            int room = Mathf.Clamp(experience.CurrentRoom, 0, CaveLayout.Rooms.Length - 1);
            Vector3 position = experience.Flight.Position;
            bool routeChanged = room != previousPassageRoom ||
                (position - previousPassagePosition).sqrMagnitude > 0.25f;
            if (!passageLabelsDirty && !routeChanged && Time.unscaledTime < nextPassageLabelUpdateAt)
                return;
            passageLabelsDirty = false;
            nextPassageLabelUpdateAt = Time.unscaledTime + 0.2f;
            previousPassageRoom = room;
            previousPassagePosition = position;
            VisiblePassageLabels = 0;
            passageLabelTexts.Clear();

            Transform view = experience.Flight.View.transform;
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

            bool gameOver = experience.Combat != null && experience.Combat.Lost;
            bool retryAvailable = experience.CanRetryRoom;
            bool visible = paused || gameOver;
            if (!visible)
            {
                pauseTitle.gameObject.SetActive(false);
                pauseItems.gameObject.SetActive(false);
                if (wasMenuVisible)
                {
                    wasMenuVisible = false;
                    studySubmenu = false;
                    pauseMenuDirty = true;
                }
                return;
            }

            if (!wasMenuVisible)
            {
                wasMenuVisible = true;
                selectedMenuItem = 0;
                studySubmenu = false;
                previousMenuItemCount = -1;
                pauseMenuDirty = true;
            }
            if (wasGameOver != gameOver)
            {
                wasGameOver = gameOver;
                selectedMenuItem = 0;
                studySubmenu = false;
                pauseMenuDirty = true;
            }

            int itemCount = CurrentMenuItemCount(gameOver, retryAvailable);
            if (itemCount != previousMenuItemCount)
            {
                previousMenuItemCount = itemCount;
                pauseMenuDirty = true;
            }
            if (selectedMenuItem >= itemCount)
            {
                selectedMenuItem = 0;
                pauseMenuDirty = true;
            }

            Vector2 axis = session != null ? session.MenuAxis : Vector2.zero;
            if (Mathf.Abs(axis.y) > 0.62f && Time.unscaledTime >= nextMenuMoveAt)
            {
                selectedMenuItem = (selectedMenuItem + (axis.y > 0f ? itemCount - 1 : 1)) % itemCount;
                nextMenuMoveAt = Time.unscaledTime + 0.24f;
                pauseMenuDirty = true;
            }

            if (!gameOver && Mathf.Abs(axis.x) > 0.55f && Time.unscaledTime >= nextMenuAdjustAt)
            {
                int direction = axis.x > 0f ? 1 : -1;
                if (AdjustSelectedOption(direction))
                    pauseMenuDirty = true;
                nextMenuAdjustAt = Time.unscaledTime + 0.16f;
            }

            pauseTitle.gameObject.SetActive(true);
            pauseItems.gameObject.SetActive(true);
            string title = gameOver ? "SIGNAL LOST" : "PAUSED";
            if (pauseTitle.text != title)
            {
                pauseTitle.text = title;
                FitText(pauseTitle, 1.2f, .09f);
            }
            PositionPanel(2f, 0f);

            if (session != null && session.MenuConfirmPressed)
            {
                ConfirmMenuSelection(gameOver, retryAvailable);
                itemCount = CurrentMenuItemCount(gameOver, retryAvailable);
                if (itemCount != previousMenuItemCount)
                {
                    previousMenuItemCount = itemCount;
                    pauseMenuDirty = true;
                }
                if (selectedMenuItem >= itemCount)
                    selectedMenuItem = 0;
            }

            if (pauseMenuDirty)
                RefreshPauseMenu(gameOver, retryAvailable, itemCount);
        }

        int CurrentMenuItemCount(bool gameOver, bool retryAvailable)
        {
            if (gameOver)
                return retryAvailable ? 4 : 3;
            if (studySubmenu)
                return 6;
            return 7 + (experience.Tutorial != null ? 2 : 0) + 4 +
                (experience.CavernMode && experience.PointStudy != null ? 1 : 0);
        }

        PauseOption GetMainOption(int index)
        {
            if (index < 7)
                return (PauseOption)index;
            index -= 7;
            if (experience.Tutorial != null)
            {
                if (index == 0) return PauseOption.ReplayTutorial;
                if (index == 1) return PauseOption.SkipTutorial;
                index -= 2;
            }

            if (index == 0) return PauseOption.Restart;
            if (index == 1) return PauseOption.Recenter;
            if (index == 2) return PauseOption.ExitPcVr;
            if (index == 3) return PauseOption.Quit;
            return PauseOption.PointStudy;
        }

        bool AdjustSelectedOption(int direction)
        {
            if (studySubmenu)
            {
                PointStudy study = experience.PointStudy;
                if (study == null) return false;
                switch ((StudyOption)selectedMenuItem)
                {
                    case StudyOption.Visible:
                        study.SetVisible(!study.Visible);
                        return true;
                    case StudyOption.RenderMode:
                        study.SetMode(study.Mode == PointStudy.RenderMode.NativePoint
                            ? PointStudy.RenderMode.SharpQuad : PointStudy.RenderMode.NativePoint);
                        return true;
                    case StudyOption.Density:
                        study.SetDensity(study.DensityMultiplier == 3 ? 1 : 3);
                        return true;
                    case StudyOption.Gain:
                        float gain = Mathf.Clamp(study.Gain + direction * 0.25f, 0.25f, 12f);
                        if (Mathf.Approximately(gain, study.Gain)) return false;
                        study.SetGain(gain);
                        return true;
                    case StudyOption.Flow:
                        float flow = Mathf.Clamp(study.Flow + direction * 0.1f, 0f, 2f);
                        if (Mathf.Approximately(flow, study.Flow)) return false;
                        study.SetFlow(flow);
                        return true;
                    default:
                        return false;
                }
            }

            switch (GetMainOption(selectedMenuItem))
            {
                case PauseOption.Music:
                    float volume = Mathf.Clamp01(experience.Music.Volume + direction * 0.05f);
                    if (Mathf.Approximately(volume, experience.Music.Volume)) return false;
                    experience.Music.SetVolume(volume);
                    return true;
                case PauseOption.Brightness:
                    if (experience.Brightness == null || !experience.Brightness.Available) return false;
                    float offset = Mathf.Clamp(experience.Brightness.Offset + direction * 0.1f,
                        DisplayBrightness.MinOffset, DisplayBrightness.MaxOffset);
                    if (Mathf.Approximately(offset, experience.Brightness.Offset)) return false;
                    experience.Brightness.SetOffset(offset);
                    experience.Brightness.Save();
                    PlayerPrefs.Save();
                    return true;
                case PauseOption.ReducedMotion:
                    experience.SetReducedMotion(!experience.ReducedMotion);
                    return true;
                case PauseOption.ParticleStyle:
                    experience.ParticleLook.SetStyle(!experience.ParticleLook.IsLegacy);
                    return true;
                case PauseOption.SideBySide:
                    experience.ParticleLook.SetSideBySide(!ParticleLook.SideBySide);
                    return true;
                default:
                    return false;
            }
        }

        void ConfirmMenuSelection(bool gameOver, bool retryAvailable)
        {
            if (gameOver)
            {
                int quitIndex = retryAvailable ? 3 : 2;
                if (retryAvailable && selectedMenuItem == 0)
                    experience.RetryCurrentRoom();
                else if (selectedMenuItem == (retryAvailable ? 1 : 0))
                    experience.Restart();
                else if (selectedMenuItem == (retryAvailable ? 2 : 1) && session != null)
                    session.RequestDisable();
                else if (selectedMenuItem == quitIndex)
                    experience.Quit();
                return;
            }

            if (studySubmenu)
            {
                if (selectedMenuItem == (int)StudyOption.Back)
                {
                    studySubmenu = false;
                    selectedMenuItem = savedMainMenuSelection;
                    pauseMenuDirty = true;
                }
                else if (selectedMenuItem < (int)StudyOption.Gain && AdjustSelectedOption(1))
                    pauseMenuDirty = true;
                return;
            }

            switch (GetMainOption(selectedMenuItem))
            {
                case PauseOption.Resume:
                    experience.TogglePause();
                    break;
                case PauseOption.ResetBrightness:
                    if (experience.Brightness != null)
                    {
                        experience.Brightness.SetOffset(0f);
                        experience.Brightness.Save();
                        PlayerPrefs.Save();
                        pauseMenuDirty = true;
                    }
                    break;
                case PauseOption.ReducedMotion:
                case PauseOption.ParticleStyle:
                case PauseOption.SideBySide:
                    if (AdjustSelectedOption(1)) pauseMenuDirty = true;
                    break;
                case PauseOption.ReplayTutorial:
                    experience.ReplayTutorial();
                    break;
                case PauseOption.SkipTutorial:
                    experience.SkipTutorial();
                    pauseMenuDirty = true;
                    break;
                case PauseOption.Restart:
                    experience.Restart();
                    break;
                case PauseOption.Recenter:
                    if (session != null) session.Recenter();
                    break;
                case PauseOption.ExitPcVr:
                    if (session != null) session.RequestDisable();
                    break;
                case PauseOption.Quit:
                    experience.Quit();
                    break;
                case PauseOption.PointStudy:
                    if (experience.PointStudy != null)
                    {
                        savedMainMenuSelection = selectedMenuItem;
                        studySubmenu = true;
                        selectedMenuItem = 0;
                        pauseMenuDirty = true;
                    }
                    break;
            }
        }

        void RefreshPauseMenu(bool gameOver, bool retryAvailable, int itemCount)
        {
            pauseMenuBuilder.Clear();
            if (gameOver)
                pauseMenuBuilder.Append("UP/DOWN SELECT\nRIGHT TRIGGER: CONFIRM\n");
            else if (studySubmenu)
                pauseMenuBuilder.Append("POINT STUDY\nUP/DOWN SELECT  LEFT/RIGHT CHANGE\nTRIGGER: CONFIRM\n");
            else
                pauseMenuBuilder.Append("UP/DOWN SELECT  LEFT/RIGHT CHANGE\nRIGHT TRIGGER: CONFIRM\n");

            int first = Mathf.Clamp(selectedMenuItem - VisibleMenuItems / 2, 0,
                Mathf.Max(0, itemCount - VisibleMenuItems));
            int last = Mathf.Min(itemCount, first + VisibleMenuItems);
            for (int i = first; i < last; i++)
            {
                pauseMenuBuilder.Append(i == selectedMenuItem ? "> " : "  ");
                if (gameOver)
                    AppendGameOverOption(i, retryAvailable);
                else if (studySubmenu)
                    AppendStudyOption((StudyOption)i);
                else
                    AppendMainOption(GetMainOption(i));
                if (i + 1 < last) pauseMenuBuilder.Append('\n');
            }

            pauseItems.text = pauseMenuBuilder.ToString();
            FitText(pauseItems, 2.1f, .95f);
            pauseMenuDirty = false;
        }

        void AppendGameOverOption(int index, bool retryAvailable)
        {
            if (retryAvailable)
            {
                switch (index)
                {
                    case 0: pauseMenuBuilder.Append("RETRY CURRENT ROOM"); return;
                    case 1: pauseMenuBuilder.Append("RESTART RUN"); return;
                    case 2: pauseMenuBuilder.Append("EXIT PCVR"); return;
                    default: pauseMenuBuilder.Append("QUIT"); return;
                }
            }

            switch (index)
            {
                case 0: pauseMenuBuilder.Append("RESTART RUN"); break;
                case 1: pauseMenuBuilder.Append("EXIT PCVR"); break;
                default: pauseMenuBuilder.Append("QUIT"); break;
            }
        }

        void AppendMainOption(PauseOption option)
        {
            switch (option)
            {
                case PauseOption.Resume: pauseMenuBuilder.Append("RESUME"); break;
                case PauseOption.Music:
                    pauseMenuBuilder.Append("MUSIC  ").Append(Mathf.RoundToInt(experience.Music.Volume * 100f))
                        .Append("%  LEFT/RIGHT");
                    break;
                case PauseOption.Brightness:
                    pauseMenuBuilder.Append("BRIGHTNESS  ");
                    if (experience.Brightness != null && experience.Brightness.Available)
                        pauseMenuBuilder.Append(experience.Brightness.Offset.ToString("+0.0;-0.0;0.0")).Append(" EV");
                    else
                        pauseMenuBuilder.Append("UNAVAILABLE");
                    break;
                case PauseOption.ResetBrightness: pauseMenuBuilder.Append("RESET BRIGHTNESS"); break;
                case PauseOption.ReducedMotion:
                    pauseMenuBuilder.Append("REDUCED MOTION  ").Append(experience.ReducedMotion ? "ON" : "OFF");
                    break;
                case PauseOption.ParticleStyle:
                    pauseMenuBuilder.Append("PARTICLE STYLE  ")
                        .Append(experience.ParticleLook.IsLegacy ? "ORIGINAL QUAD" : "SHARP QUAD");
                    break;
                case PauseOption.SideBySide:
                    pauseMenuBuilder.Append("SIDE BY SIDE  ").Append(ParticleLook.SideBySide ? "ON" : "OFF");
                    break;
                case PauseOption.ReplayTutorial: pauseMenuBuilder.Append("REPLAY TUTORIAL"); break;
                case PauseOption.SkipTutorial: pauseMenuBuilder.Append("SKIP TUTORIAL"); break;
                case PauseOption.Restart: pauseMenuBuilder.Append("RESTART RUN"); break;
                case PauseOption.Recenter: pauseMenuBuilder.Append("RECENTER VIEW"); break;
                case PauseOption.ExitPcVr: pauseMenuBuilder.Append("EXIT PCVR"); break;
                case PauseOption.Quit: pauseMenuBuilder.Append("QUIT"); break;
                case PauseOption.PointStudy: pauseMenuBuilder.Append("POINT STUDY >"); break;
            }
        }

        void AppendStudyOption(StudyOption option)
        {
            PointStudy study = experience.PointStudy;
            if (study == null) return;
            switch (option)
            {
                case StudyOption.Visible:
                    pauseMenuBuilder.Append("VISIBLE  ").Append(study.Visible ? "ON" : "OFF");
                    break;
                case StudyOption.RenderMode:
                    pauseMenuBuilder.Append("RENDER  ").Append(study.Mode == PointStudy.RenderMode.NativePoint
                        ? "NATIVE POINT" : "SHARP QUAD");
                    break;
                case StudyOption.Density:
                    pauseMenuBuilder.Append("DENSITY  ").Append(study.DensityMultiplier).Append("X");
                    break;
                case StudyOption.Gain:
                    pauseMenuBuilder.Append("GAIN  ").Append(study.Gain.ToString("0.00")).Append("  LEFT/RIGHT");
                    break;
                case StudyOption.Flow:
                    pauseMenuBuilder.Append("FLOW  ").Append(study.Flow.ToString("0.00")).Append("  LEFT/RIGHT");
                    break;
                case StudyOption.Back:
                    pauseMenuBuilder.Append("BACK");
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

        void OnDestroy()
        {
            HidePassageLabels();
            if (passageLabelsRoot != null)
                Destroy(passageLabelsRoot.gameObject);
            if (hudRoot != null)
                Destroy(hudRoot.gameObject);
            if (textMaterial != null)
                Destroy(textMaterial);
        }
    }
}
