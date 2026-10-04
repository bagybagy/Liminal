using System;
using System.Collections;
using UnityEngine;

namespace Liminal
{
    public sealed class Experience : MonoBehaviour
    {
        public AudioClip soundtrack;
        [Tooltip("Opt in only after replacement stage themes have been approved. Otherwise the main soundtrack plays throughout.")]
        public bool enableStageMusic;
        public Material particles,ribbons;
        public Material advectedParticles,membrane;
        public Material cavernSurface,marineLight;
        public Material matterLight;
        public Material horizonSurface,horizonSpray;
        public ComputeShader matterSimulation;
        public ComputeShader particleSimulation;
        public Camera sceneCamera;
        public MusicTransport Music { get; private set; }
        public Encounter Combat { get; private set; }
        public ParticleWorld World { get; private set; }
        public Flight Flight { get; private set; }
        public MarineLife Marine { get; private set; }
        public HorizonWater Horizon { get; private set; }
        public HermitEncounter Hermits { get; private set; }
        public SubmarineEncounter Submarines { get; private set; }
        public ParticleTutorial Tutorial { get; private set; }
        public PassageBeacons PassageGuide { get; private set; }
        public DisplayBrightness Brightness { get; private set; }
        public RunProgress Progress { get; }=new RunProgress();
        public AtlantisFinale Finale { get; private set; }
        public PcVrSession Vr { get; private set; }
        public PointStudy PointStudy { get; private set; }
        public ParticleLook ParticleLook { get; private set; }
        public bool CavernMode { get; private set; }
        public int CurrentRoom { get; private set; }
        public int RoomsVisited { get; private set; }
        public double WhaleAwakenedAt { get; private set; }=-1;
        public bool Ready { get; private set; }
        public bool ProofActive { get; private set; }
        public bool BackgroundProof { get; private set; }
        public bool ManualProofTick { get; set; }
        public bool ReducedMotion { get; private set; }
        readonly bool[] visitedRooms=new bool[CaveLayout.Rooms.Length];
        readonly bool[] occupiedRooms=new bool[CaveLayout.Rooms.Length];
        bool hasRoomCheckpoint;
        int checkpointRoom;
        Vector3 checkpointPosition;
        Quaternion checkpointRotation;
        BossId checkpointBossMask;
        bool whaleCalled;
        bool tutorialSaved;
        void Awake()
        {
            Application.targetFrameRate=120;
            QualitySettings.vSyncCount=1;
            string[] args=Environment.GetCommandLineArgs();
            bool legacyProof=Array.Exists(args,s=>s=="--verify"||s=="--preview");
            bool cavernProof=Array.IndexOf(args,"--verify-caverns")>=0;
            bool expansionProof=Array.IndexOf(args,"--verify-expansion")>=0;
            bool journeyProof=Array.IndexOf(args,"--verify-journey")>=0;
            bool feedbackProof=Array.IndexOf(args,"--verify-player-feedback")>=0;
            bool encounterReview=Array.IndexOf(args,"--verify-encounter-review")>=0;
            bool pressureProof=Array.IndexOf(args,"--verify-pressure-patterns")>=0;
            bool finalReviewProof=Array.IndexOf(args,"--verify-final-review")>=0;
            bool whaleSplashProof=Array.IndexOf(args,"--verify-whale-splash")>=0;
            bool roomRetryProof=Array.IndexOf(args,"--verify-room-retry")>=0;
            bool pointStudyProof=Array.IndexOf(args,"--verify-point-study")>=0;
            bool sharpMatterProof=Array.IndexOf(args,"--verify-sharp-matter")>=0;
            bool capturePV=false;
#if UNITY_EDITOR
            legacyProof|=UnityEditor.EditorPrefs.GetBool("Liminal.TrailerCapture.Autopilot",false);
            capturePV=UnityEditor.EditorPrefs.GetBool("Liminal.CavernPV",false);
#endif
            ProofActive=legacyProof||cavernProof||expansionProof||journeyProof||feedbackProof||encounterReview||pressureProof||finalReviewProof||whaleSplashProof||roomRetryProof||pointStudyProof||sharpMatterProof||capturePV;
            BackgroundProof=ProofActive && Application.isBatchMode && Array.IndexOf(args,"--background-proof")>=0;
            if(BackgroundProof) AudioListener.volume=0f;
            CavernMode=!legacyProof && Array.IndexOf(args,"--legacy-arena")<0;
            if(!BackgroundProof) Cursor.visible=ProofActive;
            World=gameObject.AddComponent<ParticleWorld>();
            ParticleLook=gameObject.AddComponent<ParticleLook>();
            ParticleTransitionSettings.Current.ApplyGlobals();
            World.particleTemplate=particles;World.ribbonMaterial=ribbons;
            World.cavernSurface=cavernSurface;World.marineLight=marineLight;
            World.matterSimulation=matterSimulation;World.matterLight=matterLight;
            World.advectedParticles=advectedParticles;World.membrane=membrane;World.particleSimulation=particleSimulation;World.Initialize(CavernMode);
            Music=gameObject.AddComponent<MusicTransport>();Music.soundtrack=soundtrack;Music.LoopSoundtrack=CavernMode;
            Music.EnableStageMusic=CavernMode && enableStageMusic;Music.Initialize(true);
            if(BackgroundProof) AudioListener.volume=0f;
            var pilot=new GameObject("Traveler rig");pilot.transform.SetParent(transform,false);
            Flight=pilot.AddComponent<Flight>();Flight.SuppressCursorChanges=BackgroundProof;Flight.Initialize(World,sceneCamera);
            Combat=gameObject.AddComponent<Encounter>();Combat.ExplorationMode=CavernMode;Combat.Initialize(Music,World,Flight);
            if(CavernMode) {
                Marine=gameObject.AddComponent<MarineLife>();Marine.Initialize(World,Combat);
                Horizon=gameObject.AddComponent<HorizonWater>();Horizon.Initialize(horizonSurface,horizonSpray);
                Hermits=gameObject.AddComponent<HermitEncounter>();Hermits.Initialize(Combat,World,Flight,Music);
                Submarines=gameObject.AddComponent<SubmarineEncounter>();Submarines.Initialize(Combat,World,Flight,Music);
                Tutorial=gameObject.AddComponent<ParticleTutorial>();Tutorial.Initialize(World,Combat,Flight,Music);
                Tutorial.SetEnabled(!ProofActive && PlayerPrefs.GetInt("particleTutorialCompleted",0)==0);
                PassageGuide=gameObject.AddComponent<PassageBeacons>();PassageGuide.Initialize(World,Combat);
                Finale=gameObject.AddComponent<AtlantisFinale>();Finale.Initialize(World,Marine,Progress);
                sceneCamera.farClipPlane=2300;
                if(!ProofActive || pointStudyProof || sharpMatterProof) {
                    PointStudy=gameObject.AddComponent<PointStudy>();PointStudy.Initialize(this);
                    if(!pointStudyProof) PointStudy.SetVisible(false);
                }
            }
            Brightness=gameObject.AddComponent<DisplayBrightness>();Brightness.Initialize(sceneCamera);
            var hud=gameObject.AddComponent<Hud>();hud.Experience=this;
            SetReducedMotion(PlayerPrefs.GetInt("reducedMotion",0)==1);
            Vr=gameObject.AddComponent<PcVrSession>();Vr.Initialize(this);
            if(CavernMode) {
                SyncRoomOccupancy(Flight.Position);
                SaveRoomCheckpoint(0,Flight.Position,Flight.transform.rotation);
            }
            if(legacyProof) gameObject.AddComponent<RuntimeProof>().Initialize(this);
            if(cavernProof) gameObject.AddComponent<CavernProof>().Initialize(this);
            if(expansionProof) gameObject.AddComponent<ExpansionProof>().Initialize(this);
            if(journeyProof) gameObject.AddComponent<JourneyProof>().Initialize(this);
            if(feedbackProof) gameObject.AddComponent<PlayerFeedbackProof>().Initialize(this);
            if(encounterReview) gameObject.AddComponent<EncounterReviewProof>().Initialize(this);
            if(pressureProof) gameObject.AddComponent<PressurePatternProof>().Initialize(this);
            if(finalReviewProof) gameObject.AddComponent<FinalReviewProof>().Initialize(this);
            if(whaleSplashProof) gameObject.AddComponent<WhaleSplashProof>().Initialize(this);
            if(roomRetryProof) gameObject.AddComponent<RoomRetryProof>().Initialize(this);
            if(pointStudyProof) gameObject.AddComponent<PointStudyProof>().Initialize(this);
            if(sharpMatterProof) gameObject.AddComponent<SharpMatterProof>().Initialize(this);
            if(capturePV) gameObject.AddComponent<PvDirector>().Initialize(this);
#if UNITY_EDITOR
            if(UnityEditor.EditorPrefs.GetBool("Liminal.TrailerCapture.Autopilot",false))
                gameObject.AddComponent<EditorTrailerRecorder>();
#endif
        }
        IEnumerator Start()
        {
            // Finish the first frame before scheduling audio; expensive setup must not consume its DSP lead time.
            yield return null;
            Music.Restart();
            Ready=true;
            Debug.Log("LIMINAL_READY particles="+World.ParticleCount+" soundtrack="+soundtrack.length);
        }
        void Update()
        {
            if(!Ready || (ProofActive && ManualProofTick)) return;
            if(!ProofActive && Input.GetKeyDown(KeyCode.Escape)) TogglePause();
            float song=(float)Music.Time,dt=Mathf.Min(Time.unscaledDeltaTime,0.05f);
            Brightness.SetFinaleGlow(Finale && Finale.Active?Finale.Formation:0f);
            ParticleTransitionSettings.Current.ApplyGlobals();
            Flight.SetTravelContext(CanTravelBoost(song),song);
            if(Vr && Vr.Enabled) {
                Vr.Tick(song,dt,!ProofActive && !Combat.Ended && !Music.Paused);
                if(!ProofActive && Vr.PausePressed) TogglePause();
            }
            if(Music.Paused) return;
            if(PointStudy && PointStudy.Visible) PointStudy.Tick(song,dt);
            if(!ProofActive && (!Vr || !Vr.Enabled)) Flight.Tick(song,dt,!Combat.Ended);
            if(CavernMode) {
                CurrentRoom=CaveLayout.NearestRoom(Flight.Position);
                if(!visitedRooms[CurrentRoom]) {visitedRooms[CurrentRoom]=true;RoomsVisited++;}
                Combat.ActiveRoom=CurrentRoom;
                if(Combat.SerpentComplete) Progress.Record(BossId.Serpent);
                if(Hermits.Complete) Progress.Record(BossId.Hermit);
                if(Submarines.Complete) Progress.Record(BossId.Submarine);
                TrackRoomEntry(Flight.Position);
                Marine.ConfigureInheritance(Progress.Defeated & RunProgress.OptionalBosses);
                PassageGuide.Tick(song);
                Tutorial.Tick(song,dt,!ProofActive && !Combat.Ended);
                if(!ProofActive && Tutorial.Complete && !tutorialSaved) {
                    tutorialSaved=true;PlayerPrefs.SetInt("particleTutorialCompleted",1);PlayerPrefs.Save();
                }
                Marine.Tick(song,dt,Flight.Position);
                Hermits.Tick(song,dt,CurrentRoom==3);
                Submarines.Tick(song,dt,CurrentRoom==4);
                if(Marine.WhaleReleased && Progress.Record(BossId.Whale)) {
                    Combat.EnterAfterglow();
                    Finale.Begin(Progress.EndingMask,song);
                }
                Finale.Tick(song,dt);
                if(!ProofActive && (CaveLayout.RoomDistance(CurrentRoom,Flight.Position)<.98f || Progress.EndingStarted))
                    Music.RequestTheme(CurrentRoom,Progress.EndingStarted);
                Horizon.Tick(song,dt,Marine.WhalePosition,Marine.WhaleRotation,Marine.WhaleVelocity,Marine.WhaleReleased);
                World.Caverns.Tick(song,dt,Flight.Position);
                Color atmosphere=CaveLayout.Rooms[CurrentRoom].Color*.006f;
                atmosphere.a=1;
                sceneCamera.backgroundColor=Color.Lerp(sceneCamera.backgroundColor,atmosphere,dt*.8f);
            }
            Combat.Tick(dt,!ProofActive && (!Vr || !Vr.Enabled));
            if(!ProofActive && Vr && Vr.Enabled && !Combat.Ended) {
                if(Vr.LockHeld) Combat.AcquireAt(Flight.AimScreenPosition);
                if(Vr.LockReleased) Combat.Release();
                if(Vr.OverdrivePressed) Combat.Nova();
            }
            if(CavernMode) World.Serpent.SetResonance(Combat.BossDamage/(float)Combat.BossDamageGoal,Combat.SerpentComplete,song);
            float evolution=CavernMode?(Combat.SerpentComplete?1:.25f):Mathf.SmoothStep(0,1,Mathf.InverseLerp(104,164,song));
            float dissolve=!CavernMode && Combat.Won?Mathf.Clamp01((song-Combat.EndTime)/9):0;
            World.Tick(song,evolution,dissolve,ReducedMotion);
            if(CavernMode && Marine.WhaleReleased) {
                if(WhaleAwakenedAt<0) WhaleAwakenedAt=Music.Time;
                if(!whaleCalled && Music.Time-WhaleAwakenedAt>=4) {whaleCalled=true;Music.WhaleCall();}
            }
            if(ProofActive && !BackgroundProof) {Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        }
        public void SetReducedMotion(bool value) { ReducedMotion=value;Flight.ReducedMotion=value; }
        public bool CanTravelBoost(float song) => CavernMode && !Music.Paused && !Combat.Ended &&
            Combat.Locks.Count==0 && !Combat.HasPending && song-Combat.LastHitTime>3f;
        public void TogglePause()
        {
            Music.SetPaused(!Music.Paused);Combat.AbandonLocks();Cursor.visible=Music.Paused;
            Flight.SuspendInput();
            SaveSettings();
        }
        public void Restart()
        {
            Music.Restart();Combat.Restart();
            Progress.Reset();
            if(CavernMode) {
                Marine.ResetLife();World.Caverns.ResetLighting();Horizon.ResetWater();
                Hermits.ResetEncounter();Submarines.ResetEncounter();Tutorial.ResetTutorial();
                PassageGuide.ResetShoals();
                Finale.ResetFinale();
                Brightness.SetFinaleGlow(0);
                Tutorial.SetEnabled(!ProofActive && PlayerPrefs.GetInt("particleTutorialCompleted",0)==0);
                Array.Clear(visitedRooms,0,visitedRooms.Length);RoomsVisited=0;CurrentRoom=0;whaleCalled=false;WhaleAwakenedAt=-1;
                SyncRoomOccupancy(Flight.Position);
                SaveRoomCheckpoint(0,CaveLayout.Spawn,CaveLayout.SpawnRotation);
            }
            if(!BackgroundProof) Cursor.visible=ProofActive;
            if(Vr && Vr.Enabled) Vr.ResetPose();
        }
        public bool HasRoomCheckpoint => hasRoomCheckpoint;
        public int RoomCheckpointRoom => checkpointRoom;
        public Vector3 RoomCheckpointPosition => checkpointPosition;
        public Quaternion RoomCheckpointRotation => checkpointRotation;
        public BossId RoomCheckpointBossMask => checkpointBossMask;
        public bool CanRetryRoom => CavernMode && hasRoomCheckpoint && Combat != null && Combat.Lost && !Progress.EndingStarted;

        public bool RetryCurrentRoom()
        {
            if(!CanRetryRoom) return false;
            int room=checkpointRoom;
            Vector3 position=checkpointPosition;
            Quaternion rotation=checkpointRotation;
            BossId defeated=checkpointBossMask;

            Combat.ClearPressureShots(Marine.Inheritance);
            Combat.ClearPressureShots(Marine.Dolphins);
            Combat.Restart();
            Progress.RestoreCheckpoint(defeated);
            Marine.ResetLife();
            Horizon.ResetWater();
            if((defeated & BossId.Hermit)==0) Hermits.ResetEncounter();
            if((defeated & BossId.Submarine)==0) Submarines.ResetEncounter();
            if((defeated & BossId.Serpent)!=0) Combat.RestoreCompletedSerpent((float)Music.Time);
            Marine.ConfigureInheritance(defeated & RunProgress.OptionalBosses);
            Flight.SetPose(position,rotation);
            if(Music.Paused) Music.SetPaused(false);
            WhaleAwakenedAt=-1;
            whaleCalled=false;
            CurrentRoom=room;
            Combat.ActiveRoom=room;
            SyncRoomOccupancy(position);
            return true;
        }

        void TrackRoomEntry(Vector3 current)
        {
            if(!CavernMode || Combat.Lost || Progress.EndingStarted) return;
            int entered=-1;
            float best=float.MaxValue;
            for(int room=0;room<CaveLayout.Rooms.Length;room++) {
                float distance=CaveLayout.RoomDistance(room,current);
                bool occupied=distance<=1f;
                if(occupied && !occupiedRooms[room] && distance<best) {entered=room;best=distance;}
            }
            if(entered>=0)
                SaveRoomCheckpoint(entered,current,Flight.transform.rotation);
            SyncRoomOccupancy(current);
        }

        void SyncRoomOccupancy(Vector3 position)
        {
            for(int room=0;room<CaveLayout.Rooms.Length;room++)
                occupiedRooms[room]=CaveLayout.RoomDistance(room,position)<=1f;
        }

        void SaveRoomCheckpoint(int room,Vector3 position,Quaternion rotation)
        {
            hasRoomCheckpoint=true;
            checkpointRoom=room;
            checkpointPosition=position;
            checkpointRotation=rotation;
            checkpointBossMask=Progress.Defeated & RunProgress.OptionalBosses;
        }
        public void Quit() { SaveSettings();Application.Quit(); }
        public void ReplayTutorial()
        {
            if(!Tutorial) return;
            Tutorial.ResetTutorial();Tutorial.SetEnabled(true);
            if(Music.Paused) TogglePause();
        }
        public void SkipTutorial() { if(Tutorial) Tutorial.SetEnabled(false); }
        void SaveSettings()
        {
            PlayerPrefs.SetFloat("volume",Music.Volume);PlayerPrefs.SetInt("reducedMotion",ReducedMotion?1:0);
            Brightness.Save();PlayerPrefs.Save();
        }
        void OnApplicationQuit() { if(Ready && !ProofActive) SaveSettings(); }
        void OnApplicationFocus(bool focused) { if(Ready&&!ProofActive&&(!Vr||!Vr.Enabled)&&!focused&&!Music.Paused&&!Combat.Ended) TogglePause(); }
        void OnDestroy() { if(!BackgroundProof) {Cursor.lockState=CursorLockMode.None;Cursor.visible=true;} AudioListener.pause=false; }
    }
}
