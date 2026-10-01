using System;
using UnityEngine;

namespace Liminal
{
    public sealed class Experience : MonoBehaviour
    {
        public AudioClip soundtrack;
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
        public bool CavernMode { get; private set; }
        public int CurrentRoom { get; private set; }
        public int RoomsVisited { get; private set; }
        public double WhaleAwakenedAt { get; private set; }=-1;
        public bool Ready { get; private set; }
        public bool ProofActive { get; private set; }
        public bool ReducedMotion { get; private set; }
        readonly bool[] visitedRooms=new bool[CaveLayout.Rooms.Length];
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
            bool capturePV=false;
#if UNITY_EDITOR
            legacyProof|=UnityEditor.EditorPrefs.GetBool("Liminal.TrailerCapture.Autopilot",false);
            capturePV=UnityEditor.EditorPrefs.GetBool("Liminal.CavernPV",false);
#endif
            ProofActive=legacyProof||cavernProof||expansionProof||capturePV;
            CavernMode=!legacyProof && Array.IndexOf(args,"--legacy-arena")<0;
            Cursor.visible=ProofActive;
            World=gameObject.AddComponent<ParticleWorld>();
            World.particleTemplate=particles;World.ribbonMaterial=ribbons;
            World.cavernSurface=cavernSurface;World.marineLight=marineLight;
            World.matterSimulation=matterSimulation;World.matterLight=matterLight;
            World.advectedParticles=advectedParticles;World.membrane=membrane;World.particleSimulation=particleSimulation;World.Initialize(CavernMode);
            Music=gameObject.AddComponent<MusicTransport>();Music.soundtrack=soundtrack;Music.LoopSoundtrack=CavernMode;Music.Initialize();
            var pilot=new GameObject("Traveler rig");pilot.transform.SetParent(transform,false);
            Flight=pilot.AddComponent<Flight>();Flight.Initialize(World,sceneCamera);
            Combat=gameObject.AddComponent<Encounter>();Combat.ExplorationMode=CavernMode;Combat.Initialize(Music,World,Flight);
            if(CavernMode) {
                Marine=gameObject.AddComponent<MarineLife>();Marine.Initialize(World,Combat);
                Horizon=gameObject.AddComponent<HorizonWater>();Horizon.Initialize(horizonSurface,horizonSpray);
                Hermits=gameObject.AddComponent<HermitEncounter>();Hermits.Initialize(Combat,World,Flight,Music);
                Submarines=gameObject.AddComponent<SubmarineEncounter>();Submarines.Initialize(Combat,World,Flight,Music);
                Tutorial=gameObject.AddComponent<ParticleTutorial>();Tutorial.Initialize(World,Combat,Flight,Music);
                Tutorial.SetEnabled(!ProofActive && PlayerPrefs.GetInt("particleTutorialCompleted",0)==0);
                PassageGuide=gameObject.AddComponent<PassageBeacons>();PassageGuide.Initialize();
                sceneCamera.farClipPlane=2300;
            }
            Brightness=gameObject.AddComponent<DisplayBrightness>();Brightness.Initialize(sceneCamera);
            var hud=gameObject.AddComponent<Hud>();hud.Experience=this;
            SetReducedMotion(PlayerPrefs.GetInt("reducedMotion",0)==1);
            Ready=true;
            if(legacyProof) gameObject.AddComponent<RuntimeProof>().Initialize(this);
            if(cavernProof) gameObject.AddComponent<CavernProof>().Initialize(this);
            if(expansionProof) gameObject.AddComponent<ExpansionProof>().Initialize(this);
            if(capturePV) gameObject.AddComponent<PvDirector>().Initialize(this);
#if UNITY_EDITOR
            if(UnityEditor.EditorPrefs.GetBool("Liminal.TrailerCapture.Autopilot",false))
                gameObject.AddComponent<EditorTrailerRecorder>();
#endif
            Debug.Log("LIMINAL_READY particles="+World.ParticleCount+" soundtrack="+soundtrack.length);
        }
        void Update()
        {
            if(!Ready) return;
            if(!ProofActive && Input.GetKeyDown(KeyCode.Escape)) TogglePause();
            if(Music.Paused) return;
            float song=(float)Music.Time,dt=Mathf.Min(Time.unscaledDeltaTime,0.05f);
            if(!ProofActive) Flight.Tick(song,dt,!Combat.Ended);
            if(CavernMode) {
                CurrentRoom=CaveLayout.NearestRoom(Flight.Position);
                if(!visitedRooms[CurrentRoom]) {visitedRooms[CurrentRoom]=true;RoomsVisited++;}
                Combat.ActiveRoom=CurrentRoom;
                PassageGuide.Tick(song);
                Tutorial.Tick(song,dt,!ProofActive && !Combat.Ended);
                if(!ProofActive && Tutorial.Complete && !tutorialSaved) {
                    tutorialSaved=true;PlayerPrefs.SetInt("particleTutorialCompleted",1);PlayerPrefs.Save();
                }
                Marine.Tick(song,dt,Flight.Position);
                Hermits.Tick(song,dt,CurrentRoom==3);
                Submarines.Tick(song,dt,CurrentRoom==4);
                Horizon.Tick(song,dt,Marine.WhalePosition,Marine.WhaleRotation,Marine.WhaleVelocity,Marine.WhaleReleased);
                World.Caverns.Tick(song,dt,Flight.Position);
                Color atmosphere=CaveLayout.Rooms[CurrentRoom].Color*.006f;
                atmosphere.a=1;
                sceneCamera.backgroundColor=Color.Lerp(sceneCamera.backgroundColor,atmosphere,dt*.8f);
            }
            Combat.Tick(dt,!ProofActive);
            if(CavernMode) World.Serpent.SetResonance(Combat.BossDamage/(float)Combat.BossDamageGoal,Combat.SerpentComplete,song);
            float evolution=CavernMode?(Combat.SerpentComplete?1:.25f):Mathf.SmoothStep(0,1,Mathf.InverseLerp(104,164,song));
            float dissolve=!CavernMode && Combat.Won?Mathf.Clamp01((song-Combat.EndTime)/9):0;
            World.Tick(song,evolution,dissolve,ReducedMotion);
            if(CavernMode && !whaleCalled && Marine.WhaleReleased) {whaleCalled=true;WhaleAwakenedAt=Music.Time;Music.WhaleCall();}
            if(ProofActive) {Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        }
        public void SetReducedMotion(bool value) { ReducedMotion=value;Flight.ReducedMotion=value; }
        public void TogglePause()
        {
            Music.SetPaused(!Music.Paused);Combat.AbandonLocks();Cursor.visible=Music.Paused;
            Flight.SuspendInput();
            SaveSettings();
        }
        public void Restart()
        {
            Music.Restart();Combat.Restart();
            if(CavernMode) {
                Marine.ResetLife();World.Caverns.ResetLighting();Horizon.ResetWater();
                Hermits.ResetEncounter();Submarines.ResetEncounter();Tutorial.ResetTutorial();
                Tutorial.SetEnabled(!ProofActive && PlayerPrefs.GetInt("particleTutorialCompleted",0)==0);
                Array.Clear(visitedRooms,0,visitedRooms.Length);RoomsVisited=0;CurrentRoom=0;whaleCalled=false;WhaleAwakenedAt=-1;
            }
            Cursor.visible=ProofActive;
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
        void OnApplicationFocus(bool focused) { if(Ready&&!ProofActive&&!focused&&!Music.Paused&&!Combat.Ended) TogglePause(); }
        void OnDestroy() { Cursor.lockState=CursorLockMode.None;Cursor.visible=true;AudioListener.pause=false; }
    }
}
