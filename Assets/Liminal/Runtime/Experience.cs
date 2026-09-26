using System;
using UnityEngine;

namespace Liminal
{
    public sealed class Experience : MonoBehaviour
    {
        public AudioClip soundtrack;
        public Material particles,ribbons;
        public Material advectedParticles,membrane;
        public ComputeShader particleSimulation;
        public Camera sceneCamera;
        public MusicTransport Music { get; private set; }
        public Encounter Combat { get; private set; }
        public ParticleWorld World { get; private set; }
        public Flight Flight { get; private set; }
        public bool Ready { get; private set; }
        public bool ProofActive { get; private set; }
        public bool ReducedMotion { get; private set; }
        void Awake()
        {
            Application.targetFrameRate=120;
            QualitySettings.vSyncCount=1;
            ProofActive=Array.Exists(Environment.GetCommandLineArgs(),s=>s=="--verify"||s=="--preview");
            Cursor.visible=ProofActive;
            World=gameObject.AddComponent<ParticleWorld>();
            World.particleTemplate=particles;World.ribbonMaterial=ribbons;
            World.advectedParticles=advectedParticles;World.membrane=membrane;World.particleSimulation=particleSimulation;World.Initialize();
            Music=gameObject.AddComponent<MusicTransport>();Music.soundtrack=soundtrack;Music.Initialize();
            var pilot=new GameObject("Traveler rig");pilot.transform.SetParent(transform,false);
            Flight=pilot.AddComponent<Flight>();Flight.Initialize(World,sceneCamera);
            Combat=gameObject.AddComponent<Encounter>();Combat.Initialize(Music,World,Flight);
            var hud=gameObject.AddComponent<Hud>();hud.Experience=this;
            SetReducedMotion(PlayerPrefs.GetInt("reducedMotion",0)==1);
            Ready=true;
            if(ProofActive) gameObject.AddComponent<RuntimeProof>().Initialize(this);
            Debug.Log("LIMINAL_READY particles="+World.ParticleCount+" soundtrack="+soundtrack.length);
        }
        void Update()
        {
            if(!Ready) return;
            if(!ProofActive && Input.GetKeyDown(KeyCode.Escape)) TogglePause();
            if(Music.Paused) return;
            float song=(float)Music.Time,dt=Mathf.Min(Time.unscaledDeltaTime,0.05f);
            if(!ProofActive) Flight.Tick(song,dt,!Combat.Ended);
            Combat.Tick(dt,!ProofActive);
            float evolution=Mathf.SmoothStep(0,1,Mathf.InverseLerp(104,164,song));
            float dissolve=Combat.Won?Mathf.Clamp01((song-Combat.EndTime)/9):0;
            World.Tick(song,evolution,dissolve,ReducedMotion);
            Cursor.visible=ProofActive||Combat.Ended;
        }
        public void SetReducedMotion(bool value) { ReducedMotion=value;Flight.ReducedMotion=value; }
        public void TogglePause()
        {
            Music.SetPaused(!Music.Paused);Combat.AbandonLocks();Cursor.visible=Music.Paused;
            Flight.SuspendInput();
            SaveSettings();
        }
        public void Restart() { Music.Restart();Combat.Restart();Cursor.visible=ProofActive; }
        public void Quit() { SaveSettings();Application.Quit(); }
        void SaveSettings() { PlayerPrefs.SetFloat("volume",Music.Volume);PlayerPrefs.SetInt("reducedMotion",ReducedMotion?1:0);PlayerPrefs.Save(); }
        void OnApplicationFocus(bool focused) { if(Ready&&!ProofActive&&!focused&&!Music.Paused&&!Combat.Ended) TogglePause(); }
        void OnDestroy() { Cursor.visible=true;AudioListener.pause=false; }
    }
}
